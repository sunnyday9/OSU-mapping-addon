using System.IO;
using ManagedBass;
using NUnit.Framework;
using osu.Game.Rulesets.AiStudio.Osu.Analysis;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

/// <summary>
/// <see cref="BassAudioAnalyzer"/> 的 BPM/节拍/段落检测测试（M2 交付物）。
/// 使用 <see cref="WavTestUtils"/> 合成的点击轨验证：
/// BPM 估计准确、节拍数量与首拍位置合理、段落为单段全覆盖、缺失文件抛异常。
/// </summary>
[TestFixture]
public class BassAudioAnalyzerTest
{
    private BassAudioAnalyzer analyzer = null!;

    private string? wavPath;

    [SetUp]
    public void Setup()
    {
        // 测试宿主可能已初始化 BASS（osu.Framework 音频引擎），返回 false / Already 错误忽略即可。
        Bass.Init(0, 44100, DeviceInitFlags.Default, IntPtr.Zero);
        analyzer = new BassAudioAnalyzer();
    }

    [TearDown]
    public void TearDown()
    {
        if (wavPath != null)
        {
            File.Delete(wavPath);
            wavPath = null;
        }
    }

    [Test]
    public void BpmDetectionIsAccurateAt120Bpm()
    {
        wavPath = createClickTrack(120);
        var grid = analyzer.AnalyseBeatAsync(wavPath, CancellationToken.None).GetAwaiter().GetResult();

        Assert.That(grid.Bpm, Is.InRange(119.5, 120.5));
        Assert.That(grid.BeatTimes.Count, Is.GreaterThanOrEqualTo(110));
        Assert.That(grid.BeatTimes[0], Is.InRange(0, 300));
    }

    [Test]
    public void BpmDetectionIsAccurateAt90Bpm()
    {
        wavPath = createClickTrack(90);
        var grid = analyzer.AnalyseBeatAsync(wavPath, CancellationToken.None).GetAwaiter().GetResult();

        Assert.That(grid.Bpm, Is.InRange(89.0, 91.0));
    }

    [Test]
    public void SectionsReturnSingleCoveringSection()
    {
        wavPath = createClickTrack(120);
        var sections = analyzer.AnalyseSectionsAsync(wavPath, CancellationToken.None).GetAwaiter().GetResult();

        Assert.That(sections, Has.Count.EqualTo(1));
        Assert.That(sections[0].StartTime, Is.EqualTo(0));
        Assert.That(sections[0].EndTime, Is.InRange(58000, 62000));
        Assert.That(sections[0].Intensity, Is.InRange(0, 1));
    }

    [Test]
    public void MissingFileThrows()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

        Assert.Throws<InvalidDataException>(() =>
            analyzer.AnalyseBeatAsync(missingPath, CancellationToken.None).GetAwaiter().GetResult());
    }

    private static string createClickTrack(double bpm)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
        return WavTestUtils.CreateClickTrackWav(path, bpm, 60);
    }
}
