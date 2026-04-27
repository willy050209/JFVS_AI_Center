using System.ComponentModel;

namespace JFVS_AI_Center.Api.Models;

/// <summary>
/// 語音辨識結果的回應
/// </summary>
/// <param name="Text">辨識出的文字內容</param>
/// <param name="DurationSeconds">音訊長度（秒）</param>
/// <param name="Language">辨識出的語言代碼 (如 zh, en)</param>
public record TranscriptionResponse(string Text, double DurationSeconds, string Language);

/// <summary>
/// 完整語音交互（對話）的回應
/// </summary>
/// <param name="UserText">從使用者音訊中辨識出的文字</param>
/// <param name="AiResponse">AI 回應的文字內容</param>
/// <param name="AudioBase64">AI 回應語音的 Base64 編碼數據 (WAV 格式)</param>
/// <param name="Status">處理狀態，例如 "success" 或 "empty_speech"</param>
public record VoiceChatResponse(
    string UserText,
    string AiResponse,
    string? AudioBase64,
    [DefaultValue("success")] string Status = "success"
);
