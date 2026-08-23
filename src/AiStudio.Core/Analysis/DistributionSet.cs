namespace AiStudio.Core.Analysis;

/// <summary>
/// 单项指标的 P5–P95 区间（ranked 语料分布，PLAN.md §3 G4）。
/// </summary>
public sealed record DistributionRange(double P5, double P95);

/// <summary>
/// 一组 ranked 语料分布区间的集合（G4 参数分布 + G3 节奏对齐备用）。
/// 由 <see cref="IDistributionProvider"/> 产出，默认回退为 QualityGateRunner 的 v1 常量。
/// </summary>
public sealed class DistributionSet
{
    public DistributionRange SpacingPx { get; init; } = new(30, 400);

    public DistributionRange SliderRatio { get; init; } = new(0.15, 0.85);

    public DistributionRange GridRatio { get; init; } = new(0.95, 1.0);

    public static DistributionSet Default => new();

    public static DistributionSet FromDictionary(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> dict)
    {
        DistributionRange read(string key, DistributionRange fallback)
        {
            if (!dict.TryGetValue(key, out var inner))
                return fallback;

            bool hasP5 = inner.TryGetValue("p5", out double p5);
            if (!hasP5) hasP5 = inner.TryGetValue("P5", out p5);
            bool hasP95 = inner.TryGetValue("p95", out double p95);
            if (!hasP95) hasP95 = inner.TryGetValue("P95", out p95);
            if (!hasP5 || !hasP95) return fallback;
            return new DistributionRange(p5, p95);
        }

        return new DistributionSet
        {
            SpacingPx = read("spacing_px", new DistributionRange(30, 400)),
            SliderRatio = read("slider_ratio", new DistributionRange(0.15, 0.85)),
            GridRatio = read("grid_ratio", new DistributionRange(0.95, 1.0)),
        };
    }
}
