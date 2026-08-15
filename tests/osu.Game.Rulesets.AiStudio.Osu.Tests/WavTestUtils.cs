using System.IO;
using System.Text;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

/// <summary>
/// 测试用 WAV 生成工具：合成单声道 16-bit PCM 44100Hz 点击轨，
/// 每拍一个 10ms 的 1000Hz 正弦线性衰减脉冲，其余采样为 0。
/// </summary>
internal static class WavTestUtils
{
    private const int SampleRate = 44100;

    private const int BitsPerSample = 16;

    private const double ClickDurationSeconds = 0.01;

    private const double ClickFrequency = 1000;

    /// <summary>
    /// 衰减包络的终点比例（脉冲起始为 amplitude，10ms 内线性衰减到 amplitude 的 20%）。
    /// </summary>
    private const double DecayEndRatio = 0.2;

    /// <summary>
    /// 生成点击轨 WAV 并写入 <paramref name="path"/>，返回该路径。
    /// </summary>
    /// <param name="path">输出文件路径。</param>
    /// <param name="bpm">节拍速度（每分钟节拍数）。</param>
    /// <param name="durationSeconds">轨道时长（秒）。</param>
    /// <param name="amplitude">脉冲峰值幅度（0..1）。</param>
    public static string CreateClickTrackWav(string path, double bpm, int durationSeconds, double amplitude = 0.8)
    {
        int totalSamples = SampleRate * durationSeconds;
        int clickLength = (int)Math.Round(ClickDurationSeconds * SampleRate);
        double clickStep = 60.0 / bpm * SampleRate;

        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true))
        {
            writeWavHeader(writer, totalSamples);

            int nextClick = 0;
            for (int i = 0; i < totalSamples; i++)
            {
                short sample = 0;
                if (i >= nextClick && i < nextClick + clickLength)
                {
                    // 脉冲内：t 为脉冲内时间（秒），包络从 1 线性衰减到 DecayEndRatio。
                    double t = (i - nextClick) / (double)SampleRate;
                    double decay = 1 - (1 - DecayEndRatio) * (i - nextClick) / clickLength;
                    sample = (short)(Math.Sin(2 * Math.PI * ClickFrequency * t) * amplitude * decay * short.MaxValue);
                }

                // 注意：在脉冲"结束"时才推进下一个脉冲起点（若在起点推进，脉冲窗口会退化为单采样静音）。
                if (i == nextClick + clickLength - 1)
                    nextClick = (int)Math.Round(nextClick + clickStep);

                writer.Write(sample);
            }
        }

        File.WriteAllBytes(path, memory.ToArray());
        return path;
    }

    /// <summary>
    /// 写标准 RIFF/WAVE 头（44 字节：fmt 16 字节 PCM + data）。
    /// </summary>
    private static void writeWavHeader(BinaryWriter writer, int totalSamples)
    {
        int dataSize = totalSamples * (BitsPerSample / 8);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // fmt 块大小
        writer.Write((short)1); // PCM
        writer.Write((short)1); // 单声道
        writer.Write(SampleRate);
        writer.Write(SampleRate * (BitsPerSample / 8)); // 字节率
        writer.Write((short)(BitsPerSample / 8)); // 块对齐
        writer.Write((short)BitsPerSample);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
    }
}
