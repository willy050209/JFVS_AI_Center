using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace JFVS_AI_Center.Api.Infrastructure;

public class FfmpegInitializer(ModelPathProvider pathProvider, ILogger<FfmpegInitializer> logger) : IResourceInitializer
{
    public int Priority => 10;

    public async Task InitializeAsync(CancellationToken ct)
    {
        var ffmpegPath = pathProvider.BaseDir;
        if (!File.Exists(Path.Combine(ffmpegPath, "ffmpeg.exe")) && !File.Exists(Path.Combine(ffmpegPath, "ffmpeg")))
        {
            logger.LogInformation("未偵測到 FFmpeg，正在下載...");
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, ffmpegPath);
        }
        FFmpeg.SetExecutablesPath(ffmpegPath);
        logger.LogInformation("FFmpeg 已就緒。");
    }
}
