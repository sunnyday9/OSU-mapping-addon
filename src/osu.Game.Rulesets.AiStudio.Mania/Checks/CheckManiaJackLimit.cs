using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.AiStudio.Mania.Checks;

/// <summary>
/// Check consecutive same-column notes (jacks) do not exceed a threshold.
///
/// RC reference: https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!mania — jack / anchor patterns.
/// Default threshold is 4 consecutive notes on the same column (configurable via ergonomics constraints in synthesis).
/// Reports Problem when exceeded.
/// </summary>
public class CheckManiaJackLimit : ICheck
{
    private const int max_consecutive_same_column = 4;

    private readonly IssueTemplateJackTooLong templateJackTooLong;

    public CheckManiaJackLimit()
    {
        templateJackTooLong = new IssueTemplateJackTooLong(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Mania jack limit");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateJackTooLong,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var objects = context.CurrentDifficulty.Playable.HitObjects.OfType<ManiaHitObject>().OrderBy(h => h.StartTime).ToList();

        if (objects.Count == 0)
            yield break;

        int runColumn = -1;
        int runLength = 0;
        double runStartTime = 0;

        for (int i = 0; i < objects.Count; i++)
        {
            int col = objects[i].Column;

            if (col == runColumn)
            {
                runLength++;
            }
            else
            {
                if (runLength > max_consecutive_same_column)
                    yield return new Issue(objects[i - 1], templateJackTooLong, runColumn, runLength, max_consecutive_same_column, runStartTime);

                runColumn = col;
                runLength = 1;
                runStartTime = objects[i].StartTime;
            }
        }

        if (runLength > max_consecutive_same_column)
            yield return new Issue(objects[^1], templateJackTooLong, runColumn, runLength, max_consecutive_same_column, runStartTime);
    }

    public class IssueTemplateJackTooLong : IssueTemplate
    {
        public IssueTemplateJackTooLong(ICheck check)
            : base(check, IssueType.Warning, "Column {0} has {1} consecutive notes (limit {2}) starting at {3:0}ms — potential jack abuse.")
        {
        }
    }
}
