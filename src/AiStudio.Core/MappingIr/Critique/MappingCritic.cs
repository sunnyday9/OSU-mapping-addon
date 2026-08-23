using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Critique;

/// <summary>Critic 问题（spec §15.2）：code/severity/time_range/message/suggested_actions。</summary>
public sealed record CriticIssue(
    string Code,
    string Severity,
    int StartTime,
    int EndTime,
    string Message,
    IReadOnlyList<string> SuggestedActions);

/// <summary>Critic 报告（spec §15.2）。</summary>
public sealed record CriticReport(
    bool Valid,
    IReadOnlyList<CriticIssue> Issues)
{
    public static CriticReport Clean => new(true, Array.Empty<CriticIssue>());

    public IEnumerable<CriticIssue> HardIssues => Issues.Where(i => i.Severity == "error");

    public IEnumerable<CriticIssue> SoftIssues => Issues.Where(i => i.Severity != "error");
}

/// <summary>
/// Critic 契约（spec §23 IMappingCritic）：生成后检查，硬问题阻断，软问题进 revision。
/// </summary>
public interface IMappingCritic
{
    CriticReport Evaluate(MappingDocument document);
}
