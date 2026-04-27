using System.ComponentModel;

namespace JFVS_AI_Center.Api.Models;

/// <summary>
/// AI 模型相關設定 (LM Studio / OpenAI 相容)
/// </summary>
public record AiOptions
{
    /// <summary>
    /// API 終端節點網址
    /// </summary>
    [DefaultValue("http://127.0.0.1:1234/v1")]
    public string Endpoint { get; init; } = "http://127.0.0.1:1234/v1";

    /// <summary>
    /// 使用的模型名稱
    /// </summary>
    [DefaultValue("local-model")]
    public string Model { get; init; } = "local-model";

    /// <summary>
    /// API 金鑰
    /// </summary>
    [DefaultValue("lm-studio")]
    public string ApiKey { get; init; } = "lm-studio";
}
