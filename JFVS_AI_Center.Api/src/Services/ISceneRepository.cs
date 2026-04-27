using JFVS_AI_Center.Api.Models;

namespace JFVS_AI_Center.Api.Services;

/// <summary>
/// 景點資料存取介面
/// </summary>
public interface ISceneRepository
{
    IEnumerable<SceneItem> GetScenes();
}
