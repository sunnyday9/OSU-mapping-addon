using AiStudio.Core.MappingIr.Calibration;
using AiStudio.Core.MappingIr.Model;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

/// <summary>
/// StarRatingCalibrationLoop 单元测试（stub 生成函数，不依赖官方 calculator）：
/// 收敛 / 迭代上限 / 评估器不可用 / 无目标 SR / 停滞保护 / 确定性 / 不可达目标。
/// </summary>
[TestFixture]
public class StarRatingCalibrationLoopTests
{
    private static readonly DifficultyProfile profile = new(
        5.5,
        new DimensionProfile(0.72, 0.64, 0.55, 0.48, 0.42, 0.20, 0.30),
        new DifficultyPreferences(false, true, true, true),
        0.15);

    /// <summary>stub 生成函数：SR = 2.5 + 1.2 × scale（随 scale 单调）。</summary>
    private static MappingDocument MakeDocument(double sr)
    {
        var doc = TestFixtures.BuildDocument(42);
        return doc with
        {
            Evaluation = doc.Evaluation with
            {
                Difficulty = new Dictionary<string, object?> { ["observed_star_rating"] = sr },
                DifficultyKnown = true,
            },
        };
    }

    private static Func<double, MappingDocument> SrModel(double slope, double intercept = 2.5)
        => scale => MakeDocument(intercept + slope * scale);

    [Test]
    public void Calibrate_Converges_WhenTargetReachable()
    {
        // SR = 2.5 + 1.2×scale：scale=2.5 → SR=5.5。循环应从 1.0 起步逼近。
        var loop = new StarRatingCalibrationLoop();
        var result = loop.Calibrate(profile, SrModel(slope: 1.2));

        Assert.That(result.Converged, Is.True, $"iterations={result.Iterations} finalScale={result.FinalDensityScale:F3} sr={result.ObservedSr:F3}");
        Assert.That(Math.Abs(result.ObservedSr!.Value - 5.5), Is.LessThanOrEqualTo(0.15));
    }

    [Test]
    public void Calibrate_RespectsMaxIterations()
    {
        // 振荡模型（永远差 0.5）：不应无限循环，预算耗尽返回 Converged=false
        var loop = new StarRatingCalibrationLoop { MaxIterations = 4 };
        var result = loop.Calibrate(profile, SrModel(slope: 0.0, intercept: 5.0)); // SR 恒 5.0，差 0.5 > 0.15

        Assert.That(result.Iterations, Is.LessThanOrEqualTo(4));
        Assert.That(result.Converged, Is.False);
        Assert.That(result.Document, Is.Not.Null);
    }

    [Test]
    public void Calibrate_EvaluatorUnavailable_ReturnsNullSr_NoCrash()
    {
        // 评估器不可用 → observed_star_rating 缺失 → 立即返回，不迭代
        var loop = new StarRatingCalibrationLoop();
        var result = loop.Calibrate(profile, _ =>
        {
            var doc = TestFixtures.BuildDocument(42);
            return doc with { Evaluation = doc.Evaluation with { Difficulty = null, DifficultyKnown = false } };
        });

        Assert.That(result.ObservedSr, Is.Null);
        Assert.That(result.Converged, Is.False);
        Assert.That(result.Iterations, Is.EqualTo(1), "must bail immediately when evaluator unavailable");
    }

    [Test]
    public void Calibrate_NoTargetStarRating_ReturnsDefaultScale_NoIteration()
    {
        var noTarget = profile with { TargetStarRating = null };
        var loop = new StarRatingCalibrationLoop();
        var result = loop.Calibrate(noTarget, SrModel(slope: 1.2));

        Assert.That(result.FinalDensityScale, Is.EqualTo(1.0));
        Assert.That(result.Iterations, Is.EqualTo(0));
    }

    [Test]
    public void Calibrate_Stagnation_DetectsNoProgress()
    {
        // SR 恒 2.5（slope=0）：delta 恒 3.0，scale 每次按公式变化——不会停滞。
        // 改用"SR 对 scale 不敏感但 delta 大"的模型验证停滞保护：让公式算出 next≈scale。
        // SR = 5.5 - 0.05×(scale-1)：scale 变化影响微小，但公式 next = scale×(1+delta/max(sr,0.5))
        // 在 delta≈0 时不触发。构造 delta 大但 next≈scale 的情况：
        // 需要 scale×(1+delta/sr) ≈ scale → delta ≈ 0，矛盾。因此停滞保护只在 clamp 边界触发。
        var loop = new StarRatingCalibrationLoop { MaxScale = 1.0 };
        // MaxScale=1.0：scale 被 clamp 在 1.0，next 恒等于 scale → 停滞
        var result = loop.Calibrate(profile, SrModel(slope: 0.0, intercept: 3.0)); // SR 恒 3.0，需升但被 clamp

        Assert.That(result.Converged, Is.False);
        Assert.That(result.FinalDensityScale, Is.EqualTo(1.0));
        Assert.That(result.Iterations, Is.LessThanOrEqualTo(2), "stagnation must bail early");
    }

    [Test]
    public void Calibrate_Deterministic_SameInputSameOutput()
    {
        var loop = new StarRatingCalibrationLoop();
        var a = loop.Calibrate(profile, SrModel(slope: 1.2));
        var b = loop.Calibrate(profile, SrModel(slope: 1.2));

        Assert.That(a.FinalDensityScale, Is.EqualTo(b.FinalDensityScale));
        Assert.That(a.ObservedSr, Is.EqualTo(b.ObservedSr));
        Assert.That(a.Iterations, Is.EqualTo(b.Iterations));
    }

    [Test]
    public void Calibrate_UnreachableTarget_ClampsToBoundary()
    {
        // 目标 9.9 超出模型上限（SR max = 2.5+1.2×4 = 7.3）：scale 应被 clamp 到 MaxScale
        var highTarget = profile with { TargetStarRating = 9.9 };
        var loop = new StarRatingCalibrationLoop();
        var result = loop.Calibrate(highTarget, SrModel(slope: 1.2));

        Assert.That(result.FinalDensityScale, Is.EqualTo(4.0), "must clamp to MaxScale when target unreachable");
        Assert.That(result.Converged, Is.False);
        Assert.That(result.Document, Is.Not.Null, "must still return a document (draft)");
    }
}
