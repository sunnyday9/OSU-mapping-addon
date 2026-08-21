using AiStudio.Core.Models;
using NUnit.Framework;
using osu.Game.Rulesets.AiStudio.Taiko.Checks;

namespace osu.Game.Rulesets.AiStudio.Taiko.Tests;

[TestFixture]
public class TaikoDifficultyRangesTest
{
    [Test]
    public void AllContainsSixLevels()
    {
        Assert.That(TaikoDifficultyRanges.All.Count, Is.EqualTo(6));
        var levels = TaikoDifficultyRanges.All.Select(r => r.Level).ToHashSet();
        foreach (var level in Enum.GetValues<DifficultyLevel>())
            Assert.That(levels, Does.Contain(level), $"{level} missing");
    }

    [Test]
    public void TryGetReturnsTrueForAllLevels()
    {
        foreach (var level in Enum.GetValues<DifficultyLevel>())
            Assert.That(TaikoDifficultyRanges.TryGet(level, out var range), Is.True, $"{level} TryGet");
    }

    [Test]
    public void GetReturnsCorrectLevel()
    {
        foreach (var level in Enum.GetValues<DifficultyLevel>())
        {
            var range = TaikoDifficultyRanges.Get(level);
            Assert.That(range.Level, Is.EqualTo(level));
        }
    }

    [Test]
    public void TryGetReturnsFalseForInvalidEnum()
    {
        var invalid = (DifficultyLevel)999;
        Assert.That(TaikoDifficultyRanges.TryGet(invalid, out _), Is.False);
        Assert.That(() => TaikoDifficultyRanges.Get(invalid), Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void AllRangesHaveValidBounds()
    {
        foreach (var range in TaikoDifficultyRanges.All)
        {
            Assert.That(range.ApproachRate.Min, Is.LessThanOrEqualTo(range.ApproachRate.Max), $"{range.Level} AR");
            Assert.That(range.OverallDifficulty.Min, Is.LessThanOrEqualTo(range.OverallDifficulty.Max), $"{range.Level} OD");
            Assert.That(range.HpDrain.Min, Is.LessThanOrEqualTo(range.HpDrain.Max), $"{range.Level} HP");
            Assert.That(range.CircleSize.Min, Is.LessThanOrEqualTo(range.CircleSize.Max), $"{range.Level} CS");
        }
    }

    [Test]
    public void ExpertPlusReusesExpertRanges()
    {
        var expert = TaikoDifficultyRanges.Get(DifficultyLevel.Expert);
        var expertPlus = TaikoDifficultyRanges.Get(DifficultyLevel.ExpertPlus);
        Assert.That(expertPlus.ApproachRate, Is.EqualTo(expert.ApproachRate));
        Assert.That(expertPlus.OverallDifficulty, Is.EqualTo(expert.OverallDifficulty));
        Assert.That(expertPlus.HpDrain, Is.EqualTo(expert.HpDrain));
        Assert.That(expertPlus.CircleSize, Is.EqualTo(expert.CircleSize));
    }

    [Test]
    public void EasyRangesMatchExpected()
    {
        var easy = TaikoDifficultyRanges.Get(DifficultyLevel.Easy);
        // Easy aligns to Kantan: OD [0,3], HP [8,10], AR/CS unconstrained [0,10]
        Assert.That(easy.ApproachRate.Min, Is.EqualTo(0));
        Assert.That(easy.ApproachRate.Max, Is.EqualTo(10));
        Assert.That(easy.OverallDifficulty.Min, Is.EqualTo(0));
        Assert.That(easy.OverallDifficulty.Max, Is.EqualTo(3));
        Assert.That(easy.HpDrain.Min, Is.EqualTo(8));
        Assert.That(easy.HpDrain.Max, Is.EqualTo(10));
        Assert.That(easy.CircleSize.Min, Is.EqualTo(0));
        Assert.That(easy.CircleSize.Max, Is.EqualTo(10));
    }

    [Test]
    public void ExpertRangesMatchExpected()
    {
        var expert = TaikoDifficultyRanges.Get(DifficultyLevel.Expert);
        // Expert aligns to Inner Oni: OD [6,10], HP [5,10]
        Assert.That(expert.ApproachRate.Min, Is.EqualTo(0));
        Assert.That(expert.ApproachRate.Max, Is.EqualTo(10));
        Assert.That(expert.OverallDifficulty.Min, Is.EqualTo(6));
        Assert.That(expert.OverallDifficulty.Max, Is.EqualTo(10));
        Assert.That(expert.HpDrain.Min, Is.EqualTo(5));
        Assert.That(expert.HpDrain.Max, Is.EqualTo(10));
        Assert.That(expert.CircleSize.Min, Is.EqualTo(0));
        Assert.That(expert.CircleSize.Max, Is.EqualTo(10));
    }

    [Test]
    public void InsaneRangesMatchExpected()
    {
        var insane = TaikoDifficultyRanges.Get(DifficultyLevel.Insane);
        // Insane aligns to Oni: OD [5,10], HP [5,10]
        Assert.That(insane.OverallDifficulty.Min, Is.EqualTo(5));
        Assert.That(insane.OverallDifficulty.Max, Is.EqualTo(10));
        Assert.That(insane.HpDrain.Min, Is.EqualTo(5));
        Assert.That(insane.HpDrain.Max, Is.EqualTo(10));
    }

    [Test]
    public void HardRangesMatchExpected()
    {
        var hard = TaikoDifficultyRanges.Get(DifficultyLevel.Hard);
        Assert.That(hard.OverallDifficulty.Min, Is.EqualTo(0));
        Assert.That(hard.OverallDifficulty.Max, Is.EqualTo(5));
        Assert.That(hard.HpDrain.Min, Is.EqualTo(6));
        Assert.That(hard.HpDrain.Max, Is.EqualTo(10));
    }

    [Test]
    public void ContainsLogicForEasyAndExpertPlus()
    {
        var easy = TaikoDifficultyRanges.Get(DifficultyLevel.Easy);
        float easyAr = (float)((easy.ApproachRate.Min + easy.ApproachRate.Max) / 2);
        float easyOd = (float)((easy.OverallDifficulty.Min + easy.OverallDifficulty.Max) / 2);
        float easyHp = (float)((easy.HpDrain.Min + easy.HpDrain.Max) / 2);
        float easyCs = (float)((easy.CircleSize.Min + easy.CircleSize.Max) / 2);
        Assert.That(easy.Contains(easyAr, easyOd, easyHp, easyCs), Is.True);

        Assert.That(easy.Contains((float)easy.ApproachRate.Min, (float)easy.OverallDifficulty.Min, (float)easy.HpDrain.Min, (float)easy.CircleSize.Min), Is.True);
        Assert.That(easy.Contains((float)easy.ApproachRate.Max, (float)easy.OverallDifficulty.Max, (float)easy.HpDrain.Max, (float)easy.CircleSize.Max), Is.True);

        // OD out of Easy [0,3] should fail when OD=10
        Assert.That(easy.Contains(easyAr, 10, easyHp, easyCs), Is.False);
        // HP out of Easy [8,10] should fail when HP=0
        Assert.That(easy.Contains(easyAr, easyOd, 0, easyCs), Is.False);

        var expertPlus = TaikoDifficultyRanges.Get(DifficultyLevel.ExpertPlus);
        float expAr = (float)((expertPlus.ApproachRate.Min + expertPlus.ApproachRate.Max) / 2);
        float expOd = (float)((expertPlus.OverallDifficulty.Min + expertPlus.OverallDifficulty.Max) / 2);
        float expHp = (float)((expertPlus.HpDrain.Min + expertPlus.HpDrain.Max) / 2);
        float expCs = (float)((expertPlus.CircleSize.Min + expertPlus.CircleSize.Max) / 2);
        Assert.That(expertPlus.Contains(expAr, expOd, expHp, expCs), Is.True);
        // OD below ExpertPlus [6,10] should fail
        Assert.That(expertPlus.Contains(expAr, 0, expHp, expCs), Is.False);
        Assert.That(expertPlus.Contains(expAr, 5, expHp, expCs), Is.False);
    }

    [Test]
    public void FloatRangeContainsBoundaryWithEpsilon()
    {
        var range = new FloatRange(0, 5);
        Assert.That(range.Contains(0), Is.True);
        Assert.That(range.Contains(5), Is.True);
        Assert.That(range.Contains(2.5), Is.True);
        Assert.That(range.Contains(-0.0000001), Is.False);
        Assert.That(range.Contains(5.0000001), Is.False);
        Assert.That(range.Contains(5 + 5e-10), Is.True);
        Assert.That(range.Contains(-5e-10), Is.True);
    }

    [Test]
    public void DifficultySettingsRangeToString()
    {
        var range = TaikoDifficultyRanges.Get(DifficultyLevel.Hard);
        string str = range.OverallDifficulty.ToString();
        Assert.That(str, Does.Contain("–"));
    }

    [Test]
    public void FloatRangeToStringContainsDash()
    {
        var range = new FloatRange(1, 3);
        Assert.That(range.ToString(), Does.Contain("–"));
    }

    [Test]
    public void GetLevelMatchesOfficialStarRatingThresholds()
    {
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
