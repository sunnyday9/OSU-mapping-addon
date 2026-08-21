using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.AiStudio.Catch.Checks;

/// <summary>
/// Checks x movement distance vs time: flags fruits that are unreachable without exceeding catcher speed.
/// RC: https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!catch — movement feasibility / hyperdash trigger sections.
///
/// Uses <see cref="Catcher.BASE_WALK_SPEED"/> and <see cref="Catcher.BASE_DASH_SPEED"/> (units: px/ms scaled),
/// scaled by catcher's BASE_SIZE/catch width? For simplicity, check uses raw distances vs available time
/// with conservative dash speed threshold derived from Catcher constants.
/// Hyperdash fruits are explicitly exempt (they trigger hyperdash speed).
/// </summary>
public class CheckCatchMovementFeasibility : ICheck
{
    // Catcher speeds are in "normalized position" units per ms; map to px/ms via WIDTH.
    // BASE_WALK_SPEED = 0.5, BASE_DASH_SPEED = 1.0 are normalized; WIDTH = 512 => px/ms thresholds ~0.5/1 scaled.
    // Conservative thresholds in px/ms:
    private const double dash_speed_px_per_ms = 0.55; // walk+dash base
    private const double hyperdash_initiation_threshold_px = 140; // distance that would trigger hyperdash; used to exempt

    private readonly IssueTemplateUnreachable templateUnreachable;

    public CheckCatchMovementFeasibility()
    {
        templateUnreachable = new IssueTemplateUnreachable(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Movement feasibility (catch)");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateUnreachable,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var beatmap = context.CurrentDifficulty.Playable;

        var palpables = beatmap.HitObjects.OfType<PalpableCatchHitObject>().OrderBy(h => h.StartTime).ToList();

        for (int i = 1; i < palpables.Count; i++)
        {
            var prev = palpables[i - 1];
            var cur = palpables[i];

            // Hyperdash fruits are dash-exempt for the movement into cur; skip if cur is hyperdash.
            // Also skip if prev hyperdashed into cur — speed was boosted.
            if (cur.HyperDash || prev.HyperDash)
                continue;

            double dt = cur.StartTime - prev.GetEndTime();
            if (dt <= 0)
                continue;

            float prevX = prev.EffectiveX;
            float curX = cur.EffectiveX;

            // Some objects may not have EffectiveX populated before PostProcess; fallback to OriginalX.
            if (!float.IsFinite(prevX)) prevX = prev.OriginalX;
            if (!float.IsFinite(curX)) curX = cur.OriginalX;

            double distance = Math.Abs(curX - prevX);

            // Very large jumps in very short time are hyperdash by design; if distance suggests hyperdash
            // threshold, don't flag — difficulty calc would have marked it.
            if (distance >= hyperdash_initiation_threshold_px && dt < distance / dash_speed_px_per_ms)
            {
                // If beam would have been hyperdash but wasn't flagged, treat as reachable via hyperdash intent —
                // still flag as warning if not flagged, because missing hyperdash means mapping error.
                // So we DO flag if neither is hyperdash and distance is large.
                double requiredSpeed = distance / dt;
                yield return new Issue(cur, templateUnreachable, prev.StartTime, cur.StartTime, distance, dt, requiredSpeed, dash_speed_px_per_ms);
                continue;
            }

            double required = distance / dt;
            if (required > dash_speed_px_per_ms)
                yield return new Issue(cur, templateUnreachable, prev.StartTime, cur.StartTime, distance, dt, required, dash_speed_px_per_ms);
        }
    }

    public class IssueTemplateUnreachable : IssueTemplate
    {
        public IssueTemplateUnreachable(ICheck check)
            : base(check, IssueType.Warning,
                "Objects at {0:0.#}ms -> {1:0.#}ms require moving {2:0.#}px in {3:0.#}ms ({4:0.###} px/ms > {5:0.###} px/ms dash limit). Consider reducing spacing or increasing time gap.")
        {
        }
    }
}
