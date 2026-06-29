using System.Speech.Synthesis;
using System.Speech.AudioFormat;
using JFVS_AI_Center.Api.Infrastructure.Utils;
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
			_logger.LogInformation("開始 SAPI 安全合成 (ESP32 優化版): {Text}", text);
			
			try
			{
				using var synthesizer = new SpeechSynthesizer();
				using var ms = new MemoryStream();

				// 嚴格限制輸出格式為 16000Hz, 16-bit, 單聲道的純 Raw PCM 數據（流中不會包含 WAV 檔頭與雜訊）
				var audioFormat = new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
				synthesizer.SetOutputToAudioStream(ms, audioFormat);

				var voices = synthesizer.GetInstalledVoices();
				var chineseVoice = voices.FirstOrDefault(v => v.VoiceInfo.Culture.Name.Contains("zh"));
				if (chineseVoice != null)
				{
					_logger.LogInformation("選擇語音: {Name}", chineseVoice.VoiceInfo.Name);
					synthesizer.SelectVoice(chineseVoice.VoiceInfo.Name);
				}

				synthesizer.Speak(text);
				synthesizer.SetOutputToNull();

				// 取得乾淨的純 PCM 陣列
				var rawPcmData = ms.ToArray();
				_logger.LogInformation("SAPI 純 PCM 合成成功，大小: {Size} bytes", rawPcmData.Length);

				// 調用音訊工具，加上乾淨、標準、無額外元數據區塊的 44 位元組 WAV 檔頭
				// 確保 ESP32 的解碼庫在讀取前 44 位元組後，能完美對齊硬體 I2S 時鐘並順暢播放
				return AudioFormatUtils.CreateWavWithHeader(rawPcmData, 16000);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "SAPI 內部發生錯誤");
				throw;
			}
		});
	}
}
