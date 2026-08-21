using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Edit;

namespace osu.Game.Rulesets.AiStudio.Mania.Edit;

/// <summary>
/// Editor right toolbox group for AI Studio (mania).
/// </summary>
public partial class AiStudioManiaToolboxGroup : EditorToolboxGroup
{
    private readonly SpriteText summaryText;

    public AiStudioManiaToolboxGroup()
        : base("AI Studio (Mania)")
    {
        summaryText = new SpriteText
        {
            Text = "AI Studio mania checks: difficulty settings, column distribution, jack limit, chord density. See Verify tab. Generation via Setup tab.",
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
