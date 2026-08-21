using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.AiStudio.Catch.Checks;

/// <summary>
/// Checks hyperdash sequencing feasibility: avoids repeated hyperdashes without adequate recovery time / distance patterns
/// that would force unrealistic catcher movement.
/// RC: https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!catch (movement / hyperdash sections)
/// Catch objects marked <see cref="PalpableCatchHitObject.HyperDash"/> require the catcher to hyperdash;
/// this check flags consecutive hyperdash fruits that are too close in time (strain) as potential ranked concern.
/// </summary>
public class CheckCatchHyperdashFeasibility : ICheck
{
    private const double min_hyperdash_gap_ms = 80;

    private readonly IssueTemplateHyperdashTooClose templateHyperdashTooClose;

    public CheckCatchHyperdashFeasibility()
    {
        templateHyperdashTooClose = new IssueTemplateHyperdashTooClose(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Hyperdash feasibility (catch)");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateHyperdashTooClose,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var beatmap = context.CurrentDifficulty.Playable;

        // After PostProcess, HyperDash flags are populated on PalpableCatchHitObjects.
        var palpables = beatmap.HitObjects.OfType<PalpableCatchHitObject>().OrderBy(h => h.StartTime).ToList();

        PalpableCatchHitObject? prevHyper = null;

        foreach (var obj in palpables)
        {
            if (!obj.HyperDash)
                continue;

            if (prevHyper != null)
            {
                double gap = obj.StartTime - prevHyper.GetEndTime();
                // Two hyperdash initiators in rapid succession: catcher would need to chain hyperdashes with little recovery.
                if (gap < min_hyperdash_gap_ms)
                    yield return new Issue(obj, templateHyperdashTooClose, prevHyper.StartTime, obj.StartTime, gap, min_hyperdash_gap_ms);
            }

            prevHyper = obj;
        }
    }

    public class IssueTemplateHyperdashTooClose : IssueTemplate
    {
        public IssueTemplateHyperdashTooClose(ICheck check)
            : base(check, IssueType.Warning,
                "Consecutive hyperdash fruits at {0:0.#}ms and {1:0.#}ms are only {2:0.#}ms apart (minimum {3:0.#}ms recommended). Consider increasing spacing or time gap.")
        {
        }
    }
}
