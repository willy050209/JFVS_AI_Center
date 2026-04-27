namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 底層 MQTT 客戶端服務介面，負責純粹的通訊邏輯。
/// </summary>
public interface IMqttClientService
{
    Task PublishAsync(string topic, string payload, bool retain = false);
    bool IsConnected { get; }
}
