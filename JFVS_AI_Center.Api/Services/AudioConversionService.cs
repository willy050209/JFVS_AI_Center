using Xabe.FFmpeg;

namespace JFVS_AI_Center.Api.Services;

/// <summary>
/// 負責將上傳的音訊檔案轉換為 Whisper 要求的 16kHz, 16-bit PCM WAV 格式。
/// </summary>
public class AudioConversionService(ILogger<AudioConversionService> logger)
{
    public async Task<string> ConvertToWavAsync(Stream inputStream, string extension, CancellationToken ct)
    {
        var tempInput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        var tempOutput = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.wav");

        try
        {
            logger.LogInformation("正在處理音訊轉碼...");
            
            using (var fs = File.Create(tempInput))
            {
                await inputStream.CopyToAsync(fs, ct);
            }

            var mediaInfo = await FFmpeg.GetMediaInfo(tempInput, ct);

            // 設定轉換參數：16kHz, 單聲道 (ac 1), 16-bit pcm (pcm_s16le)
            var conversion = FFmpeg.Conversions.New()
                .AddStream(mediaInfo.Streams)
                .SetOutput(tempOutput)
                .AddParameter("-c:a pcm_s16le")
                .AddParameter("-ar 16000")
                .AddParameter("-ac 1");

            await conversion.Start(ct);
            return tempOutput;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "音訊轉碼失敗");
            if (File.Exists(tempInput)) File.Delete(tempInput);
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
            throw;
        }
        finally
        {
            if (File.Exists(tempInput)) File.Delete(tempInput);
        }
    }
}
