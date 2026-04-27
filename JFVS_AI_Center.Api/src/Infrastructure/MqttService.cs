namespace JFVS_AI_Center.Api.Infrastructure;

using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using JFVS_AI_Center.Api.Models;

/// <summary>
/// MQTT 服務介面
/// </summary>
public interface IMqttService
{
    Task<string> ControlDeviceAsync(string deviceName, string action);
}

/// <summary>
/// MQTT 服務實作
/// </summary>
public class MqttService : IMqttService, IHostedService, IDisposable
{
    private readonly ILogger<MqttService> _logger;
    private readonly MqttOptions _options;
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _mqttClientOptions;
    private bool _disposed;

    public MqttService(ILogger<MqttService> logger, IOptions<MqttOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;

        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        _mqttClientOptions = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithCredentials(_options.Username, _options.Password)
            .WithCleanSession()
            .Build();

        _mqttClient.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("MQTT 已斷線，正在嘗試重連...");
            await Task.Delay(TimeSpan.FromSeconds(5));
            try
            {
                if (!_disposed)
                {
                    await _mqttClient.ConnectAsync(_mqttClientOptions, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT 重連失敗");
            }
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在啟動 MQTT 服務...");
        try
        {
            await _mqttClient.ConnectAsync(_mqttClientOptions, cancellationToken);
            _logger.LogInformation("MQTT 已成功連線至 {Host}", _options.Host);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT 初始連線失敗");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("正在停止 MQTT 服務...");
        if (_mqttClient.IsConnected)
        {
            await _mqttClient.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken);
        }
    }

    public async Task<string> ControlDeviceAsync(string deviceName, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        _logger.LogInformation("[MQTT 工具觸發] 正在控制設備: {DeviceName}, 動作: {Action}", deviceName, action);

        if (!_mqttClient.IsConnected)
        {
            _logger.LogWarning("MQTT 尚未連線，嘗試發送指令失敗");
            return "MQTT 伺服器連線中，請稍後再試。";
        }

        string topic;
        if (deviceName.Contains("燈")) topic = "JFVS/esp32s1";
        else if (deviceName.Contains("風扇")) topic = "JFVS/esp32s2";
        else return $"無法識別的設備：{deviceName}";

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
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(mqttPayload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _mqttClient.PublishAsync(message, CancellationToken.None);

            return $"已成功將{deviceName}{statusTw}囉！";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "發送 MQTT 指令失敗");
            return $"連線設備失敗：{e.Message}";
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _disposed = true;
        _mqttClient?.Dispose();
    }
}
