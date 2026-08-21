using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.AiStudio.Taiko.Checks;

/// <summary>
/// Checks for long mono-colour streams (don-only or kat-only runs), which are stamina/tedium risks.
///
/// Header cites https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!taiko
///
/// Taiko difficulty is highly colour-sensitive (see TaikoDifficultyCalculator colour/stamina skills);
/// this check flags any run of >= threshold consecutive Hits with the same HitType (Centre/Rim)
/// and reports its start time and length. DrumRolls/Swells are ignored — only Hit objects.
/// </summary>
public class CheckTaikoMonoPattern : ICheck
{
    /// <summary>Run length that is considered "mono" (stream of one colour).</summary>
    private const int mono_threshold = 8;

    private readonly IssueTemplateMonoPattern templateMonoPattern;

    public CheckTaikoMonoPattern()
    {
        templateMonoPattern = new IssueTemplateMonoPattern(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Mono-colour pattern (long don or kat stream)");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[] { templateMonoPattern };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var hits = context.CurrentDifficulty.Playable.HitObjects
                         .OfType<Hit>()
                         .OrderBy(h => h.StartTime)
                         .ToList();

        if (hits.Count < mono_threshold)
            yield break;

        int runStart = 0;
        for (int i = 1; i <= hits.Count; i++)
        {
            bool same = i < hits.Count && hits[i].Type == hits[runStart].Type;
            if (same)
                continue;

            int runLength = i - runStart;
            if (runLength >= mono_threshold)
            {
                var first = hits[runStart];
                string colour = first.Type == HitType.Centre ? "don" : "kat";
                yield return new Issue(first, templateMonoPattern, colour, runLength, first.StartTime);
            }

            runStart = i;
        }
    }

    public class IssueTemplateMonoPattern : IssueTemplate
    {
        public IssueTemplateMonoPattern(ICheck check)
            : base(check, IssueType.Warning,
                "Long mono-colour run: {1} consecutive {0} hits starting at {2:0} ms. Consider breaking mono streams with colour changes (hits like ddk/kkd).")
        {
        }
    }
}
