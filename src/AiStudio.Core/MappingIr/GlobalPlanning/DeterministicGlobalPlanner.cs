using AiStudio.Core.MappingIr.Evidence;
using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.GlobalPlanning;

/// <summary>
/// 全局规划器契约（spec §23 IGlobalMappingPlanner）。
/// 在本地 pattern 生成之前运行，产出全曲映射弧线。
/// </summary>
public interface IGlobalMappingPlanner
{
    GlobalMappingPlan Plan(MusicTimeline music, IReadOnlyList<MappingEvidence> evidence, DifficultyProfile difficultyProfile, RulesetInfo ruleset);
}

/// <summary>
/// 确定性全局规划器（spec §9.6 baseline）：
/// 由段落能量/类型 + 难度档案派生难度曲线、段落角色、全局高潮（future-aware：final climax 保留余量）。
/// </summary>
public sealed class DeterministicGlobalPlanner : IGlobalMappingPlanner
{
    public GlobalMappingPlan Plan(MusicTimeline music, IReadOnlyList<MappingEvidence> evidence, DifficultyProfile difficultyProfile, RulesetInfo ruleset)
    {
        ArgumentNullException.ThrowIfNull(music);
        ArgumentNullException.ThrowIfNull(evidence);

        var sections = music.Sections;
        if (sections.Count == 0)
            return new GlobalMappingPlan(Array.Empty<DifficultyCurvePoint>(), Array.Empty<SectionPlan>(), new GlobalClimaxInfo(0, 0, false), Array.Empty<ContrastPoint>());

        double targetSr = difficultyProfile.TargetStarRating ?? 5.0;
        double maxEnergy = sections.Max(s => s.Energy);
        var curve = new List<DifficultyCurvePoint>(sections.Count);
        var sectionPlans = new List<SectionPlan>(sections.Count);
        var contrastPoints = new List<ContrastPoint>();

        // 全局高潮：最高能量段；若其后还有高能量段（能量 ≥ 峰值的 80%），则当前不是 final climax
        int peakIndex = -1;
        double peakEnergy = -1;
        for (int i = 0; i < sections.Count; i++)
        {
            if (sections[i].Energy > peakEnergy)
            {
                peakEnergy = sections[i].Energy;
                peakIndex = i;
            }
        }

        bool hasLaterClimax = peakIndex >= 0 && sections.Skip(peakIndex + 1).Any(s => s.Energy >= peakEnergy * 0.8);
        bool isFinalClimax = !hasLaterClimax;

        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            double normalized = maxEnergy > 0 ? section.Energy / maxEnergy : 0;

            // 难度曲线：能量归一化映射到 [0.4, 1.0] × targetSr；final climax 前的段保留余量（×0.85）
            double budget = 0.4 + normalized * 0.6;
            if (!isFinalClimax && i == peakIndex)
                budget *= 0.85; // future-aware：非最终高潮不强满
            double target = targetSr * budget;

            curve.Add(new DifficultyCurvePoint(section.StartTime, Math.Round(target, 2)));

            string role = roleFor(section.Type);
            sectionPlans.Add(new SectionPlan(
                section.Id,
                section.StartTime,
                section.EndTime,
                role,
                DensityBudget: Math.Round(budget, 2),
                IntensityBudget: Math.Round(normalized, 2)));

            // 对比点：相邻段类型/能量突变
            if (i > 0)
            {
                var prev = sections[i - 1];
                if (Math.Abs(section.Energy - prev.Energy) > 0.3)
                    contrastPoints.Add(new ContrastPoint(section.StartTime, "energy_contrast", $"energy {prev.Energy:0.00} → {section.Energy:0.00}"));
                else if (section.Type != prev.Type)
                    contrastPoints.Add(new ContrastPoint(section.StartTime, "section_change", $"{prev.Type} → {section.Type}"));
            }
        }

        var climax = peakIndex >= 0
            ? new GlobalClimaxInfo(sections[peakIndex].StartTime, peakEnergy, isFinalClimax)
            : new GlobalClimaxInfo(0, 0, false);

        return new GlobalMappingPlan(curve, sectionPlans, climax, contrastPoints);
    }

    private static string roleFor(SectionType type)
        => type switch
        {
            SectionType.Intro => "establish",
            SectionType.Verse => "variation",
            SectionType.PreChorus => "escalation",
            SectionType.Chorus or SectionType.Drop => "climax",
            SectionType.Bridge or SectionType.Break => "release",
            SectionType.Outro => "resolution",
            _ => "continuity",
        };
}
