namespace AiStudio.Core.Synthesis;

/// <summary>
/// Spread 约束常量（与 CheckSpreadStarRatingGaps 对齐）。
/// 相邻难度星距 ≤2.0★，drain 阶梯 3:30 / 4:15 / 5:00。
/// </summary>
public static class SpreadConstraint
{
    /// <summary>相邻难度最大星距（★）。</summary>
    public const double MaxAdjacentGap = 2.0;

    /// <summary>3:30 drain 阈值（秒）。</summary>
    public const double Drain_3_30 = 210;

    /// <summary>4:15 drain 阈值（秒）。</summary>
    public const double Drain_4_15 = 255;

    /// <summary>5:00 drain 阈值（秒）。</summary>
    public const double Drain_5_00 = 300;

    /// <summary>3:30 秒别名。</summary>
    public const double Drain_3_30_Sec = 210;

    /// <summary>4:15 秒别名。</summary>
    public const double Drain_4_15_Sec = 255;

    /// <summary>5:00 秒别名。</summary>
    public const double Drain_5_00_Sec = 300;

    /// <summary>3:30 drain 阈值（毫秒）。</summary>
    public const double Drain_3_30_Ms = 210 * 1000;

    /// <summary>4:15 drain 阈值（毫秒）。</summary>
    public const double Drain_4_15_Ms = 255 * 1000;

    /// <summary>5:00 drain 阈值（毫秒）。</summary>
    public const double Drain_5_00_Ms = 300 * 1000;

    /// <summary>阈值别名（毫秒）——与 CheckSpreadStarRatingGaps 命名对齐。</summary>
    public const double DrainThreshold_3_30_Sec = 210;
    public const double DrainThreshold_4_15_Sec = 255;
    public const double DrainThreshold_5_00_Sec = 300;
    public const double DrainThreshold_3_30_Ms = 210 * 1000;
    public const double DrainThreshold_4_15_Ms = 255 * 1000;
    public const double DrainThreshold_5_00_Ms = 300 * 1000;
}
