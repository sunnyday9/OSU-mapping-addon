using osu.Game.Rulesets.AiStudio.Catch.Checks;
using osu.Game.Rulesets.Catch.Edit;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;

namespace osu.Game.Rulesets.AiStudio.Catch.Edit;

/// <summary>
/// Verify page verifier (PLAN.md §2.2 injection 3 / §7):
/// Aggregates official catch checks + AI Studio incremental checks, displayed alongside built-in checks.
/// Adds CatchStarRating, CatchDifficultyRanges, CheckCatchHyperdashFeasibility, CheckCatchOffscreen, CheckCatchMovementFeasibility.
/// </summary>
public class AiStudioCatchBeatmapVerifier : IBeatmapVerifier
{
    private readonly IBeatmapVerifier catchVerifier = new CatchBeatmapVerifier();

    private readonly ICheck[] studioChecks =
    {
        new CheckCatchDifficultyRanges(),
        new CheckCatchHyperdashFeasibility(),
        new CheckCatchOffscreen(),
        new CheckCatchMovementFeasibility(),
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var issues = catchVerifier.Run(context).ToList();
        issues.AddRange(studioChecks.SelectMany(check => check.Run(context)));
        return issues;
    }
}
