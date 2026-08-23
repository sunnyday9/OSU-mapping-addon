using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Edit;

namespace osu.Game.Rulesets.AiStudio.Catch.Edit;

/// <summary>
/// Right toolbox panel for AI Studio (catch).
/// </summary>
public partial class AiStudioCatchToolboxGroup : EditorToolboxGroup
{
    private readonly SpriteText summaryText;

    public AiStudioCatchToolboxGroup()
        : base("AI Studio (Catch)")
    {
        summaryText = new SpriteText
        {
            Text = "Catch checks active: star rating, difficulty ranges, hyperdash feasibility, offscreen, movement feasibility. See Verify tab. Generation entry in Setup.",
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
