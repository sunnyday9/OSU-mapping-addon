using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Edit;

namespace osu.Game.Rulesets.AiStudio.Osu.Edit;

/// <summary>
/// 编辑器右工具箱的 AI Studio 面板。
/// M1 起提供实时 ranked 检查摘要；M2 起提供音频生成入口。
/// </summary>
public partial class AiStudioToolboxGroup : EditorToolboxGroup
{
    public AiStudioToolboxGroup()
        : base("AI Studio")
    {
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
                    new SpriteText
                    {
                        Text = "AI 工具将在 M1（ranked 检查）/ M2（音频生成）接入。",
                        Font = FontUsage.Default.With(size: 13),
                    },
                },
            },
        };
    }
}
