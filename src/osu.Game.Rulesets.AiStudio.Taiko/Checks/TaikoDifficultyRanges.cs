using AiStudio.Core.Models;

namespace osu.Game.Rulesets.AiStudio.Taiko.Checks;

/// <summary>
/// Taiko difficulty setting ranges per Ranking Criteria (osu!taiko).
///
/// Header cites https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!taiko
///
/// RC taiko tiers (2026-08): Kantan OD 3 HP 8+ / Futsuu OD 4 HP 7+ / Muzukashii OD 5 HP 6+ / Oni OD 5+ HP 5+ / Inner Oni OD 6+ HP 5+.
/// These five map to the internal DifficultyLevel scale as Easy..ExpertPlus (worst-case strict mapping for validation):
///  - Easy aligns to Kantan: OD [0,3], HP [8,10]
///  - Normal aligns to Futsuu: OD [0,4], HP [7,10]
///  - Hard aligns to Muzukashii: OD [0,5], HP [6,10]
///  - Insane aligns to Oni: OD [5,10], HP [5,10]
///  - Expert/ExpertPlus align to Inner Oni: OD [6,10], HP [5,10]
/// Note: taiko has no CS and no AR — only OD/HP. Missing fields are treated as unconstrained (0-10 passthrough).
/// </summary>
public static class TaikoDifficultyRanges
{
    public static readonly IReadOnlyList<DifficultySettingsRange> All = new[]
    {
        new DifficultySettingsRange(DifficultyLevel.Easy,
            new FloatRange(0, 10), new FloatRange(0, 3), new FloatRange(8, 10), new FloatRange(0, 10)),
        new DifficultySettingsRange(DifficultyLevel.Normal,
            new FloatRange(0, 10), new FloatRange(0, 4), new FloatRange(7, 10), new FloatRange(0, 10)),
        new DifficultySettingsRange(DifficultyLevel.Hard,
            new FloatRange(0, 10), new FloatRange(0, 5), new FloatRange(6, 10), new FloatRange(0, 10)),
        new DifficultySettingsRange(DifficultyLevel.Insane,
            new FloatRange(0, 10), new FloatRange(5, 10), new FloatRange(5, 10), new FloatRange(0, 10)),
        new DifficultySettingsRange(DifficultyLevel.Expert,
            new FloatRange(0, 10), new FloatRange(6, 10), new FloatRange(5, 10), new FloatRange(0, 10)),
        new DifficultySettingsRange(DifficultyLevel.ExpertPlus,
            new FloatRange(0, 10), new FloatRange(6, 10), new FloatRange(5, 10), new FloatRange(0, 10)),
    };

    public static bool TryGet(DifficultyLevel level, out DifficultySettingsRange range)
    {
        foreach (var candidate in All)
        {
            if (candidate.Level == level)
            {
                range = candidate;
                return true;
            }
        }

        range = default!;
        return false;
    }

    public static DifficultySettingsRange Get(DifficultyLevel level)
    {
        if (TryGet(level, out var range))
            return range;

        throw new InvalidOperationException($"Difficulty level {level} has no taiko ranking criteria range defined.");
    }
}
