using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.AiStudio.Taiko.Checks;

/// <summary>
/// RC: taiko maps should not be excessively dominated by one colour (don vs kat).
///
/// Header cites https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!taiko
///
/// Strictly, the RC does not give a numeric "balance" rule; this check flags extreme imbalance
/// (>= 85% of Hits being a single HitType) as a quality signal. Small maps are exempt.
/// Reports as Warning; uses map-level hit counts only (Hit objects, not DrumRoll/Swell ticks).
/// </summary>
public class CheckTaikoDonKatBalance : ICheck
{
    private const int min_hits = 20;
    private const double imbalance_threshold = 0.85;

    private readonly IssueTemplateImbalance templateImbalance;

    public CheckTaikoDonKatBalance()
    {
        templateImbalance = new IssueTemplateImbalance(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Don/Kat imbalance");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[] { templateImbalance };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var hits = context.CurrentDifficulty.Playable.HitObjects.OfType<Hit>().ToList();
        if (hits.Count < min_hits)
            yield break;

        int don = hits.Count(h => h.Type == HitType.Centre);
        int kat = hits.Count - don;
        double maxRatio = Math.Max(don, kat) / (double)hits.Count;

        if (maxRatio >= imbalance_threshold)
        {
            string dominant = don >= kat ? "don (centre)" : "kat (rim)";
            yield return new Issue(templateImbalance, hits.Count, don, kat, dominant, maxRatio);
        }
    }

    public class IssueTemplateImbalance : IssueTemplate
    {
        public IssueTemplateImbalance(ICheck check)
            : base(check, IssueType.Warning,
                "Don/Kat balance is {3:0.0%} {4} ({1} don / {2} kat over {0} hits). Consider mixing don and kat more evenly (threshold {5:0.0%} would flag).")
        {
        }
    }
}
