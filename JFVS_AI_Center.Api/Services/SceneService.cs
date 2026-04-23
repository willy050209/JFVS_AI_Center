using System.Text.Json;
using JFVS_AI_Center.Api.Models;

namespace JFVS_AI_Center.Api.Services;

public interface ISceneService
{
    string GetSceneInfo(string sceneName);
}

public class SceneService : ISceneService
{
    private readonly List<SceneItem> _scenes = new();
    private readonly ILogger<SceneService> _logger;

    public SceneService(ILogger<SceneService> logger)
    {
        _logger = logger;
        LoadScenes();
    }

    private void LoadScenes()
    {
        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, "scenes.json");
            
            // 如果在開發環境，可能在專案根目錄
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
        _logger.LogInformation("[MCP 工具觸發] 正在查詢: {SceneName}", sceneName);

        var match = _scenes.FirstOrDefault(s => s.Keywords.Any(k => sceneName.Contains(k)));

        if (match != null)
        {
            return match.Content;
        }

        return "目前沒有這個景點的即時資訊。";
    }
}
