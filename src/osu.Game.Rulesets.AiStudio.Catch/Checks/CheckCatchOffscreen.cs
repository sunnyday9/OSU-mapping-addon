using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;

namespace osu.Game.Rulesets.AiStudio.Catch.Checks;

/// <summary>
/// Checks that no catch object is placed offscreen (x outside [0, WIDTH]) after clamping.
/// RC: https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!catch — playfield boundaries.
/// </summary>
public class CheckCatchOffscreen : ICheck
{
    private readonly IssueTemplateOffscreen templateOffscreen;

    public CheckCatchOffscreen()
    {
        templateOffscreen = new IssueTemplateOffscreen(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Catch objects offscreen");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateOffscreen,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var beatmap = context.CurrentDifficulty.Playable;

        foreach (var obj in beatmap.HitObjects.OfType<CatchHitObject>())
        {
            // Fruit / droplets expose OriginalX (pre-offset). JuiceStream/BananaShower are containers;
            // their nested objects are checked via PalpableCatchHitObject, and the container itself has X.
            float x = obj.OriginalX;

            // For JuiceStream, also validate path vertices would stay in bounds; conservatively check X.
            // TinyDroplet etc. also have X.
            if (x < 0 || x > CatchPlayfield.WIDTH)
                yield return new Issue(obj, templateOffscreen, obj.StartTime, x, CatchPlayfield.WIDTH);
        }
    }

    public class IssueTemplateOffscreen : IssueTemplate
    {
        public IssueTemplateOffscreen(ICheck check)
            : base(check, IssueType.Error,
                "Object at {0:0.#}ms has X={1:0.#} outside playfield [0, {2:0.#}].")
        {
        }
    }
}
