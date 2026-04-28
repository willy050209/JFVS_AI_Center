namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 資源初始化器接口，負責特定組件的就緒檢查與準備
/// </summary>
public interface IResourceInitializer
{
    /// <summary>
    /// 優先級，數字越小越先執行
    /// </summary>
    int Priority => 100;

    /// <summary>
    /// 執行初始化動作
    /// </summary>
    Task InitializeAsync(CancellationToken ct);
}
