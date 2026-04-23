using OpenAI.Chat;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JFVS_AI_Center.Api.Services;

public interface IAiService
{
    Task<string> ProcessChatAsync(string userText);
}

public class AiService : IAiService
{
    private readonly IMqttService _mqttService;
    private readonly ISceneService _sceneService;
    private readonly ILogger<AiService> _logger;
    private readonly ChatClient _client;
    private readonly List<ChatMessage> _chatMessages = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    private const string SystemPrompt = @"你現在扮演『小瑞』，一個來自技術型高中資訊科的學生。
你負責介紹學校景點（圖書館、實習大樓、思源亭）。
你具備控制物聯網設備的能力，如果訪客要求開關「燈光」或「風扇」，請務必使用 control_device 工具。
回覆必須簡練（30-50字），語氣溫文爾雅且熱情。
介紹景點時請使用 get_scene_info 工具，且擷取最相關的一兩句話回答，不要給太多資料。
回覆時不要提到具體校名與地名，使用籠統稱呼即可。";

    public AiService(IMqttService mqttService, ISceneService sceneService, ILogger<AiService> logger)
    {
        _mqttService = mqttService;
        _sceneService = sceneService;
        _logger = logger;

        // LM Studio local endpoint
        var clientOptions = new OpenAI.OpenAIClientOptions();
        var openAiClient = new OpenAI.OpenAIClient(new System.ClientModel.ApiKeyCredential("lm-studio"), new OpenAI.OpenAIClientOptions
        {
            Endpoint = new Uri("http://127.0.0.1:1234/v1")
        });
        _client = openAiClient.GetChatClient("local-model");

        _chatMessages.Add(ChatMessage.CreateSystemMessage(SystemPrompt));
    }

    public async Task<string> ProcessChatAsync(string userText)
    {
        // 1. 捷徑檢查
        var (device, action, fastReply) = FastIntentMatcher(userText);
        if (device != null && action != null && fastReply != null)
        {
            _ = Task.Run(async () => await BackgroundDeviceTask(device, action, userText, fastReply));
            return fastReply;
        }

        await _lock.WaitAsync();
        try
        {
            _chatMessages.Add(ChatMessage.CreateUserMessage(userText));

            // 限制歷史紀錄長度
            if (_chatMessages.Count > 15)
            {
                var systemMsg = _chatMessages[0];
                var recentMsgs = _chatMessages.Skip(_chatMessages.Count - 10).ToList();
                _chatMessages.Clear();
                _chatMessages.Add(systemMsg);
                _chatMessages.AddRange(recentMsgs);
            }

            return await RunChatWithToolsAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string> RunChatWithToolsAsync()
    {
        var tools = new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                "get_scene_info",
                "獲取學校景點的詳細資訊與即時狀態。",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""scene_name"": { ""type"": ""string"", ""description"": ""景點名稱"" }
                    },
                    ""required"": [""scene_name""]
                }")
            ),
            ChatTool.CreateFunctionTool(
                "control_device",
                "透過 MQTT 協定控制實體設備（燈光或風扇）。",
                BinaryData.FromString(@"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""device_name"": { ""type"": ""string"", ""description"": ""設備名稱"" },
                        ""action"": { ""type"": ""string"", ""description"": ""動作 (on 或 off)"" }
                    },
                    ""required"": [""device_name"", ""action""]
                }")
            )
        };

        var options = new ChatCompletionOptions
        {
             ToolChoice = ChatToolChoice.CreateAutoChoice()
        };
        foreach (var tool in tools) options.Tools.Add(tool);

        ChatCompletion completion = await _client.CompleteChatAsync(_chatMessages, options);

        if (completion.FinishReason == ChatFinishReason.ToolCalls)
        {
            _chatMessages.Add(ChatMessage.CreateAssistantMessage(completion));

            foreach (var toolCall in completion.ToolCalls)
            {
                string result = "";
                if (toolCall.FunctionName == "get_scene_info")
                {
                    using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
                    var sceneName = doc.RootElement.GetProperty("scene_name").GetString() ?? "";
                    result = _sceneService.GetSceneInfo(sceneName);
                }
                else if (toolCall.FunctionName == "control_device")
                {
                    using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
                    var deviceName = doc.RootElement.GetProperty("device_name").GetString() ?? "";
                    var action = doc.RootElement.GetProperty("action").GetString() ?? "";
                    result = await _mqttService.ControlDeviceAsync(deviceName, action);
                }

                _chatMessages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
            }

            // 第二次請求
            completion = await _client.CompleteChatAsync(_chatMessages);
        }

        var finalReply = completion.Content[0].Text;
        _chatMessages.Add(ChatMessage.CreateAssistantMessage(finalReply));

        // 清理文字 (比照 Python translate/replace)
        finalReply = Regex.Replace(finalReply, "[*#「」『』]", "").Replace("\n", " ").Trim();
        _logger.LogInformation("<< [回傳]: {Reply}", finalReply);
        return finalReply;
    }

    private (string? Device, string? Action, string? FastReply) FastIntentMatcher(string text)
    {
        if (text.Contains("風扇") || text.Contains("电扇"))
        {
            if (new[] { "關", "停", "off" }.Any(text.Contains))
                return ("風扇", "off", "好的，已經為您關閉涼亭的風扇囉！");
            if (new[] { "開", "啟動", "on", "很熱" }.Any(text.Contains))
                return ("風扇", "on", "好的，馬上為您開啟風扇！");
        }
        if (text.Contains("燈") || text.Contains("灯"))
        {
            if (new[] { "關", "熄", "off" }.Any(text.Contains))
                return ("燈光", "off", "好的，已經幫您把燈關掉了。");
            if (new[] { "開", "亮", "on", "很暗" }.Any(text.Contains))
                return ("燈光", "on", "沒問題，已經為您點亮燈光了！");
        }
        return (null, null, null);
    }

    private async Task BackgroundDeviceTask(string device, string action, string userText, string fastReply)
    {
        await _mqttService.ControlDeviceAsync(device, action);
        try
        {
            await Task.Delay(1000);
            await _lock.WaitAsync();
            try
            {
                var syncMsg = $"[系統通知] 訪客剛說「{userText}」，系統已啟動捷徑控制了{device}並回覆「{fastReply}」。請知悉設備狀態，不需回覆此訊息。";
                _chatMessages.Add(ChatMessage.CreateSystemMessage(syncMsg));
                _logger.LogInformation(" >> [背景同步] 已更新本地大腦狀態。");
            }
            finally
            {
                _lock.Release();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, " >> [背景同步] 失敗");
        }
    }
}
