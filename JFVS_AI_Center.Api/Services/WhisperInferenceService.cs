using Whisper.net;
using System.Text;

namespace JFVS_AI_Center.Api.Services;

/// <summary>
/// 封裝標準 Whisper 推論邏輯 (相容性最佳化)。
/// </summary>
public class WhisperInferenceService(ModelManagerService modelManager, ILogger<WhisperInferenceService> logger) : IDisposable
{
    private readonly WhisperFactory _factory = WhisperFactory.FromPath(modelManager.GetModelPath());
    private string? _detectedDevice;

    public async Task<string> TranscribeAsync(string wavPath, CancellationToken ct)
    {
        var device = GetBestDevice();
        try
        {
            using var processor = _factory.CreateBuilder()
                .WithLanguage("zh") // 固定為中文，縮短語言偵測時間
                .WithOpenVinoEncoder(modelManager.GetOpenVinoXmlPath(), device, null)
                .Build();

            using var fileStream = File.OpenRead(wavPath);
            var resultText = new StringBuilder();

            logger.LogInformation("開始執行語音辨識 (OpenVINO 加速模式，使用裝置: {Device})...", device);
            await foreach (var segment in processor.ProcessAsync(fileStream, ct))
            {
                resultText.Append(segment.Text);
            }

            var result = resultText.ToString().Trim();
            logger.LogInformation("辨識結果: {Text}", result);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "使用裝置 {Device} 進行轉錄時發生致命錯誤，清除偵測快取。", device);
            _detectedDevice = null; // 下次重新偵測
            throw;
        }
    }

    private string GetBestDevice()
    {
        if (_detectedDevice != null) return _detectedDevice;

        try
        {
            logger.LogInformation("正在使用 OpenVINO C API 探測可用裝置...");
            var availableDevices = OpenVinoDeviceDetector.GetAvailableDevices(logger);
            
            if (availableDevices.Count > 0)
            {
                logger.LogInformation("偵測到可用 OpenVINO 裝置: {Devices}", string.Join(", ", availableDevices));
                
                // 優先順序: NPU -> GPU -> CPU
                
                // 1. 尋找 NPU (包含 NPU.0 等變體)
                var npuDevice = availableDevices
                    .Where(d => d.StartsWith("NPU", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(d => d)
                    .FirstOrDefault();

                if (npuDevice != null)
                {
                    _detectedDevice = npuDevice;
                }
                else
                {
                    // 2. 尋找 GPU (包含 GPU.0, GPU.1 等)
                    // 在 Intel 平台上，通常 GPU.0 是內顯，GPU.1 是獨顯 (如果有)，優先選編號大的
                    var gpuDevice = availableDevices
                        .Where(d => d.StartsWith("GPU", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(d => d)
                        .FirstOrDefault();

                    if (gpuDevice != null)
                    {
                        _detectedDevice = gpuDevice;
                    }
                    else if (availableDevices.Any(d => d.Equals("CPU", StringComparison.OrdinalIgnoreCase)))
                    {
                        _detectedDevice = "CPU";
                    }
                }
            }
            else
            {
                logger.LogWarning("未偵測到任何 OpenVINO 加速裝置，將使用預設探測邏輯。");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "探測裝置時發生非預期錯誤");
        }

        if (_detectedDevice != null)
        {
            logger.LogInformation("選擇推論裝置: {Device}", _detectedDevice);
            return _detectedDevice;
        }

        // 備用方案: 傳統探測 (如果 C API 偵測失敗或列表為空)
        var devicesToTry = new[] { "GPU" };
        
        foreach (var device in devicesToTry)
        {
            try
            {
                logger.LogInformation("正在嘗試傳統探測 OpenVINO 裝置: {Device}...", device);
                using var testProcessor = _factory.CreateBuilder()
                    .WithOpenVinoEncoder(modelManager.GetOpenVinoXmlPath(), device, null)
                    .Build();
                
                _detectedDevice = device;
                return device;
            }
            catch (Exception ex)
            {
                logger.LogWarning("裝置 {Device} 探測失敗: {Message}", device, ex.Message);
            }
        }

        logger.LogWarning("所有高效能裝置均不可用或探測失敗，使用 CPU 模式。");
        _detectedDevice = "CPU";
        return _detectedDevice;
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
