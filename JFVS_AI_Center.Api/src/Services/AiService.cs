using OpenAI.Chat;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using JFVS_AI_Center.Api.Models;
using JFVS_AI_Center.Api.Infrastructure;
using Microsoft.Extensions.Options;

namespace JFVS_AI_Center.Api.Services;

/// <summary>
/// AI 服務介面
/// </summary>
public interface IAiService
{
    Task<string> ProcessChatAsync(string userText, string sessionId = "default");
}

/// <summary>
/// AI 服務實作
/// </summary>
public class AiService : IAiService
{
    private readonly IDeviceControlService _deviceControlService;
    private readonly ISceneService _sceneService;
    private readonly ILogger<AiService> _logger;
    private readonly ChatClient _client;
    private readonly ConcurrentDictionary<string, ChatSession> _sessions = new();

    private const string SystemPrompt = @"你現在扮演『小瑞』，一個來自技術型高中資訊科的學生。
你負責介紹學校景點（圖書館、實習大樓、思源亭）。
你具備控制物聯網設備的能力，如果訪客要求開關「燈光」或「風扇」，請務必使用 control_device 工具。
回覆必須簡練（30-50字），語氣溫文爾雅且熱情。
介紹景點時請使用 get_scene_info 工具，且擷取最相關的一兩句話回答，不要給太多資料。
回覆時不要提到具體校名與地名，使用籠統稱呼即可。
**回覆時嚴禁使用 Emoji 或任何特殊表情符號。**";

    public AiService(
        IDeviceControlService deviceControlService, 
        ISceneService sceneService, 
        ILogger<AiService> logger,
        IOptions<AiOptions> aiOptions)
    {
        ArgumentNullException.ThrowIfNull(deviceControlService);
        ArgumentNullException.ThrowIfNull(sceneService);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(aiOptions);

        _deviceControlService = deviceControlService;
        _sceneService = sceneService;
        _logger = logger;

        var options = aiOptions.Value;
        
        var openAiClient = new OpenAI.OpenAIClient(
            new System.ClientModel.ApiKeyCredential(options.ApiKey), 
            new OpenAI.OpenAIClientOptions { Endpoint = new Uri(options.Endpoint) }
        );
        _client = openAiClient.GetChatClient(options.Model);
    }

    private ChatSession GetOrCreateSession(string sessionId)
    {
        return _sessions.GetOrAdd(sessionId, id => new ChatSession(id, SystemPrompt));
    }

    public async Task<string> ProcessChatAsync(string userText, string sessionId = "default")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userText);

        try
        {
            var (device, action, fastReply) = FastIntentMatcher(userText);
            if (device != null && action != null && fastReply != null)
            {
                _ = Task.Run(async () => await BackgroundDeviceTask(sessionId, device, action, userText, fastReply));
                return fastReply;
            }

            var session = GetOrCreateSession(sessionId);
            session.AddMessage(ChatMessage.CreateUserMessage(userText));

            return await RunChatWithToolsAsync(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 服務處理失敗，使用預設回覆。");
            return "本地大腦連線異常，但我還是可以跟您聊天喔。";
        }
    }

    private async Task<string> RunChatWithToolsAsync(ChatSession session)
    {
        List<ChatTool> tools =
        [
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
        ];

        var options = new ChatCompletionOptions
        {
             ToolChoice = ChatToolChoice.CreateAutoChoice()
        };
        foreach (var tool in tools) options.Tools.Add(tool);

        ChatCompletion completion = await _client.CompleteChatAsync(session.Messages, options);

        if (completion.FinishReason == ChatFinishReason.ToolCalls)
        {
            session.AddMessage(ChatMessage.CreateAssistantMessage(completion));

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
                    result = await _deviceControlService.ControlDeviceAsync(deviceName, action);
                }

                session.AddMessage(ChatMessage.CreateToolMessage(toolCall.Id, result));
            }

            completion = await _client.CompleteChatAsync(session.Messages);
        }

        var finalReply = completion.Content[0].Text;
        session.AddMessage(ChatMessage.CreateAssistantMessage(finalReply));

        finalReply = CleanTextForTts(finalReply);
        
        _logger.LogInformation("<< [Session: {SessionId}] [回傳]: {Reply}", session.SessionId, finalReply);
        return finalReply;
    }

    /// <summary>
    /// 純函數：清理文字 (移除 Emoji 與不適合語音合成的符號)
    /// </summary>
    private static string CleanTextForTts(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = Regex.Replace(text, @"[*#_~「」『』]", "");
        text = Regex.Replace(text, @"\p{So}|\p{Cs}", "");
        text = text.Replace("\n", " ").Replace("\r", " ");
        text = Regex.Replace(text, @"\s+", " ");

        return text.Trim();
    }

    /// <summary>
    /// 純函數：意圖快速配對
    /// </summary>
    private static (string? Device, string? Action, string? FastReply) FastIntentMatcher(string text)
    {
        string[] negations = ["不", "別", "毋", "沒", "取消"];
        
        bool IsPositive(string actionKey)
        {
            int index = text.IndexOf(actionKey, StringComparison.Ordinal);
            if (index <= 0) return index == 0;
            
            int start = Math.Max(0, index - 2);
            var prefix = text.Substring(start, index - start);
            return !negations.Any(prefix.Contains);
        }

        if (text.Contains("風扇") || text.Contains("电扇"))
        {
            if (new[] { "關", "停", "off" }.Any(k => text.Contains(k) && IsPositive(k)))
                return ("風扇", "off", "好的，已經為您關閉涼亭的風扇囉！");
            if (new[] { "開", "啟", "on", "熱" }.Any(k => text.Contains(k) && IsPositive(k)))
                return ("風扇", "on", "好的，馬上為您開啟風扇！");
        }
        if (text.Contains("燈") || text.Contains("灯"))
        {
            if (new[] { "關", "熄", "off" }.Any(k => text.Contains(k) && IsPositive(k)))
                return ("燈光", "off", "好的，已經幫您把燈關掉了。");
            if (new[] { "開", "亮", "on", "暗" }.Any(k => text.Contains(k) && IsPositive(k)))
                return ("燈光", "on", "沒問題，已經為您點亮燈光了！");
        }
        return (null, null, null);
    }

    private async Task BackgroundDeviceTask(string sessionId, string device, string action, string userText, string fastReply)
    {
        await _deviceControlService.ControlDeviceAsync(device, action);
        try
        {
            await Task.Delay(1000);
            var session = GetOrCreateSession(sessionId);
            var syncMsg = $"[系統通知] 訪客剛說「{userText}」，系統已啟動捷徑控制了{device}並回覆「{fastReply}」。請知悉設備狀態，不需回覆此訊息。";
            session.AddMessage(ChatMessage.CreateSystemMessage(syncMsg));
            _logger.LogInformation(" >> [背景同步] 已更新本地大腦狀態 (Session: {SessionId})。", sessionId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, " >> [背景同步] 失敗");
        }
    }
}
