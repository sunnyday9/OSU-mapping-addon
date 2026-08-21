using osu.Game.Rulesets.AiStudio.Taiko.Checks;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Taiko.Edit;

namespace osu.Game.Rulesets.AiStudio.Taiko.Edit;

/// <summary>
/// Verify page verifier for taiko: aggregates <see cref="TaikoBeatmapVerifier"/> + studio checks.
/// </summary>
public class AiStudioTaikoBeatmapVerifier : IBeatmapVerifier
{
    private readonly IBeatmapVerifier taikoVerifier = new TaikoBeatmapVerifier();

    private readonly ICheck[] studioChecks =
    {
        new CheckTaikoDifficultyRanges(),
        new CheckTaikoDonKatBalance(),
        new CheckTaikoMonoPattern(),
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var issues = taikoVerifier.Run(context).ToList();
        issues.AddRange(studioChecks.SelectMany(check => check.Run(context)));
        return issues;
    }
}
