using System.Diagnostics;
using System.Text;
using JFVS_AI_Center.Api.Infrastructure.Utils;

namespace JFVS_AI_Center.Api.Infrastructure;

public interface ITtsService
{
    Task<byte[]> SynthesizeAsync(string text, string? voiceName = null);
}

public class TtsService : ITtsService
{
    private readonly ModelPathProvider _pathProvider;
    private readonly ILogger<TtsService> _logger;

    public TtsService(ModelPathProvider pathProvider, ILogger<TtsService> logger)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        ArgumentNullException.ThrowIfNull(logger);
        _pathProvider = pathProvider;
        _logger = logger;
    }

    public async Task<byte[]> SynthesizeAsync(string text, string? voiceName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var piperExe = _pathProvider.PiperExePath;
        var modelPath = _pathProvider.PiperModelPath;

        if (!File.Exists(piperExe) || !File.Exists(modelPath))
        {
            throw new FileNotFoundException("Piper TTS 引擎或模型尚未就緒。");
        }

        _logger.LogInformation("正在使用 Piper 本機合成語音: {Text}", text);

        var startInfo = new ProcessStartInfo
        {
            FileName = piperExe,
            Arguments = $"--model \"{modelPath}\" --output_raw",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using (var sw = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
        {
            await sw.WriteLineAsync(text);
        }

        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms);
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            _logger.LogError("Piper 執行失敗: {Error}", error);
            throw new InvalidOperationException($"Piper TTS 合成失敗: {error}");
        }

        int sampleRate = 22050; 
        var rawData = ms.ToArray();
        
        return AudioFormatUtils.CreateWavWithHeader(rawData, sampleRate);
    }
}
