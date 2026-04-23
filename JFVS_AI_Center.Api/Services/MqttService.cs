using MQTTnet;
using MQTTnet.Client;
using System.Text;

namespace JFVS_AI_Center.Api.Services;

public class MqttService : IMqttService
{
    private readonly ILogger<MqttService> _logger;
    private readonly string _mqttHost = "broker.emqx.io";
    private readonly int _mqttPort = 1883;
    private readonly string _username = "jfvs000";
    private readonly string _password = "jfvs000";

    public MqttService(ILogger<MqttService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ControlDeviceAsync(string deviceName, string action)
    {
        _logger.LogInformation("[MQTT 工具觸發] 正在控制設備: {DeviceName}, 動作: {Action}", deviceName, action);

        string topic;
        if (deviceName.Contains("燈"))
        {
            topic = "JFVS/esp32s1";
        }
        else if (deviceName.Contains("風扇"))
        {
            topic = "JFVS/esp32s2";
        }
        else
        {
            return $"無法識別的設備：{deviceName}";
        }

        string mqttPayload;
        string statusTw;
        string actionStr = action.ToLower();

        if (new[] { "on", "開", "打開", "true", "1" }.Contains(actionStr))
        {
            mqttPayload = "on";
            statusTw = "開啟";
        }
        else if (new[] { "off", "關", "關閉", "false", "0" }.Contains(actionStr))
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
            var mqttFactory = new MqttFactory();
            using var mqttClient = mqttFactory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttHost, _mqttPort)
                .WithCredentials(_username, _password)
                .Build();

            await mqttClient.ConnectAsync(options, CancellationToken.None);

            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(mqttPayload)
                .Build();

            await mqttClient.PublishAsync(message, CancellationToken.None);
            await mqttClient.DisconnectAsync();

            return $"已成功將{deviceName}{statusTw}囉！";
        }
        catch (Exception e)
        {
            _logger.LogError(e, "連線設備失敗");
            return $"連線設備失敗：{e.Message}";
        }
    }
}
