using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Candidates;

/// <summary>
/// 候选 pattern（spec §11.3）：PatternIntent + 评分元数据。
/// </summary>
public sealed record PatternCandidate(
    string CandidateId,
    PatternIntent Intent,
    double PredictedFit,
    double ExpectedDifficultyCost,
    double MusicAlignmentPrior,
    double ContinuityPrior,
    IReadOnlyList<string> ReasonCodes);

/// <summary>
/// 候选生成器契约（spec §23 IPatternCandidateGenerator）：
/// 对每个意图生成少量（3–5 个）合法候选，而非立即选定一个。
/// </summary>
public interface IPatternCandidateGenerator
{
    IReadOnlyList<PatternCandidate> Generate(MappingIntent intent, DifficultyProfile difficultyProfile, RulesetKind ruleset, int seed, double bpm = 180.0);
}

/// <summary>
/// 确定性候选生成器（spec §11）：按意图 primary + 难度密度选择 family 参数组合，
/// 产出 3–5 个候选（含不同 subdivision / family / LN 变体），reason codes 标注选择理由。
/// </summary>
public sealed class DeterministicCandidateGenerator : IPatternCandidateGenerator
{
    public IReadOnlyList<PatternCandidate> Generate(MappingIntent intent, DifficultyProfile difficultyProfile, RulesetKind ruleset, int seed, double bpm = 180.0)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (ruleset != RulesetKind.Mania)
            return Array.Empty<PatternCandidate>();

        double density = intent.Emphasis.Density;
        var candidates = new List<PatternCandidate>();

        // 依据意图 primary 选择候选 family 组合
        string[] families = intent.Primary switch
        {
            MappingPrimaryIntent.Climax => new[] { "jumpstream", "stream", "single_ln", "jump" },
            MappingPrimaryIntent.Escalation => new[] { "stream", "jump", "jumpstream" },
            MappingPrimaryIntent.Establish => new[] { "single", "jump", "single_ln" },
            MappingPrimaryIntent.DeEscalation or MappingPrimaryIntent.Resolution => new[] { "single", "single_ln", "jump" },
            MappingPrimaryIntent.Repeat or MappingPrimaryIntent.Variation => new[] { "stream", "jump", "burst" },
            _ => new[] { "single", "jump", "stream" },
        };

        string[] subdivisions = density > 0.7
            ? new[] { "1/16", "1/8", "1/16" }
            : density > 0.5
                ? new[] { "1/8", "1/16", "1/8" }
                : new[] { "1/4", "1/8", "1/4" };

        for (int i = 0; i < families.Length && candidates.Count < 4; i++)
        {
            string family = families[i];
            if (i >= subdivisions.Length)
                continue;

            var parameters = new Dictionary<string, object?>
            {
                ["subdivision"] = subdivisions[i],
                ["density"] = Math.Round(density, 2),
                ["column_strategy"] = i % 2 == 0 ? "alternating" : "mirror",
                ["column_order"] = i % 2 == 0 ? new object[] { 0, 2, 1, 3 } : new object[] { 0, 3, 1, 2 },
                ["jack_tolerance"] = 0.05,
                ["bpm"] = bpm,
            };

            var constraints = new Dictionary<string, object?>
            {
                ["max_consecutive_same_column"] = 1,
                ["allow_chords"] = family is "jump" or "jumpstream",
                ["allow_ln"] = family.StartsWith("ln", StringComparison.Ordinal),
                ["max_chord_size"] = 2,
            };

            var patternIntent = new PatternIntent(
                $"candidate_{intent.Id}_{i}",
                ruleset,
                family,
                intent.StartTime,
                intent.EndTime,
                parameters,
                constraints,
                0.8 - i * 0.05,
                Rationale: $"Candidate {i + 1}: {family} at {subdivisions[i]} for {snake(intent.Primary.ToString())}.");

            candidates.Add(new PatternCandidate(
                patternIntent.Id,
                patternIntent,
                PredictedFit: Math.Round(0.8 - i * 0.1, 2),
                ExpectedDifficultyCost: Math.Round(0.3 + i * 0.15, 2),
                MusicAlignmentPrior: Math.Round(0.7 + (families.Length - i) * 0.05, 2),
                ContinuityPrior: 0.6,
                new[] { $"intent.{snake(intent.Primary.ToString())}", $"density.{density:0.00}" }));
        }

        return candidates;
    }

    private static string snake(string pascal) => string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
