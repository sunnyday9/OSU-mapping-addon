using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Mania.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.AiStudio.Mania.Edit;

/// <summary>
/// Compose page composer: extends <see cref="ManiaHitObjectComposer"/> adding AI Studio toolbox.
/// </summary>
public partial class AiStudioManiaHitObjectComposer : ManiaHitObjectComposer
{
    private AiStudioManiaToolboxGroup toolboxGroup = null!;

    [Resolved]
    private EditorBeatmap editorBeatmap { get; set; } = null!;

    public AiStudioManiaHitObjectComposer(Ruleset ruleset)
        : base(ruleset)
    {
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        toolboxGroup = new AiStudioManiaToolboxGroup();
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

            var verifier = new AiStudioManiaBeatmapVerifier();
            var working = new Synthesis.ManiaInMemoryWorkingBeatmap(editorBeatmap.PlayableBeatmap);
            var ctx = new osu.Game.Rulesets.Edit.BeatmapVerifierContext(editorBeatmap.PlayableBeatmap, working);
            var issues = verifier.Run(ctx).ToList();
            int problemOrError = issues.Count(i => i.Template.Type == osu.Game.Rulesets.Edit.Checks.Components.IssueType.Problem || i.Template.Type == osu.Game.Rulesets.Edit.Checks.Components.IssueType.Error);
            int warnings = issues.Count - problemOrError;

            string summary = issues.Count == 0
                ? "No issues (mania checks passed)"
                : $"{issues.Count} issue(s): {(problemOrError > 0 ? $"{problemOrError} Problem/Error" : "")}{(problemOrError > 0 && warnings > 0 ? ", " : "")}{(warnings > 0 ? $"{warnings} Warning" : "")}. See Verify tab.";

            toolboxGroup.UpdateSummary(summary);
        }
        catch
        {
        }
    }
}
