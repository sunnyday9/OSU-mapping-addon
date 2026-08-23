using AiStudio.Core.Analysis;
using AiStudio.Core.Models;

namespace AiStudio.Core.Synthesis;

/// <summary>
/// 多难度 spread 规划器（M3）：按 drainTime 与星距约束展开难度集合。
/// 约束对齐 CheckSpreadStarRatingGaps（相邻 ≤2.0★、不跳级、drain 阶梯 3:30/4:15/5:00）。
/// </summary>
public static class SpreadPlanner
{
    private const double max_gap = SpreadConstraint.MaxAdjacentGap;
    private const double drain_3_30 = SpreadConstraint.Drain_3_30_Ms;
    private const double drain_4_15 = SpreadConstraint.Drain_4_15_Ms;
    private const double drain_5_00 = SpreadConstraint.Drain_5_00_Ms;

    private static readonly (DifficultyLevel Level, double TargetSr)[] presets =
    {
        (DifficultyLevel.Easy, 1.5),
        (DifficultyLevel.Normal, 2.3),
        (DifficultyLevel.Hard, 3.0),
        (DifficultyLevel.Insane, 4.2),
        (DifficultyLevel.Expert, 5.3),
    };

    public static IReadOnlyList<DifficultySpec> Plan(BeatGrid grid, IReadOnlyList<AudioSection> sections, GenerationSettings settings)
    {
        if (settings.Difficulties != null && settings.Difficulties.Count > 0)
            return settings.Difficulties;

        double drainMs = 0;
        if (grid.BeatTimes.Count >= 2)
            drainMs = grid.BeatTimes[^1] - grid.BeatTimes[0];

        DifficultyLevel minLevel = drainMs switch
        {
            // drain 越长 → 起跳难度越高（RC drain 阶梯：3:30 可 Normal、4:15 可 Hard、5:00 可 Insane）
            >= drain_5_00 => DifficultyLevel.Insane,
            >= drain_4_15 => DifficultyLevel.Hard,
            >= drain_3_30 => DifficultyLevel.Normal,
            _ => DifficultyLevel.Normal,
        };

        int startIdx = Array.FindIndex(presets, p => p.Level == minLevel);
        if (startIdx < 0) startIdx = 1;

        var chosen = new List<(DifficultyLevel Level, double Sr)>();
        for (int i = startIdx; i < presets.Length; i++)
        {
            double sr = presets[i].TargetSr;
            if (i == startIdx)
            {
                chosen.Add(presets[i]);
                continue;
            }

            double gap = sr - chosen[^1].Sr;
            if (gap <= max_gap)
            {
                chosen.Add(presets[i]);
            }
            else
            {
                double midSr = (chosen[^1].Sr + sr) / 2;
                var midLevel = levelForSr(midSr);
                chosen.Add((midLevel, midSr));
                if (sr - midSr <= max_gap)
                    chosen.Add(presets[i]);
                else
                {
                    double mid2 = (midSr + sr) / 2;
                    chosen.Add((levelForSr(mid2), mid2));
                    chosen.Add(presets[i]);
                }
            }
        }

        if (chosen.Count == 1)
        {
            var only = chosen[0];
            chosen.Clear();
            chosen.Add((DifficultyLevel.Normal, 2.3));
            if (only.Sr - 2.3 > max_gap)
            {
                double mid = (2.3 + only.Sr) / 2;
                chosen.Add((levelForSr(mid), mid));
            }
            chosen.Add(only);
            chosen = chosen.OrderBy(c => c.Sr).ToList();
        }

        return chosen.Select(c => new DifficultySpec
        {
            Level = c.Level,
            TargetStarRating = c.Sr,
            StarRatingTolerance = settings.StarRatingTolerance,
        }).ToList();
    }

    public static IReadOnlyList<GenerationSettings> ExpandSettings(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections)
    {
        var specs = Plan(grid, sections, settings);
        return specs.Select(s => new GenerationSettings
        {
            TargetLevel = s.Level,
            TargetStarRating = s.TargetStarRating,
            StarRatingTolerance = s.StarRatingTolerance,
            AudioPath = settings.AudioPath,
            OutputDirectory = settings.OutputDirectory,
            IncludeBreakSections = settings.IncludeBreakSections,
            Difficulties = new[] { s },
        }).ToList();
    }

    private static DifficultyLevel levelForSr(double sr) => DifficultyRatingHelper.GetLevel(sr);
}
