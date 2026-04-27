using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using System.IO;

namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// Windows SAPI 語音合成服務介面
/// </summary>
public interface ISapiTtsService
{
    Task<byte[]> SynthesizeAsync(string text);
}

/// <summary>
/// Windows SAPI 語音合成服務實作
/// <remarks>警告：此服務在 Windows 容器環境下通常無法運作，請優先使用基於 Piper 的 TtsService。</remarks>
/// </summary>
public class SapiTtsService : ISapiTtsService
{
    private readonly ILogger<SapiTtsService> _logger;
    private readonly bool _isRunningInContainer;

    public SapiTtsService(ILogger<SapiTtsService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        
        // 偵測是否運行於容器環境
        _isRunningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
    }

    public Task<byte[]> SynthesizeAsync(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (_isRunningInContainer)
        {
            _logger.LogError("偵測到處於容器環境，Windows SAPI (System.Speech) 無法運作。請改用 /api/tts (Piper)。");
            throw new PlatformNotSupportedException("Windows SAPI 在容器環境下不被支援，請改用 Piper TTS 服務。");
        }

        return Task.Run(() =>
        {
            _logger.LogInformation("開始 SAPI 合成: {Text}", text);
            
            try
            {
                using var synthesizer = new SpeechSynthesizer();
                _logger.LogInformation("SpeechSynthesizer 已建立");

                using var ms = new MemoryStream();
                synthesizer.SetOutputToWaveStream(ms);
                _logger.LogInformation("已設定輸出流");

                var voices = synthesizer.GetInstalledVoices();
                _logger.LogInformation("偵測到 {Count} 個語音角色", voices.Count);

                var chineseVoice = voices.FirstOrDefault(v => v.VoiceInfo.Culture.Name.Contains("zh"));
                if (chineseVoice != null)
                {
                    _logger.LogInformation("選擇語音: {Name}", chineseVoice.VoiceInfo.Name);
                    synthesizer.SelectVoice(chineseVoice.VoiceInfo.Name);
                }

                _logger.LogInformation("開始執行 Speak...");
                synthesizer.Speak(text);
                _logger.LogInformation("Speak 完成");

                synthesizer.SetOutputToNull();
                var data = ms.ToArray();
                _logger.LogInformation("合成成功，大小: {Size} bytes", data.Length);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SAPI 內部發生錯誤 (可能缺少語音引擎或音訊設備)");
                throw;
            }
        });
    }
}
