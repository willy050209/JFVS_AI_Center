using System.IO.Compression;
using Whisper.net.Ggml;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace JFVS_AI_Center.Api.Services;

/// <summary>
/// 啟動時負責確保 Whisper 模型與 FFmpeg 執行檔已準備就緒。
/// </summary>
public class ModelManagerService(ILogger<ModelManagerService> logger) : IHostedService
{
    private const string ModelZipName = "ggml-base-models.zip";
    private const string ModelFileName = "ggml-base.bin";
    private const string OpenVinoXmlName = "ggml-base-encoder-openvino.xml";
    private const string OpenVinoBinName = "ggml-base-encoder-openvino.bin";

    private readonly string _modelFolder = Path.Combine(AppContext.BaseDirectory, "Models");
    private readonly string _modelPath = Path.Combine(AppContext.BaseDirectory, "Models", ModelFileName);
    private readonly string _openVinoXmlPath = Path.Combine(AppContext.BaseDirectory, "Models", OpenVinoXmlName);
    private readonly string _openVinoBinPath = Path.Combine(AppContext.BaseDirectory, "Models", OpenVinoBinName);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("正在檢查系統環境...");

        // 1. 確保 FFmpeg 已安裝
        var ffmpegPath = AppContext.BaseDirectory;
        if (!File.Exists(Path.Combine(ffmpegPath, "ffmpeg.exe")) && !File.Exists(Path.Combine(ffmpegPath, "ffmpeg")))
        {
            logger.LogInformation("未偵測到 FFmpeg，正在下載至 {Path}...", ffmpegPath);
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);
            logger.LogInformation("FFmpeg 下載完成。");
        }
        
        // 明確設定 FFmpeg 執行檔路徑
        FFmpeg.SetExecutablesPath(ffmpegPath);

        // 2. 確保目錄存在
        if (!Directory.Exists(_modelFolder)) Directory.CreateDirectory(_modelFolder);

        // 3. 確保 Whisper 模型與 OpenVINO 檔案已存在
        await EnsureModelFilesExistAsync(cancellationToken);
    }

    private async Task EnsureModelFilesExistAsync(CancellationToken ct)
    {
        // 如果三個關鍵檔案都存在，就跳過下載
        if (File.Exists(_modelPath) && File.Exists(_openVinoXmlPath) && File.Exists(_openVinoBinPath))
        {
            logger.LogInformation("Whisper 模型與 OpenVINO 檔案已就緒。");
            return;
        }

        string zipPath = Path.Combine(_modelFolder, ModelZipName);
        
        // 1. 下載完整的模型壓縮包 (包含標準 GGML 與 OpenVINO Encoder)
        if (!File.Exists(zipPath))
        {
            logger.LogInformation("正在從 Intel Hugging Face 下載完整模型包 (包含 OpenVINO 加速組件)...");
            var url = "https://huggingface.co/Intel/whisper.cpp-openvino-models/resolve/main/ggml-base-models.zip";
            await DownloadFileAsync(url, zipPath, ct);
            logger.LogInformation("模型包下載完成。");
        }

        // 2. 解壓縮
        logger.LogInformation("正在解壓縮模型包...");
        try 
        {
            ZipFile.ExtractToDirectory(zipPath, _modelFolder, overwriteFiles: true);
            logger.LogInformation("解壓縮完成。");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "解壓縮模型包時發生錯誤。");
            throw;
        }
        finally
        {
            // 刪除暫存的 zip
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }
    }

    private async Task DownloadFileAsync(string url, string path, CancellationToken ct)
    {
        var tempPath = path + ".tmp";
        if (File.Exists(tempPath)) File.Delete(tempPath);

        using var client = new HttpClient();
        // 設定較長的逾時時間，因為模型檔案較大
        client.Timeout = TimeSpan.FromMinutes(10);
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        
        using (var fs = File.Create(tempPath))
        {
            await response.Content.CopyToAsync(fs, ct);
        }

        if (File.Exists(path)) File.Delete(path);
        File.Move(tempPath, path);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public string GetModelPath() => _modelPath;
    public string GetOpenVinoXmlPath() => _openVinoXmlPath;
}
