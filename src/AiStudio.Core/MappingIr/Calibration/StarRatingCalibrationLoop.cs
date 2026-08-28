using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Calibration;

/// <summary>SR 校准结果（MVP-B）。</summary>
public sealed record CalibrationResult(
    MappingDocument Document,
    double? ObservedSr,
    double FinalDensityScale,
    int Iterations,
    bool Converged);

/// <summary>
/// SR 校准闭环（MVP-B，spec §16 Difficulty Feedback Loop）：
/// 调节密度旋钮（DensityScale）重跑生成，直到实测 SR 落在目标 ± 容差内。
/// 纯算法、零外部依赖、确定性——生成函数与评估器由调用方注入
/// （ruleset 程序集提供"官方 ManiaDifficultyCalculator 评估 + 重跑管线"的闭包）。
///
/// 收敛公式沿用 <c>ManiaMapGenerator.calibrate</c>（M0-M6 已验证）：
///   scale_{n+1} = clamp(scale_n × (1 + delta / max(sr, 0.5)), MinScale, MaxScale)
/// 其中 delta = TargetStarRating − sr；|delta| ≤ Tolerance 即收敛；
/// 停滞保护 |scale_{n+1} − scale_n| &lt; 1e-3 提前退出。
/// </summary>
public sealed class StarRatingCalibrationLoop
{
    /// <summary>最大迭代次数（spec §19 有界循环）。</summary>
    public int MaxIterations { get; init; } = 6;

    /// <summary>密度旋钮范围下限。</summary>
    public double MinScale { get; init; } = 0.2;

    /// <summary>密度旋钮范围上限。</summary>
    public double MaxScale { get; init; } = 4.0;

    /// <summary>停滞保护阈值（scale 变化小于此值视为不再收敛）。</summary>
    public double StagnationEpsilon { get; init; } = 1e-3;

    /// <summary>
    /// 校准主循环。
    /// </summary>
    /// <param name="profile">目标难度档案（含 TargetStarRating 与 Tolerance）。</param>
    /// <param name="generate">生成函数：给定 density scale → 返回已评估 SR 的文档（evaluator 已注入管线）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>最终文档（含 Evaluation.Difficulty["observed_star_rating"]）与校准元数据。</returns>
    public CalibrationResult Calibrate(
        DifficultyProfile profile,
        Func<double, MappingDocument> generate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(generate);

        double? target = profile.TargetStarRating;
        if (target is null)
        {
            // 无目标 SR：不校准，直接返回默认密度文档。
            var doc = generate(1.0);
            return new CalibrationResult(doc, readObservedSr(doc), 1.0, 0, Converged: false);
        }

        double tolerance = Math.Max(profile.Tolerance, 0.01); // 容差下限防除零/无意义迭代
        double scale = 1.0;
        MappingDocument document = generate(scale);
        double? sr = readObservedSr(document);

        for (int i = 0; i < MaxIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sr is null)
            {
                // 评估器不可用：保持 DifficultyKnown=false 语义，返回当前草稿。
                return new CalibrationResult(document, null, scale, i + 1, Converged: false);
            }

            double delta = target.Value - sr.Value;
            if (Math.Abs(delta) <= tolerance)
                return new CalibrationResult(document, sr, scale, i + 1, Converged: true);

            double next = Math.Clamp(scale * (1 + delta / Math.Max(sr.Value, 0.5)), MinScale, MaxScale);
            if (Math.Abs(next - scale) < StagnationEpsilon)
                return new CalibrationResult(document, sr, scale, i + 1, Converged: false);

            scale = next;
            document = generate(scale);
            sr = readObservedSr(document);
        }

        return new CalibrationResult(document, sr, scale, MaxIterations, Converged: false);
    }

    private static double? readObservedSr(MappingDocument document)
    {
        if (document.Evaluation.Difficulty is null)
            return null;
        if (!document.Evaluation.Difficulty.TryGetValue("observed_star_rating", out var v) || v is null)
            return null;

        return v switch
        {
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            _ => null,
        };
    }
}
