using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Timeline;

namespace AiStudio.Core.MappingIr.Planning;

/// <summary>
/// 规划器接口：时间线 + 难度档案 → 映射计划。
/// 当前唯一实现为 <see cref="DeterministicMappingPlanner"/>（规则型）；
/// 未来 LLM Planner 实现同一接口即可替换（ADR-MVP-A-004）。
/// </summary>
public interface IMappingPlanner
{
    MappingPlan Plan(MusicTimeline timeline, DifficultyProfile difficultyProfile, int seed = 0);
}

/// <summary>
/// 规则型规划器：段落强度 → MappingIntent；Intent + 难度维度 → PatternIntent。
/// 所有决策带 rationale（可解释性）；同 seed 同输入 → 同输出。
/// </summary>
public sealed class DeterministicMappingPlanner : IMappingPlanner
{
    private const int ms_per_minute = 60000;

    public MappingPlan Plan(MusicTimeline timeline, DifficultyProfile difficultyProfile, int seed = 0)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(difficultyProfile);

        var intents = new List<MappingIntent>();
        var patterns = new List<PatternIntent>();
        var transitions = new List<PatternTransition>();

        for (int i = 0; i < timeline.Sections.Count; i++)
        {
            var section = timeline.Sections[i];
            if (section.EndTime <= section.StartTime)
                continue;

            var intent = createIntent(section, timeline, difficultyProfile, i, seed);
            intents.Add(intent);

            var pattern = createPattern(intent, section, difficultyProfile, i, seed, timeline.Tempo.BaseBpm);
            patterns.Add(pattern);

            if (i > 0)
                transitions.Add(createTransition(intents[i - 1], intent, patterns[i - 1], pattern));
        }

        return new MappingPlan(intents, patterns, transitions);
    }

    private static MappingIntent createIntent(MusicSection section, MusicTimeline timeline, DifficultyProfile profile, int index, int seed)
    {
        var primary = primaryIntentFor(section.Type, section.Energy);
        var emphasis = emphasisFor(section, profile);

        string rationale = primary switch
        {
            MappingPrimaryIntent.Climax => $"High-energy {snake(section.Type.ToString())} with peak intensity warrants a climactic density build.",
            MappingPrimaryIntent.Escalation => $"{snake(section.Type.ToString())} energy {section.Energy:0.00} escalates toward the chorus peak.",
            MappingPrimaryIntent.Establish => $"{snake(section.Type.ToString())} opens the passage: establish baseline density and rhythm.",
            MappingPrimaryIntent.DeEscalation => $"{snake(section.Type.ToString())} winds down: release density and return to the root movement.",
            MappingPrimaryIntent.Resolution => $"{snake(section.Type.ToString())} resolves the phrase: land on stable rhythm and readable spacing.",
            MappingPrimaryIntent.Repeat => $"{snake(section.Type.ToString())} re-states the established motif for continuity.",
            MappingPrimaryIntent.Variation => $"{snake(section.Type.ToString())} varies the established motif to avoid monotony.",
            _ => $"{snake(section.Type.ToString())} keeps the musical flow with moderate intensity.",
        };

        return new MappingIntent(
            $"intent_{section.Id}",
            section.StartTime,
            section.EndTime,
            primary,
            secondaryIntentsFor(primary),
            new[] { snake(section.Type.ToString()) },
            emphasis,
            complexityFor(profile, section.Energy),
            0.9,
            new MappingContinuity(index == 0 ? ContinuityRelation.StartNew : ContinuityRelation.Vary, index == 0 ? null : $"intent_{timeline.Sections[index - 1].Id}"),
            rationale);
    }

    private static MappingPrimaryIntent primaryIntentFor(SectionType type, double energy)
        => type switch
        {
            SectionType.Intro => energy > 0.6 ? MappingPrimaryIntent.Escalation : MappingPrimaryIntent.Establish,
            SectionType.Chorus or SectionType.Drop => energy > 0.7 ? MappingPrimaryIntent.Climax : MappingPrimaryIntent.Escalation,
            SectionType.Bridge or SectionType.Break => MappingPrimaryIntent.DeEscalation,
            SectionType.Outro => MappingPrimaryIntent.Resolution,
            SectionType.Verse => energy > 0.6 ? MappingPrimaryIntent.Variation : MappingPrimaryIntent.Repeat,
            _ => MappingPrimaryIntent.Establish,
        };

    private static IReadOnlyList<string> secondaryIntentsFor(MappingPrimaryIntent primary)
        => primary switch
        {
            MappingPrimaryIntent.Climax => new[] { "rhythm_emphasis", "increase_density" },
            MappingPrimaryIntent.Escalation => new[] { "increase_density" },
            MappingPrimaryIntent.DeEscalation => new[] { "decrease_density" },
            MappingPrimaryIntent.Establish => new[] { "set_baseline" },
            MappingPrimaryIntent.Repeat => new[] { "continuity" },
            MappingPrimaryIntent.Variation => new[] { "subtle_change" },
            MappingPrimaryIntent.Resolution => new[] { "landing" },
            _ => new[] { "continuity" },
        };

    private static MappingEmphasis emphasisFor(MusicSection section, DifficultyProfile profile)
        => new(
            clamp01(section.Energy * 0.7 + profile.Dimensions.RhythmComplexity * 0.3),
            clamp01(section.Energy * 0.6 + profile.Dimensions.Density * 0.4),
            clamp01(profile.Dimensions.Movement),
            clamp01(profile.Dimensions.Technicality * 0.5 + section.Energy * 0.3),
            clamp01(section.Energy),
            clamp01(1.0 - section.Energy));

    private static double complexityFor(DifficultyProfile profile, double energy)
        => clamp01(profile.Dimensions.RhythmComplexity * 0.5 + energy * 0.5);

    private static PatternIntent createPattern(MappingIntent intent, MusicSection section, DifficultyProfile profile, int sectionIndex, int seed, double bpm)
    {
        var (subdivision, family) = selectRhythm(intent, profile.Dimensions.Density);
        string columnStrategy = pickColumnStrategy(sectionIndex, seed);

        var parameters = new Dictionary<string, object?>
        {
            ["subdivision"] = subdivision,
            ["density"] = Math.Round(clamp01(intent.Emphasis.Density), 2),
            ["column_strategy"] = columnStrategy,
            ["column_order"] = columnOrderFor(columnStrategy),
            ["jack_tolerance"] = 0.05,
            ["bpm"] = bpm, // 让 provider 落在真实 beat 网格上（对齐 MusicTimeline.Tempo）
        };

        var constraints = new Dictionary<string, object?>
        {
            ["max_consecutive_same_column"] = 1,
            ["allow_chords"] = family == "jumpstream" || family == "jump",
            ["allow_ln"] = family.StartsWith("ln", StringComparison.Ordinal) || family == "ln_rice",
            ["max_chord_size"] = 2,
        };

        return new PatternIntent(
            $"pattern_{section.Id}",
            RulesetKind.Mania,
            family,
            intent.StartTime,
            intent.EndTime,
            parameters,
            constraints,
            0.85,
            TransitionIn: null,
            TransitionOut: null,
            $"Density {profile.Dimensions.Density:0.00} and section energy {section.Energy:0.00} select {family} at {subdivision}.");
    }

    /// <summary>密度 → 节奏细分 + family 的选择表（确定性：只依赖输入，不用 rng 的决策路径留作随机轮换）。</summary>
    private static (string subdivision, string family) selectRhythm(MappingIntent intent, double density)
    {
        switch (intent.Primary)
        {
            case MappingPrimaryIntent.Climax:
                return ("1/16", "jumpstream");
            case MappingPrimaryIntent.Escalation:
                return density > 0.65 ? ("1/8", "stream") : ("1/8", "jump");
            case MappingPrimaryIntent.DeEscalation:
            case MappingPrimaryIntent.Resolution:
                return ("1/4", "single");
            case MappingPrimaryIntent.Establish:
                return ("1/8", "single");
            case MappingPrimaryIntent.Repeat:
            case MappingPrimaryIntent.Variation:
                return density > 0.7 ? ("1/16", "stream") : ("1/8", "jump");
            default:
                return ("1/8", "single");
        }
    }

    private static string pickColumnStrategy(int sectionIndex, int seed)
    {
        // 确定性轮换：seed 决定起点，section 序号决定偏移——不依赖跨段的 RNG 序列（ADR-MVP-A-003/008）。
        var strategies = new[] { "alternating", "mirror", "staircase" };
        return strategies[(seed + sectionIndex) % strategies.Length];
    }

    private static int[] columnOrderFor(string strategy)
        => strategy switch
        {
            "mirror" => new[] { 0, 3, 1, 2 },
            "staircase" => new[] { 0, 1, 2, 3 },
            _ => new[] { 0, 2, 1, 3 },
        };

    private static PatternTransition createTransition(MappingIntent fromIntent, MappingIntent toIntent, PatternIntent fromPattern, PatternIntent toPattern)
    {
        string transitionType = fromPattern.Family == toPattern.Family
            ? "same_family"
            : fromPattern.Family == "jumpstream" && toPattern.Family == "stream" ? "chord_removal"
            : fromPattern.Family == "stream" && toPattern.Family == "jumpstream" ? "chord_introduction"
            : fromPattern.Family.StartsWith("ln", StringComparison.Ordinal) ? "ln_release"
            : "hand_rebalance";

        return new PatternTransition(
            $"transition_{toIntent.Id}",
            fromPattern.Id,
            toPattern.Id,
            transitionType,
            new TransitionOverlap(fromIntent.EndTime - 250, fromIntent.EndTime),
            new Dictionary<string, object?> { ["overlap_policy"] = "no_objects_in_overlap" });
    }

    private static double clamp01(double v) => Math.Clamp(v, 0.0, 1.0);

    private static string snake(string pascal) => string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
