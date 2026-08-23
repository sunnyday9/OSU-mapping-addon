using System.Text.Json.Serialization;

namespace AiStudio.Core.MappingIr.Model;

/// <summary>
/// Ruleset 枚举（对应 schema 的 ruleset 字段）。
/// </summary>
public enum RulesetKind
{
    Osu,
    Taiko,
    Catch,
    Mania,
}

/// <summary>
/// 顶层 IR 文档（对应 schema 根对象）。
/// </summary>
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
    Evaluation Evaluation)
{
    public const string SchemaName = "osu-ai-mapping-ir";

    public const string SchemaVersion = "0.1.0";

    /// <summary>创建最小合法文档骨架（字段全部填默认值，供构建器填充）。</summary>
    public static MappingDocument CreateEmpty(
        string documentId,
        MapInfo map,
        RulesetInfo ruleset,
        DifficultyProfile difficultyProfile)
        => new(
            SchemaName,
            SchemaVersion,
            documentId,
            map,
            ruleset,
            difficultyProfile,
            MusicTimeline.Empty,
            MappingPlan.Empty,
            null,
            MappingConstraints.Default,
            null,
            Provenance.EmptyRuleBased,
            Evaluation.Empty);
}

public sealed record MapInfo(
    string AudioHash,
    int? BeatmapId = null,
    int? DifficultyId = null,
    string? Title = null,
    string? Artist = null,
    string? Creator = null,
    string? AudioFilename = null);

public sealed record RulesetInfo(
    RulesetKind Ruleset,
    IReadOnlyDictionary<string, object?> Variant);

/// <summary>
/// 难度档案：用户可见目标是 <see cref="TargetStarRating"/>，规划层操作正交设计维度（全为 [0,1]）。
/// </summary>
public sealed record DifficultyProfile(
    double? TargetStarRating,
    DimensionProfile Dimensions,
    DifficultyPreferences Preferences,
    double Tolerance = 0.0)
{
    /// <summary>一个默认的"均衡"难度档案（density 0.6 / rhythm 0.5，偏可读性）。</summary>
    public static DifficultyProfile Balanced => new(
        5.0,
        new DimensionProfile(0.60, 0.50, 0.55, 0.45, 0.40, 0.25, 0.20),
        DifficultyPreferences.Default);

    public static DifficultyProfile FromDimensions(DimensionProfile dimensions, DifficultyPreferences? preferences = null, double? targetSr = null)
        => new(targetSr, dimensions, preferences ?? DifficultyPreferences.Default);
}

public sealed record DimensionProfile(
    double Density,
    double RhythmComplexity,
    double Reading,
    double Stamina,
    double Technicality,
    double Movement,
    double LnComplexity);

public sealed record DifficultyPreferences(
    bool AllowExtremePatterns = false,
    bool PreferReadability = true,
    bool PreferMusicSync = true,
    bool PreferPatternVariety = true,
    IReadOnlyDictionary<string, object?>? Extensions = null)
{
    public static DifficultyPreferences Default => new();
}

/// <summary>音乐时间线：<c>Timeline → Section → Phrase → MusicEvent</c> 层级。</summary>
public sealed record MusicTimeline(
    int DurationMs,
    TempoInfo Tempo,
    IReadOnlyList<MusicSection> Sections,
    IReadOnlyList<MusicPhrase> Phrases,
    IReadOnlyList<MusicEvent> Events)
{
    public static MusicTimeline Empty => new(0, new TempoInfo(0, Array.Empty<TempoChange>()), Array.Empty<MusicSection>(), Array.Empty<MusicPhrase>(), Array.Empty<MusicEvent>());
}

public sealed record TempoInfo(
    double BaseBpm,
    IReadOnlyList<TempoChange> Changes);

public sealed record TempoChange(
    int TimeMs,
    double Bpm);

/// <summary>音乐段落（schema 枚举：intro/verse/pre_chorus/chorus/drop/bridge/break/outro/instrumental/transition/unknown）。</summary>
public sealed class MusicSection
{
    public MusicSection(string id, int startTime, int endTime, SectionType type, double energy, double confidence, IReadOnlyList<string>? labels = null)
    {
        Id = id;
        StartTime = startTime;
        EndTime = endTime;
        Type = type;
        Energy = energy;
        Confidence = confidence;
        Labels = labels;
    }

    public string Id { get; init; }

    public int StartTime { get; init; }

    public int EndTime { get; init; }

    [JsonConverter(typeof(SnakeCaseStringEnumConverter))]
    public SectionType Type { get; init; }

    public double Energy { get; init; }

    public double Confidence { get; init; }

    /// <summary>schema 要求 labels 为 array 且非 null；null 归一为空数组输出。</summary>
    [JsonPropertyName("labels")]
    [JsonConverter(typeof(NullToEmptyStringArrayConverter))]
    public IReadOnlyList<string>? Labels { get; init; }

    public override string ToString() => $"MusicSection({Id}, {StartTime}-{EndTime}, {Type})";

    public void Deconstruct(out string id, out int startTime, out int endTime, out SectionType type, out double energy, out double confidence, out IReadOnlyList<string>? labels)
    {
        id = Id;
        startTime = StartTime;
        endTime = EndTime;
        type = Type;
        energy = Energy;
        confidence = Confidence;
        labels = Labels;
    }
}

public enum SectionType
{
    Intro,
    Verse,
    PreChorus,
    Chorus,
    Drop,
    Bridge,
    Break,
    Outro,
    Instrumental,
    Transition,
    Unknown,
}

public sealed record MusicPhrase(
    string Id,
    string SectionId,
    int StartTime,
    int EndTime,
    [property: JsonConverter(typeof(SnakeCaseStringEnumConverter))] PhraseType Type,
    [property: JsonConverter(typeof(SnakeCaseStringEnumConverter))] MusicalRole MusicalRole);

public enum PhraseType
{
    VocalPhrase,
    InstrumentalPhrase,
    RhythmPhrase,
    MelodicPhrase,
    CallResponse,
    Motif,
    Fill,
    Unknown,
}

public enum MusicalRole
{
    Lead,
    Support,
    Rhythm,
    Bass,
    Transition,
    Accent,
    Background,
    Unknown,
}

/// <summary>音乐事件（schema 枚举：beat/onset/kick/snare/hihat/percussion/bass/chord/vocal/vocal_phrase/melody/accent/silence/transition）。</summary>
public sealed record MusicEvent(
    string Id,
    int Time,
    int Duration,
    [property: JsonConverter(typeof(SnakeCaseStringEnumConverter))] MusicEventType Type,
    double Strength,
    double Confidence,
    string Source,
    string? PhraseId = null,
    IReadOnlyDictionary<string, object?>? Features = null);

public enum MusicEventType
{
    Beat,
    Onset,
    Kick,
    Snare,
    Hihat,
    Percussion,
    Bass,
    Chord,
    Vocal,
    VocalPhrase,
    Melody,
    Accent,
    Silence,
    Transition,
}

/// <summary>映射计划 = 意图 + 模式 + 转换。</summary>
public sealed record MappingPlan(
    IReadOnlyList<MappingIntent> Intents,
    IReadOnlyList<PatternIntent> Patterns,
    IReadOnlyList<PatternTransition> Transitions)
{
    public static MappingPlan Empty => new(Array.Empty<MappingIntent>(), Array.Empty<PatternIntent>(), Array.Empty<PatternTransition>());
}

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
    Resolution,
}

/// <summary>映射意图：回答"这一段作为 mapping 想表达什么"。</summary>
public sealed record MappingIntent(
    string Id,
    int StartTime,
    int EndTime,
    [property: JsonConverter(typeof(SnakeCaseStringEnumConverter))] MappingPrimaryIntent Primary,
    IReadOnlyList<string> Secondary,
    IReadOnlyList<string> MusicalTargets,
    MappingEmphasis Emphasis,
    double Complexity,
    double Confidence,
    MappingContinuity? Continuity = null,
    string? Rationale = null);

public sealed record MappingContinuity(
    [property: JsonConverter(typeof(SnakeCaseStringEnumConverter))] ContinuityRelation Relation,
    string? Reference = null);

public enum ContinuityRelation
{
    StartNew,
    Continue,
    Repeat,
    Vary,
    Contrast,
    Release,
    Reset,
}

/// <summary>强调向量（六维，全 [0,1]）。</summary>
public sealed record MappingEmphasis(
    double Rhythm,
    double Density,
    double Movement,
    double PatternComplexity,
    double Accent,
    double Contrast);

/// <summary>模式意图：ruleset 专属但统一信封包裹。</summary>
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

/// <summary>具体对象（renderer 输出）。</summary>
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
    IReadOnlyDictionary<string, object?>? MusicAlignment)
{
    public static MappingConstraints Default => new(
        new TimingConstraints("beat_grid", new[] { "1/1", "1/2", "1/4", "1/8", "1/16" }),
        new Dictionary<string, object?> { ["max_density"] = 0.9, ["allow_extreme_pattern"] = false },
        new Dictionary<string, object?> { ["require_onset_alignment"] = true });
}

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
    Imported,
}

public sealed record Provenance(
    [property: JsonConverter(typeof(SnakeCaseStringEnumConverter))] ProvenanceOrigin Origin,
    AgentInfo? Agent,
    ModelInfo? Model,
    DateTimeOffset? GeneratedAt,
    IReadOnlyList<HumanEdit>? HumanEdits)
{
    public static Provenance EmptyRuleBased => new(ProvenanceOrigin.RuleBased, null, null, null, Array.Empty<HumanEdit>());
}

public sealed record AgentInfo(string Name, string Version);

public sealed record ModelInfo(string Provider, string Name, string Version);

public sealed record HumanEdit(
    string Type,
    IReadOnlyList<int> TimeRange,
    string? From = null,
    string? To = null);

/// <summary>
/// 评估（观察性，非生成器隐藏真相源）。
/// <see cref="DifficultyKnown"/> 为 false 表示难度评估器不可用——系统不得声称达到目标 SR（spec §25.4）。
/// </summary>
public sealed record Evaluation(
    bool? Valid,
    [property: JsonConverter(typeof(NullToEmptyDictionaryConverter))] IReadOnlyDictionary<string, object?>? Difficulty = null,
    double? MusicAlignmentScore = null,
    double? TransitionScore = null,
    double? HumanAcceptance = null,
    IReadOnlyList<IReadOnlyDictionary<string, object?>>? Issues = null,
    bool? DifficultyKnown = null)
{
    public static Evaluation Empty => new(null, null, null, null, null, Array.Empty<IReadOnlyDictionary<string, object?>>());
}
