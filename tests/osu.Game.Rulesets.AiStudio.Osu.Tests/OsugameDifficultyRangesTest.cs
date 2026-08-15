using AiStudio.Core.Models;
using NUnit.Framework;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

[TestFixture]
public class OsugameDifficultyRangesTest
{
    [Test]
    public void AllDifficultyLevelsArePresentInTable()
    {
        foreach (var level in Enum.GetValues<DifficultyLevel>())
        {
            Assert.That(OsugameDifficultyRanges.TryGet(level, out _), Is.True, $"{level} should be present in the table");
        }
    }

    [Test]
    public void AllRangesHaveValidBounds()
    {
        foreach (var range in OsugameDifficultyRanges.All)
        {
            Assert.That(range.ApproachRate.Min, Is.LessThanOrEqualTo(range.ApproachRate.Max), $"{range.Level} AR bounds");
            Assert.That(range.OverallDifficulty.Min, Is.LessThanOrEqualTo(range.OverallDifficulty.Max), $"{range.Level} OD bounds");
            Assert.That(range.HpDrain.Min, Is.LessThanOrEqualTo(range.HpDrain.Max), $"{range.Level} HP bounds");
            Assert.That(range.CircleSize.Min, Is.LessThanOrEqualTo(range.CircleSize.Max), $"{range.Level} CS bounds");
        }
    }

    [Test]
    public void RangesMatchRankingCriteriaDocument()
    {
        // 数值抽查，与 https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu 各难度 "Difficulty setting guidelines" 原文一致。
        var easy = OsugameDifficultyRanges.Get(DifficultyLevel.Easy);
        Assert.That(easy.ApproachRate.Max, Is.EqualTo(5), "Easy: \"Approach rate should be 5 or less.\"");
        Assert.That(easy.OverallDifficulty.Max, Is.EqualTo(3), "Easy: \"Overall difficulty / HP drain rate should be between 1 and 3.\"");
        Assert.That(easy.CircleSize.Max, Is.EqualTo(4), "Easy: \"Circle size should be 4 or lower.\"");

        var normal = OsugameDifficultyRanges.Get(DifficultyLevel.Normal);
        Assert.That(normal.ApproachRate.Min, Is.EqualTo(4), "Normal: \"Approach rate should be between 4 and 6.\"");
        Assert.That(normal.ApproachRate.Max, Is.EqualTo(6), "Normal: \"Approach rate should be between 4 and 6.\"");

        var insane = OsugameDifficultyRanges.Get(DifficultyLevel.Insane);
        Assert.That(insane.ApproachRate.Max, Is.EqualTo(9.3), "Insane: \"Approach rate should be between 7 and 9.3.\"");
        Assert.That(insane.OverallDifficulty.Max, Is.EqualTo(9), "Insane: \"Overall difficulty should be between 7 and 9.\"");

        var expert = OsugameDifficultyRanges.Get(DifficultyLevel.Expert);
        Assert.That(expert.OverallDifficulty.Min, Is.EqualTo(8), "Expert: \"Overall difficulty should be 8 or higher.\"");
        Assert.That(expert.CircleSize.Max, Is.EqualTo(7), "Expert: \"Circle size should be 7 or lower.\"");
    }

    [Test]
    public void GetLevelMatchesOfficialStarRatingThresholds()
    {
        // 与官方 StarDifficulty.GetDifficultyRating（osu.Game/Beatmaps/StarDifficulty.cs）阈值一致。
        Assert.That(DifficultyRatingHelper.GetLevel(0.5), Is.EqualTo(DifficultyLevel.Easy));
        Assert.That(DifficultyRatingHelper.GetLevel(1.99), Is.EqualTo(DifficultyLevel.Easy));
        Assert.That(DifficultyRatingHelper.GetLevel(2.0), Is.EqualTo(DifficultyLevel.Normal));
        Assert.That(DifficultyRatingHelper.GetLevel(2.69), Is.EqualTo(DifficultyLevel.Normal));
        Assert.That(DifficultyRatingHelper.GetLevel(2.7), Is.EqualTo(DifficultyLevel.Hard));
        Assert.That(DifficultyRatingHelper.GetLevel(4.0), Is.EqualTo(DifficultyLevel.Insane));
        Assert.That(DifficultyRatingHelper.GetLevel(5.3), Is.EqualTo(DifficultyLevel.Expert));
        Assert.That(DifficultyRatingHelper.GetLevel(6.5), Is.EqualTo(DifficultyLevel.ExpertPlus));
        Assert.That(DifficultyRatingHelper.GetLevel(double.NaN), Is.EqualTo(DifficultyLevel.Easy));
    }
}
