using AiStudio.Core.Analysis;

namespace AiStudio.Core.MappingIr.Analysis;

/// <summary>
/// 确定性合成分析器：按给定 BPM/段落生成 beat 网格与段落（无音频文件依赖）。
/// 用途：Core 内测试、golden 测试、无音频的演示闭环；真实音频请注入 ruleset 程序集的 BassAudioAnalyzer。
/// </summary>
public sealed class SyntheticAudioAnalyzer : IAudioAnalyzer
{
    private readonly double bpm;
    private readonly int durationMs;
    private readonly double[] sectionStarts;
    private readonly double[] sectionEnergies;

    public SyntheticAudioAnalyzer(double bpm, int durationMs, double[]? sectionStarts = null, double[]? sectionEnergies = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bpm, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(durationMs, 0);

        this.bpm = bpm;
        this.durationMs = durationMs;
        this.sectionStarts = sectionStarts ?? new[] { 0.0 };
        this.sectionEnergies = sectionEnergies ?? new[] { 0.5 };

        // fail-fast：段起点与能量数组长度不匹配时直接报错，避免静默截断/复用最后一个能量
        if (this.sectionStarts.Length != this.sectionEnergies.Length)
            throw new ArgumentException($"sectionStarts ({this.sectionStarts.Length}) must match sectionEnergies ({this.sectionEnergies.Length}).", nameof(sectionEnergies));
    }

    public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
    {
        double beatMs = 60000.0 / bpm;
        var beats = new List<double>();
        for (double t = 0; t <= durationMs; t += beatMs)
            beats.Add(t);
        return Task.FromResult(new BeatGrid(bpm, 0, beats));
    }

    public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
    {
        var sections = new List<AudioSection>();
        for (int i = 0; i < sectionStarts.Length; i++)
        {
            double start = sectionStarts[i];
            double end = i + 1 < sectionStarts.Length ? sectionStarts[i + 1] : durationMs;
            double energy = sectionEnergies[i];
            sections.Add(new AudioSection(start, end, energy, SectionType: i == 0 ? AudioSectionType.Intro : i == 1 ? AudioSectionType.Chorus : AudioSectionType.Outro));
        }

        return Task.FromResult<IReadOnlyList<AudioSection>>(sections);
    }
}
