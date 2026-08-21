namespace AiStudio.Core.Analysis;

/// <summary>
/// 音频节拍网格分析结果（模式无关，由共享分析层产出）。
/// </summary>
public sealed record BeatGrid(double Bpm, double Offset, IReadOnlyList<double> BeatTimes);

/// <summary>
/// 段落类型（M3 细分：能量包络 + 重复度估计；v1 仅单段 Verse）.
/// </summary>
public enum AudioSectionType
{
    Intro,
    Verse,
    Chorus,
    Bridge,
    Outro,
}

/// <summary>
/// 音频段落（M3 起 2–5 段；v1 为全曲单段）。
/// Intensity 为 onset 密度归一化 [0,1]；KiaiCandidate 由强度与段长判定（Intensity&gt;0.7 且段长≥8s）。
/// </summary>
public sealed record AudioSection(
    double StartTime,
    double EndTime,
    double Intensity,
    AudioSectionType SectionType = AudioSectionType.Verse,
    bool KiaiCandidate = false,
    string? Label = null);

/// <summary>
/// 共享分析层接口（PLAN.md §5.1）。
/// M2 用 BASS + 谱通量法实现；M3 在此基础上扩展段落细分（能量包络滑动窗口+z-score）。
/// </summary>
public interface IAudioAnalyzer
{
    Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default);
}
