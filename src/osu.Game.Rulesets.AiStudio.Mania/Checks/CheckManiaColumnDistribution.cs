using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.AiStudio.Mania.Checks;

/// <summary>
/// Check that columns are reasonably distributed (no column completely unused and no extreme imbalance).
///
/// RC reference: https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!mania — mania patterning / column usage.
/// This is a guideline-level check; it reports Warning when any column is unused.
/// </summary>
public class CheckManiaColumnDistribution : ICheck
{
    private readonly IssueTemplateColumnUnused templateColumnUnused;
    private readonly IssueTemplateColumnImbalance templateImbalance;

    public CheckManiaColumnDistribution()
    {
        templateColumnUnused = new IssueTemplateColumnUnused(this);
        templateImbalance = new IssueTemplateColumnImbalance(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Mania column distribution");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateColumnUnused,
        templateImbalance,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var playable = context.CurrentDifficulty.Playable;

        int totalColumns = 0;
        if (playable is ManiaBeatmap maniaPlayable)
            totalColumns = maniaPlayable.TotalColumns;

        if (totalColumns <= 0)
        {
            var maybeBeatmap = playable as IBeatmap;
            if (maybeBeatmap != null)
            {
                var countByConverter = playable.HitObjects.OfType<ManiaHitObject>().Select(h => h.Column).DefaultIfEmpty(-1).Max();
                totalColumns = countByConverter >= 0 ? countByConverter + 1 : 4;
            }
            else
            {
                totalColumns = 4;
            }
        }

        var counts = new int[totalColumns];
        int totalObjects = 0;

        foreach (var obj in playable.HitObjects.OfType<ManiaHitObject>())
        {
            if (obj.Column >= 0 && obj.Column < totalColumns)
            {
                counts[obj.Column]++;
                totalObjects++;
            }
        }

        if (totalObjects == 0)
            yield break;

        // Unused column
        for (int c = 0; c < totalColumns; c++)
        {
            if (counts[c] == 0)
                yield return new Issue(templateColumnUnused, c, totalColumns);
        }

        // Extreme imbalance: any column more than 3x the smallest non-zero
        int minNonZero = counts.Where(v => v > 0).DefaultIfEmpty(int.MaxValue).Min();
        int maxCount = counts.Max();
        if (minNonZero != int.MaxValue && minNonZero > 0 && maxCount > minNonZero * 3)
            yield return new Issue(templateImbalance, maxCount, minNonZero, totalColumns);
    }

    public class IssueTemplateColumnUnused : IssueTemplate
    {
        public IssueTemplateColumnUnused(ICheck check)
            : base(check, IssueType.Warning, "Column {0} is unused in a {1}K mania beatmap.")
        {
        }
    }

    public class IssueTemplateColumnImbalance : IssueTemplate
    {
        public IssueTemplateColumnImbalance(ICheck check)
            : base(check, IssueType.Warning, "Column distribution is heavily imbalanced ({0} vs {1} objects) in a {2}K mania beatmap.")
        {
        }
    }
}
