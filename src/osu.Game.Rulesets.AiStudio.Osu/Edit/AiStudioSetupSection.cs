using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace osu.Game.Rulesets.AiStudio.Osu.Edit;

/// <summary>
/// Setup 页的 "AI Studio" 分区（PLAN.md §2.2 注入点 2）。
/// M2 起提供音频上传与一键生成入口。
/// </summary>
public partial class AiStudioSetupSection : Container
{
    public AiStudioSetupSection()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        Padding = new MarginPadding { Vertical = 20 };

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
                        Text = "AI Studio",
                        Font = FontUsage.Default.With(size: 18),
                    },
                    new SpriteText
                    {
                        Text = "音频上传与一键生成将在 M2 接入。",
                        Font = FontUsage.Default.With(size: 13),
                    },
                },
            },
        };
    }
}
