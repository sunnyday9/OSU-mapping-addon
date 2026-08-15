using AiStudio.Core.Models;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;

namespace osu.Game.Rulesets.AiStudio.Osu.Checks;

/// <summary>
/// 检查每个难度的 AR/OD/HP/CS 是否落在对应难度等级的 RC 参数区间内。
///
/// RC 条款：osu! ranking criteria 各难度小节 "Difficulty setting guidelines"
/// （https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu ，2026-08 核对）：
/// Easy "Approach rate should be 5 or less." / Normal "Overall difficulty / HP drain rate should be
/// between 3 and 5." / Hard "Approach rate should be between 6 and 8." / Insane "Approach rate should be
/// between 7 and 9.3." / Expert "Approach rate / Overall difficulty should be 8 or higher." 等。
/// 区间表见 <see cref="OsugameDifficultyRanges"/>（含每条 RC 原文引用）。
///
/// 注意：上述条款在 RC 中为 guideline 而非 rule；按 M1 规格本检查以 Problem 上报超范围，
/// 消息包含实测值与允许区间。难度等级由该难度星数经 <see cref="DifficultyRatingHelper"/> 得出；
/// 星数计算失败（<see cref="OsuStarRating.TryCalculate"/> 返回 null）时跳过该难度。
/// </summary>
public class CheckDifficultySettingsRanges : ICheck
{
    private readonly IssueTemplateSettingsOutOfRange templateOutOfRange;

    public CheckDifficultySettingsRanges()
    {
        templateOutOfRange = new IssueTemplateSettingsOutOfRange(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Settings, "Difficulty settings outside ranking criteria ranges");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateOutOfRange,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        foreach (var verified in context.AllDifficulties)
        {
            // 与官方 CheckLowestDiffDrainTime 一致：只检查同一规则集的难度，避免对转换谱面误报。
            if (!verified.Playable.BeatmapInfo.Ruleset.Equals(context.CurrentDifficulty.Playable.BeatmapInfo.Ruleset))
                continue;

            double? stars = OsuStarRating.TryCalculate(verified.Working, verified.Playable.BeatmapInfo);
            if (stars == null)
                continue;

            var level = DifficultyRatingHelper.GetLevel(stars.Value);
            if (!OsugameDifficultyRanges.TryGet(level, out var range))
                continue;

            var difficulty = verified.Playable.BeatmapInfo.Difficulty;

            if (!range.Contains(difficulty.ApproachRate, difficulty.OverallDifficulty, difficulty.DrainRate, difficulty.CircleSize))
            {
                yield return new Issue(
                    templateOutOfRange,
                    verified.Playable.BeatmapInfo.DifficultyName,
                    level,
                    difficulty.ApproachRate,
                    difficulty.OverallDifficulty,
                    difficulty.DrainRate,
                    difficulty.CircleSize,
                    range.ApproachRate,
                    range.OverallDifficulty,
                    range.HpDrain,
                    range.CircleSize);
            }
        }
    }

    public class IssueTemplateSettingsOutOfRange : IssueTemplate
    {
        public IssueTemplateSettingsOutOfRange(ICheck check)
            : base(check, IssueType.Problem,
                "Difficulty \"{0}\" ({1}) has settings outside the ranking criteria ranges: AR {2:0.##}, OD {3:0.##}, HP {4:0.##}, CS {5:0.##}; allowed AR {6}, OD {7}, HP {8}, CS {9}.")
        {
        }
    }
}
