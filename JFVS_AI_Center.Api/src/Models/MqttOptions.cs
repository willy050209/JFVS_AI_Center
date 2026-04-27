namespace JFVS_AI_Center.Api.Models;

/// <summary>
/// MQTT 連線設定
/// </summary>
public record MqttOptions
{
    public string Host { get; init; } = "broker.emqx.io";
    public int Port { get; init; } = 1883;
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
