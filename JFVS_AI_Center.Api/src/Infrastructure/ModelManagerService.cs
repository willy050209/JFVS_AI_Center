using System.IO.Compression;
using Whisper.net.Ggml;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 模型管理服務：啟動時負責確保 Whisper 模型、FFmpeg 與 Piper TTS 組件已準備就緒。
/// </summary>
public class ModelManagerService : IHostedService
{
    private const string ModelZipName = "ggml-base-models.zip";
    private const string ModelFileName = "ggml-base.bin";
    private const string OpenVinoXmlName = "ggml-base-encoder-openvino.xml";
    private const string OpenVinoBinName = "ggml-base-encoder-openvino.bin";

    private const string PiperZipUrl = "https://github.com/rhasspy/piper/releases/latest/download/piper_windows_amd64.zip";
    private const string PiperOnnxUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx";
    private const string PiperJsonUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx.json";

    private readonly string _baseDir = AppContext.BaseDirectory;
    private readonly string _modelFolder;
    private readonly string _piperFolder;
    private readonly ILogger<ModelManagerService> _logger;

    public ModelManagerService(ILogger<ModelManagerService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        _modelFolder = Path.Combine(_baseDir, "Models");
        _piperFolder = Path.Combine(_baseDir, "Piper");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在檢查系統環境...");

        var ffmpegPath = _baseDir;
        if (!File.Exists(Path.Combine(ffmpegPath, "ffmpeg.exe")) && !File.Exists(Path.Combine(ffmpegPath, "ffmpeg")))
        {
            _logger.LogInformation("未偵測到 FFmpeg，正在下載...");
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);
        }
        FFmpeg.SetExecutablesPath(ffmpegPath);

        if (!Directory.Exists(_modelFolder)) Directory.CreateDirectory(_modelFolder);
        if (!Directory.Exists(_piperFolder)) Directory.CreateDirectory(_piperFolder);

        await EnsureWhisperFilesAsync(cancellationToken);
        await EnsurePiperFilesAsync(cancellationToken);
    }

    private async Task EnsureWhisperFilesAsync(CancellationToken ct)
    {
        string modelPath = Path.Combine(_modelFolder, ModelFileName);
        string xmlPath = Path.Combine(_modelFolder, OpenVinoXmlName);
        string binPath = Path.Combine(_modelFolder, OpenVinoBinName);

        if (File.Exists(modelPath) && File.Exists(xmlPath) && File.Exists(binPath))
        {
            _logger.LogInformation("Whisper 模型已就緒。");
            return;
        }

        string zipPath = Path.Combine(_modelFolder, ModelZipName);
        if (!File.Exists(zipPath))
        {
            _logger.LogInformation("正在下載 Whisper OpenVINO 模型包...");
            await DownloadFileAsync("https://huggingface.co/Intel/whisper.cpp-openvino-models/resolve/main/ggml-base-models.zip", zipPath, ct);
        }

        _logger.LogInformation("正在解壓縮 Whisper 模型...");
        ZipFile.ExtractToDirectory(zipPath, _modelFolder, overwriteFiles: true);
        File.Delete(zipPath);
    }

    private async Task EnsurePiperFilesAsync(CancellationToken ct)
    {
        string piperExe = Path.Combine(_piperFolder, "piper", "piper.exe");
        if (!File.Exists(piperExe))
        {
            _logger.LogInformation("正在下載 Piper TTS 引擎...");
            string zipPath = Path.Combine(_piperFolder, "piper.zip");
            await DownloadFileAsync(PiperZipUrl, zipPath, ct);
            
            _logger.LogInformation("正在解壓縮 Piper...");
            ZipFile.ExtractToDirectory(zipPath, _piperFolder, overwriteFiles: true);
            File.Delete(zipPath);
        }

        string modelPath = GetPiperModelPath();
        string jsonPath = modelPath + ".json";

        if (!File.Exists(modelPath))
        {
            _logger.LogInformation("正在下載 Piper 中文模型 (.onnx)...");
            await DownloadFileAsync(PiperOnnxUrl, modelPath, ct);
        }

        if (!File.Exists(jsonPath))
        {
            _logger.LogInformation("正在下載 Piper 中文模型設定 (.json)...");
            await DownloadFileAsync(PiperJsonUrl, jsonPath, ct);
        }

        _logger.LogInformation("Piper TTS 組件已就緒。");
    }

    private static async Task DownloadFileAsync(string url, string path, CancellationToken ct)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "JFVS-AI-Center-Server");
        client.Timeout = TimeSpan.FromMinutes(10);
        
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        
        using var fs = File.Create(path);
        await response.Content.CopyToAsync(fs, ct);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public string GetModelPath() => Path.Combine(_modelFolder, ModelFileName);
    public string GetOpenVinoXmlPath() => Path.Combine(_modelFolder, OpenVinoXmlName);
    
    public string GetPiperExePath() => Path.Combine(_piperFolder, "piper", "piper.exe");
    public string GetPiperModelPath() => Path.Combine(_piperFolder, "zh_CN-huayan-medium.onnx");
}
