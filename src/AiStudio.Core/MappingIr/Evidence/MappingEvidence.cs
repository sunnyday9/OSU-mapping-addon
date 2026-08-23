namespace AiStudio.Core.MappingIr.Evidence;

/// <summary>
/// 音乐证据：连接音乐分析与映射决策的桥梁（mapping-intelligence-v0.1-spec §7）。
/// 证据回答"音乐的什么属性支持某个映射决策"——它约束/告知 planner，但不直接指定 pattern。
/// </summary>
public sealed record MappingEvidence(
    string Id,
    int StartTime,
    int EndTime,
    double Rhythm,
    double Accent,
    double Energy,
    double Vocal,
    double Movement,
    double Density,
    double Repetition,
    double Climax,
    double Novelty,
    double BeatConfidence,
    double Confidence,
    IReadOnlyList<string> Sources);
