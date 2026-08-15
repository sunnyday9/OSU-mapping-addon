using osu.Game.Rulesets.AiStudio.Osu.Checks;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Osu.Edit;

namespace osu.Game.Rulesets.AiStudio.Osu.Edit;

/// <summary>
/// Verify 页校验器（PLAN.md §2.2 注入点 3 / §7）：
/// 聚合官方 osu! 检查 + 自研增量检查，与内置检查并列展示。
/// M1 起追加 <see cref="Checks"/> 目录中的自研规则：
/// <see cref="CheckDifficultySettingsRanges"/>（难度参数区间）、<see cref="CheckSpreadStarRatingGaps"/>
/// （难度梯度/星距/缺档）、<see cref="CheckComboColourCount"/>（combo 颜色数量）、
/// <see cref="CheckSpinnerSpacing"/>（spinner 前后间隔）。
/// 官方 OsuBeatmapVerifier 已覆盖的检查（few hitsounds、offscreen、spinner 长度等）不在此重复实现。
/// </summary>
public class AiStudioBeatmapVerifier : IBeatmapVerifier
{
    private readonly IBeatmapVerifier osuVerifier = new OsuBeatmapVerifier();

    private readonly ICheck[] studioChecks =
    {
        new CheckDifficultySettingsRanges(),
        new CheckSpreadStarRatingGaps(),
        new CheckComboColourCount(),
        new CheckSpinnerSpacing(),
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var issues = osuVerifier.Run(context).ToList();
        issues.AddRange(studioChecks.SelectMany(check => check.Run(context)));
        return issues;
    }
}
