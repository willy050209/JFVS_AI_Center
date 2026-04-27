namespace JFVS_AI_Center.Api.Services;

using JFVS_AI_Center.Api.Models;

/// <summary>
/// 景點服務介面
/// </summary>
public interface ISceneService
{
    string GetSceneInfo(string sceneName);
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

        if (match != null)
        {
            return match.Content;
        }

        return "目前沒有這個景點的即時資訊。";
    }
}
