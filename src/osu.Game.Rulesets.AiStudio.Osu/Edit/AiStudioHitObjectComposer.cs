using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.AiStudio.Osu.Edit;

/// <summary>
/// Compose 页作曲器：继承官方 <see cref="OsuHitObjectComposer"/> 保留全部 osu! 编辑能力，
/// 并向右工具箱追加 AI Studio 面板（PLAN.md §2.2 注入点 1）。
/// M3 起订阅 EditorBeatmap 事件并经 SuggestionEngine 刷新面板摘要。
/// </summary>
public partial class AiStudioHitObjectComposer : OsuHitObjectComposer
{
    private AiStudioToolboxGroup toolboxGroup = null!;

    [Resolved]
    private EditorBeatmap editorBeatmap { get; set; } = null!;

    public AiStudioHitObjectComposer(Ruleset ruleset)
        : base(ruleset)
    {
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        toolboxGroup = new AiStudioToolboxGroup();
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

            var verifier = new AiStudioBeatmapVerifier();
            var working = new Synthesis.InMemoryWorkingBeatmap(editorBeatmap.PlayableBeatmap);
            var ctx = new osu.Game.Rulesets.Edit.BeatmapVerifierContext(editorBeatmap.PlayableBeatmap, working);
            var issues = verifier.Run(ctx).ToList();
            var suggestions = Suggestions.SuggestionEngine.FromIssues(issues);

            int problemOrError = issues.Count(i => i.Template.Type == osu.Game.Rulesets.Edit.Checks.Components.IssueType.Problem || i.Template.Type == osu.Game.Rulesets.Edit.Checks.Components.IssueType.Error);
            int warnings = issues.Count - problemOrError;

            string summary = suggestions.Count == 0
                ? "暂无问题（Verbose: 全部检查通过）"
                : $"{suggestions.Count} 条建议：{(problemOrError > 0 ? $"{problemOrError} Problem/Error" : "")}{(problemOrError > 0 && warnings > 0 ? "、" : "")}{(warnings > 0 ? $"{warnings} Warning" : "")}。详见 Verify 页。";

            toolboxGroup.UpdateSummary(summary);
        }
        catch
        {
        }
    }
}
