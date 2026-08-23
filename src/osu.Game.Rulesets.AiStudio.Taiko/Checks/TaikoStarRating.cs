using osu.Game.Beatmaps;
using osu.Game.Rulesets.Taiko.Difficulty;

namespace osu.Game.Rulesets.AiStudio.Taiko.Checks;

/// <summary>
/// Star rating helper for taiko maps.
/// </summary>
internal static class TaikoStarRating
{
    public static double? TryCalculate(IWorkingBeatmap working, BeatmapInfo beatmapInfo)
    {
        try
        {
            var ruleset = beatmapInfo.Ruleset.CreateInstance();
            if (ruleset == null)
                return null;

            double stars = new TaikoDifficultyCalculator(ruleset.RulesetInfo, working).Calculate().StarRating;
            return double.IsFinite(stars) ? stars : null;
        }
        catch
        {
            return null;
        }
    }
}
