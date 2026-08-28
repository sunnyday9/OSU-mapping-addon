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
/// <see cref="DensityScale"/> 是 SR 校准旋钮（MVP-B）：缩放 subdivision 档位选择，
/// 使对象密度（进而官方 SR）随 scale 单调变化（默认 1.0 保持既有行为）。
/// </summary>
public sealed class DeterministicCandidateGenerator : IPatternCandidateGenerator
{
    /// <summary>密度缩放系数（相对意图默认档全量的密度倍数，≈0.2–2.0）：1.0 = 既有行为。校准循环用它逼近目标 SR。</summary>
    public double DensityScale { get; init; } = 1.0;

    public IReadOnlyList<PatternCandidate> Generate(MappingIntent intent, DifficultyProfile difficultyProfile, RulesetKind ruleset, int seed, double bpm = 180.0)
    {
        ArgumentNullException.ThrowIfNull(intent);

        if (ruleset != RulesetKind.Mania)
            return Array.Empty<PatternCandidate>();

        // 密度旋钮（MVP-B SR 校准）：DensityScale = 相对"意图默认档全量"的目标密度倍数。
        // 选满足 档位密度 ≥ 目标密度 的最低 subdivision 档，档内用 density 参数稀疏化——
        // 使对象密度随 scale 全程连续单调（scale 0.5→默认档半量，1.0→默认档全量，
        // 2.0→1/16 档全量，4.0→1/24 档全量）。
        double baseDensity = intent.Emphasis.Density;
        int baseLevel = baseDensity > 0.7 ? 2 : baseDensity > 0.5 ? 1 : 0; // 0=1/4 档, 1=1/8 档, 2=1/16 档
        double[] levelDensity = { 0.25, 0.5, 1.0, 2.0 }; // 各档全量相对密度（1/4、1/8、1/16、1/24）
        double target = Math.Clamp(DensityScale * levelDensity[baseLevel], 0.02, 4.0);

        int level = baseLevel;
        while (level < levelDensity.Length - 1 && levelDensity[level] < target)
            level++;
        while (level > 0 && levelDensity[level - 1] >= target)
            level--;

        string[] subdivisions = level switch
        {
            0 => new[] { "1/4", "1/4", "1/4", "1/4" },
            2 => new[] { "1/16", "1/16", "1/16", "1/16" },
            3 => new[] { "1/24", "1/24", "1/24", "1/24" },
            _ => new[] { "1/8", "1/8", "1/8", "1/8" },
        };
        double densityParam = Math.Clamp(target / levelDensity[level], 0.05, 1.0);
        var candidates = new List<PatternCandidate>();
        // 依据意图 primary 选择候选 family 组合（每个意图 4 个候选，满足 spec §11.1 的 3-5）
        string[] families = intent.Primary switch
        {
            MappingPrimaryIntent.Climax => new[] { "jumpstream", "stream", "single_ln", "jump" },
            MappingPrimaryIntent.Escalation => new[] { "stream", "jump", "jumpstream", "burst" },
            MappingPrimaryIntent.Establish => new[] { "single", "jump", "single_ln", "stream" },
            MappingPrimaryIntent.DeEscalation or MappingPrimaryIntent.Resolution => new[] { "single", "single_ln", "jump", "stream" },
            MappingPrimaryIntent.Repeat or MappingPrimaryIntent.Variation => new[] { "stream", "jump", "burst", "single_ln" },
            _ => new[] { "single", "jump", "stream", "burst" },
        };

        for (int i = 0; i < families.Length && candidates.Count < 4; i++)
        {
            string family = families[i];
            if (i >= subdivisions.Length)
                continue;

            var parameters = new Dictionary<string, object?>
            {
                ["subdivision"] = subdivisions[i],
                // density = densityParam：provider 用它稀疏化节奏点（1.0=全量，<1 稀疏）。
                // 这是 SR 校准的连续旋钮（MVP-B），与 subdivision 档位正交。
                ["density"] = densityParam,
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
                new[] { $"intent.{snake(intent.Primary.ToString())}", $"density.{densityParam:0.00}" }));
        }

        return candidates;
    }

    private static string snake(string pascal) => string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
