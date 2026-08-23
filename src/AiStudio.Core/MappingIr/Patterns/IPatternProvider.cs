using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Patterns;

/// <summary>
/// Pattern 提供器契约（对应 mapping-ir-types.cs 的 IPatternProvider）。
/// 消费 PatternIntent + 上下文，产出具体对象 + 问题列表。必须对固定 seed 与固定输入确定。
/// </summary>
public interface IPatternProvider
{
    RulesetKind Ruleset { get; }

    PatternGenerationResult Generate(PatternIntent intent, PatternGenerationContext context);
}

public sealed record PatternGenerationContext(
    MusicTimeline Music,
    MappingDocument CurrentDocument,
    IReadOnlyList<ConcreteObject> PreviousObjects,
    DifficultyProfile DifficultyProfile,
    int Seed = 0)
{
    /// <summary>派生随机数：每个 family 独立 seed（ADR-MVP-A-003），用 FNV-1a 稳定哈希保证跨进程可复现（ADR-MVP-A-008）。</summary>
    public Random CreateFamilyRandom(string family)
        => new(DeterministicHash.DeriveSeed(family, Seed));
}

public sealed record PatternGenerationResult(
    IReadOnlyList<ConcreteObject> Objects,
    IReadOnlyList<PatternIssue> Issues);

public sealed record PatternIssue(
    string Code,
    string Severity,
    string Message);
