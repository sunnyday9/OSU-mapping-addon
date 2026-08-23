using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Evidence;

/// <summary>
/// 证据构建器契约（spec §23 IMappingEvidenceBuilder）。
/// 从 MusicTimeline + 难度档案派生可检查的音乐证据。
/// </summary>
public interface IMappingEvidenceBuilder
{
    IReadOnlyList<MappingEvidence> Build(MusicTimeline music, DifficultyProfile difficultyProfile);
}

/// <summary>
/// 确定性证据构建器（spec §13.2 baseline）：
/// 每个 section 产出一条证据，维度由 section 能量/类型/拍密度派生，全部 [0,1]，
/// sources 标注证据来源（可解释性）。
/// </summary>
public sealed class DeterministicEvidenceBuilder : IMappingEvidenceBuilder
{
    public IReadOnlyList<MappingEvidence> Build(MusicTimeline music, DifficultyProfile difficultyProfile)
    {
        ArgumentNullException.ThrowIfNull(music);

        if (music.Sections.Count == 0)
            return Array.Empty<MappingEvidence>();

        double maxEnergy = music.Sections.Max(s => s.Energy);
        var result = new List<MappingEvidence>(music.Sections.Count);

        for (int i = 0; i < music.Sections.Count; i++)
        {
            var section = music.Sections[i];
            double energy = clamp01(section.Energy);
            double normalizedEnergy = maxEnergy > 0 ? clamp01(section.Energy / maxEnergy) : 0;

            // 拍密度：段内事件数 / 段长（归一化到 16 拍/秒 上限）
            double eventsPerMs = Math.Max(section.EndTime - section.StartTime, 1) > 0
                ? music.Events.Count(e => e.Time >= section.StartTime && e.Time < section.EndTime) / (double)Math.Max(section.EndTime - section.StartTime, 1)
                : 0;
            double density = clamp01(eventsPerMs * 1000.0 / 16.0);

            // climax：该段是否为全局最高能量候选（未来由 Global Planner 精化）
            bool isGlobalPeak = Math.Abs(section.Energy - maxEnergy) < 1e-9;

            var sources = new List<string>
            {
                "audio.energy",
                $"structure.{section.Type.ToString().ToLowerInvariant()}",
                "audio.onset",
            };

            result.Add(new MappingEvidence(
                $"evidence_{section.Id}",
                section.StartTime,
                section.EndTime,
                Rhythm: clamp01(energy * 0.7 + density * 0.3),
                Accent: clamp01(energy),
                Energy: energy,
                Vocal: clamp01(section.Type == Model.SectionType.Chorus ? energy * 0.8 : energy * 0.4),
                Movement: clamp01(difficultyProfile.Dimensions.Movement),
                Density: density,
                Repetition: clamp01(i > 0 && section.Type == music.Sections[i - 1].Type ? 0.6 : 0.3),
                Climax: isGlobalPeak ? clamp01(normalizedEnergy * 1.1) : clamp01(normalizedEnergy * 0.6),
                Novelty: clamp01(i == 0 ? 0.4 : section.Type != music.Sections[i - 1].Type ? 0.7 : 0.3),
                BeatConfidence: 0.9,
                Confidence: clamp01(0.5 + energy * 0.5),
                sources));
        }

        return result;
    }

    private static double clamp01(double v) => Math.Clamp(v, 0.0, 1.0);
}
