namespace AiStudio.Core.Models;

/// <summary>
/// 闭区间 [Min, Max]。单侧无限制时用 double 极值表示。
/// </summary>
public readonly record struct FloatRange(double Min, double Max)
{
    public bool Contains(double value) => value >= Min - 1e-9 && value <= Max + 1e-9;

    public bool ContainsApproximately(double value) => Contains(value);

    public override string ToString() => $"{Min:0.##}–{Max:0.##}";
}

/// <summary>
/// 某一难度等级在 Ranking Criteria 中的参数合法区间（AR/OD/HP/CS）。
/// </summary>
public sealed record DifficultySettingsRange(
    DifficultyLevel Level,
    FloatRange ApproachRate,
    FloatRange OverallDifficulty,
    FloatRange HpDrain,
    FloatRange CircleSize)
{
    public bool Contains(float ar, float od, float hp, float cs)
        => ApproachRate.Contains(ar) && OverallDifficulty.Contains(od) && HpDrain.Contains(hp) && CircleSize.Contains(cs);
}
