using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Taiko.Edit;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.AiStudio.Taiko.Edit;

/// <summary>
/// Compose page composer: extends official <see cref="TaikoHitObjectComposer"/> and appends AI Studio panel to RightToolbox.
/// </summary>
public partial class AiStudioTaikoHitObjectComposer : TaikoHitObjectComposer
{
    private AiStudioTaikoToolboxGroup toolboxGroup = null!;

    [Resolved]
    private EditorBeatmap editorBeatmap { get; set; } = null!;

#pragma warning disable IDE0060
    public AiStudioTaikoHitObjectComposer(Ruleset ruleset)
        : base(new osu.Game.Rulesets.Taiko.TaikoRuleset())
    {
        // TaikoHitObjectComposer requires a TaikoRuleset, not our AiStudioTaikoRuleset.
        // We pass a real TaikoRuleset for composition; gameplay/editing behaviour is equivalent
        // and the editor still resolves via AiStudioTaikoRuleset.CreateHitObjectComposer().
    }
#pragma warning restore IDE0060

    [BackgroundDependencyLoader]
    private void load()
    {
        toolboxGroup = new AiStudioTaikoToolboxGroup();
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

            var verifier = new AiStudioTaikoBeatmapVerifier();
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
