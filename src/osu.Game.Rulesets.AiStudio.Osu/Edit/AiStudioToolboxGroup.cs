using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Edit;

namespace osu.Game.Rulesets.AiStudio.Osu.Edit;

/// <summary>
/// 编辑器右工具箱的 AI Studio 面板。
/// M1 已集成 4 项 ranked 检查（难度设置/星距/颜色/spinner），在 Verify 页查看详情；生成入口在 Setup 页。
/// M3 起支持实时侧栏摘要（由 HitObjectComposer 订阅 EditorBeatmap 事件后经 UpdateSummary 刷新）。
/// </summary>
public partial class AiStudioToolboxGroup : EditorToolboxGroup
{
    private readonly SpriteText summaryText;

    public AiStudioToolboxGroup()
        : base("AI Studio")
    {
        summaryText = new SpriteText
        {
            Text = "已集成 4 项 ranked 检查（难度设置/星距/颜色/spinner）。在 Verify 页查看详情。生成入口在 Setup 页。",
            Font = FontUsage.Default.With(size: 13),
        };

        Children = new Drawable[]
        {
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new osuTK.Vector2(4),
                Children = new Drawable[]
                {
                    summaryText,
                },
            },
        };
    }

    public void UpdateSummary(string text)
    {
        Scheduler.AddOnce(() => summaryText.Text = text);
    }
}
