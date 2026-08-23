using AiStudio.Core.Analysis;
using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Timeline;

/// <summary>
/// 由共享分析层产物（<see cref="BeatGrid"/> + <see cref="AudioSection"/>）构建 IR 时间线。
/// 确定性的纯映射：不引入随机。
/// </summary>
public sealed class MusicTimelineBuilder
{
    private const double ms_per_minute = 60000.0;

    public MusicTimeline Build(BeatGrid grid, IReadOnlyList<AudioSection> sections, string? audioLabel = null)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(sections);

        if (grid.Bpm <= 0 || grid.BeatTimes.Count == 0)
            return MusicTimeline.Empty;

        double beatLengthMs = ms_per_minute / grid.Bpm;
        int durationMs = Math.Max(0, (int)Math.Round(grid.BeatTimes[^1] + beatLengthMs));

        var normalizedSections = normalizeSections(sections, grid, durationMs);
        var irSections = normalizedSections.Select(s => toIrSection(s, grid)).ToList();
        var phrases = buildPhrases(irSections);
        var events = buildEvents(grid, irSections, beatLengthMs);

        return new MusicTimeline(
            durationMs,
            new TempoInfo(grid.Bpm, Array.Empty<TempoChange>()),
            irSections,
            phrases,
            events);
    }

    private static List<AudioSection> normalizeSections(IReadOnlyList<AudioSection> sections, BeatGrid grid, int durationMs)
    {
        var result = sections
            .Select(s => s with
            {
                // 与 beat 网格对齐到最近 beat，保证 section 边界落在量化网格上。
                StartTime = snapToBeat(s.StartTime, grid),
                EndTime = snapToBeat(s.EndTime, grid),
            })
            .ToList();

        // 分析层可能返回空/缺段（如 v1 全曲单段）——至少保证一段覆盖整个时间线。
        if (result.Count == 0)
            result.Add(new AudioSection(0, durationMs, 0.5));

        // 排序 + 相邻段首尾衔接（取首段起点、末段终点铺满全曲）。
        result = result.OrderBy(s => s.StartTime).ToList();
        result[0] = result[0] with { StartTime = 0 };
        result[^1] = result[^1] with { EndTime = durationMs };
        return result;
    }

    private static double snapToBeat(double time, BeatGrid grid)
    {
        if (grid.BeatTimes.Count == 0)
            return time;

        double best = grid.BeatTimes[0];
        double bestDist = double.MaxValue;
        foreach (double t in grid.BeatTimes)
        {
            double d = Math.Abs(t - time);
            if (d < bestDist)
            {
                bestDist = d;
                best = t;
            }
        }

        return best;
    }

    private static MusicSection toIrSection(AudioSection s, BeatGrid grid)
        => new(
            $"section_{s.StartTime:0}",
            (int)Math.Round(s.StartTime),
            (int)Math.Round(s.EndTime),
            mapSectionType(s.SectionType),
            clamp01(s.Intensity),
            0.9,
            s.Label is null ? null : new[] { s.Label });

    private static SectionType mapSectionType(AudioSectionType type)
        => type switch
        {
            AudioSectionType.Intro => SectionType.Intro,
            AudioSectionType.Verse => SectionType.Verse,
            AudioSectionType.Chorus => SectionType.Chorus,
            AudioSectionType.Bridge => SectionType.Bridge,
            AudioSectionType.Outro => SectionType.Outro,
            _ => SectionType.Unknown,
        };

    private static List<MusicPhrase> buildPhrases(IReadOnlyList<MusicSection> sections)
    {
        var phrases = new List<MusicPhrase>();
        foreach (var section in sections)
        {
            // MVP：每段一个 phrase，role 由能量决定（高能量 → lead，低能量 → support）。
            var role = section.Energy >= 0.6 ? MusicalRole.Lead : MusicalRole.Support;
            phrases.Add(new MusicPhrase(
                $"{section.Id}_phrase",
                section.Id,
                section.StartTime,
                section.EndTime,
                section.Type == SectionType.Chorus ? PhraseType.VocalPhrase : PhraseType.RhythmPhrase,
                role));
        }

        return phrases;
    }

    private static List<MusicEvent> buildEvents(BeatGrid grid, IReadOnlyList<MusicSection> sections, double beatLengthMs)
    {
        var events = new List<MusicEvent>(grid.BeatTimes.Count);
        for (int i = 0; i < grid.BeatTimes.Count; i++)
        {
            double t = grid.BeatTimes[i];
            var section = findSection(sections, t);

            // 小节起点（第 1 拍）作为 downbeat 强调事件；其余为普通 beat。
            bool downbeat = i == 0 || (beatLengthMs > 0 && Math.Abs((t - grid.BeatTimes[0]) / beatLengthMs % 4.0) < 0.01);
            var type = downbeat ? MusicEventType.Onset : MusicEventType.Beat;

            events.Add(new MusicEvent(
                $"ev_{t:0}",
                (int)Math.Round(t),
                0,
                type,
                clamp01(section?.Energy ?? 0.5),
                downbeat ? 0.95 : 0.9,
                "beat_grid",
                section is null ? null : $"{section.Id}_phrase",
                new Dictionary<string, object?> { ["beat_index"] = i }));
        }

        return events;
    }

    private static MusicSection? findSection(IReadOnlyList<MusicSection> sections, double time)
        => sections.FirstOrDefault(s => time >= s.StartTime && time < s.EndTime);

    private static double clamp01(double v) => Math.Clamp(v, 0.0, 1.0);
}
