namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 檔案下載服務介面
/// </summary>
public interface IFileDownloadService
{
    Task DownloadFileAsync(string url, string path, CancellationToken ct);
}

/// <summary>
/// 檔案下載服務實作
/// </summary>
public class FileDownloadService : IFileDownloadService
{
    private readonly ILogger<FileDownloadService> _logger;

    public FileDownloadService(ILogger<FileDownloadService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DownloadFileAsync(string url, string path, CancellationToken ct)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "JFVS-AI-Center-Server");
        client.Timeout = TimeSpan.FromMinutes(10);
        
        _logger.LogInformation("正在從 {Url} 下載檔案至 {Path}...", url, path);
        
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        
        using var fs = File.Create(path);
        await response.Content.CopyToAsync(fs, ct);
        
        _logger.LogInformation("下載完成。");
    }
}
