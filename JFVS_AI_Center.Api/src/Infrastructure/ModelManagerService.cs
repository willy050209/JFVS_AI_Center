using System.IO.Compression;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 模型管理服務：協調者職責，負責啟動時確保環境就緒。
/// </summary>
public class ModelManagerService : IHostedService
{
    private const string ModelZipName = "ggml-base-models.zip";
    private const string PiperZipUrl = "https://github.com/rhasspy/piper/releases/latest/download/piper_windows_amd64.zip";
    private const string PiperOnnxUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx";
    private const string PiperJsonUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx.json";

    private readonly ModelPathProvider _pathProvider;
    private readonly IFileDownloadService _downloadService;
    private readonly ILogger<ModelManagerService> _logger;

    public ModelManagerService(
        ModelPathProvider pathProvider,
        IFileDownloadService downloadService,
        ILogger<ModelManagerService> logger)
    {
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _downloadService = downloadService ?? throw new ArgumentNullException(nameof(downloadService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在檢查系統環境...");

        // FFmpeg 處理
        var ffmpegPath = _pathProvider.BaseDir;
        if (!File.Exists(Path.Combine(ffmpegPath, "ffmpeg.exe")) && !File.Exists(Path.Combine(ffmpegPath, "ffmpeg")))
        {
            _logger.LogInformation("未偵測到 FFmpeg，正在下載...");
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);
        }
        FFmpeg.SetExecutablesPath(ffmpegPath);

        // 目錄確保
        if (!Directory.Exists(_pathProvider.ModelFolder)) Directory.CreateDirectory(_pathProvider.ModelFolder);
        if (!Directory.Exists(_pathProvider.PiperFolder)) Directory.CreateDirectory(_pathProvider.PiperFolder);

        await EnsureWhisperFilesAsync(cancellationToken);
        await EnsurePiperFilesAsync(cancellationToken);
    }

    private async Task EnsureWhisperFilesAsync(CancellationToken ct)
    {
        if (File.Exists(_pathProvider.WhisperModelPath) && 
            File.Exists(_pathProvider.WhisperOpenVinoXmlPath) && 
            File.Exists(_pathProvider.WhisperOpenVinoBinPath))
        {
            _logger.LogInformation("Whisper 模型已就緒。");
            return;
        }

        string zipPath = Path.Combine(_pathProvider.ModelFolder, ModelZipName);
        if (!File.Exists(zipPath))
        {
            _logger.LogInformation("正在下載 Whisper OpenVINO 模型包...");
            await _downloadService.DownloadFileAsync("https://huggingface.co/Intel/whisper.cpp-openvino-models/resolve/main/ggml-base-models.zip", zipPath, ct);
        }

        _logger.LogInformation("正在解壓縮 Whisper 模型...");
        ZipFile.ExtractToDirectory(zipPath, _pathProvider.ModelFolder, overwriteFiles: true);
        File.Delete(zipPath);
    }

    private async Task EnsurePiperFilesAsync(CancellationToken ct)
    {
        if (!File.Exists(_pathProvider.PiperExePath))
        {
            _logger.LogInformation("正在下載 Piper TTS 引擎...");
            string zipPath = Path.Combine(_pathProvider.PiperFolder, "piper.zip");
            await _downloadService.DownloadFileAsync(PiperZipUrl, zipPath, ct);
            
            _logger.LogInformation("正在解壓縮 Piper...");
            ZipFile.ExtractToDirectory(zipPath, _pathProvider.PiperFolder, overwriteFiles: true);
            File.Delete(zipPath);
        }

        if (!File.Exists(_pathProvider.PiperModelPath))
        {
            _logger.LogInformation("正在下載 Piper 中文模型 (.onnx)...");
            await _downloadService.DownloadFileAsync(PiperOnnxUrl, _pathProvider.PiperModelPath, ct);
        }

        if (!File.Exists(_pathProvider.PiperJsonPath))
        {
            _logger.LogInformation("正在下載 Piper 中文模型設定 (.json)...");
            await _downloadService.DownloadFileAsync(PiperJsonUrl, _pathProvider.PiperJsonPath, ct);
        }

        _logger.LogInformation("Piper TTS 組件已就緒。");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
