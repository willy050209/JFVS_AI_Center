using System.ComponentModel;

namespace JFVS_AI_Center.Api.Models;

/// <summary>
/// MQTT Broker 連線設定
/// </summary>
public record MqttOptions
{
    /// <summary>
    /// MQTT Broker 主機位址
    /// </summary>
    [DefaultValue("broker.emqx.io")]
    public string Host { get; init; } = "broker.emqx.io";

    /// <summary>
    /// MQTT Broker 埠號
    /// </summary>
    [DefaultValue(1883)]
    public int Port { get; init; } = 1883;

    /// <summary>
    /// 使用者名稱
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// 密碼
    /// </summary>
    public string Password { get; init; } = string.Empty;
}
