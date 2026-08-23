using AiStudio.Core.Models;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;

namespace osu.Game.Rulesets.AiStudio.Taiko.Checks;

/// <summary>
/// Checks that each difficulty's OD/HP falls within taiko RC per-level ranges.
/// RC: https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!taiko — per-difficulty guidelines.
/// </summary>
public class CheckTaikoDifficultyRanges : ICheck
{
    private readonly IssueTemplateSettingsOutOfRange templateOutOfRange;

    public CheckTaikoDifficultyRanges()
    {
        templateOutOfRange = new IssueTemplateSettingsOutOfRange(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Settings, "Taiko difficulty settings outside ranking criteria ranges");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateOutOfRange,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        foreach (var verified in context.AllDifficulties)
        {
            if (!verified.Playable.BeatmapInfo.Ruleset.Equals(context.CurrentDifficulty.Playable.BeatmapInfo.Ruleset))
                continue;

            double? stars = TaikoStarRating.TryCalculate(verified.Working, verified.Playable.BeatmapInfo);
            if (stars == null)
                continue;

            var level = DifficultyRatingHelper.GetLevel(stars.Value);
            if (!TaikoDifficultyRanges.TryGet(level, out var range))
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
                "Difficulty \"{0}\" ({1}) has Taiko settings outside the ranking criteria ranges: AR {2:0.##}, OD {3:0.##}, HP {4:0.##}, CS {5:0.##}; allowed AR {6}, OD {7}, HP {8}, CS {9}. [https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!taiko]")
        {
        }
    }
}
