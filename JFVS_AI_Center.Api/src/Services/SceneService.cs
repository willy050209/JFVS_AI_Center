namespace JFVS_AI_Center.Api.Services;

using JFVS_AI_Center.Api.Models;

/// <summary>
/// 景點服務介面
/// </summary>
public interface ISceneService
{
    /// <summary>以景點名稱查詢景點資訊</summary>
    string GetSceneInfo(string sceneName);

    /// <summary>
    /// 從使用者輸入文字中比對關鍵字，回傳第一個命中景點的內容；未命中則回傳 null。
    /// </summary>
    string? TryGetSceneInfo(string userText);
}

/// <summary>
/// 景點服務實作，專注於查詢與配對邏輯。
/// </summary>
public class SceneService : ISceneService
{
    private readonly ISceneRepository _repository;
    private readonly ILogger<SceneService> _logger;

    public SceneService(ISceneRepository repository, ILogger<SceneService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string GetSceneInfo(string sceneName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sceneName);
        _logger.LogInformation("[景點查詢] 正在查詢: {SceneName}", sceneName);

        var scenes = _repository.GetScenes();
        var match = scenes.FirstOrDefault(s => s.Keywords.Any(sceneName.Contains));

        return match?.Content ?? "目前沒有這個景點的即時資訊。";
    }

    public string? TryGetSceneInfo(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText)) return null;

        var scenes = _repository.GetScenes();
        // 反向比對：檢查使用者文字中是否包含任一景點關鍵字
        var match = scenes.FirstOrDefault(s => s.Keywords.Any(userText.Contains));
        if (match != null)
        {
            _logger.LogInformation("[RAG 景點命中] {Title}", match.Title);
            return match.Content;
        }
        return null;
    }
}
