namespace AiStudio.Core.Models;

/// <summary>
/// 音频 → 谱面生成的输入设置。
/// </summary>
public sealed class GenerationSettings
{
    /// <summary>目标难度等级（决定 RC 参数区间与模板库选择）。</summary>
    public DifficultyLevel TargetLevel { get; set; } = DifficultyLevel.Hard;

    /// <summary>目标星数。</summary>
    public double TargetStarRating { get; set; } = 4.0;

    /// <summary>星数校准容差（±）。</summary>
    public double StarRatingTolerance { get; set; } = 0.3;

    /// <summary>输入音频文件路径。</summary>
    public string AudioPath { get; set; } = string.Empty;

    /// <summary>是否在合适位置插入 break 休息段。</summary>
    public bool IncludeBreakSections { get; set; } = true;
}
