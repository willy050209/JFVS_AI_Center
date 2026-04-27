namespace JFVS_AI_Center.Api.Infrastructure.Utils;

/// <summary>
/// 音訊格式轉換工具
/// </summary>
public static class AudioFormatUtils
{
    /// <summary>
    /// 為原始 PCM 資料加入 WAV 檔頭
    /// </summary>
    public static byte[] CreateWavWithHeader(byte[] pcmData, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // RIFF 區塊
        bw.Write("RIFF"u8.ToArray());
        bw.Write(36 + pcmData.Length);
        bw.Write("WAVE"u8.ToArray());

        // fmt 區塊
        bw.Write("fmt "u8.ToArray());
        bw.Write(16); // 區塊大小
        bw.Write((short)1); // 音訊格式 (1 = PCM)
        bw.Write((short)1); // 聲道數 (1 = 單聲道)
        bw.Write(sampleRate); // 取樣率
        bw.Write(sampleRate * 2); // 每秒位元組數 (取樣率 * 聲道 * 位元深度/8)
        bw.Write((short)2); // 區塊對齊 (聲道 * 位元深度/8)
        bw.Write((short)16); // 位元深度

        // data 區塊
        bw.Write("data"u8.ToArray());
        bw.Write(pcmData.Length);
        bw.Write(pcmData);

        return ms.ToArray();
    }
}
