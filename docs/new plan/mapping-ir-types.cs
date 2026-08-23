using System;
using System.Collections.Generic;

namespace OsuAiMapper.MappingIR;

public enum RulesetKind
{
    Osu,
    Taiko,
    Catch,
    Mania
}

public sealed record MappingDocument(
    string Schema,
    string Version,
    string DocumentId,
    MapInfo Map,
    RulesetInfo Ruleset,
    DifficultyProfile DifficultyProfile,
    MusicTimeline MusicTimeline,
    MappingPlan MappingPlan,
    IReadOnlyList<ConcreteObject>? ConcreteObjects,
    MappingConstraints Constraints,
    StyleProfile? Style,
    Provenance Provenance,
    Evaluation Evaluation);

public sealed record MapInfo(
    string AudioHash,
    int? BeatmapId = null,
    int? DifficultyId = null,
    string? Title = null,
    string? Artist = null,
    string? Creator = null);

public sealed record RulesetInfo(
    RulesetKind Ruleset,
    IReadOnlyDictionary<string, object?> Variant);

public sealed record DifficultyProfile(
    double? TargetStarRating,
    DimensionProfile Dimensions,
    DifficultyPreferences Preferences,
    double Tolerance = 0.0);

public sealed record DimensionProfile(
    double Density,
    double RhythmComplexity,
    double Reading,
    double Stamina,
    double Technicality,
    double Movement,
    double LnComplexity);

public sealed record DifficultyPreferences(
    bool AllowExtremePatterns,
    bool PreferReadability,
    bool PreferMusicSync,
    bool PreferPatternVariety,
    IReadOnlyDictionary<string, object?>? Extensions = null);

public sealed record MusicTimeline(
    int DurationMs,
    TempoInfo Tempo,
    IReadOnlyList<MusicSection> Sections,
    IReadOnlyList<MusicPhrase> Phrases,
    IReadOnlyList<MusicEvent> Events);

public sealed record TempoInfo(
    double BaseBpm,
    IReadOnlyList<TempoChange> Changes);

public sealed record TempoChange(
    int TimeMs,
    double Bpm);

public sealed record MusicSection(
    string Id,
    int StartTime,
    int EndTime,
    string Type,
    double Energy,
    double Confidence,
    IReadOnlyList<string>? Labels = null);

public sealed record MusicPhrase(
    string Id,
    string SectionId,
    int StartTime,
    int EndTime,
    string Type,
    string MusicalRole);

public sealed record MusicEvent(
    string Id,
    int Time,
    int Duration,
    string Type,
    double Strength,
    double Confidence,
    string Source,
    string? PhraseId = null,
    IReadOnlyDictionary<string, object?>? Features = null);

public sealed record MappingPlan(
    IReadOnlyList<MappingIntent> Intents,
    IReadOnlyList<PatternIntent> Patterns,
    IReadOnlyList<PatternTransition> Transitions);

public enum MappingPrimaryIntent
{
    Establish,
    Repeat,
    Variation,
    Escalation,
    Release,
    Climax,
    DeEscalation,
    Contrast,
    Transition,
    Accent,
    Silence,
    Anticipation,
    Resolution
}

public sealed record MappingIntent(
    string Id,
    int StartTime,
    int EndTime,
    MappingPrimaryIntent Primary,
    IReadOnlyList<string> Secondary,
    IReadOnlyList<string> MusicalTargets,
    MappingEmphasis Emphasis,
    double Complexity,
    double Confidence,
    MappingContinuity? Continuity = null,
    string? Rationale = null);

public sealed record MappingContinuity(
    string Relation,
    string? Reference = null);

public sealed record MappingEmphasis(
    double Rhythm,
    double Density,
    double Movement,
    double PatternComplexity,
    double Accent,
    double Contrast);

public sealed record PatternIntent(
    string Id,
    RulesetKind Ruleset,
    string Family,
    int StartTime,
    int EndTime,
    IReadOnlyDictionary<string, object?> Parameters,
    IReadOnlyDictionary<string, object?> Constraints,
    double Confidence,
    string? TransitionIn = null,
    string? TransitionOut = null,
    string? Rationale = null);

public sealed record PatternTransition(
    string Id,
    string FromPattern,
    string ToPattern,
    string TransitionType,
    TransitionOverlap? Overlap,
    IReadOnlyDictionary<string, object?> Constraints);

public sealed record TransitionOverlap(int Start, int End);

public sealed record ConcreteObject(
    string Id,
    string Type,
    int Time,
    int? EndTime = null,
    int? Column = null,
    Position? Position = null,
    string? SourcePatternId = null);

public sealed record Position(double X, double Y);

public sealed record MappingConstraints(
    TimingConstraints? Timing,
    IReadOnlyDictionary<string, object?>? Playability,
    IReadOnlyDictionary<string, object?>? MusicAlignment);

public sealed record TimingConstraints(
    string Snap,
    IReadOnlyList<string> AllowedSubdivisions);

public sealed record StyleProfile(
    string Id,
    IReadOnlyDictionary<string, object?> Parameters);

public enum ProvenanceOrigin
{
    Human,
    RuleBased,
    AiGenerated,
    Hybrid,
    Imported
}

public sealed record Provenance(
    ProvenanceOrigin Origin,
    AgentInfo? Agent,
    ModelInfo? Model,
    DateTimeOffset? GeneratedAt,
    IReadOnlyList<HumanEdit>? HumanEdits);

public sealed record AgentInfo(string Name, string Version);
public sealed record ModelInfo(string Provider, string Name, string Version);

public sealed record HumanEdit(
    string Type,
    IReadOnlyList<int> TimeRange,
    string? From = null,
    string? To = null);

public sealed record Evaluation(
    bool? Valid,
    IReadOnlyDictionary<string, object?>? Difficulty = null,
    double? MusicAlignmentScore = null,
    double? TransitionScore = null,
    double? HumanAcceptance = null,
    IReadOnlyList<IReadOnlyDictionary<string, object?>>? Issues = null);

public interface IPatternProvider
{
    RulesetKind Ruleset { get; }

    PatternGenerationResult Generate(
        PatternIntent intent,
        PatternGenerationContext context);
}

public sealed record PatternGenerationContext(
    MusicTimeline Music,
    MappingDocument CurrentDocument,
    IReadOnlyList<ConcreteObject> PreviousObjects,
    DifficultyProfile DifficultyProfile);

public sealed record PatternGenerationResult(
    IReadOnlyList<ConcreteObject> Objects,
    IReadOnlyList<PatternIssue> Issues);

public sealed record PatternIssue(
    string Code,
    string Severity,
    string Message);

public interface IMappingValidator
{
    ValidationResult Validate(MappingDocument document);
}

public sealed record ValidationResult(
    bool Valid,
    IReadOnlyList<PatternIssue> Issues);
