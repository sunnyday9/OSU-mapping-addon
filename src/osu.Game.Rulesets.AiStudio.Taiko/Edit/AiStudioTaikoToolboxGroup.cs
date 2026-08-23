using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Edit;

namespace osu.Game.Rulesets.AiStudio.Taiko.Edit;

/// <summary>
/// Right toolbox panel for AI Studio (taiko).
/// </summary>
public partial class AiStudioTaikoToolboxGroup : EditorToolboxGroup
{
    private readonly SpriteText summaryText;

    public AiStudioTaikoToolboxGroup()
        : base("AI Studio (Taiko)")
    {
        summaryText = new SpriteText
        {
            Text = "Taiko checks active: star rating, difficulty ranges, don/kat balance, mono pattern. See Verify tab. Generation entry in Setup.",
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
