namespace AiStudio.Core.MappingIr.Calibration;

/// <summary>密度旋钮搜索结果：最终 scale、迭代数、是否收敛、最后一次实测 SR。</summary>
public sealed record ScaleSearchResult(double FinalScale, int Iterations, bool Converged, double? ObservedSr);

/// <summary>
/// SR 校准收敛核（唯一实现）：调节密度旋钮直到实测 SR 落在目标 ± 容差内。
/// 所有 SR 校准路径（<see cref="StarRatingCalibrationLoop"/> 文档环、
/// Mania/OsuMapGenerator 生成器环）都必须经由本类——内联收敛公式曾导致
/// 参数漂移（架构走查候选 5："报告与实测不一致"事故源）。
///
/// 收敛公式（M0-M6 与 MVP-B 已验证）：
///   scale_{n+1} = clamp(scale_n × (1 + delta / max(sr, 0.5)), MinScale, MaxScale)
/// 其中 delta = target − sr；|delta| ≤ tolerance 即收敛；
/// 停滞保护 |scale_{n+1} − scale_n| &lt; StagnationEpsilon 提前退出。
/// </summary>
public sealed class DensityScaleSearch
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
    /// 数值搜索主循环。
    /// </summary>
    /// <param name="target">目标 SR。</param>
    /// <param name="tolerance">收敛容差（内部下限 0.01 防无意义迭代）。</param>
    /// <param name="evaluate">评估函数：给定 scale 返回实测 SR；null 表示评估器不可用（立即返回草稿）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public ScaleSearchResult Search(double target, double tolerance, Func<double, double?> evaluate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evaluate);

        double clampedTolerance = Math.Max(tolerance, 0.01);
        double scale = 1.0;
        double? sr = evaluate(scale);

        for (int i = 0; i < MaxIterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sr is null)
                return new ScaleSearchResult(scale, i + 1, Converged: false, ObservedSr: null);

            double delta = target - sr.Value;
            if (Math.Abs(delta) <= clampedTolerance)
                return new ScaleSearchResult(scale, i + 1, Converged: true, ObservedSr: sr);

            double next = Math.Clamp(scale * (1 + delta / Math.Max(sr.Value, 0.5)), MinScale, MaxScale);
            if (Math.Abs(next - scale) < StagnationEpsilon)
                return new ScaleSearchResult(scale, i + 1, Converged: false, ObservedSr: sr);

            scale = next;
            sr = evaluate(scale);
        }

        return new ScaleSearchResult(scale, MaxIterations, Converged: false, ObservedSr: sr);
    }
}
