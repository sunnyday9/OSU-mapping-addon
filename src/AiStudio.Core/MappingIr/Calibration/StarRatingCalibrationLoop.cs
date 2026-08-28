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
/// SR 校准闭环（MVP-B，spec §16 Difficulty Feedback Loop）的文档门面：
/// 调节密度旋钮（DensityScale）重跑生成，直到实测 SR 落在目标 ± 容差内。
/// 纯算法、零外部依赖、确定性——生成函数与评估器由调用方注入
/// （ruleset 程序集提供"官方难度计算器评估 + 重跑管线"的闭包）；
/// 收敛数值搜索由 <see cref="DensityScaleSearch"/> 唯一实现（Mania/OsuMapGenerator 同源复用）。
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

        MappingDocument document = null!;
        var search = new DensityScaleSearch
        {
            MaxIterations = MaxIterations,
            MinScale = MinScale,
            MaxScale = MaxScale,
            StagnationEpsilon = StagnationEpsilon,
        };
        var result = search.Search(
            target.Value,
            profile.Tolerance,
            scale =>
            {
                document = generate(scale);
                return readObservedSr(document);
            },
            cancellationToken);

        return new CalibrationResult(document, result.ObservedSr, result.FinalScale, result.Iterations, result.Converged);
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
