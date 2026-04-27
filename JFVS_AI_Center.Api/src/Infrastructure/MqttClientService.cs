using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using JFVS_AI_Center.Api.Models;

namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// MQTT 客戶端實作，專注於連線管理與訊息發送。
/// </summary>
public class MqttClientService : IMqttClientService, IHostedService, IDisposable
{
    private readonly ILogger<MqttClientService> _logger;
    private readonly MqttOptions _options;
    private readonly IMqttClient _mqttClient;
    private readonly MqttClientOptions _mqttClientOptions;
    private bool _disposed;

    public MqttClientService(ILogger<MqttClientService> logger, IOptions<MqttOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        var mqttClientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithTcpServer(_options.Host, _options.Port)
            .WithClientId($"JFVS_AI_Center_{Guid.NewGuid():N}")
            .WithCleanSession();

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            mqttClientOptionsBuilder.WithCredentials(_options.Username, _options.Password);
        }

        _mqttClientOptions = mqttClientOptionsBuilder.Build();

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

    public async Task PublishAsync(string topic, string payload, bool retain = false)
    {
        if (!_mqttClient.IsConnected)
        {
            throw new InvalidOperationException("MQTT 尚未連線，無法發送訊息。");
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithRetainFlag(retain)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(message, CancellationToken.None);
    }

    public bool IsConnected => _mqttClient.IsConnected;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _disposed = true;
        _mqttClient?.Dispose();
    }
}
