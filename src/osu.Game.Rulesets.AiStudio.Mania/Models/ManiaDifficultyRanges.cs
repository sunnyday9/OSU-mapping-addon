using AiStudio.Core.Models;

namespace osu.Game.Rulesets.AiStudio.Mania.Models;

/// <summary>
/// osu!mania 各难度等级的 Ranking Criteria 参数区间表（OD/HP 为核心，AR/CS 按 osu! 通用约束收录以满足 <see cref="DifficultySettingsRange"/> 结构）。
///
/// 数值来源：https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!mania
/// （osu!mania ranking criteria，各难度小节 "Difficulty setting guidelines"；2026-08 核对，placeholder 区间按 RC 趋势拟合，4K/7K 适用的同一套阈值）。
/// 注意：mania RC 对 OD/HP 有明确区间，AR/CS 在 mania 模式下仍存在于 BeatmapDifficulty 但非主要约束；本表按 osu! 标准模式的层级趋势为 mania 提供占位区间，
/// 后续以 ranked 语料 P5–P95 拟合替换（tools/analysis）。区间含端点。
/// </summary>
public static class ManiaDifficultyRanges
{
    /// <summary>
    /// 全部 6 个难度等级的区间表（Easy–ExpertPlus）。ExpertPlus 复用 Expert 区间：RC 只定义 Easy–Expert 五个难度。
    /// 4K 与 7K 共用同一套区间；键数相关约束由 <see cref="Checks.CheckManiaColumnDistribution"/> 等检查另行覆盖。
    /// </summary>
    public static readonly IReadOnlyList<DifficultySettingsRange> All = new[]
    {
        // Easy —— mania RC 对低难度要求宽松的 OD/HP，下限取 3 附近为占位（任务示例：Easy OD 3-5 HP 3-5）。
        new DifficultySettingsRange(DifficultyLevel.Easy,
            new FloatRange(0, 5), new FloatRange(3, 5), new FloatRange(3, 5), new FloatRange(0, 4)),

        // Normal —— OD/HP 适中。
        new DifficultySettingsRange(DifficultyLevel.Normal,
            new FloatRange(2, 6), new FloatRange(4, 6), new FloatRange(4, 6), new FloatRange(0, 5)),

        // Hard —— OD/HP 中高。
        new DifficultySettingsRange(DifficultyLevel.Hard,
            new FloatRange(4, 8), new FloatRange(5, 7), new FloatRange(5, 7), new FloatRange(0, 6)),

        // Insane —— OD/HP 较高。
        new DifficultySettingsRange(DifficultyLevel.Insane,
            new FloatRange(6, 9.3), new FloatRange(6, 8), new FloatRange(6, 8), new FloatRange(0, 7)),

        // Expert —— OD/HP 高（上限取 10）。
        new DifficultySettingsRange(DifficultyLevel.Expert,
            new FloatRange(8, 10), new FloatRange(7, 10), new FloatRange(7, 10), new FloatRange(0, 7)),

        // ExpertPlus —— 沿用 Expert。
        new DifficultySettingsRange(DifficultyLevel.ExpertPlus,
            new FloatRange(8, 10), new FloatRange(7, 10), new FloatRange(7, 10), new FloatRange(0, 7)),
    };

    public static bool TryGet(DifficultyLevel level, out DifficultySettingsRange range)
    {
        foreach (var candidate in All)
        {
            if (candidate.Level == level)
            {
                range = candidate;
                return true;
            }
        }

        range = default!;
        return false;
    }

    public static DifficultySettingsRange Get(DifficultyLevel level)
    {
        if (TryGet(level, out var range))
            return range;

        throw new InvalidOperationException($"Difficulty level {level} has no mania ranking criteria range defined.");
    }
}
