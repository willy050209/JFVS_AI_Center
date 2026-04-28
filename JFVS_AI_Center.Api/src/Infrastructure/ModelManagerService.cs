namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 模型管理服務：協調者職責，負責啟動時遍歷所有初始化器確保環境就緒。
/// </summary>
public class ModelManagerService(
    IEnumerable<IResourceInitializer> initializers,
    ILogger<ModelManagerService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("正在啟動系統環境初始化...");

        var sortedInitializers = initializers.OrderBy(i => i.Priority);

        foreach (var initializer in sortedInitializers)
        {
            try
            {
                await initializer.InitializeAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "初始化器 {Type} 執行失敗", initializer.GetType().Name);
                // 這裡可以根據需求決定是否要中斷啟動
            }
        }

        logger.LogInformation("系統環境初始化完成。");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
