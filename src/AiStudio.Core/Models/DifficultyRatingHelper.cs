namespace AiStudio.Core.Models;

/// <summary>
/// 星数 → 难度等级（<see cref="DifficultyLevel"/>）的映射。
/// 阈值与官方 <c>StarDifficulty.GetDifficultyRating</c>（osu.Game/Beatmaps/StarDifficulty.cs）完全一致：
/// ≥ 6.5★ ExpertPlus；≥ 5.3★ Expert；≥ 4.0★ Insane；≥ 2.7★ Hard；≥ 2.0★ Normal；其余 Easy。
/// 官方实现额外使用 0.005 的近似容差（Precision.AlmostBigger），此处直接以 &gt;= 比较，数值差异可忽略。
/// </summary>
public static class DifficultyRatingHelper
{
    /// <summary>Normal 的最低星数（官方阈值 2.0）。</summary>
    public const double NORMAL_MIN_STARS = 2.0;

    /// <summary>Hard 的最低星数（官方阈值 2.7）。</summary>
    public const double HARD_MIN_STARS = 2.7;

    /// <summary>Insane 的最低星数（官方阈值 4.0）。</summary>
    public const double INSANE_MIN_STARS = 4.0;

    /// <summary>Expert 的最低星数（官方阈值 5.3）。</summary>
    public const double EXPERT_MIN_STARS = 5.3;

    /// <summary>ExpertPlus 的最低星数（官方阈值 6.5）。</summary>
    public const double EXPERT_PLUS_MIN_STARS = 6.5;

    /// <summary>
    /// 把星数映射为难度等级。
    /// 非有限值（NaN/∞）按官方行为处理为 <see cref="DifficultyLevel.Easy"/>。
    /// </summary>
    public static DifficultyLevel GetLevel(double starRating)
    {
        if (!double.IsFinite(starRating))
            return DifficultyLevel.Easy;

        if (starRating >= EXPERT_PLUS_MIN_STARS)
            return DifficultyLevel.ExpertPlus;

        if (starRating >= EXPERT_MIN_STARS)
            return DifficultyLevel.Expert;

        if (starRating >= INSANE_MIN_STARS)
            return DifficultyLevel.Insane;

        if (starRating >= HARD_MIN_STARS)
            return DifficultyLevel.Hard;

        if (starRating >= NORMAL_MIN_STARS)
            return DifficultyLevel.Normal;

        return DifficultyLevel.Easy;
    }
}
