using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty;

namespace osu.Game.Rulesets.AiStudio.Osu.Checks;

/// <summary>
/// 星数计算统一入口：<c>new OsuDifficultyCalculator(rulesetInfo, workingBeatmap).Calculate().StarRating</c>。
/// 任何失败（异常、规则集缺失、非有限值）返回 null，调用方据此跳过该难度而不是抛异常。
/// ruleset 实例来自 <see cref="BeatmapInfo.Ruleset"/>.CreateInstance()（与官方 AiStudioRuleset
/// CreateDifficultyCalculator 的委托目标一致）。
/// </summary>
internal static class OsuStarRating
{
    /// <summary>
    /// 尝试计算谱面星数；失败返回 null。
    /// </summary>
    public static double? TryCalculate(IWorkingBeatmap working, BeatmapInfo beatmapInfo)
    {
        try
        {
            var ruleset = beatmapInfo.Ruleset.CreateInstance();
            if (ruleset == null)
                return null;

            double stars = new OsuDifficultyCalculator(ruleset.RulesetInfo, working).Calculate().StarRating;
            return double.IsFinite(stars) ? stars : null;
        }
        catch
        {
            // 计算失败不阻塞检查流程：调用方跳过该难度即可（M1 规格：宁可少报，不要误报）。
            return null;
        }
    }
}
