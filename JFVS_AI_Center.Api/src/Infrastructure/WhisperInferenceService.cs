using Whisper.net;
using System.Text;

namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 語音辨識服務
/// </summary>
public class WhisperInferenceService : IDisposable
{
    private readonly WhisperFactory _factory;
    private readonly ModelManagerService _modelManager;
    private readonly ILogger<WhisperInferenceService> _logger;
    private string? _detectedDevice;

    public WhisperInferenceService(ModelManagerService modelManager, ILogger<WhisperInferenceService> logger)
    {
        ArgumentNullException.ThrowIfNull(modelManager);
        ArgumentNullException.ThrowIfNull(logger);
        _modelManager = modelManager;
        _logger = logger;
        _factory = WhisperFactory.FromPath(_modelManager.GetModelPath());
    }

    public async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(wavPath);

        var device = GetBestDevice();
        try
        {
            using var processor = _factory.CreateBuilder()
                .WithLanguage("zh")
                .WithOpenVinoEncoder(_modelManager.GetOpenVinoXmlPath(), device, null)
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

        try
        {
            _logger.LogInformation("正在使用 OpenVINO C API 探測可用裝置...");
            var availableDevices = OpenVinoDeviceDetector.GetAvailableDevices(_logger);
            
            if (availableDevices.Count != 0)
			{
				_logger.LogInformation("偵測到可用 OpenVINO 裝置: {Devices}", string.Join(", ", availableDevices));

				string[] priorityPrefixes = ["NPU", "GPU", "CPU"];

				_detectedDevice = priorityPrefixes
					.Select(prefix => availableDevices
						.Where(d => d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
						.OrderByDescending(d => d)
						.FirstOrDefault())
					.FirstOrDefault(d => d != null);
			}
			else
			{
				_logger.LogWarning("未偵測到任何 OpenVINO 加速裝置，將使用預設探測邏輯。");
			}
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "探測裝置時發生非預期錯誤");
        }

        if (_detectedDevice != null)
        {
            _logger.LogInformation("選擇推論裝置: {Device}", _detectedDevice);
            return _detectedDevice;
        }

        var devicesToTry = new[] { "GPU" };
        
        foreach (var device in devicesToTry)
        {
            try
            {
                _logger.LogInformation("正在嘗試傳統探測 OpenVINO 裝置: {Device}...", device);
                using var testProcessor = _factory.CreateBuilder()
                    .WithOpenVinoEncoder(_modelManager.GetOpenVinoXmlPath(), device, null)
                    .Build();
                
                _detectedDevice = device;
                return device;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("裝置 {Device} 探測失敗: {Message}", device, ex.Message);
            }
        }

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
