using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.AiStudio.Mania.Synthesis;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Difficulty;

namespace osu.Game.Rulesets.AiStudio.Mania.Tests;

// 注意：本测试命名空间会使 "AiStudio.Core.*" 被解析为 osu.Game.Rulesets.AiStudio.Core.*，
// 因此 IR/Core 类型一律用 global::AiStudio.Core.* 全限定（同 ManiaIrCalibrationTest）。

/// <summary>
/// T1（spec #16 工单 #17）：Mania IR 生成适配器经 IMapGenerator 接缝的验收。
/// 覆盖：校准落盘（官方 SR 落在目标 ± 容差，报告 SR = 实测 SR）、失败不落盘、
/// 异常折入失败结果（不抛出契约）、设置最小映射。
/// </summary>
[TestFixture]
public class ManiaIrMapGeneratorTest
{
    [Test]
    public void Generate_Calibrates_WritesDecodableOsu_WithinTolerance()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-irgen-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var settings = new global::AiStudio.Core.Models.GenerationSettings
            {
                AudioPath = audioPath,
                OutputDirectory = outputDir,
                TargetStarRating = 5.5,
                StarRatingTolerance = 0.15,
            };
            var result = new ManiaIrMapGenerator(IrAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.OutputFilePath, Does.EndWith(".osu"));
            Assert.That(File.Exists(result.OutputFilePath), Is.True);
            Assert.That(result.AudioOutputPath, Is.Not.Null);
            Assert.That(File.Exists(result.AudioOutputPath), Is.True, "audio must be copied next to the output");

            var decoded = Decode(File.ReadAllText(result.OutputFilePath!));
            Assert.That(decoded.HitObjects, Is.Not.Empty);

            double sr = new ManiaDifficultyCalculator(new ManiaRuleset().RulesetInfo, new ManiaInMemoryWorkingBeatmap(decoded)).Calculate().StarRating;
            TestContext.Progress.WriteLine($"adapter calibrated sr={sr:F4} (target 5.5 ± 0.15)");
            Assert.That(Math.Abs(sr - 5.5), Is.LessThanOrEqualTo(0.15),
                $"official SR {sr:F4} must be within 5.5 ± 0.15 (report SR = actual SR)");

            string detail = result.QualityReport!.Gates.Single().Detail!;
            Assert.Multiple(() =>
            {
                // 四个校准元数据字段名与 spec #16 验收口径一致，消费方不必解析散文。
                Assert.That(detail, Does.Contain("converged=True"), detail);
                Assert.That(detail, Does.Contain("iterations="), detail);
                Assert.That(detail, Does.Contain("observed_sr="), detail);
                Assert.That(detail, Does.Contain("final_density_scale="), detail);
            });
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void Generate_MissingAudio_FailsWithoutOutput()
    {
        string outputDir = Path.Combine(Path.GetTempPath(), $"aistudio-irgen-{Guid.NewGuid():N}");
        var settings = new global::AiStudio.Core.Models.GenerationSettings
        {
            AudioPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}.mp3"),
            OutputDirectory = outputDir,
        };

        var result = new ManiaIrMapGenerator(IrAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Audio file not found"));
        Assert.That(result.OutputFilePath, Is.Null);
        Assert.That(Directory.Exists(outputDir), Is.False, "failed generation must not create output");
    }

    [Test]
    public void Generate_AnalysisFailure_FoldsIntoFailedResult()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-irgen-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var settings = new global::AiStudio.Core.Models.GenerationSettings
            {
                AudioPath = audioPath,
                OutputDirectory = outputDir,
            };
            var result = new ManiaIrMapGenerator(new ThrowingAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Does.Contain("Mania IR generation failed"));
            Assert.That(result.ErrorMessage, Does.Contain("boom"));
            Assert.That(Directory.Exists(outputDir), Is.False, "failed generation must not create output");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void MapProfile_MapsTargetAndTolerance_BalancedDimensions_IgnoresTargetLevel()
    {
        var hard = new global::AiStudio.Core.Models.GenerationSettings { TargetLevel = global::AiStudio.Core.Models.DifficultyLevel.Hard, TargetStarRating = 4.2 };
        var expert = new global::AiStudio.Core.Models.GenerationSettings { TargetLevel = global::AiStudio.Core.Models.DifficultyLevel.Expert, TargetStarRating = 4.2 };

        var fromHard = ManiaIrMapGenerator.MapProfile(hard);
        var fromExpert = ManiaIrMapGenerator.MapProfile(expert);

        Assert.That(fromHard.TargetStarRating, Is.EqualTo(4.2));
        Assert.That(fromHard.Tolerance, Is.EqualTo(0.3), "tolerance must flow from StarRatingTolerance");
        Assert.That(fromHard, Is.EqualTo(fromExpert), "TargetLevel is intentionally unmapped this round (ADR-011 known gap)");
        Assert.That(fromHard.Dimensions, Is.EqualTo(ManiaIrMapGenerator.CalibrationVerifiedDimensions),
            "balanced dimension defaults must match the calibration-verified profile");
        Assert.That(fromHard.Preferences, Is.EqualTo(
            new global::AiStudio.Core.MappingIr.Model.DifficultyPreferences(AllowExtremePatterns: false, PreferReadability: true, PreferMusicSync: true, PreferPatternVariety: true)));
    }

    private static global::AiStudio.Core.MappingIr.Analysis.SyntheticAudioAnalyzer IrAnalyzer()
        => new(174.0, 60000, new[] { 0.0, 20000.0, 40000.0 }, new[] { 0.35, 0.85, 0.30 });

    private sealed class ThrowingAnalyzer : global::AiStudio.Core.Analysis.IAudioAnalyzer
    {
        public Task<global::AiStudio.Core.Analysis.BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");

        public Task<IReadOnlyList<global::AiStudio.Core.Analysis.AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }

    private static Beatmap Decode(string osu)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(osu));
        using var reader = new LineBufferedReader(stream);
        return new LegacyBeatmapDecoder().Decode(reader, Array.Empty<LineBufferedReader>());
    }
}
