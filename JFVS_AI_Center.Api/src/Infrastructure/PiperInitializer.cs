using System.IO.Compression;

namespace JFVS_AI_Center.Api.Infrastructure;

public class PiperInitializer(
    ModelPathProvider pathProvider, 
    IFileDownloadService downloadService, 
    ILogger<PiperInitializer> logger) : IResourceInitializer
{
    private const string PiperZipUrl = "https://github.com/rhasspy/piper/releases/latest/download/piper_windows_amd64.zip";
    private const string PiperOnnxUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx";
    private const string PiperJsonUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/zh/zh_CN/huayan/medium/zh_CN-huayan-medium.onnx.json";

    public int Priority => 30;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!Directory.Exists(pathProvider.PiperFolder)) Directory.CreateDirectory(pathProvider.PiperFolder);

        if (!File.Exists(pathProvider.PiperExePath))
        {
            logger.LogInformation("正在下載 Piper TTS 引擎...");
            string zipPath = Path.Combine(pathProvider.PiperFolder, "piper.zip");
            await downloadService.DownloadFileAsync(PiperZipUrl, zipPath, ct);
            
            logger.LogInformation("正在解壓縮 Piper...");
            ZipFile.ExtractToDirectory(zipPath, pathProvider.PiperFolder, overwriteFiles: true);
            File.Delete(zipPath);
        }

        if (!File.Exists(pathProvider.PiperModelPath))
        {
            logger.LogInformation("正在下載 Piper 中文模型 (.onnx)...");
            await downloadService.DownloadFileAsync(PiperOnnxUrl, pathProvider.PiperModelPath, ct);
        }

        if (!File.Exists(pathProvider.PiperJsonPath))
        {
            logger.LogInformation("正在下載 Piper 中文模型設定 (.json)...");
            await downloadService.DownloadFileAsync(PiperJsonUrl, pathProvider.PiperJsonPath, ct);
        }

        logger.LogInformation("Piper TTS 組件已就緒。");
    }
}
