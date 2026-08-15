using AiStudio.Core.Models;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.AiStudio.Osu.Checks;

/// <summary>
/// 检查谱面集合（beatmapset）的难度梯度：星距过大 / 按 drain time 缺失最低难度。
///
/// RC 条款（2026-08 核对）：
/// 1. 通用 RC "Overall > General > Rules"（https://osu.ppy.sh/wiki/en/Ranking_criteria ）：
///    "the spread cannot skip any difficulty levels and there cannot be any drastically large
///    difficulty gaps between any two difficulties"——RC 未给出数值，M1 规格以相邻难度星距
///    &gt; 2.0★ 视为"过大"，报 Warning 并建议拆分难度或补档。
/// 2. osu! RC "Overall > General > Rules"（https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu ）：
///    drain time &lt; 3:30 时最低难度不得高于 Normal；3:30–4:15 不得高于 Hard；4:15–5:00 不得高于 Insane。
///    drain time 取集合内最大难度的 drain time（最后一个物件结束时间 − 第一个物件开始时间，毫秒）。
///
/// 星数计算失败的难度跳过；集合只有一个难度时不报缺档。
/// </summary>
public class CheckSpreadStarRatingGaps : ICheck
{
    /// <summary>相邻难度星距上限（M1 规格，RC 无数值）。</summary>
    private const double star_gap_threshold = 2.0;

    /// <summary>3:30（毫秒）。</summary>
    private const double drain_time_3_30 = 3.5 * 60 * 1000;

    /// <summary>4:15（毫秒）。</summary>
    private const double drain_time_4_15 = 4.25 * 60 * 1000;

    /// <summary>5:00（毫秒）。</summary>
    private const double drain_time_5_00 = 5.0 * 60 * 1000;

    private readonly IssueTemplateStarGap templateStarGap;
    private readonly IssueTemplateMissingDifficulty templateMissingDifficulty;

    public CheckSpreadStarRatingGaps()
    {
        templateStarGap = new IssueTemplateStarGap(this);
        templateMissingDifficulty = new IssueTemplateMissingDifficulty(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Spread, "Beatmapset spread issues (star rating gaps / missing difficulties)");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateStarGap,
        templateMissingDifficulty,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        // 与官方 CheckLowestDiffDrainTime 一致：只统计同一规则集的难度。
        var difficulties = context.AllDifficulties
                                  .Where(d => d.Playable.BeatmapInfo.Ruleset.Equals(context.CurrentDifficulty.Playable.BeatmapInfo.Ruleset))
                                  .Select(d => new { Verified = d, Stars = OsuStarRating.TryCalculate(d.Working, d.Playable.BeatmapInfo) })
                                  .Where(x => x.Stars != null)
                                  .Select(x => new { x.Verified, Stars = x.Stars!.Value })
                                  .OrderBy(x => x.Stars)
                                  .ToList();

        // 集合只有一个难度（或无难度）时：没有星距可查，也不报缺档。
        if (difficulties.Count < 2)
            yield break;

        // —— 相邻难度星距检查 ——
        for (int i = 1; i < difficulties.Count; i++)
        {
            double gap = difficulties[i].Stars - difficulties[i - 1].Stars;

            if (gap > star_gap_threshold)
            {
                yield return new Issue(
                    templateStarGap,
                    difficulties[i - 1].Verified.Playable.BeatmapInfo.DifficultyName,
                    difficulties[i - 1].Stars,
                    difficulties[i].Verified.Playable.BeatmapInfo.DifficultyName,
                    difficulties[i].Stars,
                    gap,
                    star_gap_threshold);
            }
        }

        // —— 缺档检查（按集合最大 drain time）——
        double maxDrainTime = difficulties.Max(x => drainTimeOf(x.Verified.Playable));
        if (maxDrainTime <= 0)
            yield break;

        DifficultyLevel lowestLevel = DifficultyRatingHelper.GetLevel(difficulties[0].Stars);

        DifficultyLevel? maxAllowedLevel = maxDrainTime switch
        {
            < drain_time_3_30 => DifficultyLevel.Normal,
            < drain_time_4_15 => DifficultyLevel.Hard,
            < drain_time_5_00 => DifficultyLevel.Insane,
            _ => null,
        };

        if (maxAllowedLevel != null && lowestLevel > maxAllowedLevel)
        {
            yield return new Issue(
                templateMissingDifficulty,
                lowestLevel,
                maxAllowedLevel.Value,
                TimeSpan.FromMilliseconds(maxDrainTime).ToString(@"m\:ss"));
        }
    }

    /// <summary>
    /// 难度 drain time：最后一个物件结束时间 − 第一个物件开始时间（毫秒）；无物件时为 0。
    /// </summary>
    private static double drainTimeOf(IBeatmap beatmap)
    {
        if (beatmap.HitObjects.Count == 0)
            return 0;

        return beatmap.HitObjects.Max(h => h.GetEndTime()) - beatmap.HitObjects.Min(h => h.StartTime);
    }

    public class IssueTemplateStarGap : IssueTemplate
    {
        public IssueTemplateStarGap(ICheck check)
            : base(check, IssueType.Warning,
                "Star rating gap between \"{0}\" ({1:0.##}★) and \"{2}\" ({3:0.##}★) is {4:0.##}★, exceeding the {5:0.##}★ threshold. Consider splitting the difficulties or adding an intermediate difficulty.")
        {
        }
    }

    public class IssueTemplateMissingDifficulty : IssueTemplate
    {
        public IssueTemplateMissingDifficulty(ICheck check)
            : base(check, IssueType.Warning,
                "Beatmapset drain time ({2}) requires the lowest difficulty to be no harder than {1}, but the lowest difficulty is {0}. Consider adding a lower difficulty.")
        {
        }
    }
}
