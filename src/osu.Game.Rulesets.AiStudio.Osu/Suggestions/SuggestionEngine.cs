using AiStudio.Core.Models;
using osu.Game.Rulesets.Edit.Checks.Components;

namespace osu.Game.Rulesets.AiStudio.Osu.Suggestions;

/// <summary>
/// 建议系统骨架（M1 交付物）：把 Verify 页的 <see cref="Issue"/> 翻译为面向制图者的
/// <see cref="Suggestion"/>（Title=检查名/条款、Detail=issue 消息、Time=issue 时间、RelatedCheck=检查名）。
/// 严重度映射：Problem/Error → Warning；Warning → Advice；Negligible → Info。
/// </summary>
public static class SuggestionEngine
{
    /// <summary>
    /// 把一组 Issue 翻译为建议列表。
    /// </summary>
    public static IReadOnlyList<Suggestion> FromIssues(IEnumerable<Issue> issues)
    {
        return issues.Select(issue => new Suggestion
        {
            Title = issue.Check.Metadata.Description,
            Detail = issue.ToString(),
            Severity = mapSeverity(issue.Template.Type),
            Time = issue.Time,
            RelatedCheck = issue.Check.Metadata.Description,
        }).ToList();
    }

    private static SuggestionSeverity mapSeverity(IssueType type) => type switch
    {
        IssueType.Problem => SuggestionSeverity.Warning,
        IssueType.Error => SuggestionSeverity.Warning,
        IssueType.Warning => SuggestionSeverity.Advice,
        IssueType.Negligible => SuggestionSeverity.Info,
        _ => SuggestionSeverity.Info,
    };
}
