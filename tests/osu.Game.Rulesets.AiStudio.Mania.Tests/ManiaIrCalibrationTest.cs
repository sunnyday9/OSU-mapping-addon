using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.AiStudio.Mania.Synthesis;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Difficulty;

namespace osu.Game.Rulesets.AiStudio.Mania.Tests;

// 注意：本测试命名空间 osu.Game.Rulesets.AiStudio.Mania.Tests 会使 "AiStudio.Core.*"
// 被解析为 osu.Game.Rulesets.AiStudio.Core.*（外层命名空间优先），
// 因此 IR 类型一律用 global::AiStudio.Core.* 全限定。

/// <summary>
/// MVP-B 验收：IR 文档 → 官方 ManiaDifficultyCalculator 的 SR 评估链路 + 校准闭环。
/// 覆盖：评估链路可算、解码对象一致、密度旋钮单调、端到端 SR 容差、DifficultyKnown、确定性、空图容错。
/// </summary>
[TestFixture]
public class ManiaIrCalibrationTest
{
    [Test]
    public void Render_Decode_Calculate_StarRatingIsFiniteAndPositive()
    {
        var doc = BuildIrDocument(seed: 42);
        double? sr = EvaluateSr(doc);

        Assert.That(sr, Is.Not.Null);
        Assert.That(double.IsFinite(sr!.Value), Is.True);
        Assert.That(sr!.Value, Is.GreaterThan(0.0), "empty/zero-difficulty map would be a red flag");
    }

    [Test]
    public void Render_Decode_HitObjectCountMatches()
    {
        var doc = BuildIrDocument(seed: 42);
        string osu = new global::AiStudio.Core.MappingIr.Rendering.ManiaOsuRenderer().Render(doc);
        var beatmap = Decode(osu);

        Assert.That(beatmap.HitObjects.Count, Is.EqualTo(doc.ConcreteObjects!.Count),
            "decoded hit objects must match IR concrete objects");
    }

    [Test]
    public void DensitySweep_StarRatingIncreasesWithDensityScale()
    {
        // 校准前提：DensityScale 单调 → SR 单调（否则校准循环无法收敛）。
        // 实测曲线（174BPM 三段式）：scale 1.0→2.76★, 2.0→4.99★, 3.0→7.55★，5.5 可达。
        double srLow = EvaluateSrForScale(1.0);
        double srMid = EvaluateSrForScale(2.0);
        double srHigh = EvaluateSrForScale(3.0);

        TestContext.Progress.WriteLine($"SR sweep: scale=1.00 -> {srLow:F4} | scale=2.00 -> {srMid:F4} | scale=3.00 -> {srHigh:F4}");
        Assert.That(srMid, Is.GreaterThan(srLow), "SR must increase with density scale (low->mid)");
        Assert.That(srHigh, Is.GreaterThan(srMid), "SR must increase with density scale (mid->high)");
        Assert.That(srHigh, Is.GreaterThan(5.5), "scale 3.0 must reach above target 5.5 (reachability)");
    }

    [Test]
    public void CalibratedPipeline_StarRatingWithinTolerance()
    {
        // 端到端：ManiaIrCalibratedPipeline 应把官方 SR 校准到 5.5 ± 0.15。
        var analyzer = new global::AiStudio.Core.MappingIr.Analysis.SyntheticAudioAnalyzer(174.0, 60000, new[] { 0.0, 20000.0, 40000.0 }, new[] { 0.35, 0.85, 0.30 });
        var pipeline = new global::osu.Game.Rulesets.AiStudio.Mania.Synthesis.ManiaIrCalibratedPipeline(analyzer);
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpb_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, BalancedProfile(), seed: 42);
            double? sr = EvaluateSr(doc);

            Assert.That(sr, Is.Not.Null, "calibrated pipeline must produce a measurable SR");
            TestContext.Progress.WriteLine($"calibrated sr={sr!.Value:F4} (target 5.5 ± 0.15)");
            Assert.That(Math.Abs(sr!.Value - 5.5), Is.LessThanOrEqualTo(0.15),
                $"calibrated SR {sr.Value:F4} must be within 5.5 ± 0.15");
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    [Test]
    public void CalibratedPipeline_DifficultyKnownTrue()
    {
        var pipeline = new global::osu.Game.Rulesets.AiStudio.Mania.Synthesis.ManiaIrCalibratedPipeline(
            new global::AiStudio.Core.MappingIr.Analysis.SyntheticAudioAnalyzer(174.0, 60000, new[] { 0.0, 20000.0, 40000.0 }, new[] { 0.35, 0.85, 0.30 }));
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpb_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, BalancedProfile(), seed: 42);

            Assert.That(doc.Evaluation.DifficultyKnown, Is.True, "official evaluator present → DifficultyKnown must be true");
            Assert.That(doc.Evaluation.Difficulty!["observed_star_rating"], Is.Not.Null);
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    [Test]
    public void CalibratedPipeline_Deterministic_SameSeedSameOutput()
    {
        var analyzer = new global::AiStudio.Core.MappingIr.Analysis.SyntheticAudioAnalyzer(174.0, 60000, new[] { 0.0, 20000.0, 40000.0 }, new[] { 0.35, 0.85, 0.30 });
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpb_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var a = new global::osu.Game.Rulesets.AiStudio.Mania.Synthesis.ManiaIrCalibratedPipeline(analyzer).Run(pseudo, BalancedProfile(), seed: 42);
            var b = new global::osu.Game.Rulesets.AiStudio.Mania.Synthesis.ManiaIrCalibratedPipeline(analyzer).Run(pseudo, BalancedProfile(), seed: 42);

            Assert.That(b.ConcreteObjects!.Count, Is.EqualTo(a.ConcreteObjects!.Count));
            Assert.That(b.ConcreteObjects!.Select(o => o.Time), Is.EqualTo(a.ConcreteObjects!.Select(o => o.Time)));
            Assert.That(b.ConcreteObjects!.Select(o => o.Column), Is.EqualTo(a.ConcreteObjects!.Select(o => o.Column)));
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    [Test]
    public void Evaluator_EmptyObjects_DoesNotThrow()
    {
        var doc = BuildIrDocument(42) with { ConcreteObjects = Array.Empty<global::AiStudio.Core.MappingIr.Model.ConcreteObject>() };
        var evaluator = new global::osu.Game.Rulesets.AiStudio.Mania.Synthesis.ManiaOfficialDifficultyEvaluator();

        double? sr = evaluator.TryEvaluateStarRating(doc);

        // 空图要么评估为 0（可解码但无难度），要么 null（评估器不可用语义）——绝不能抛异常。
        Assert.That(sr is null or 0.0, Is.True, $"empty map must yield null or 0.0, got {sr}");
    }

    internal static double EvaluateSrForScale(double densityScale)
    {
        var generator = new global::AiStudio.Core.MappingIr.Candidates.DeterministicCandidateGenerator { DensityScale = densityScale };
        var pipeline = new global::AiStudio.Core.MappingIr.MappingIrPipeline(
            new global::AiStudio.Core.MappingIr.Analysis.SyntheticAudioAnalyzer(174.0, 60000, new[] { 0.0, 20000.0, 40000.0 }, new[] { 0.35, 0.85, 0.30 }),
            new global::AiStudio.Core.MappingIr.Evidence.DeterministicEvidenceBuilder(),
            new global::AiStudio.Core.MappingIr.GlobalPlanning.DeterministicGlobalPlanner(),
            new global::AiStudio.Core.MappingIr.LocalPlanning.DeterministicLocalPlanner(),
            generator,
            new global::AiStudio.Core.MappingIr.Candidates.DeterministicCandidateRanker(),
            new global::AiStudio.Core.MappingIr.Backends.Mania4KMappingBackend(),
            new global::AiStudio.Core.MappingIr.Critique.BaselineMappingCritic(),
            new global::AiStudio.Core.MappingIr.Difficulty.UnavailableDifficultyEvaluator());
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpb_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, BalancedProfile(), seed: 42);
            return EvaluateSr(doc)!.Value;
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    internal static global::AiStudio.Core.MappingIr.Model.MappingDocument BuildIrDocument(int seed)
    {
        var analyzer = new global::AiStudio.Core.MappingIr.Analysis.SyntheticAudioAnalyzer(
            174.0, 60000, new[] { 0.0, 20000.0, 40000.0 }, new[] { 0.35, 0.85, 0.30 });
        var pipeline = new global::AiStudio.Core.MappingIr.MappingIrPipeline(analyzer);
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpb_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            return pipeline.Run(pseudo, BalancedProfile(), seed);
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    internal static global::AiStudio.Core.MappingIr.Model.DifficultyProfile BalancedProfile()
        => new(
            5.5,
            new global::AiStudio.Core.MappingIr.Model.DimensionProfile(0.72, 0.64, 0.55, 0.48, 0.42, 0.20, 0.30),
            new global::AiStudio.Core.MappingIr.Model.DifficultyPreferences(AllowExtremePatterns: false, PreferReadability: true, PreferMusicSync: true, PreferPatternVariety: true),
            0.15);

    internal static double? EvaluateSr(global::AiStudio.Core.MappingIr.Model.MappingDocument document)
    {
        string osu = new global::AiStudio.Core.MappingIr.Rendering.ManiaOsuRenderer().Render(document);
        var beatmap = Decode(osu);
        var working = new ManiaInMemoryWorkingBeatmap(beatmap);
        double stars = new ManiaDifficultyCalculator(new ManiaRuleset().RulesetInfo, working).Calculate().StarRating;
        return double.IsFinite(stars) ? stars : null;
    }

    internal static Beatmap Decode(string osu)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(osu));
        using var reader = new LineBufferedReader(stream);
        return new LegacyBeatmapDecoder().Decode(reader, Array.Empty<LineBufferedReader>());
    }
}
