namespace JFVS_AI_Center.Api.Models;

public class SceneItem
{
    public List<string> Keywords { get; set; } = new();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
