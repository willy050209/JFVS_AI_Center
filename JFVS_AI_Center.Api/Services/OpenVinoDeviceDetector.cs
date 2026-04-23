using System.Runtime.InteropServices;
using System.Text;

namespace JFVS_AI_Center.Api.Services;

public static class OpenVinoDeviceDetector
{
    private const string DllName = "openvino_c";

    [StructLayout(LayoutKind.Sequential)]
    private struct ov_available_devices_t
    {
        public IntPtr devices;
        public UIntPtr size;
    }

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ov_core_create(out IntPtr core);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ov_core_free(IntPtr core);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ov_core_get_available_devices(IntPtr core, out ov_available_devices_t devices);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ov_available_devices_free(ref ov_available_devices_t devices);

    public static List<string> GetAvailableDevices(ILogger? logger = null)
    {
        var deviceList = new List<string>();
        IntPtr corePtr = IntPtr.Zero;
        try
        {
            int status = ov_core_create(out corePtr);
            if (status != 0)
            {
                logger?.LogWarning("無法建立 OpenVINO Core, 狀態碼: {Status}", status);
                return deviceList;
            }

            status = ov_core_get_available_devices(corePtr, out ov_available_devices_t availableDevices);
            if (status == 0)
            {
                int count = (int)availableDevices.size.ToUInt32();
                for (int i = 0; i < count; i++)
                {
                    IntPtr stringPtr = Marshal.ReadIntPtr(availableDevices.devices, i * IntPtr.Size);
                    string? deviceName = Marshal.PtrToStringAnsi(stringPtr);
                    if (!string.IsNullOrEmpty(deviceName))
                    {
                        deviceList.Add(deviceName);
                    }
                }
                ov_available_devices_free(ref availableDevices);
            }
            else
            {
                logger?.LogWarning("無法取得 OpenVINO 可用裝置, 狀態碼: {Status}", status);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "探測 OpenVINO 裝置時發生例外狀況 (可能是缺少 DLL 或不支援的平台)");
        }
        finally
        {
            if (corePtr != IntPtr.Zero)
            {
                ov_core_free(corePtr);
            }
        }

        return deviceList;
    }
}
