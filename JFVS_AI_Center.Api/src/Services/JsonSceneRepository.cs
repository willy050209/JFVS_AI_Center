using System.Text.Json;
using JFVS_AI_Center.Api.Models;

namespace JFVS_AI_Center.Api.Services;

/// <summary>
/// 從 JSON 檔案讀取景點資料的 Repository 實作
/// </summary>
public class JsonSceneRepository : ISceneRepository
{
    private readonly ILogger<JsonSceneRepository> _logger;
    private readonly List<SceneItem> _scenes = [];

    public JsonSceneRepository(ILogger<JsonSceneRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

    public IEnumerable<SceneItem> GetScenes() => _scenes;
}
