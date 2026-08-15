using System.IO;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.AiStudio.Osu.Synthesis;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

[TestFixture]
public class OsuMapGeneratorTest
{
    /// <summary>
    /// 固定节拍网格的假分析器：120BPM、首拍 0、每 500ms 一拍、共 60 拍（30 秒）、
    /// 单一高强段落（强度 0.6 &gt; 0.45 → 生成器走 dense 模式）。
    /// </summary>
    private sealed class FakeAudioAnalyzer : IAudioAnalyzer
    {
        public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult(new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList()));

        public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudioSection>>(new[] { new AudioSection(0, 30000, 0.6) });
    }

    [Test]
    public void GenerateSucceedsWithAllGatesPassing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-gen-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 生成器 A 步只检查文件存在性；假分析器不读取音频内容，占位文件即可。
            File.WriteAllText(audioPath, "placeholder");

            var result = generate(audioPath, outputDir, targetStarRating: 3.5);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.QualityReport, Is.Not.Null);
            Assert.That(result.QualityReport.AllPassed, Is.True);
            Assert.That(result.OutputFilePath, Is.Not.Null);
            Assert.That(File.Exists(result.OutputFilePath), Is.True);
            Assert.That(result.OutputFilePath, Does.EndWith(".osu"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void StarRatingIsWithinTolerance()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-gen-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var result = generate(audioPath, outputDir, targetStarRating: 3.5);
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            // roundtrip 解码后重算 SR（与导出产物一致）。
            var decoded = decodeOsu(result.OutputFilePath!);
            double sr = new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, new InMemoryWorkingBeatmap(decoded)).Calculate().StarRating;

            Assert.That(Math.Abs(sr - 3.5), Is.LessThanOrEqualTo(0.31));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void ExportedFileRoundTrips()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-gen-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var result = generate(audioPath, outputDir, targetStarRating: 3.5);
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            var decoded = decodeOsu(result.OutputFilePath!);

            // 生成器候选逻辑的镜像：60 拍 dense，最后 2 拍不落点 → 58 拍 × 每拍 2 候选 = 116。
            // 实测（2026.730.0）编码→解码为 1:1 无损（116/116），此处做精确相等断言。
            int expectedObjectCount = (60 - 2) * 2;
            Assert.That(decoded.HitObjects, Is.Not.Empty);
            Assert.That(decoded.HitObjects.Count, Is.EqualTo(expectedObjectCount));
            // 解码产物是 Legacy 转换对象（internal 类型，不可直接引用），按类型名断言。
            var decodedTypeNames = decoded.HitObjects.Select(h => h.GetType().Name).ToHashSet();
            Assert.That(decodedTypeNames, Does.Contain("ConvertSlider"), "导出的谱面应包含 slider");
            Assert.That(decodedTypeNames, Does.Contain("ConvertHitCircle"), "导出的谱面应包含 circle");
            Assert.That(decoded.HitObjects.Select(h => h.StartTime).All(t => isOnGrid(t)), Is.True, "导出谱面所有物件都应落在拍/半拍网格上");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>物件时间是否落在 500ms 拍/250ms 半拍网格上（容差 1ms）。</summary>
    private static bool isOnGrid(double time)
    {
        const double beat_length = 500;
        const double tolerance = 1.0;
        double beatPhase = Math.Abs(time % beat_length);
        return beatPhase <= tolerance || Math.Abs(beatPhase - beat_length / 2) <= tolerance;
    }

    [Test]
    public void UnreachableStarRatingFailsGracefully()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-gen-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            // 8.0★ 超出校准夹逼区间（spacing 乘子 ≤ 1.8 时 120BPM dense 上限约 3.3★）→ G5 失败、不落盘。
            var result = generate(audioPath, outputDir, targetStarRating: 8.0);

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(result.QualityReport, Is.Not.Null);
            Assert.That(result.QualityReport.AllPassed, Is.False);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void MissingAudioFileFails()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-gen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string missingAudio = Path.Combine(tempDir, "does_not_exist.mp3");
            var settings = createSettings(missingAudio, tempDir, 3.5);

            var result = new OsuMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void GenerateFromRealClickTrackWav()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-gen-{Guid.NewGuid():N}");
        string wavPath = Path.Combine(tempDir, "clicktrack.wav");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 真实分析器 + 真实 60s 120BPM 点击音轨（WavTestUtils 与 BassAudioAnalyzer 同批交付）。
            WavTestUtils.CreateClickTrackWav(wavPath, bpm: 120, durationSeconds: 60, amplitude: 0.8);
            Assert.That(File.Exists(wavPath), Is.True, "WAV 生成失败");

            var settings = createSettings(wavPath, outputDir, targetStarRating: 3.0);
            var result = new OsuMapGenerator().GenerateAsync(settings).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(File.Exists(result.OutputFilePath), Is.True);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static GenerationSettings createSettings(string audioPath, string outputDir, double targetStarRating)
        => new GenerationSettings
        {
            AudioPath = audioPath,
            TargetLevel = DifficultyLevel.Hard,
            TargetStarRating = targetStarRating,
            OutputDirectory = outputDir,
        };

    private static GenerationResult generate(string audioPath, string outputDir, double targetStarRating)
        => new OsuMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(createSettings(audioPath, outputDir, targetStarRating)).GetAwaiter().GetResult();

    private static Beatmap decodeOsu(string osuPath)
    {
        using var reader = new LineBufferedReader(File.OpenRead(osuPath));
        return new LegacyBeatmapDecoder().Decode(reader, Array.Empty<LineBufferedReader>());
    }
}
