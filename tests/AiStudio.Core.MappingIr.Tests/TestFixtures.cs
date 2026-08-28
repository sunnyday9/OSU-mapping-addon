using AiStudio.Core.Analysis;
using AiStudio.Core.MappingIr.Analysis;
using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Timeline;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

/// <summary>测试公共夹具：构造标准 3 段式合成时间线与文档。</summary>
public static class TestFixtures
{
    public const double Bpm = 174.0;

    public const int DurationMs = 60000;

    public static SyntheticAudioAnalyzer Analyzer()
        => new(
            Bpm,
            DurationMs,
            new[] { 0.0, 20000.0, 40000.0 },
            new[] { 0.35, 0.85, 0.30 });

    public static MusicTimeline Timeline()
        => new MusicTimelineBuilder().Build(
            new BeatGrid(Bpm, 0, beatTimes()),
            new[]
            {
                new AudioSection(0, 20000, 0.35, AudioSectionType.Intro),
                new AudioSection(20000, 40000, 0.85, AudioSectionType.Chorus),
                new AudioSection(40000, 60000, 0.30, AudioSectionType.Outro),
            });

    private static List<double> beatTimes()
    {
        var beats = new List<double>();
        double beatMs = 60000.0 / Bpm;
        for (double t = 0; t <= DurationMs; t += beatMs)
            beats.Add(t);
        return beats;
    }

    public static DifficultyProfile BalancedProfile()
        => new(
            5.5,
            new DimensionProfile(0.72, 0.64, 0.55, 0.48, 0.42, 0.20, 0.30),
            new DifficultyPreferences(AllowExtremePatterns: false, PreferReadability: true, PreferMusicSync: true, PreferPatternVariety: true),
            0.15);

    /// <summary>构造完整文档（时间线 + 规划 + 全部 pattern 生成 + 校验 + Critic），走生产管线。</summary>
    public static MappingDocument BuildDocument(int seed = 42)
    {
        // 管线的 hashAudio 需要真实文件：写占位音频（SyntheticAudioAnalyzer 返回构造时注入的合成数据）。
        string audioPath = Path.Combine(Path.GetTempPath(), $"aistudio_fixture_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(audioPath, "fixture audio placeholder");
        try
        {
            return new MappingIrPipeline(Analyzer()).Run(audioPath, BalancedProfile(), seed);
        }
        finally
        {
            File.Delete(audioPath);
        }
    }
}
