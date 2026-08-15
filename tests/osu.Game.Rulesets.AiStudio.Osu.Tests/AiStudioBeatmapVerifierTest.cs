using System.IO;
using AiStudio.Core.Models;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.AiStudio.Osu.Checks;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

[TestFixture]
public class AiStudioBeatmapVerifierTest
{
    [Test]
    public void DifficultySettingsOutOfRangeReportsProblem()
    {
        // CS 8 超过所有难度等级的 CS 上限（7），无论星数映射到哪个等级都必然越界。
        var beatmap = createBeatmap(ar: 10, od: 10, hp: 10, cs: 8);
        addSparseCircles(beatmap);

        var issues = new CheckDifficultySettingsRanges().Run(createContext(beatmap)).ToList();

        Assert.That(issues, Is.Not.Empty);
        Assert.That(issues.All(i => i.Template is CheckDifficultySettingsRanges.IssueTemplateSettingsOutOfRange), Is.True);
        Assert.That(issues.All(i => i.Template.Type == IssueType.Problem), Is.True);
        Assert.That(issues[0].ToString(), Does.Contain("AR"));
    }

    [Test]
    public void CompliantDifficultySettingsProduceNoIssues()
    {
        // AR5/OD3/HP3/CS4 同时落在 Easy 与 Normal 区间内（稀疏谱面星数低，映射为 Easy/Normal）。
        var beatmap = createBeatmap(ar: 5, od: 3, hp: 3, cs: 4);
        addSparseCircles(beatmap);

        Assert.That(new CheckDifficultySettingsRanges().Run(createContext(beatmap)), Is.Empty);
    }

    [Test]
    public void LargeStarRatingGapReportsWarning()
    {
        var easy = createBeatmap(ar: 1, od: 1, hp: 1, cs: 2);
        easy.BeatmapInfo.DifficultyName = "Easy";
        addSparseCircles(easy, count: 3, startTime: 1000, gap: 1000);

        var hard = createBeatmap(ar: 10, od: 10, hp: 10, cs: 7);
        hard.BeatmapInfo.DifficultyName = "Hard";
        addDenseJumpyCircles(hard, count: 40, startTime: 1000, gap: 100);

        // 前置条件：两个难度确实拉开超过 2.0★ 的星距。
        Assert.That(calculateStars(hard) - calculateStars(easy), Is.GreaterThan(2.0), "test beatmaps must have a star gap > 2.0");

        var issues = new CheckSpreadStarRatingGaps().Run(createMultiDifficultyContext(hard, easy)).ToList();

        Assert.That(issues, Is.Not.Empty);
        Assert.That(issues.All(i => i.Template is CheckSpreadStarRatingGaps.IssueTemplateStarGap), Is.True);
    }

    [Test]
    public void CompliantSpreadProducesNoIssues()
    {
        // 两个 Easy/Normal 级稀疏难度，drain 时间短 → 无星距问题、最低难度合规 → 无缺档问题。
        var easy1 = createBeatmap(ar: 1, od: 1, hp: 1, cs: 2);
        easy1.BeatmapInfo.DifficultyName = "Easy";
        addSparseCircles(easy1, count: 3, startTime: 1000, gap: 1000);

        var easy2 = createBeatmap(ar: 1, od: 1, hp: 1, cs: 2);
        easy2.BeatmapInfo.DifficultyName = "Normal";
        addSparseCircles(easy2, count: 4, startTime: 1000, gap: 1000);

        Assert.That(calculateStars(easy1), Is.LessThan(2.0), "test beatmaps must rate below Normal");

        Assert.That(new CheckSpreadStarRatingGaps().Run(createMultiDifficultyContext(easy1, easy2)), Is.Empty);
    }

    [Test]
    public void SingleDifficultyDoesNotReportMissingDifficulty()
    {
        var hard = createBeatmap(ar: 10, od: 10, hp: 10, cs: 7);
        hard.BeatmapInfo.DifficultyName = "Hard";
        addDenseJumpyCircles(hard, count: 40, startTime: 1000, gap: 100);

        // 前置条件：该难度确实在 Hard 及以上；即便如此，单难度集合也不报缺档。
        Assert.That(calculateStars(hard), Is.GreaterThanOrEqualTo(DifficultyRatingHelper.HARD_MIN_STARS));

        Assert.That(new CheckSpreadStarRatingGaps().Run(createContext(hard)), Is.Empty);
    }

    [Test]
    public void MissingLowestDifficultyReportsWarning()
    {
        // 两个都在 Hard 及以上的难度，drain 时间 < 3:30 → 最低难度不得高于 Normal → 缺档 Warning。
        var lower = createBeatmap(ar: 10, od: 10, hp: 10, cs: 7);
        lower.BeatmapInfo.DifficultyName = "Hard";
        addDenseJumpyCircles(lower, count: 40, startTime: 1000, gap: 100);

        var upper = createBeatmap(ar: 10, od: 10, hp: 10, cs: 7);
        upper.BeatmapInfo.DifficultyName = "Insane";
        addDenseJumpyCircles(upper, count: 40, startTime: 1000, gap: 100);

        // 前置条件：两个难度都在 Hard（2.7★）以上。
        Assert.That(Math.Min(calculateStars(lower), calculateStars(upper)), Is.GreaterThanOrEqualTo(DifficultyRatingHelper.HARD_MIN_STARS));

        var issues = new CheckSpreadStarRatingGaps().Run(createMultiDifficultyContext(lower, upper)).ToList();

        Assert.That(issues, Is.Not.Empty);
        Assert.That(issues.All(i => i.Template is CheckSpreadStarRatingGaps.IssueTemplateMissingDifficulty), Is.True);
    }

    [Test]
    public void SpinnerTooCloseToObjectsReportsWarning()
    {
        // 拍长 500ms：spinner 前间隔 250ms、后间隔 125ms，任何等级（Easy 4 拍/2s、Normal 2 拍/1s、Hard+ 1 拍/500ms）都不足。
        var beatmap = createBeatmap(ar: 5, od: 3, hp: 3, cs: 4);
        beatmap.HitObjects.Add(new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 });
        beatmap.HitObjects.Add(new Spinner { StartTime = 1250, Duration = 500 });
        beatmap.HitObjects.Add(new HitCircle { Position = new Vector2(384, 192), StartTime = 1875 });

        var issues = new CheckSpinnerSpacing().Run(createContext(beatmap)).ToList();

        Assert.That(issues, Is.Not.Empty);
        Assert.That(issues.All(i => i.Template is CheckSpinnerSpacing.IssueTemplateSpinnerTooClose), Is.True);
        Assert.That(issues.All(i => i.Template.Type == IssueType.Warning), Is.True);
    }

    [Test]
    public void SpinnerWithSufficientSpacingProducesNoIssues()
    {
        // 拍长 500ms：前后间隔 3500ms / 2000ms，满足 Easy 的 4 拍（2000ms）要求，其他等级阈值更低。
        var beatmap = createBeatmap(ar: 5, od: 3, hp: 3, cs: 4);
        beatmap.HitObjects.Add(new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 });
        beatmap.HitObjects.Add(new Spinner { StartTime = 4500, Duration = 500 });
        beatmap.HitObjects.Add(new HitCircle { Position = new Vector2(384, 192), StartTime = 7000 });

        Assert.That(new CheckSpinnerSpacing().Run(createContext(beatmap)), Is.Empty);
    }

    [Test]
    public void SingleCustomComboColourReportsProblem()
    {
        var beatmap = createBeatmap();
        addSparseCircles(beatmap);

        var working = new SkinTestWorkingBeatmap(beatmap, comboColourCount: 1);
        var issues = new CheckComboColourCount().Run(new BeatmapVerifierContext(beatmap, working)).ToList();

        Assert.That(issues, Is.Not.Empty);
        Assert.That(issues.All(i => i.Template is CheckComboColourCount.IssueTemplateTooFewComboColours), Is.True);
        Assert.That(issues.All(i => i.Template.Type == IssueType.Problem), Is.True);
    }

    [Test]
    public void TwoCustomComboColoursProduceNoIssues()
    {
        var beatmap = createBeatmap();
        addSparseCircles(beatmap);

        var working = new SkinTestWorkingBeatmap(beatmap, comboColourCount: 2);

        Assert.That(new CheckComboColourCount().Run(new BeatmapVerifierContext(beatmap, working)), Is.Empty);
    }

    [Test]
    public void NoCustomComboColoursProduceNoIssues()
    {
        // 无自定义颜色 → 强制默认皮肤，RC 明确豁免（"unless the default skin is forced"）。
        var beatmap = createBeatmap();
        addSparseCircles(beatmap);

        var working = new SkinTestWorkingBeatmap(beatmap, comboColourCount: 0);

        Assert.That(new CheckComboColourCount().Run(new BeatmapVerifierContext(beatmap, working)), Is.Empty);
    }

    private static Beatmap createBeatmap(float ar = 5, float od = 3, float hp = 3, float cs = 4)
    {
        var beatmap = new Beatmap();
        beatmap.BeatmapInfo.Ruleset = new AiStudioRuleset().RulesetInfo;
        beatmap.BeatmapInfo.Difficulty = new BeatmapDifficulty
        {
            ApproachRate = ar,
            OverallDifficulty = od,
            DrainRate = hp,
            CircleSize = cs,
        };
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        return beatmap;
    }

    /// <summary>
    /// 稀疏圈（低星数）：1 拍间隔。
    /// </summary>
    private static void addSparseCircles(Beatmap beatmap, int count = 3, double startTime = 1000, double gap = 500)
    {
        for (int i = 0; i < count; i++)
            beatmap.HitObjects.Add(new HitCircle { Position = new Vector2(256, 192), StartTime = startTime + i * gap });
    }

    /// <summary>
    /// 高频左右跳圈（高星数）：100ms 间隔 + 256px 跳。
    /// </summary>
    private static void addDenseJumpyCircles(Beatmap beatmap, int count = 40, double startTime = 1000, double gap = 100)
    {
        for (int i = 0; i < count; i++)
        {
            beatmap.HitObjects.Add(new HitCircle
            {
                Position = i % 2 == 0 ? new Vector2(128, 192) : new Vector2(384, 192),
                StartTime = startTime + i * gap,
            });
        }
    }

    private static double calculateStars(Beatmap beatmap)
        => new OsuDifficultyCalculator(new AiStudioRuleset().RulesetInfo, new TestWorkingBeatmap(beatmap)).Calculate().StarRating;

    private static BeatmapVerifierContext createContext(Beatmap beatmap)
        => new BeatmapVerifierContext(beatmap, new TestWorkingBeatmap(beatmap));

    private static BeatmapVerifierContext createMultiDifficultyContext(params Beatmap[] beatmaps)
    {
        var verified = beatmaps.Select(b => new BeatmapVerifierContext.VerifiedBeatmap(new TestWorkingBeatmap(b), b)).ToList();
        return new BeatmapVerifierContext(verified[0], verified.Skip(1).ToList(), DifficultyRating.ExpertPlus);
    }

    /// <summary>
    /// 可注入自定义 combo 颜色的 WorkingBeatmap（真实编辑器中 Working.Skin 即谱面 LegacyBeatmapSkin）。
    /// </summary>
    private class SkinTestWorkingBeatmap : WorkingBeatmap
    {
        private readonly IBeatmap beatmap;
        private readonly LegacyBeatmapSkin skin;

        public SkinTestWorkingBeatmap(IBeatmap beatmap, int comboColourCount)
            : base(beatmap.BeatmapInfo, null!)
        {
            this.beatmap = beatmap;
            skin = new LegacyBeatmapSkin(beatmap.BeatmapInfo, null);

            for (int i = 0; i < comboColourCount; i++)
                skin.Configuration.CustomComboColours.Add(new Colour4((byte)(60 + i * 80), 100, 200, 255));
        }

        protected override IBeatmap GetBeatmap() => beatmap;

        public override Texture GetBackground() => null!;

        protected override Track GetBeatmapTrack() => null!;

        protected override ISkin GetSkin() => skin;

        public override Stream GetStream(string storagePath) => null!;
    }
}
