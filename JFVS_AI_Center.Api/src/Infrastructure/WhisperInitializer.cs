using System.IO.Compression;

namespace JFVS_AI_Center.Api.Infrastructure;

public class WhisperInitializer(
    ModelPathProvider pathProvider, 
    IFileDownloadService downloadService, 
    WhisperInferenceService whisperService,
    ILogger<WhisperInitializer> logger) : IResourceInitializer
{
    private const string ModelZipName = "ggml-base-models.zip";
    public int Priority => 20;

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (!Directory.Exists(pathProvider.ModelFolder)) Directory.CreateDirectory(pathProvider.ModelFolder);

        if (!File.Exists(pathProvider.WhisperModelPath) || 
            !File.Exists(pathProvider.WhisperOpenVinoXmlPath) || 
            !File.Exists(pathProvider.WhisperOpenVinoBinPath))
        {
            string zipPath = Path.Combine(pathProvider.ModelFolder, ModelZipName);
            if (!File.Exists(zipPath))
            {
                logger.LogInformation("正在下載 Whisper OpenVINO 模型包...");
                await downloadService.DownloadFileAsync("https://huggingface.co/Intel/whisper.cpp-openvino-models/resolve/main/ggml-base-models.zip", zipPath, ct);
            }

            logger.LogInformation("正在解壓縮 Whisper 模型...");
            ZipFile.ExtractToDirectory(zipPath, pathProvider.ModelFolder, overwriteFiles: true);
            File.Delete(zipPath);
        }

        logger.LogInformation("Whisper 模型已就緒。");
        
        // 觸發預熱
        _ = whisperService.PreloadAsync(ct);
    }
}
