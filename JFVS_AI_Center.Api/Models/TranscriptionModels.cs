namespace JFVS_AI_Center.Api.Models;

public record TranscriptionResponse(string Text, double DurationSeconds, string Language);

public record VoiceChatResponse(
    string UserText,
    string AiResponse,
    string? AudioBase64,
    string Status = "success"
);
