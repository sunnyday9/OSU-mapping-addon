using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty;

namespace osu.Game.Rulesets.AiStudio.Mania.Checks;

/// <summary>
/// Mania star rating helper — wrapper around <see cref="ManiaDifficultyCalculator"/> like <c>OsuStarRating</c>.
/// Reference: https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!mania
/// Any failure returns null so callers can skip rather than mis-report.
/// </summary>
internal static class ManiaStarRating
{
    public static double? TryCalculate(IWorkingBeatmap working, BeatmapInfo beatmapInfo)
    {
        try
        {
            var ruleset = beatmapInfo.Ruleset.CreateInstance();
            if (ruleset == null)
                return null;

            double stars = new ManiaDifficultyCalculator(ruleset.RulesetInfo, working).Calculate().StarRating;
            return double.IsFinite(stars) ? stars : null;
        }
        catch
        {
            return null;
        }
    }
}
