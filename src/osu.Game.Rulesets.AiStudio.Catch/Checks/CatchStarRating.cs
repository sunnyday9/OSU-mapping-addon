using osu.Game.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty;

namespace osu.Game.Rulesets.AiStudio.Catch.Checks;

/// <summary>
/// Star rating helper for catch (CatchDifficultyCalculator).
/// Mirrors <c>OsuStarRating</c> pattern. Returns null on failure.
/// </summary>
public static class CatchStarRating
{
    public static double? TryCalculate(IWorkingBeatmap working, BeatmapInfo beatmapInfo)
    {
        try
        {
            var ruleset = beatmapInfo.Ruleset.CreateInstance();
            if (ruleset == null)
                return null;

            double stars = new CatchDifficultyCalculator(ruleset.RulesetInfo, working).Calculate().StarRating;
            return double.IsFinite(stars) ? stars : null;
        }
        catch
        {
            return null;
        }
    }
}
