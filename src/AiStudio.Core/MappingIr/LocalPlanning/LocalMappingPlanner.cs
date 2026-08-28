using AiStudio.Core.MappingIr.Evidence;
using AiStudio.Core.MappingIr.GlobalPlanning;
using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.LocalPlanning;

/// <summary>
/// 本地规划上下文（spec §8.1）：当前段/短语 + 相关证据 + 全局计划 + 前序 pattern + 难度预算。
/// </summary>
public sealed record LocalMappingContext(
    MusicSection Section,
    IReadOnlyList<MappingEvidence> Evidence,
    GlobalMappingPlan GlobalPlan,
    IReadOnlyList<PatternIntent> PreviousPatterns,
    IReadOnlyList<PatternIntent> NextPatterns,
    DifficultyProfile DifficultyProfile);

/// <summary>
/// 本地规划器契约（spec §23 ILocalMappingPlanner）：
/// 把上下文 + 证据 + 全局计划转化为单条 MappingIntent。
/// </summary>
public interface ILocalMappingPlanner
{
    MappingIntent Plan(LocalMappingContext context, StyleProfile? style = null);
}

/// <summary>
/// 确定性本地规划器：规则型意图决策（primaryIntentFor/secondaryIntentsFor）之上，
/// 引入证据与全局计划（climax 强度、密度预算）做 future-aware 意图决策。
/// </summary>
public sealed class DeterministicLocalPlanner : ILocalMappingPlanner
{
    public MappingIntent Plan(LocalMappingContext context, StyleProfile? style = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var section = context.Section;
        var evidence = context.Evidence.FirstOrDefault(e => e.StartTime <= section.StartTime && e.EndTime >= section.EndTime)
                       ?? context.Evidence.FirstOrDefault(e => e.StartTime >= section.StartTime && e.StartTime < section.EndTime);

        var sectionPlan = context.GlobalPlan.SectionPlans.FirstOrDefault(p => p.SectionId == section.Id);
        bool isGlobalClimax = Math.Abs(section.StartTime - context.GlobalPlan.GlobalClimax.TimeMs) < 50;

        var primary = primaryIntentFor(section.Type, section.Energy, sectionPlan?.Role, isGlobalClimax);
        double density = evidence?.Density ?? clamp01(section.Energy);
        double rhythm = evidence?.Rhythm ?? clamp01(section.Energy * 0.7 + 0.3);
        double accent = evidence?.Accent ?? clamp01(section.Energy);

        string rationale = primary switch
        {
            MappingPrimaryIntent.Climax => $"Evidence (energy {evidence?.Energy:0.00}, rhythm {rhythm:0.00}) + global climax at {context.GlobalPlan.GlobalClimax.TimeMs} support a climactic passage.",
            MappingPrimaryIntent.Escalation => $"Evidence energy {evidence?.Energy:0.00} with density budget {sectionPlan?.DensityBudget ?? 0.5:0.00} supports escalation.",
            MappingPrimaryIntent.Establish => $"Section {snake(section.Type.ToString())} opens with density budget {sectionPlan?.DensityBudget ?? 0.5:0.00}: establish baseline.",
            MappingPrimaryIntent.DeEscalation => $"Bridge/break energy {evidence?.Energy:0.00} supports release before the next build.",
            MappingPrimaryIntent.Resolution => $"Outro resolves the arc (global climax at {context.GlobalPlan.GlobalClimax.TimeMs}): land on stable rhythm.",
            MappingPrimaryIntent.Repeat => $"Continuity preferred: previous section type repeats with similar evidence.",
            MappingPrimaryIntent.Variation => $"Verse varies the established motif (novelty {evidence?.Novelty:0.00}).",
            _ => $"Section {snake(section.Type.ToString())} keeps flow with intensity {section.Energy:0.00}.",
        };

        return new MappingIntent(
            $"intent_{section.Id}",
            section.StartTime,
            section.EndTime,
            primary,
            secondaryIntentsFor(primary),
            new[] { snake(section.Type.ToString()) },
            new MappingEmphasis(
                Rhythm: rhythm,
                Density: clamp01(density * 0.7 + (context.DifficultyProfile.Dimensions.Density * 0.3)),
                Movement: clamp01(context.DifficultyProfile.Dimensions.Movement),
                PatternComplexity: clamp01(context.DifficultyProfile.Dimensions.Technicality * 0.5 + density * 0.3),
                Accent: accent,
                Contrast: clamp01(1.0 - section.Energy)),
            complexityFor(context.DifficultyProfile, section.Energy),
            0.9,
            context.PreviousPatterns.Count == 0
                ? new MappingContinuity(ContinuityRelation.StartNew, null)
                : new MappingContinuity(ContinuityRelation.Vary, context.PreviousPatterns[^1].Id),
            rationale);
    }

    private static MappingPrimaryIntent primaryIntentFor(SectionType type, double energy, string? globalRole, bool isGlobalClimax)
    {
        if (isGlobalClimax && energy > 0.6)
            return MappingPrimaryIntent.Climax;

        return type switch
        {
            SectionType.Intro => energy > 0.6 ? MappingPrimaryIntent.Escalation : MappingPrimaryIntent.Establish,
            SectionType.Chorus or SectionType.Drop => energy > 0.7 ? MappingPrimaryIntent.Climax : MappingPrimaryIntent.Escalation,
            SectionType.Bridge or SectionType.Break => MappingPrimaryIntent.DeEscalation,
            SectionType.Outro => MappingPrimaryIntent.Resolution,
            SectionType.Verse => energy > 0.6 ? MappingPrimaryIntent.Variation : MappingPrimaryIntent.Repeat,
            _ => MappingPrimaryIntent.Establish,
        };
    }

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

    private static double complexityFor(DifficultyProfile profile, double energy)
        => clamp01(profile.Dimensions.RhythmComplexity * 0.5 + energy * 0.5);

    private static double clamp01(double v) => Math.Clamp(v, 0.0, 1.0);

    private static string snake(string pascal) => string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
