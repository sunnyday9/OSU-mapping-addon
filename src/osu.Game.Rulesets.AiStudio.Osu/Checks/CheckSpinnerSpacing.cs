using AiStudio.Core.Models;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.AiStudio.Osu.Checks;

/// <summary>
/// 检查每个 spinner 与相邻物件的间隔。
///
/// RC 条款：osu! RC 各难度 Guidelines（https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu ，2026-08 核对）：
/// Easy "There should be at least 4 beats between a spinner's end and the next object."；
/// Normal 至少 2 拍；Hard 至少 1 拍。Insane/Expert 无此 guideline，按 M1 规格沿用 Hard 的 1 拍。
/// 1 拍 = spinner 所在时间点最近 TimingControlPoint 的 BeatLength（毫秒）。
///
/// 注意：RC 原文只约束 spinner 之后的间隔（"between a spinner's end and the next object"），
/// 按 M1 规格本检查对前后相邻物件都检查（前一个物件结束 → spinner 开始 / spinner 结束 → 下一个物件开始），
/// 间隔不足报 Warning，消息含需要的/实际的拍数。
/// 等级由该难度星数经 <see cref="DifficultyRatingHelper"/> 映射；星数计算失败时跳过。
/// </summary>
public class CheckSpinnerSpacing : ICheck
{
    private readonly IssueTemplateSpinnerTooClose templateSpinnerTooClose;

    public CheckSpinnerSpacing()
    {
        templateSpinnerTooClose = new IssueTemplateSpinnerTooClose(this);
    }

    public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Compose, "Spinner too close to nearby objects");

    public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
    {
        templateSpinnerTooClose,
    };

    public IEnumerable<Issue> Run(BeatmapVerifierContext context)
    {
        var playable = context.CurrentDifficulty.Playable;

        double? stars = OsuStarRating.TryCalculate(context.CurrentDifficulty.Working, playable.BeatmapInfo);
        if (stars == null)
            yield break;

        var level = DifficultyRatingHelper.GetLevel(stars.Value);

        // Easy 4 拍 / Normal 2 拍 / Hard 及以上 1 拍。
        int requiredBeats = level switch
        {
            DifficultyLevel.Easy => 4,
            DifficultyLevel.Normal => 2,
            _ => 1,
        };

        var hitObjects = playable.HitObjects.OrderBy(h => h.StartTime).ToList();

        for (int i = 0; i < hitObjects.Count; i++)
        {
            if (hitObjects[i] is not Spinner spinner)
                continue;

            double beatLength = playable.ControlPointInfo.TimingPointAt(spinner.StartTime).BeatLength;
            if (beatLength <= 0)
                continue;

            double requiredGap = requiredBeats * beatLength;

            // 与前一个物件的间隔（前一个物件结束 → spinner 开始）。
            if (i > 0)
            {
                double gapBefore = spinner.StartTime - hitObjects[i - 1].GetEndTime();

                if (gapBefore < requiredGap)
                    yield return new Issue(spinner, templateSpinnerTooClose, gapBefore, gapBefore / beatLength, "before", requiredGap, requiredBeats, level);
            }

            // 与后一个物件的间隔（spinner 结束 → 下一个物件开始）。
            if (i < hitObjects.Count - 1)
            {
                double gapAfter = hitObjects[i + 1].StartTime - spinner.EndTime;

                if (gapAfter < requiredGap)
                    yield return new Issue(spinner, templateSpinnerTooClose, gapAfter, gapAfter / beatLength, "after", requiredGap, requiredBeats, level);
            }
        }
    }

    public class IssueTemplateSpinnerTooClose : IssueTemplate
    {
        public IssueTemplateSpinnerTooClose(ICheck check)
            : base(check, IssueType.Warning,
                "Spinner has only {0:0.##} ms ({1:0.##} beats) {2} it; at least {3:0.##} ms ({4} beats) are required for a {5} difficulty.")
        {
        }
    }
}
