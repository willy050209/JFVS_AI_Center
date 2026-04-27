using JFVS_AI_Center.Api.Infrastructure;

namespace JFVS_AI_Center.Api.Services;

/// <summary>
/// 設備控制服務實作，將抽象的設備控制需求轉換為具體的 MQTT 訊息。
/// </summary>
public class DeviceControlService : IDeviceControlService
{
    private readonly IMqttClientService _mqttClientService;
    private readonly ILogger<DeviceControlService> _logger;

    public DeviceControlService(IMqttClientService mqttClientService, ILogger<DeviceControlService> logger)
    {
        _mqttClientService = mqttClientService ?? throw new ArgumentNullException(nameof(mqttClientService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> ControlDeviceAsync(string deviceName, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        _logger.LogInformation("[設備控制] 正在處理請求: {DeviceName}, 動作: {Action}", deviceName, action);

        if (!_mqttClientService.IsConnected)
        {
            _logger.LogWarning("MQTT 客戶端尚未連線，無法執行指令");
            return "MQTT 伺服器連線中，請稍後再試。";
        }

        // 業務邏輯：對應設備名稱到 Topic
        string topic;
        if (deviceName.Contains("燈")) topic = "JFVS/esp32s1";
        else if (deviceName.Contains("風扇")) topic = "JFVS/esp32s2";
        else return $"無法識別的設備：{deviceName}";

        // 業務邏輯：對應動作到 Payload 與回應文字
        string mqttPayload;
        string statusTw;
        string actionStr = action.ToLowerInvariant();

        if (((string[])["on", "開", "打開", "true", "1"]).Contains(actionStr))
        {
            mqttPayload = "on";
            statusTw = "開啟";
        }
        else if (((string[])["off", "關", "關閉", "false", "0"]).Contains(actionStr))
        {
            mqttPayload = "off";
            statusTw = "關閉";
        }
        else
        {
            return $"無法識別的動作：{action}";
        }

        try
        {
            await _mqttClientService.PublishAsync(topic, mqttPayload);
            return $"已成功將{deviceName}{statusTw}囉！";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "執行設備控制時發生錯誤");
            return $"連線設備失敗：{e.Message}";
        }
    }
}
