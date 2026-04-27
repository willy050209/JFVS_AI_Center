namespace JFVS_AI_Center.Api.Models;

/// <summary>
/// 情境劇本設定
/// </summary>
public record SceneItem
{
    public List<string> Keywords { get; init; } = [];
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}
