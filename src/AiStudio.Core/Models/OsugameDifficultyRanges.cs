namespace AiStudio.Core.Models;

/// <summary>
/// osu! 标准模式各难度等级的 Ranking Criteria 参数区间表（AR/OD/HP/CS）。
///
/// 数值来源：https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu
/// （osu! ranking criteria，各难度小节 "Difficulty setting guidelines"；2026-08 核对）。
/// 注意：RC 原文均为 guideline（"should be ..."）而非 rule；本表按官方数值原样收录，
/// 上限类规则（如 "should be 5 or less"）取闭区间 [0, 上限]，osu! 参数实际取值范围为 0–10。
/// </summary>
public static class OsugameDifficultyRanges
{
    /// <summary>
    /// 全部 6 个难度等级的区间表（Easy–ExpertPlus）。
    /// ExpertPlus 复用 Expert 区间：RC 只定义 Easy–Expert 五个难度，而 Expert+ 谱面
    /// 仍须满足 Expert 的条款（osu!lazer 的 DifficultyRating 含 ExpertPlus 但没有对应 RC 章节）。
    /// </summary>
    public static readonly IReadOnlyList<DifficultySettingsRange> All = new[]
    {
        // Easy —— "Approach rate should be 5 or less."
        //          "Overall difficulty / HP drain rate should be between 1 and 3."
        //          "Circle size should be 4 or lower."
        new DifficultySettingsRange(DifficultyLevel.Easy,
            new FloatRange(0, 5), new FloatRange(1, 3), new FloatRange(1, 3), new FloatRange(0, 4)),

        // Normal —— "Approach rate should be between 4 and 6."
        //            "Overall difficulty / HP drain rate should be between 3 and 5."
        //            "Circle size should be 5 or lower."
        new DifficultySettingsRange(DifficultyLevel.Normal,
            new FloatRange(4, 6), new FloatRange(3, 5), new FloatRange(3, 5), new FloatRange(0, 5)),

        // Hard —— "Approach rate should be between 6 and 8."
        //          "Overall difficulty should be between 5 and 7."
        //          "HP drain rate should be between 4 and 6."
        //          "Circle size should be 6 or lower."
        new DifficultySettingsRange(DifficultyLevel.Hard,
            new FloatRange(6, 8), new FloatRange(5, 7), new FloatRange(4, 6), new FloatRange(0, 6)),

        // Insane —— "Approach rate should be between 7 and 9.3."
        //            "Overall difficulty should be between 7 and 9."
        //            "HP drain rate should be between 5 and 8."
        //            "Circle size should be 7 or lower."
        new DifficultySettingsRange(DifficultyLevel.Insane,
            new FloatRange(7, 9.3), new FloatRange(7, 9), new FloatRange(5, 8), new FloatRange(0, 7)),

        // Expert —— "Approach rate / Overall difficulty should be 8 or higher."（上限取 osu! 最大值 10）
        //            "HP drain rate should be 5 or higher."（上限取 10）
        //            "Circle size should be 7 or lower."
        new DifficultySettingsRange(DifficultyLevel.Expert,
            new FloatRange(8, 10), new FloatRange(8, 10), new FloatRange(5, 10), new FloatRange(0, 7)),

        // ExpertPlus —— RC 无专门条款，按 M1 规格沿用 Expert 区间。
        new DifficultySettingsRange(DifficultyLevel.ExpertPlus,
            new FloatRange(8, 10), new FloatRange(8, 10), new FloatRange(5, 10), new FloatRange(0, 7)),
    };

    /// <summary>
    /// 按难度等级取区间；表内不存在该等级时返回 false。
    /// </summary>
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

    /// <summary>
    /// 按难度等级取区间；表内不存在该等级时抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    public static DifficultySettingsRange Get(DifficultyLevel level)
    {
        if (TryGet(level, out var range))
            return range;

        throw new InvalidOperationException($"Difficulty level {level} has no ranking criteria range defined.");
    }
}
