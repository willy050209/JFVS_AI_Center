namespace JFVS_AI_Center.Api.Models;

public class ChatRequest
{
    public string Text { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
}
