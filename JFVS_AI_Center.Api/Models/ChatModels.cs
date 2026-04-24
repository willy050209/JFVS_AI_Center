using OpenAI.Chat;

namespace JFVS_AI_Center.Api.Models;

public class ChatRequest
{
    public string Text { get; set; } = string.Empty;
    public string SessionId { get; set; } = "default";
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
}

public class ChatSession
{
    public string SessionId { get; set; } = string.Empty;
    public List<ChatMessage> Messages { get; } = new();
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    public ChatSession(string sessionId, string systemPrompt)
    {
        SessionId = sessionId;
        Messages.Add(ChatMessage.CreateSystemMessage(systemPrompt));
    }

    public void AddMessage(ChatMessage message)
    {
        lock (Messages)
        {
            Messages.Add(message);
            LastAccessedAt = DateTime.UtcNow;

            // 限制長度
            if (Messages.Count > 15)
            {
                var systemMsg = Messages[0];
                var recentMsgs = Messages.Skip(Messages.Count - 10).ToList();
                Messages.Clear();
                Messages.Add(systemMsg);
                Messages.AddRange(recentMsgs);
            }
        }
    }
}
