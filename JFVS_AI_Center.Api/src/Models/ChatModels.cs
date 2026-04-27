using OpenAI.Chat;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace JFVS_AI_Center.Api.Models;

/// <summary>
/// 聊天請求參數
/// </summary>
public record ChatRequest
{
    /// <summary>
    /// 使用者輸入的對話文字內容
    /// </summary>
    [Required]
    [DefaultValue("你好")]
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// 會話識別碼，用於維持連續對話的上下文。若不提供則使用 "default"
    /// </summary>
    [DefaultValue("default")]
    public string SessionId { get; init; } = "default";
}

/// <summary>
/// 聊天回應內容
/// </summary>
public record ChatResponse
{
    /// <summary>
    /// AI 回傳的文字內容
    /// </summary>
    public string Response { get; init; } = string.Empty;
}

/// <summary>
/// 聊天會話紀錄管理
/// </summary>
public class ChatSession
{
    public string SessionId { get; }
    public List<ChatMessage> Messages { get; } = [];
    public DateTime LastAccessedAt { get; private set; } = DateTime.UtcNow;

    public ChatSession(string sessionId, string systemPrompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemPrompt);

        SessionId = sessionId;
        Messages.Add(ChatMessage.CreateSystemMessage(systemPrompt));
    }

    public void AddMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (Messages)
        {
            Messages.Add(message);
            LastAccessedAt = DateTime.UtcNow;

            if (Messages.Count > 15)
            {
                var systemMsg = Messages[0];
                var recentMsgs = Messages.Skip(Messages.Count - 10).ToArray();
                Messages.Clear();
                Messages.Add(systemMsg);
                Messages.AddRange(recentMsgs);
            }
        }
    }
}
