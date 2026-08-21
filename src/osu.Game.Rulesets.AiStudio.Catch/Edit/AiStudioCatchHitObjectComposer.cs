using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Catch.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.AiStudio.Catch.Edit;

/// <summary>
/// Compose page composer: extends official <see cref="CatchHitObjectComposer"/> and appends AI Studio panel to RightToolbox.
/// Subscribes to EditorBeatmap events and refreshes summary via <see cref="AiStudioCatchBeatmapVerifier"/>.
/// </summary>
public partial class AiStudioCatchHitObjectComposer : CatchHitObjectComposer
{
    private AiStudioCatchToolboxGroup toolboxGroup = null!;

    [Resolved]
    private EditorBeatmap editorBeatmap { get; set; } = null!;

    public AiStudioCatchHitObjectComposer(Ruleset ruleset)
        : base((osu.Game.Rulesets.Catch.CatchRuleset)Activator.CreateInstance(typeof(osu.Game.Rulesets.Catch.CatchRuleset))!)
    {
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        toolboxGroup = new AiStudioCatchToolboxGroup();
        RightToolbox.AddRange(new Drawable[]
        {
            toolboxGroup,
        });

        try
        {
            if (editorBeatmap != null)
            {
                editorBeatmap.HitObjectAdded += onBeatmapChanged;
                editorBeatmap.HitObjectRemoved += onBeatmapChanged;
                editorBeatmap.HitObjectUpdated += onBeatmapChanged;
                editorBeatmap.BeatmapReprocessed += onBeatmapReprocessed;
            }
        }
        catch
        {
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        if (editorBeatmap != null)
        {
            try
            {
                editorBeatmap.HitObjectAdded -= onBeatmapChanged;
                editorBeatmap.HitObjectRemoved -= onBeatmapChanged;
                editorBeatmap.HitObjectUpdated -= onBeatmapChanged;
                editorBeatmap.BeatmapReprocessed -= onBeatmapReprocessed;
            }
            catch
            {
            }
        }

        base.Dispose(isDisposing);
    }

    private void onBeatmapChanged(HitObject _)
        => scheduleRefresh();

    private void onBeatmapReprocessed()
        => scheduleRefresh();

    private void scheduleRefresh()
    {
        try
        {
            Scheduler.AddOnce(refreshSummary);
        }
        catch
        {
        }
    }

    private void refreshSummary()
    {
        try
        {
            if (editorBeatmap == null || toolboxGroup == null)
                return;

            var verifier = new AiStudioCatchBeatmapVerifier();
            var working = new Synthesis.InMemoryWorkingBeatmap(editorBeatmap.PlayableBeatmap);
            var ctx = new osu.Game.Rulesets.Edit.BeatmapVerifierContext(editorBeatmap.PlayableBeatmap, working);
            var issues = verifier.Run(ctx).ToList();
            int problemOrError = issues.Count(i => i.Template.Type == osu.Game.Rulesets.Edit.Checks.Components.IssueType.Problem || i.Template.Type == osu.Game.Rulesets.Edit.Checks.Components.IssueType.Error);
            int warnings = issues.Count - problemOrError;

            string summary = issues.Count == 0
                ? "No issues (Verbose: all checks passed)."
                : $"{issues.Count} issue(s): {(problemOrError > 0 ? $"{problemOrError} Problem/Error" : "")}{(problemOrError > 0 && warnings > 0 ? ", " : "")}{(warnings > 0 ? $"{warnings} Warning" : "")}. See Verify tab.";

            toolboxGroup.UpdateSummary(summary);
        }
        catch
        {
        }
    }
}
