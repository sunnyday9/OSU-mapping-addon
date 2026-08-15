namespace AiStudio.Core.Analysis;

/// <summary>
/// 音频节拍网格分析结果（模式无关，由共享分析层产出）。
/// </summary>
public sealed record BeatGrid(double Bpm, double Offset, IReadOnlyList<double> BeatTimes);

/// <summary>
/// 音频段落（intro/verse/chorus/bridge/outro 由强度曲线切分）。
/// </summary>
public sealed record AudioSection(double StartTime, double EndTime, double Intensity);

/// <summary>
/// 共享分析层接口（PLAN.md §5.1）。
/// M2 用 BASS_FX 实现：BPM/beat/onset 检测 + 能量包络 + 段落切分。
/// </summary>
public interface IAudioAnalyzer
{
    Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default);
}
