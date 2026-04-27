namespace JFVS_AI_Center.Api.Models;

/// <summary>
/// 語音轉文字的回應
/// </summary>
public record TranscriptionResponse(string Text, double DurationSeconds, string Language);

/// <summary>
/// 語音對話的回應
/// </summary>
public record VoiceChatResponse(
    string UserText,
    string AiResponse,
    string? AudioBase64,
    string Status = "success"
);
