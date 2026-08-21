using AiStudio.Core.Models;

namespace osu.Game.Rulesets.AiStudio.Catch.Checks;

/// <summary>
/// osu!catch per-difficulty Difficulty settings ranges.
///
/// RC source (2026-08): https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!catch
/// Per-difficulty "Difficulty setting guidelines" sections. Where RC says
/// "X should be N or lower/higher", the implicit bounds are 0–10. Catch uses CS as "Circle size"
/// (affects fruit size/catcher width) and planted in same numeric domain.
/// Ranges mirror the osu! standard table where catch-specific overrides are absent,
/// narrowed where catch RC states tighter recommendation. Cited as header per requirement.
/// </summary>
public static class CatchDifficultyRanges
{
    public static readonly IReadOnlyList<DifficultySettingsRange> All = new[]
    {
        // Cup (Easy) — RC: catch Cup: AR ~5 or less; CS similar to osu! Easy
        new DifficultySettingsRange(DifficultyLevel.Easy,
            new FloatRange(0, 5), new FloatRange(1, 3), new FloatRange(1, 3), new FloatRange(0, 4)),

        // Salad (Normal) — RC: Salad: AR 4–6
        new DifficultySettingsRange(DifficultyLevel.Normal,
            new FloatRange(4, 6), new FloatRange(3, 5), new FloatRange(3, 5), new FloatRange(0, 5)),

        // Platter (Hard) — RC: Platter: AR 6–8
        new DifficultySettingsRange(DifficultyLevel.Hard,
            new FloatRange(6, 8), new FloatRange(5, 7), new FloatRange(4, 6), new FloatRange(0, 6)),

        // Rain (Insane) — RC: Rain: AR 7–9.3
        new DifficultySettingsRange(DifficultyLevel.Insane,
            new FloatRange(7, 9.3), new FloatRange(7, 9), new FloatRange(5, 8), new FloatRange(0, 7)),

        // Overdose (Expert) — RC: Overdose: AR/OD 8+
        new DifficultySettingsRange(DifficultyLevel.Expert,
            new FloatRange(8, 10), new FloatRange(8, 10), new FloatRange(5, 10), new FloatRange(0, 7)),

        new DifficultySettingsRange(DifficultyLevel.ExpertPlus,
            new FloatRange(8, 10), new FloatRange(8, 10), new FloatRange(5, 10), new FloatRange(0, 7)),
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

        throw new InvalidOperationException($"Difficulty level {level} has no ranking criteria range defined.");
    }
}
