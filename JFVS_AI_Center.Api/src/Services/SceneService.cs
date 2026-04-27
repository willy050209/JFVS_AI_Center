namespace JFVS_AI_Center.Api.Services;

using System.Text.Json;
using JFVS_AI_Center.Api.Models;

/// <summary>
/// 景點服務介面
/// </summary>
public interface ISceneService
{
    string GetSceneInfo(string sceneName);
}

/// <summary>
/// 景點服務實作
/// </summary>
public class SceneService : ISceneService
{
    private readonly List<SceneItem> _scenes = [];
    private readonly ILogger<SceneService> _logger;

    public SceneService(ILogger<SceneService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        LoadScenes();
    }

    private void LoadScenes()
    {
        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, "scenes.json");
            
            if (!File.Exists(filePath))
            {
                filePath = "scenes.json";
            }

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var items = JsonSerializer.Deserialize<List<SceneItem>>(json);
                if (items != null)
                {
                    _scenes.AddRange(items);
                    _logger.LogInformation("已載入 {Count} 個景點資訊。", _scenes.Count);
                }
            }
            else
            {
                _logger.LogWarning("找不到景點資訊檔案: {Path}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "載入景點資訊時發生錯誤。");
        }
    }

    public string GetSceneInfo(string sceneName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
        _logger.LogInformation("[MCP 工具觸發] 正在查詢: {SceneName}", sceneName);

        var match = _scenes.FirstOrDefault(s => s.Keywords.Any(sceneName.Contains));

        if (match != null)
        {
            return match.Content;
        }

        return "目前沒有這個景點的即時資訊。";
    }
}
