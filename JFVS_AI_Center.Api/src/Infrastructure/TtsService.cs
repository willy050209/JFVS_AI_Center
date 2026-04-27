using System.Diagnostics;
using System.Text;

namespace JFVS_AI_Center.Api.Infrastructure;

public interface ITtsService
{
    Task<byte[]> SynthesizeAsync(string text, string? voiceName = null);
}

public class TtsService : ITtsService
{
    private readonly ModelManagerService _modelManager;
    private readonly ILogger<TtsService> _logger;

    public TtsService(ModelManagerService modelManager, ILogger<TtsService> logger)
    {
        ArgumentNullException.ThrowIfNull(modelManager);
        ArgumentNullException.ThrowIfNull(logger);
        _modelManager = modelManager;
        _logger = logger;
    }

    public async Task<byte[]> SynthesizeAsync(string text, string? voiceName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var piperExe = _modelManager.GetPiperExePath();
        var modelPath = _modelManager.GetPiperModelPath();

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
        return CreateWavWithHeader(rawData, sampleRate);
    }

    private static byte[] CreateWavWithHeader(byte[] pcmData, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write("RIFF"u8.ToArray());
        bw.Write(36 + pcmData.Length);
        bw.Write("WAVE"u8.ToArray());

        bw.Write("fmt "u8.ToArray());
        bw.Write(16); 
        bw.Write((short)1); 
        bw.Write((short)1); 
        bw.Write(sampleRate); 
        bw.Write(sampleRate * 2); 
        bw.Write((short)2); 
        bw.Write((short)16); 

        bw.Write("data"u8.ToArray());
        bw.Write(pcmData.Length);
        bw.Write(pcmData);

        return ms.ToArray();
    }
}
