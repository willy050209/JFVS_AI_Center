using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JFVS_AI_Center.Api.Models;

namespace JFVS_AI_Center.Api.Infrastructure;

/// <summary>
/// 負責建立 Python 虛擬環境、安裝依賴，並在啟動時執行 My-AI-Server 的託管服務與資源初始化器。
/// </summary>
public class MyAiServerService(
    IOptions<AiOptions> aiOptions,
    ILogger<MyAiServerService> logger) : IResourceInitializer, IHostedService
{
    private Process? _serverProcess;
    private readonly AiOptions _options = aiOptions.Value;

    /// <summary>
    /// 初始化器的執行優先級。設定為 5 以確保在 Whisper / Piper 等其他推論初始化器之前執行。
    /// </summary>
    public int Priority => 5;

    /// <summary>
    /// 執行初始化：建立虛擬環境、安裝依賴、啟動伺服器並等待就緒。
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        logger.LogInformation("正在檢查並準備 My-AI-Server 執行環境...");

        var myAiServerDir = FindMyAiServerDirectory();
        if (myAiServerDir == null)
        {
            throw new DirectoryNotFoundException("找不到 My-AI-Server 目錄。請確認專案結構中包含 My-AI-Server 資料夾。");
        }

        logger.LogInformation("找到 My-AI-Server 目錄: {Path}", myAiServerDir);

        var venvDir = Path.Combine(myAiServerDir, ".venv");
        var sentinelFile = Path.Combine(venvDir, "sentinel.txt");
        var pythonExe = Path.Combine(venvDir, "Scripts", "python.exe");

        // 檢查虛擬環境是否已初始化
        bool setupVenv = !Directory.Exists(venvDir) || !File.Exists(sentinelFile);

        if (setupVenv)
        {
            logger.LogInformation("未偵測到虛擬環境或初始化標記，正在建立虛擬環境與安裝依賴...");

            // 1. 建立 venv
            var venvCreated = await RunCommandAsync("python", "-m venv .venv", myAiServerDir, ct);
            if (!venvCreated)
            {
                throw new InvalidOperationException("建立 Python 虛擬環境 (.venv) 失敗，請確認本機已安裝 Python 並加入環境變數中。");
            }

            // 2. 安裝 requirements.txt 依賴
            if (!File.Exists(pythonExe))
            {
                throw new FileNotFoundException($"找不到虛擬環境中的 Python 執行檔: {pythonExe}");
            }

            var pipInstalled = await RunCommandAsync(pythonExe, "-m pip install -r requirements.txt --disable-pip-version-check", myAiServerDir, ct);
            if (!pipInstalled)
            {
                throw new InvalidOperationException("安裝 requirements.txt 依賴失敗。");
            }

            // 寫入初始化成功標記檔
            Directory.CreateDirectory(venvDir);
            await File.WriteAllTextAsync(sentinelFile, $"Initialized on {DateTime.Now}", ct);
            logger.LogInformation("虛擬環境與依賴初始化成功。");
        }
        else
        {
            logger.LogInformation("偵測到現有的虛擬環境，跳過環境初始化。");
        }

        // 解析連接埠口，若解析失敗則預設為 8000
        int port = 8000;
        try
        {
            var uri = new Uri(_options.Endpoint);
            port = uri.Port;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "解析 AI Endpoint 連接埠口失敗，將使用預設的 8000。Endpoint: {Endpoint}", _options.Endpoint);
        }

        // 啟動 FastAPI 伺服器
        StartServerProcess(pythonExe, port, myAiServerDir);

        // 等待伺服器就緒
        var isReady = await WaitForServerReadyAsync(port, ct);
        if (!isReady)
        {
            throw new InvalidOperationException("My-AI-Server 啟動失敗或超時未回應。");
        }
    }

    /// <summary>
    /// HostedService 的啟動進入點（由 IResourceInitializer.InitializeAsync 處理主要邏輯）。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// HostedService 的停止進入點，負責清理 Python 處理序樹。
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            logger.LogInformation("正在停止 My-AI-Server 服務進程...");
            try
            {
                _serverProcess.Kill(entireProcessTree: true);
                await _serverProcess.WaitForExitAsync(cancellationToken);
                logger.LogInformation("My-AI-Server 服務已成功停止。");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "停止 My-AI-Server 進程時發生異常。");
            }
            finally
            {
                _serverProcess.Dispose();
                _serverProcess = null;
            }
        }
    }

    private string? FindMyAiServerDirectory()
    {
        var startDirs = new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var startDir in startDirs)
        {
            var dir = startDir;
            while (!string.IsNullOrEmpty(dir))
            {
                var target = Path.Combine(dir, "My-AI-Server");
                if (Directory.Exists(target) && File.Exists(Path.Combine(target, "requirements.txt")))
                {
                    return Path.GetFullPath(target);
                }
                var parent = Directory.GetParent(dir);
                if (parent == null || parent.FullName == dir) break;
                dir = parent.FullName;
            }
        }
        return null;
    }

    private async Task<bool> RunCommandAsync(string fileName, string arguments, string workingDirectory, CancellationToken ct)
    {
        logger.LogInformation("執行指令: {FileName} {Arguments} 於 {WorkingDirectory}", fileName, arguments, workingDirectory);
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.OutputDataReceived += (s, e) => { if (e.Data != null) logger.LogInformation("[Python Setup] {Data}", e.Data); };
        process.ErrorDataReceived += (s, e) => { if (e.Data != null) logger.LogWarning("[Python Setup Error] {Data}", e.Data); };

        if (!process.Start())
        {
            logger.LogError("無法啟動程序: {FileName}", fileName);
            return false;
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0;
    }

    /// <summary>
    /// 依 Python/uvicorn 日誌前綴（INFO/WARNING/ERROR/CRITICAL）將輸出路由至正確的 ILogger 等級，
    /// 避免 uvicorn 啟動訊息因輸出至 stderr 而被誤標為 fail:。
    /// </summary>
    private void LogPythonLine(string line)
    {
        if (line.StartsWith("CRITICAL:", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("[My-AI-Server] {Line}", line);
        }
        else if (line.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase) ||
                 line.StartsWith("WARN:", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[My-AI-Server] {Line}", line);
        }
        else
        {
            logger.LogInformation("[My-AI-Server] {Line}", line);
        }
    }

    private void StartServerProcess(string pythonExe, int port, string myAiServerDir)
    {
        logger.LogInformation("正在啟動 My-AI-Server 於埠口 {Port}... 預設載入模型: {Model}", port, _options.Model);

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"-m uvicorn app.main:app --host 127.0.0.1 --port {port}",
            WorkingDirectory = myAiServerDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // 將組態傳遞至 Python 進程的環境變數中
        startInfo.EnvironmentVariables["HOST"] = "127.0.0.1";
        startInfo.EnvironmentVariables["PORT"] = port.ToString();
        startInfo.EnvironmentVariables["DEFAULT_MODEL"] = _options.Model;
        startInfo.EnvironmentVariables["OPENVINO_DEVICE"] = "AUTO";
        startInfo.EnvironmentVariables["MODELS_DIR"] = "./models";

        _serverProcess = new Process { StartInfo = startInfo };

        _serverProcess.OutputDataReceived += (s, e) => { if (e.Data != null) LogPythonLine(e.Data); };
        // uvicorn 故意將 INFO/WARNING 等啟動訊息輸出至 stderr，需依前綴智慧路由日誌等級
        _serverProcess.ErrorDataReceived += (s, e) => { if (e.Data != null) LogPythonLine(e.Data); };

        if (!_serverProcess.Start())
        {
            throw new InvalidOperationException("無法啟動 My-AI-Server 進程。");
        }

        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();
    }

    private async Task<bool> WaitForServerReadyAsync(int port, CancellationToken ct)
    {
        using var client = new HttpClient();
        var url = $"http://127.0.0.1:{port}/v1/models";
        var startTime = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(300); // 預留足夠的時間讓第一次啟動下載並載入模型（首次下載約需 3-5 分鐘）

        logger.LogInformation("正在等待 My-AI-Server 回應 (目標網址: {Url})...", url);

        while (DateTime.UtcNow - startTime < timeout)
        {
            if (ct.IsCancellationRequested) return false;

            if (_serverProcess == null || _serverProcess.HasExited)
            {
                logger.LogError("My-AI-Server 進程已提前退出。");
                return false;
            }

            try
            {
                var response = await client.GetAsync(url, ct);
                if (response.IsSuccessStatusCode)
                {
                    logger.LogInformation("My-AI-Server 已成功就緒並回應。");
                    return true;
                }
            }
            catch
            {
                // 忽略連線失敗的例外，直到超時為止
            }

            await Task.Delay(1000, ct);
        }

        logger.LogError("等待 My-AI-Server 啟動超時。");
        return false;
    }
}
