using AiStudio.Core.Models;
using NUnit.Framework;
using osu.Game.Rulesets.AiStudio.Mania.Models;

namespace osu.Game.Rulesets.AiStudio.Mania.Tests;

[TestFixture]
public class ManiaDifficultyRangesTest
{
    [Test]
    public void AllContainsSixLevels()
    {
        Assert.That(ManiaDifficultyRanges.All.Count, Is.EqualTo(6));
        var levels = ManiaDifficultyRanges.All.Select(r => r.Level).ToHashSet();
        foreach (var level in Enum.GetValues<DifficultyLevel>())
            Assert.That(levels, Does.Contain(level), $"{level} missing");
    }

    [Test]
    public void TryGetReturnsTrueForAllLevels()
    {
        foreach (var level in Enum.GetValues<DifficultyLevel>())
            Assert.That(ManiaDifficultyRanges.TryGet(level, out var range), Is.True, $"{level} TryGet");
    }

    [Test]
    public void GetReturnsCorrectLevel()
    {
        foreach (var level in Enum.GetValues<DifficultyLevel>())
        {
            var range = ManiaDifficultyRanges.Get(level);
            Assert.That(range.Level, Is.EqualTo(level));
        }
    }

    [Test]
    public void TryGetReturnsFalseForInvalidEnum()
    {
        var invalid = (DifficultyLevel)999;
        Assert.That(ManiaDifficultyRanges.TryGet(invalid, out _), Is.False);
        Assert.That(() => ManiaDifficultyRanges.Get(invalid), Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void AllRangesHaveValidBounds()
    {
        foreach (var range in ManiaDifficultyRanges.All)
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
        var expert = ManiaDifficultyRanges.Get(DifficultyLevel.Expert);
        var expertPlus = ManiaDifficultyRanges.Get(DifficultyLevel.ExpertPlus);
        Assert.That(expertPlus.ApproachRate, Is.EqualTo(expert.ApproachRate));
        Assert.That(expertPlus.OverallDifficulty, Is.EqualTo(expert.OverallDifficulty));
        Assert.That(expertPlus.HpDrain, Is.EqualTo(expert.HpDrain));
        Assert.That(expertPlus.CircleSize, Is.EqualTo(expert.CircleSize));
    }

    [Test]
    public void EasyRangesMatchExpectedPlaceholder()
    {
        var easy = ManiaDifficultyRanges.Get(DifficultyLevel.Easy);
        Assert.That(easy.ApproachRate.Min, Is.EqualTo(0));
        Assert.That(easy.ApproachRate.Max, Is.EqualTo(5));
        Assert.That(easy.OverallDifficulty.Min, Is.EqualTo(3));
        Assert.That(easy.OverallDifficulty.Max, Is.EqualTo(5));
        Assert.That(easy.HpDrain.Min, Is.EqualTo(3));
        Assert.That(easy.HpDrain.Max, Is.EqualTo(5));
        Assert.That(easy.CircleSize.Min, Is.EqualTo(0));
        Assert.That(easy.CircleSize.Max, Is.EqualTo(4));
    }

    [Test]
    public void ExpertRangesMatchExpected()
    {
        var expert = ManiaDifficultyRanges.Get(DifficultyLevel.Expert);
        Assert.That(expert.ApproachRate.Min, Is.EqualTo(8));
        Assert.That(expert.ApproachRate.Max, Is.EqualTo(10));
        Assert.That(expert.OverallDifficulty.Min, Is.EqualTo(7));
        Assert.That(expert.OverallDifficulty.Max, Is.EqualTo(10));
        Assert.That(expert.HpDrain.Min, Is.EqualTo(7));
        Assert.That(expert.HpDrain.Max, Is.EqualTo(10));
    }

    [Test]
    public void ContainsLogicForEasyAndExpertPlus()
    {
        var easy = ManiaDifficultyRanges.Get(DifficultyLevel.Easy);
        // Midpoint should be inside
        float easyAr = (float)((easy.ApproachRate.Min + easy.ApproachRate.Max) / 2);
        float easyOd = (float)((easy.OverallDifficulty.Min + easy.OverallDifficulty.Max) / 2);
        float easyHp = (float)((easy.HpDrain.Min + easy.HpDrain.Max) / 2);
        float easyCs = (float)((easy.CircleSize.Min + easy.CircleSize.Max) / 2);
        Assert.That(easy.Contains(easyAr, easyOd, easyHp, easyCs), Is.True);

        // Boundary inclusive
        Assert.That(easy.Contains((float)easy.ApproachRate.Min, (float)easy.OverallDifficulty.Min, (float)easy.HpDrain.Min, (float)easy.CircleSize.Min), Is.True);
        Assert.That(easy.Contains((float)easy.ApproachRate.Max, (float)easy.OverallDifficulty.Max, (float)easy.HpDrain.Max, (float)easy.CircleSize.Max), Is.True);

        // Outside should fail
        Assert.That(easy.Contains(10, easyOd, easyHp, easyCs), Is.False);
        Assert.That(easy.Contains(easyAr, 10, easyHp, easyCs), Is.False);

        var expertPlus = ManiaDifficultyRanges.Get(DifficultyLevel.ExpertPlus);
        float expAr = (float)((expertPlus.ApproachRate.Min + expertPlus.ApproachRate.Max) / 2);
        float expOd = (float)((expertPlus.OverallDifficulty.Min + expertPlus.OverallDifficulty.Max) / 2);
        float expHp = (float)((expertPlus.HpDrain.Min + expertPlus.HpDrain.Max) / 2);
        float expCs = (float)((expertPlus.CircleSize.Min + expertPlus.CircleSize.Max) / 2);
        Assert.That(expertPlus.Contains(expAr, expOd, expHp, expCs), Is.True);
        Assert.That(expertPlus.Contains(0, expOd, expHp, expCs), Is.False);
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
        // Within epsilon 1e-9 should still be considered inside
        Assert.That(range.Contains(5 + 5e-10), Is.True);
        Assert.That(range.Contains(-5e-10), Is.True);
    }

    [Test]
    public void DifficultySettingsRangeToString()
    {
        var range = ManiaDifficultyRanges.Get(DifficultyLevel.Hard);
        string str = range.OverallDifficulty.ToString();
        Assert.That(str, Does.Contain("–"));
    }
}
