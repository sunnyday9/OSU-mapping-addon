using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.AiStudio.Mania.Checks;

/// <summary>
/// Check chord density: too many simultaneous notes relative to key count.
///
/// RC reference: https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!mania — chord / density guidelines.
/// For 4K, more than 2 simultaneous notes is heavy; for 7K, more than 3. Reports Warning when exceeded.
/// Simultaneous is defined as same StartTime within 1ms.
/// </summary>
public class CheckManiaChordDensity : ICheck
{
    private readonly IssueTemplateTooDense templateTooDense;

    public CheckManiaChordDensity()
    {
        templateTooDense = new IssueTemplateTooDense(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Mania chord density");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateTooDense,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var objects = context.CurrentDifficulty.Playable.HitObjects.OfType<ManiaHitObject>().OrderBy(h => h.StartTime).ToList();

        if (objects.Count == 0)
            yield break;

        // Group by StartTime bucket (1ms tolerance)
        var groups = new List<List<ManiaHitObject>>();
        List<ManiaHitObject>? current = null;
        double bucket = double.NaN;

        foreach (var obj in objects)
        {
            if (current == null || Math.Abs(obj.StartTime - bucket) > 1.0)
            {
                current = new List<ManiaHitObject> { obj };
                groups.Add(current);
                bucket = obj.StartTime;
            }
            else
            {
                current.Add(obj);
            }
        }

        // Infer key count from max column + 1, fallback to 4
        int keyCount = objects.Max(h => h.Column) + 1;
        if (keyCount <= 0) keyCount = 4;

        int allowedMax = keyCount <= 4 ? 2 : keyCount <= 7 ? 3 : 4;

        foreach (var group in groups)
        {
            if (group.Count > allowedMax)
                yield return new Issue(group[0], templateTooDense, group[0].StartTime, group.Count, allowedMax, keyCount);
        }
    }

    public class IssueTemplateTooDense : IssueTemplate
    {
        public IssueTemplateTooDense(ICheck check)
            : base(check, IssueType.Warning, "Chord at {0:0}ms has {1} simultaneous notes (limit {2} for {3}K).")
        {
        }
    }
}
