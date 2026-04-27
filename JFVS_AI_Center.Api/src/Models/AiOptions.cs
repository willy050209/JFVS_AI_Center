namespace JFVS_AI_Center.Api.Models;

/// <summary>
/// AI 模型相關設定
/// </summary>
public record AiOptions
{
    public string Endpoint { get; init; } = "http://127.0.0.1:1234/v1";
    public string Model { get; init; } = "local-model";
    public string ApiKey { get; init; } = "lm-studio";
}
