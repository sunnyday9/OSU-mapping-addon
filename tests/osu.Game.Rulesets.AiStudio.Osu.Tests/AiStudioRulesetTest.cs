using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu.Objects;
using osuTK;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

[TestFixture]
public class AiStudioRulesetTest
{
    private AiStudioRuleset ruleset = null!;

    [SetUp]
    public void Setup()
    {
        ruleset = new AiStudioRuleset();
    }

    [Test]
    public void ShortNameIsRegisteredAsAistudio()
    {
        Assert.That(ruleset.ShortName, Is.EqualTo(AiStudioRuleset.SHORT_NAME));
        Assert.That(ruleset.RulesetInfo.ShortName, Is.EqualTo(AiStudioRuleset.SHORT_NAME));
    }

    [Test]
    public void RulesetInfoIsNotLegacy()
    {
        // PLAN.md §2.3：非 legacy ruleset 的 OnlineID 必须为 -1，
        // 否则与内置 osu!（OnlineID=0）撞车而被 RealmRulesetStore 静默跳过。
        Assert.That(ruleset.RulesetInfo.OnlineID, Is.EqualTo(-1));
    }

    [Test]
    public void ApiVersionMatchesCurrentRulesetApi()
    {
        Assert.That(ruleset.RulesetAPIVersionSupported, Is.EqualTo(Ruleset.CURRENT_RULESET_API_VERSION));
    }

    [Test]
    public void AllModsCanBeCreated()
    {
        // 启动兼容性测试（testRulesetCompatibility）会枚举全部 mods，任何异常都会导致插件被禁用。
        Assert.DoesNotThrow(() => ruleset.CreateAllMods().ToList());
        Assert.That(ruleset.CreateAllMods().Any(), Is.True);
    }

    [Test]
    public void AssistantModIsNotRanked()
    {
        var assistant = ruleset.CreateAllMods().OfType<AiStudioAssistantMod>().Single();
        Assert.That(assistant.Ranked, Is.False);
#pragma warning disable CS0618 // Mod.ScoreMultiplier is obsolete; test asserts legacy compatibility shim still returns 1
        Assert.That(assistant.ScoreMultiplier, Is.EqualTo(1));
#pragma warning restore CS0618
    }

    [Test]
    public void OsuBeatmapConvertsWithoutError()
    {
        var beatmap = createTestBeatmap();
        var converter = ruleset.CreateBeatmapConverter(beatmap);

        Assert.That(converter.CanConvert, Is.True);

        var converted = converter.Convert();
        Assert.That(converted.HitObjects, Has.Count.EqualTo(beatmap.HitObjects.Count));
    }

    [Test]
    public void DifficultyCalculationDoesNotThrow()
    {
        var working = new TestWorkingBeatmap(createTestBeatmap());
        var calculator = ruleset.CreateDifficultyCalculator(working);

        var attributes = calculator.Calculate();
        Assert.That(attributes.StarRating, Is.GreaterThanOrEqualTo(0));
    }

    private static Beatmap createTestBeatmap()
    {
        var beatmap = new Beatmap();
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

        beatmap.HitObjects.Add(new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 });
        beatmap.HitObjects.Add(new HitCircle { Position = new Vector2(320, 192), StartTime = 1500 });
        beatmap.HitObjects.Add(new HitCircle { Position = new Vector2(384, 192), StartTime = 2000 });

        return beatmap;
    }
}
