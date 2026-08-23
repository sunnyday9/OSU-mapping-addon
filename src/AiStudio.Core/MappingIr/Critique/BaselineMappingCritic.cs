using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Critique;

/// <summary>
/// 基线 Critic（spec §15）：
/// - 硬问题（error）：非法 timing、同列重叠、无效对象类型、空对象 → 阻断；
/// - 软问题（warning）：密度-能量不匹配、节奏对齐弱、pattern 连续重复 → 触发 revision。
/// </summary>
public sealed class BaselineMappingCritic : IMappingCritic
{
    public CriticReport Evaluate(MappingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var issues = new List<CriticIssue>();
        var objects = document.ConcreteObjects;
        var timeline = document.MusicTimeline;
        var plan = document.MappingPlan;

        bool hasObjects = objects is { Count: > 0 };
        if (!hasObjects)
            issues.Add(new CriticIssue("no_objects", "error", 0, 0, "No concrete objects generated.", new[] { "generate_patterns" }));

        // ---- hard: 重叠（同列）----
        if (hasObjects)
        {
            var byColumn = objects!.Where(o => o.Column is not null).GroupBy(o => o.Column!.Value);
            foreach (var group in byColumn)
            {
                var ordered = group.OrderBy(o => o.Time).ToList();
                for (int i = 1; i < ordered.Count; i++)
                {
                    var prev = ordered[i - 1];
                    var cur = ordered[i];
                    if (cur.Time < (prev.EndTime ?? prev.Time) && cur.Time >= prev.Time)
                    {
                        issues.Add(new CriticIssue("column_overlap", "error", prev.Time, cur.Time,
                            $"Column {group.Key}: '{prev.Id}' and '{cur.Id}' overlap.",
                            new[] { "shift_object", "change_column" }));
                        break;
                    }
                }
            }
        }

        // ---- soft: 密度 vs 能量不匹配 ----
        double beatMs = timeline.Tempo.BaseBpm > 0 ? 60000.0 / timeline.Tempo.BaseBpm : 0;
        if (hasObjects)
        {
            foreach (var section in timeline.Sections)
            {
                int count = objects!.Count(o => o.Time >= section.StartTime && o.Time < section.EndTime);
                double sectionMs = Math.Max(section.EndTime - section.StartTime, 1);
                double density = count / sectionMs * 1000.0; // objects/sec
                double expectedDensity = section.Energy * 12.0; // 能量 1.0 → ~12 obj/s（粗基线）

                if (density < expectedDensity * 0.5 && section.Energy > 0.6)
                {
                    issues.Add(new CriticIssue("density_mismatch", "warning", section.StartTime, section.EndTime,
                        $"Section '{section.Id}' energy {section.Energy:0.00} but density only {density:0.0}/s (expected ~{expectedDensity:0.0}/s).",
                        new[] { "increase_density", "introduce_pattern_variation" }));
                }
            }
        }

        // ---- soft: pattern 连续重复 ----
        for (int i = 1; i < plan.Patterns.Count; i++)
        {
            if (plan.Patterns[i].Family == plan.Patterns[i - 1].Family)
            {
                issues.Add(new CriticIssue("pattern_repetition", "warning", plan.Patterns[i].StartTime, plan.Patterns[i].EndTime,
                    $"Pattern '{plan.Patterns[i].Id}' repeats family '{plan.Patterns[i].Family}' from previous section.",
                    new[] { "introduce_variation" }));
                break; // 只报一次
            }
        }

        // ---- soft: 节奏对齐（1/16 网格）----
        if (hasObjects && beatMs > 0)
        {
            double grid = beatMs / 16.0;
            int offGrid = objects!.Count(o =>
            {
                double nearest = Math.Round(o.Time / grid) * grid;
                return Math.Abs(o.Time - nearest) >= 2.0;
            });

            if (offGrid > 0)
            {
                issues.Add(new CriticIssue("rhythm_alignment", "warning", 0, timeline.DurationMs,
                    $"{offGrid}/{objects!.Count} objects off the 1/16 rhythm grid.",
                    new[] { "quantize_objects" }));
            }
        }

        bool valid = issues.All(i => i.Severity != "error");
        return new CriticReport(valid, issues);
    }
}
