using AiStudio.Core.MappingIr.Calibration;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

/// <summary>
/// DensityScaleSearch（收敛核，架构走查候选 5 唯一实现）单元测试：
/// 生成器路径用法（自定义 clamp 范围）与评估器不可用/停滞保护边界。
/// 文档环路径由 StarRatingCalibrationLoopTests 覆盖（经同一核心）。
/// </summary>
[TestFixture]
public class DensityScaleSearchTests
{
    [Test]
    public void Search_RespectsCustomClampRange()
    {
        // 生成器用法：MinScale=0.5/MaxScale=2.0（Mania 密度乘子范围）。
        // SR 恒 1.0（delta=2.5 恒正）→ scale 应被 clamp 到 2.0 而非核心默认 4.0。
        var search = new DensityScaleSearch { MinScale = 0.5, MaxScale = 2.0 };
        var result = search.Search(3.5, 0.3, _ => 1.0);

        Assert.That(result.FinalScale, Is.EqualTo(2.0), "must clamp to the caller's MaxScale");
        Assert.That(result.Converged, Is.False);
        Assert.That(result.ObservedSr, Is.EqualTo(1.0));
    }

    [Test]
    public void Search_ConvergesImmediately_WithinTolerance()
    {
        // 宽容差（生成器默认 0.3）：第一次评估即收敛，只构建一次 beatmap（evaluate 调用 1 次）。
        int calls = 0;
        var search = new DensityScaleSearch();
        var result = search.Search(3.5, 0.3, scale => { calls++; return 3.4; });

        Assert.That(result.Converged, Is.True);
        Assert.That(result.Iterations, Is.EqualTo(1));
        Assert.That(result.FinalScale, Is.EqualTo(1.0));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void Search_EvaluatorUnavailable_BailsImmediately()
    {
        var search = new DensityScaleSearch();
        var result = search.Search(3.5, 0.3, _ => null);

        Assert.That(result.ObservedSr, Is.Null);
        Assert.That(result.Converged, Is.False);
        Assert.That(result.Iterations, Is.EqualTo(1), "must bail immediately when evaluator unavailable");
        Assert.That(result.FinalScale, Is.EqualTo(1.0));
    }
}
