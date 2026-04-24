namespace JFVS_AI_Center.Api.Models;

public class MqttOptions
{
    public string Host { get; set; } = "broker.emqx.io";
    public int Port { get; set; } = 1883;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
