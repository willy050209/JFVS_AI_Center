using System.Diagnostics;
using System.Text;

namespace JFVS_AI_Center.Api.Services;

public interface ITtsService
{
    Task<byte[]> SynthesizeAsync(string text, string? voiceName = null);
}

public class TtsService(ModelManagerService modelManager, ILogger<TtsService> logger) : ITtsService
{
    public async Task<byte[]> SynthesizeAsync(string text, string? voiceName = null)
    {
        var piperExe = modelManager.GetPiperExePath();
        var modelPath = modelManager.GetPiperModelPath();

        if (!File.Exists(piperExe) || !File.Exists(modelPath))
        {
            throw new FileNotFoundException("Piper TTS 引擎或模型尚未就緒。");
        }

        logger.LogInformation("正在使用 Piper 本機合成語音: {Text}", text);

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

        // 1. 輸入文字 (Piper 讀取 stdin)
        using (var sw = new StreamWriter(process.StandardInput.BaseStream, new UTF8Encoding(false)))
        {
            await sw.WriteLineAsync(text);
        }

        // 2. 讀取輸出的 Raw PCM 數據
        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms);
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            logger.LogError("Piper 執行失敗: {Error}", error);
            throw new Exception($"Piper TTS 合成失敗: {error}");
        }

        // 3. 封裝 WAV 標頭 (Piper 預設 huayan-medium 通常是 22050Hz 或 16000Hz)
        // 根據 Piper 模型設定，huayan-medium 通常是 22050Hz
        // 我們先檢查 json 設定檔中的 sample_rate，或者預設使用 22050
        int sampleRate = 22050; 
        
        var rawData = ms.ToArray();
        return CreateWavWithHeader(rawData, sampleRate);
    }

    private byte[] CreateWavWithHeader(byte[] pcmData, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write("RIFF".ToCharArray());
        bw.Write(36 + pcmData.Length);
        bw.Write("WAVE".ToCharArray());

        // fmt chunk
        bw.Write("fmt ".ToCharArray());
        bw.Write(16); // subchunk1size
        bw.Write((short)1); // audio format (PCM)
        bw.Write((short)1); // num channels (Mono)
        bw.Write(sampleRate); // sample rate
        bw.Write(sampleRate * 2); // byte rate (sampleRate * numChannels * bitsPerSample/8)
        bw.Write((short)2); // block align (numChannels * bitsPerSample/8)
        bw.Write((short)16); // bits per sample

        // data chunk
        bw.Write("data".ToCharArray());
        bw.Write(pcmData.Length);
        bw.Write(pcmData);

        return ms.ToArray();
    }
}
