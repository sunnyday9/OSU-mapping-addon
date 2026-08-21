using osu.Game.Rulesets.AiStudio.Mania.Checks;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Mania.Edit;

namespace osu.Game.Rulesets.AiStudio.Mania.Edit;

/// <summary>
/// Verify page verifier: aggregates official <see cref="ManiaBeatmapVerifier"/> plus AI Studio mania checks.
/// </summary>
public class AiStudioManiaBeatmapVerifier : IBeatmapVerifier
{
    private readonly IBeatmapVerifier maniaVerifier = new ManiaBeatmapVerifier();

    private readonly ICheck[] studioChecks =
    {
        new CheckManiaDifficultySettingsRanges(),
        new CheckManiaColumnDistribution(),
        new CheckManiaJackLimit(),
        new CheckManiaChordDensity(),
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var issues = maniaVerifier.Run(context).ToList();
        issues.AddRange(studioChecks.SelectMany(check => check.Run(context)));
        return issues;
    }
}
