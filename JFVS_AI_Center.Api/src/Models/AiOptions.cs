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
    [DefaultValue("http://127.0.0.1:8000/v1")]
    public string Endpoint { get; init; } = "http://127.0.0.1:8000/v1";

    /// <summary>
    /// 使用的模型名稱
    /// </summary>
    [DefaultValue("OpenVINO/Qwen2.5-Coder-3B-Instruct-int4-ov")]
    public string Model { get; init; } = "OpenVINO/Qwen2.5-Coder-3B-Instruct-int4-ov";

    /// <summary>
    /// API 金鑰
    /// </summary>
    [DefaultValue("local-server")]
    public string ApiKey { get; init; } = "local-server";
}
