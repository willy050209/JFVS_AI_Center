using Whisper.net;
using System.Text;

namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 語音辨識服務
/// </summary>
public class WhisperInferenceService : IDisposable
{
    private readonly WhisperFactory _factory;
    private readonly ModelPathProvider _pathProvider;
    private readonly ILogger<WhisperInferenceService> _logger;
    private string? _detectedDevice;

    public WhisperInferenceService(ModelPathProvider pathProvider, ILogger<WhisperInferenceService> logger)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _pathProvider = pathProvider;
        _logger = logger;
        _factory = WhisperFactory.FromPath(_pathProvider.WhisperModelPath);
    }

    public async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wavPath);

        var device = GetBestDevice();
        try
        {
            using var processor = _factory.CreateBuilder()
                .WithLanguage("zh")
                .WithOpenVinoEncoder(_pathProvider.WhisperOpenVinoXmlPath, device, null)
                .Build();

            using var fileStream = File.OpenRead(wavPath);
            var resultText = new StringBuilder();

            _logger.LogInformation("開始執行語音辨識 (OpenVINO 加速模式，使用裝置: {Device})...", device);
            await foreach (var segment in processor.ProcessAsync(fileStream, ct))
            {
                resultText.Append(segment.Text);
            }

            var result = resultText.ToString().Trim();
            _logger.LogInformation("辨識結果: {Text}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "使用裝置 {Device} 進行轉錄時發生致命錯誤，清除偵測快取。", device);
            _detectedDevice = null;
            throw;
        }
    }

    private string GetBestDevice()
    {
        if (_detectedDevice != null) return _detectedDevice;

        // 1. 嘗試透過 OpenVINO C API 探測
        _logger.LogInformation("正在探測 OpenVINO 最佳裝置...");
        _detectedDevice = OpenVinoDeviceDetector.GetBestDevice(_logger);

        if (_detectedDevice != null)
        {
            _logger.LogInformation("選擇推論裝置: {Device}", _detectedDevice);
            return _detectedDevice;
        }

        // 2. Fallback: 嘗試傳統探測 (針對 GPU)
        var devicesToTry = new[] { "GPU" };
        foreach (var device in devicesToTry)
        {
            try
            {
                _logger.LogInformation("正在嘗試傳統探測 OpenVINO 裝置: {Device}...", device);
                using var testProcessor = _factory.CreateBuilder()
                    .WithOpenVinoEncoder(_pathProvider.WhisperOpenVinoXmlPath, device, null)
                    .Build();
                
                _detectedDevice = device;
                return device;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("裝置 {Device} 傳統探測失敗: {Message}", device, ex.Message);
            }
        }

        // 3. 最終 Fallback: CPU
        _logger.LogWarning("所有高效能裝置均不可用或探測失敗，使用 CPU 模式。");
        _detectedDevice = "CPU";
        return _detectedDevice;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _factory.Dispose();
    }
}
