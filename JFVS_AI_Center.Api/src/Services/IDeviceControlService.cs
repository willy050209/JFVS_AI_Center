namespace JFVS_AI_Center.Api.Services;

/// <summary>
/// 設備控制服務介面，負責處理具體的業務控制邏輯與回應。
/// </summary>
public interface IDeviceControlService
{
    Task<string> ControlDeviceAsync(string deviceName, string action);
}
