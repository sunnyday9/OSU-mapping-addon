using System.IO;
using System.Text;

namespace osu.Game.Rulesets.AiStudio.Taiko.Tests;

internal static class WavTestUtils
{
    private const int SampleRate = 44100;
    private const int BitsPerSample = 16;
    private const double ClickDurationSeconds = 0.01;
    private const double ClickFrequency = 1000;
    private const double DecayEndRatio = 0.2;

    public static string CreateClickTrackWav(string path, double bpm, int durationSeconds, double amplitude = 0.8)
    {
        int totalSamples = SampleRate * durationSeconds;
        int clickLength = (int)Math.Round(ClickDurationSeconds * SampleRate);
        double clickStep = 60.0 / bpm * SampleRate;

        using var memory = new MemoryStream();
        using (var writer = new BinaryWriter(memory, Encoding.UTF8, leaveOpen: true))
        {
            WriteWavHeader(writer, totalSamples);

            int nextClick = 0;
            for (int i = 0; i < totalSamples; i++)
            {
                short sample = 0;
                if (i >= nextClick && i < nextClick + clickLength)
                {
                    double t = (i - nextClick) / (double)SampleRate;
                    double decay = 1 - (1 - DecayEndRatio) * (i - nextClick) / clickLength;
                    sample = (short)(Math.Sin(2 * Math.PI * ClickFrequency * t) * amplitude * decay * short.MaxValue);
                }

                if (i == nextClick + clickLength - 1)
                    nextClick = (int)Math.Round(nextClick + clickStep);

                writer.Write(sample);
            }
        }

        File.WriteAllBytes(path, memory.ToArray());
        return path;
    }

    private static void WriteWavHeader(BinaryWriter writer, int totalSamples)
    {
        int dataSize = totalSamples * (BitsPerSample / 8);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * (BitsPerSample / 8));
        writer.Write((short)(BitsPerSample / 8));
        writer.Write((short)BitsPerSample);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
    }
}
