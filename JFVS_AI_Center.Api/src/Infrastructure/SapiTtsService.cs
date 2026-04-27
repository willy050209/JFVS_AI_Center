using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using System.IO;

namespace JFVS_AI_Center.Api.Infrastructure;

public interface ISapiTtsService
{
    Task<byte[]> SynthesizeAsync(string text);
}

public class SapiTtsService : ISapiTtsService
{
    private readonly ILogger<SapiTtsService> _logger;

    public SapiTtsService(ILogger<SapiTtsService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task<byte[]> SynthesizeAsync(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

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
                _logger.LogError(ex, "SAPI 內部發生錯誤");
                throw;
            }
        });
    }
}
