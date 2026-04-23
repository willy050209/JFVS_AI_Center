namespace JFVS_AI_Center.Api.Services;

public interface IMqttService
{
    Task<string> ControlDeviceAsync(string deviceName, string action);
}
