namespace AiStudio.Core.Models;

public enum SuggestionSeverity
{
    Info,
    Advice,
    Warning,
}

/// <summary>
/// 面向制图者的一条可执行建议（由检查结果翻译而来）。
/// </summary>
public sealed class Suggestion
{
    public required string Title { get; init; }

    public required string Detail { get; init; }

    public SuggestionSeverity Severity { get; init; } = SuggestionSeverity.Info;

    /// <summary>建议指向的时间点（如有）。</summary>
    public double? Time { get; init; }

    /// <summary>来源检查名（对应 rc-coverage.md 中的条目）。</summary>
    public string? RelatedCheck { get; init; }
}
