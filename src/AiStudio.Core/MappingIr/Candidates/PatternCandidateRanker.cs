using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Candidates;

/// <summary>带排名的候选（spec §12）。</summary>
public sealed record RankedPatternCandidate(
    PatternCandidate Candidate,
    double Score,
    double MusicAlignment,
    double DifficultyFit,
    double Continuity,
    double Readability,
    double StructuralFit,
    double Validity);

/// <summary>
/// 候选排名器契约（spec §23 IPatternCandidateRanker）。
/// </summary>
public interface IPatternCandidateRanker
{
    IReadOnlyList<RankedPatternCandidate> Rank(IReadOnlyList<PatternCandidate> candidates, MappingIntent intent, DifficultyProfile difficultyProfile, StyleProfile? style = null);
}

/// <summary>
/// 确定性权重排名器（spec §12.1 baseline）：
/// Score = 0.30*MusicAlignment + 0.20*DifficultyFit + 0.20*Continuity + 0.15*Readability + 0.10*StructuralFit + 0.05*Validity。
/// 权重可配置（默认值），硬无效候选（Validity=0）在排名前被拒绝。
/// </summary>
public sealed class DeterministicCandidateRanker : IPatternCandidateRanker
{
    public static readonly IReadOnlyDictionary<string, double> DefaultWeights = new Dictionary<string, double>
    {
        ["music_alignment"] = 0.30,
        ["difficulty_fit"] = 0.20,
        ["continuity"] = 0.20,
        ["readability"] = 0.15,
        ["structural_fit"] = 0.10,
        ["validity"] = 0.05,
    };

    private readonly IReadOnlyDictionary<string, double> weights;

    public DeterministicCandidateRanker(IReadOnlyDictionary<string, double>? weights = null)
        => this.weights = weights ?? DefaultWeights;

    public IReadOnlyList<RankedPatternCandidate> Rank(IReadOnlyList<PatternCandidate> candidates, MappingIntent intent, DifficultyProfile difficultyProfile, StyleProfile? style = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(intent);

        var ranked = new List<RankedPatternCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            double validity = validityScore(candidate);
            if (validity <= 0)
                continue; // 硬无效候选在排名前拒绝（spec §12.7）

            double musicAlignment = musicAlignmentScore(candidate, intent);
            double difficultyFit = difficultyFitScore(candidate, difficultyProfile);
            double continuity = continuityScore(candidate, intent);
            double readability = readabilityScore(candidate);
            double structuralFit = structuralFitScore(candidate, intent);

            double score =
                w("music_alignment") * musicAlignment +
                w("difficulty_fit") * difficultyFit +
                w("continuity") * continuity +
                w("readability") * readability +
                w("structural_fit") * structuralFit +
                w("validity") * validity;

            ranked.Add(new RankedPatternCandidate(candidate, Math.Round(score, 4), musicAlignment, difficultyFit, continuity, readability, structuralFit, validity));
        }

        return ranked.OrderByDescending(r => r.Score).ToList();
    }

    private double w(string key) => weights.TryGetValue(key, out double v) ? v : 0;

    // ---- sub-scores ------------------------------------------------------

    private static double validityScore(PatternCandidate candidate)
    {
        // baseline：family 必须属于 mania 已知集；未知 → 0（拒绝）
        string family = candidate.Intent.Family;
        return family is "single" or "stream" or "burst" or "jack" or "jump" or "jumpstream" or "single_ln" or "ln_rice" or "ln_release" ? 1.0 : 0.0;
    }

    private static double musicAlignmentScore(PatternCandidate candidate, MappingIntent intent)
        => candidate.MusicAlignmentPrior * 0.7 + intent.Emphasis.Rhythm * 0.3;

    private static double difficultyFitScore(PatternCandidate candidate, DifficultyProfile profile)
    {
        // 候选的 density 与目标 density 的接近度
        double targetDensity = profile.Dimensions.Density;
        double candidateDensity = candidate.Intent.Parameters.TryGetValue("density", out var v) && v is not null
            ? Convert.ToDouble(v)
            : 0.5;
        return 1.0 - Math.Min(Math.Abs(candidateDensity - targetDensity), 1.0);
    }

    private static double continuityScore(PatternCandidate candidate, MappingIntent intent)
        => candidate.ContinuityPrior * 0.6 + (intent.Continuity is null ? 0.5 : 0.4);

    private static double readabilityScore(PatternCandidate candidate)
    {
        // 简单 family（single）更可读；chord/复杂 family 略低
        return candidate.Intent.Family switch
        {
            "single" => 0.9,
            "stream" => 0.8,
            "burst" => 0.7,
            "jump" => 0.75,
            "jumpstream" => 0.65,
            "single_ln" => 0.7,
            "ln_rice" => 0.6,
            "ln_release" => 0.6,
            "jack" => 0.5,
            _ => 0.5,
        };
    }

    private static double structuralFitScore(PatternCandidate candidate, MappingIntent intent)
        => intent.Primary switch
        {
            MappingPrimaryIntent.Climax => candidate.Intent.Family is "jumpstream" or "stream" ? 0.9 : 0.5,
            MappingPrimaryIntent.Escalation => candidate.Intent.Family is "stream" or "jumpstream" ? 0.85 : 0.5,
            MappingPrimaryIntent.Establish => candidate.Intent.Family is "single" or "jump" ? 0.85 : 0.5,
            MappingPrimaryIntent.DeEscalation or MappingPrimaryIntent.Resolution => candidate.Intent.Family is "single" or "single_ln" ? 0.85 : 0.5,
            MappingPrimaryIntent.Repeat or MappingPrimaryIntent.Variation => candidate.Intent.Family is "stream" or "jump" ? 0.8 : 0.5,
            _ => 0.6,
        };
}
