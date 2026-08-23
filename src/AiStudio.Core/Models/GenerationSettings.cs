namespace AiStudio.Core.Models;

/// <summary>
/// 单个难度的生成规格（M3 多难度展开的基本单元）。
/// </summary>
public sealed class DifficultySpec
{
    public DifficultyLevel Level { get; init; } = DifficultyLevel.Hard;

    public double TargetStarRating { get; init; } = 3.5;

    public double StarRatingTolerance { get; init; } = 0.3;
}

/// <summary>
/// 音频 → 谱面生成的输入设置。
/// M2 为单难度；M3 起支持多难度展开（Difficulties 非空时以其为准，否则回退 TargetLevel/TargetStarRating 单难度兼容路径）。
/// </summary>
public sealed class GenerationSettings
{
    /// <summary>目标难度等级（单难度兼容路径；多难度时以 Difficulties 为准）。</summary>
    public DifficultyLevel TargetLevel { get; set; } = DifficultyLevel.Hard;

    /// <summary>目标星数（单难度兼容路径）。</summary>
    public double TargetStarRating { get; set; } = 4.0;

    /// <summary>星数校准容差（±）。</summary>
    public double StarRatingTolerance { get; set; } = 0.3;

    /// <summary>多难度规格列表；为空时回退单难度 TargetLevel/TargetStarRating。</summary>
    public IReadOnlyList<DifficultySpec>? Difficulties { get; set; }

    /// <summary>输入音频文件路径。</summary>
    public string AudioPath { get; set; } = string.Empty;

    /// <summary>输出目录；为空时使用默认目录（我的文档/osu-ai-studio-output）。</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>是否在合适位置插入 break 休息段（M3 起生效）。</summary>
    public bool IncludeBreakSections { get; set; } = true;

    public bool IsMultiDifficulty => Difficulties != null && Difficulties.Count > 1;

    public IReadOnlyList<DifficultySpec> EffectiveDifficulties => Difficulties != null && Difficulties.Count > 0
        ? Difficulties
        : new[] { new DifficultySpec { Level = TargetLevel, TargetStarRating = TargetStarRating, StarRatingTolerance = StarRatingTolerance } };
}
