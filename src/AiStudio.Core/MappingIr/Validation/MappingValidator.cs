using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Patterns;

namespace AiStudio.Core.MappingIr.Validation;

/// <summary>
/// 校验器契约（对应 mapping-ir-types.cs 的 IMappingValidator）。
/// 纯确定性：不依赖 LLM，检查列合法性/重叠/密度/jack 上限/LN 约束/节奏量化。
/// </summary>
public interface IMappingValidator
{
    ValidationResult Validate(MappingDocument document);
}

public sealed record ValidationResult(
    bool Valid,
    IReadOnlyList<PatternIssue> Issues);

/// <summary>
/// Mapping IR v0.1 文档校验器（mania 4K 专用规则 + 通用结构检查）。
/// </summary>
public sealed class MappingValidator : IMappingValidator
{
    private const double ms_per_minute = 60000.0;

    public ValidationResult Validate(MappingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issues = new List<PatternIssue>();
        validateStructure(document, issues);
        validateRuleset(document, issues);
        validateObjects(document, issues);
        validatePlanConsistency(document, issues);

        return new ValidationResult(issues.All(i => i.Severity != "error"), issues);
    }

    private static void validateStructure(MappingDocument document, List<PatternIssue> issues)
    {
        if (document.Schema != MappingDocument.SchemaName)
            issues.Add(new PatternIssue("schema_mismatch", "error", $"Schema must be '{MappingDocument.SchemaName}', got '{document.Schema}'."));

        if (document.Version != MappingDocument.SchemaVersion)
            issues.Add(new PatternIssue("version_mismatch", "error", $"Version must be '{MappingDocument.SchemaVersion}', got '{document.Version}'."));

        if (string.IsNullOrEmpty(document.DocumentId))
            issues.Add(new PatternIssue("missing_document_id", "error", "document_id is required."));

        if (document.MusicTimeline is null)
            issues.Add(new PatternIssue("missing_timeline", "error", "music_timeline is required."));
    }

    private static void validateRuleset(MappingDocument document, List<PatternIssue> issues)
    {
        if (document.Ruleset.Ruleset != RulesetKind.Mania)
        {
            // MVP A 仅支持 mania；其他 ruleset 报 warning（结构合法但无 provider）。
            issues.Add(new PatternIssue("unsupported_ruleset", "warning", $"MVP A supports mania only; got {document.Ruleset.Ruleset}."));
            return;
        }

        int? keys = document.Ruleset.Variant.TryGetValue("keys", out var v) && v is not null ? Convert.ToInt32(v) : null;
        if (keys != 4)
            issues.Add(new PatternIssue("unsupported_keycount", "error", $"MVP A supports 4K mania; got {keys} keys."));
    }

    private static void validateObjects(MappingDocument document, List<PatternIssue> issues)
    {
        var objects = document.ConcreteObjects;
        if (objects is null || objects.Count == 0)
        {
            issues.Add(new PatternIssue("no_objects", "error", "concrete_objects is empty."));
            return;
        }

        bool mania = document.Ruleset.Ruleset == RulesetKind.Mania;
        int maxColumn = 3;
        var byColumn = new Dictionary<int, List<ConcreteObject>>();

        foreach (var obj in objects)
        {
            if (mania)
            {
                if (obj.Column is null || obj.Column < 0 || obj.Column > maxColumn)
                    issues.Add(new PatternIssue("invalid_column", "error", $"Object '{obj.Id}' column {obj.Column} out of range [0,{maxColumn}]."));

                if (obj.Type == "hold")
                {
                    if (obj.EndTime is null || obj.EndTime <= obj.Time)
                        issues.Add(new PatternIssue("invalid_ln", "error", $"Hold '{obj.Id}' end_time must be greater than start_time."));
                    else if (obj.EndTime.Value - obj.Time < 20)
                        issues.Add(new PatternIssue("ln_too_short", "warning", $"Hold '{obj.Id}' shorter than 20ms."));
                }
            }

            if (obj.Time < 0)
                issues.Add(new PatternIssue("negative_time", "error", $"Object '{obj.Id}' has negative time."));
        }

        // 同列重叠检查（mania：同列对象不能时间重叠；LN 允许嵌套——只检查 start 重叠）。
        var ordered = objects.OrderBy(o => o.Time).ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            var a = ordered[i];
            if (a.Column is null)
                continue;
            for (int j = i + 1; j < ordered.Count; j++)
            {
                var b = ordered[j];
                if (b.Column != a.Column)
                    continue;
                if (b.Time >= (a.EndTime ?? a.Time))
                    break; // 已按时间排序，后续对象更晚
                // a 与 b 同列且 b 的 start 在 a 的区间内 → 重叠
                if (b.Time < (a.EndTime ?? a.Time) && b.Time >= a.Time)
                {
                    issues.Add(new PatternIssue("column_overlap", "error", $"Objects '{a.Id}' and '{b.Id}' overlap in column {a.Column}."));
                    break;
                }
            }
        }
    }

    private static void validatePlanConsistency(MappingDocument document, List<PatternIssue> issues)
    {
        foreach (var intent in document.MappingPlan.Intents)
        {
            if (intent.EndTime <= intent.StartTime)
                issues.Add(new PatternIssue("invalid_intent_range", "error", $"Intent '{intent.Id}' has non-positive range."));
        }

        foreach (var pattern in document.MappingPlan.Patterns)
        {
            if (pattern.Ruleset != document.Ruleset.Ruleset)
                issues.Add(new PatternIssue("pattern_ruleset_mismatch", "error", $"Pattern '{pattern.Id}' ruleset {pattern.Ruleset} != document ruleset {document.Ruleset.Ruleset}."));

            if (pattern.EndTime <= pattern.StartTime)
                issues.Add(new PatternIssue("invalid_pattern_range", "error", $"Pattern '{pattern.Id}' has non-positive range."));
        }
    }
}
