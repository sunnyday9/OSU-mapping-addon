using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.AiStudio.Catch.Checks;
using osu.Game.Rulesets.AiStudio.Catch.Edit;
using osu.Game.Rulesets.AiStudio.Catch.Synthesis;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.AiStudio.Catch.Tests;

[TestFixture]
public class CatchVerifierAndRulesetTest
{
    private sealed class FakeAudioAnalyzer : IAudioAnalyzer
    {
        public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult(new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList()));

        public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudioSection>>(new[] { new AudioSection(0, 30000, 0.6) });
    }

    private AiStudioCatchRuleset ruleset = null!;

    [SetUp]
    public void Setup()
    {
        ruleset = new AiStudioCatchRuleset();
    }

    [Test]
    public void ShortNameIsAistudioCatch()
    {
        Assert.That(ruleset.ShortName, Is.EqualTo(AiStudioCatchRuleset.SHORT_NAME));
        Assert.That(ruleset.ShortName, Is.EqualTo("aistudio-catch"));
        Assert.That(ruleset.RulesetInfo.ShortName, Is.EqualTo("aistudio-catch"));
    }

    [Test]
    public void DescriptionIsCorrect()
    {
        Assert.That(ruleset.Description, Is.EqualTo("AI Studio (catch)"));
    }

    [Test]
    public void ApiVersionMatchesCurrent()
    {
        Assert.That(ruleset.RulesetAPIVersionSupported, Is.EqualTo(Ruleset.CURRENT_RULESET_API_VERSION));
    }

    [Test]
    public void RulesetInfoIsNotLegacy()
    {
        Assert.That(ruleset.RulesetInfo.OnlineID, Is.EqualTo(-1));
    }

    [Test]
    public void AllModsCanBeCreated()
    {
        Assert.DoesNotThrow(() => ruleset.CreateAllMods().ToList());
        Assert.That(ruleset.CreateAllMods().Any(), Is.True);
    }

    [Test]
    public void AssistantModProperties()
    {
        var assistant = ruleset.CreateAllMods().OfType<AiStudioCatchAssistantMod>().Single();
        Assert.That(assistant.Ranked, Is.False);
        Assert.That(assistant.Type, Is.EqualTo(ModType.Fun));
        Assert.That(assistant.Acronym, Is.EqualTo("AIAC"));
        Assert.That(assistant.Name, Is.EqualTo("AI Studio Assistant (Catch)"));
        Assert.That(assistant.Description.ToString(), Does.Contain("catch").IgnoreCase);
    }

    [Test]
    public void GetModsForFunContainsAssistant()
    {
        var funMods = ruleset.GetModsFor(ModType.Fun).ToList();
        Assert.That(funMods.OfType<AiStudioCatchAssistantMod>().Any(), Is.True);
    }

    [Test]
    public void GetModsForNonFunDoesNotContainAssistant()
    {
        foreach (var type in new[] { ModType.DifficultyReduction, ModType.DifficultyIncrease, ModType.Automation, ModType.Conversion, ModType.System })
        {
            var mods = ruleset.GetModsFor(type).ToList();
            Assert.That(mods.OfType<AiStudioCatchAssistantMod>().Any(), Is.False, $"{type} should not contain AIAC");
        }
    }

    [Test]
    public void FactoryCreateBeatmapConverter()
    {
        var beatmap = createEmptyCatchBeatmap();
        var converter = ruleset.CreateBeatmapConverter(beatmap);
        Assert.That(converter, Is.Not.Null);
        Assert.That(converter.CanConvert, Is.True);
        var converted = converter.Convert();
        Assert.That(converted, Is.Not.Null);
    }

    [Test]
    public void FactoryCreateBeatmapProcessor()
    {
        var beatmap = createEmptyCatchBeatmap();
        var processor = ruleset.CreateBeatmapProcessor(beatmap);
        Assert.That(processor, Is.Not.Null);
    }

    [Test]
    public void FactoryCreateDifficultyCalculator()
    {
        var beatmap = createCatchBeatmapWithFruits();
        var working = new InMemoryWorkingBeatmap(beatmap);
        var calculator = ruleset.CreateDifficultyCalculator(working);
        Assert.That(calculator, Is.Not.Null);
        var attrs = calculator.Calculate();
        Assert.That(attrs.StarRating, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void FactoryCreatePerformanceCalculator()
    {
        var calc = ruleset.CreatePerformanceCalculator();
        Assert.That(calc, Is.Not.Null);
    }

    [Test]
    public void FactoryCreateHitObjectComposer()
    {
        var composer = ruleset.CreateHitObjectComposer();
        Assert.That(composer, Is.Not.Null);
    }

    [Test]
    public void FactoryCreateBeatmapVerifier()
    {
        var verifier = ruleset.CreateBeatmapVerifier();
        Assert.That(verifier, Is.Not.Null);
        Assert.That(verifier, Is.InstanceOf<AiStudioCatchBeatmapVerifier>());
    }

    [Test]
    public void FactoryCreateDrawableRuleset()
    {
        var beatmap = createCatchBeatmapWithFruits();
        var drawable = ruleset.CreateDrawableRulesetWith(beatmap, Array.Empty<Mod>());
        Assert.That(drawable, Is.Not.Null);
    }

    [Test]
    public void FactoryCreateEditorSetupSections()
    {
        var sections = ruleset.CreateEditorSetupSections().ToList();
        Assert.That(sections, Is.Not.Empty);
    }

    [Test]
    public void VerifierRunsWithoutThrowOnEmptyBeatmap()
    {
        var beatmap = createCatchBeatmapWithFruits();
        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));
        var verifier = new AiStudioCatchBeatmapVerifier();
        Assert.DoesNotThrow(() => verifier.Run(context).ToList());
    }

    [Test]
    public void VerifierRunsWithoutThrowOnGeneratedBeatmap()
    {
        var beatmap = generateCatchBeatmapViaGenerator();
        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));
        var verifier = new AiStudioCatchBeatmapVerifier();
        Assert.DoesNotThrow(() => verifier.Run(context).ToList());
    }

    [Test]
    public void ChecksRunWithoutThrow()
    {
        var beatmap = generateCatchBeatmapViaGenerator();
        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));

        Assert.DoesNotThrow(() => new CheckCatchDifficultyRanges().Run(context).ToList());
        Assert.DoesNotThrow(() => new CheckCatchHyperdashFeasibility().Run(context).ToList());
        Assert.DoesNotThrow(() => new CheckCatchOffscreen().Run(context).ToList());
        Assert.DoesNotThrow(() => new CheckCatchMovementFeasibility().Run(context).ToList());
    }

    [Test]
    public void OffscreenCheckFlagsOutOfBounds()
    {
        var beatmap = new Beatmap<CatchHitObject>();
        beatmap.BeatmapInfo.Ruleset = new CatchRuleset().RulesetInfo;
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        beatmap.HitObjects.Add(new Fruit { StartTime = 1000, X = -5 });
        beatmap.HitObjects.Add(new Fruit { StartTime = 1500, X = 600 });
        beatmap.HitObjects.Add(new Fruit { StartTime = 2000, X = 256 });

        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));
        var issues = new CheckCatchOffscreen().Run(context).ToList();
        Assert.That(issues.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(issues.All(i => i.Template is CheckCatchOffscreen.IssueTemplateOffscreen), Is.True);
    }

    [Test]
    public void CatchStarRatingReturnsNullOnInvalidWorking()
    {
        var beatmap = createEmptyCatchBeatmap();
        var working = new FailingWorkingBeatmap(beatmap);
        var info = new BeatmapInfo { Ruleset = new CatchRuleset().RulesetInfo };
        double? stars = CatchStarRating.TryCalculate(working, info);
        Assert.That(stars, Is.Null);
    }

    private static Beatmap<CatchHitObject> createEmptyCatchBeatmap()
    {
        var beatmap = new Beatmap<CatchHitObject>();
        beatmap.BeatmapInfo.Ruleset = new AiStudioCatchRuleset().RulesetInfo;
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        return beatmap;
    }

    private static Beatmap<CatchHitObject> createCatchBeatmapWithFruits()
    {
        var beatmap = new Beatmap<CatchHitObject>();
        beatmap.BeatmapInfo.Ruleset = new AiStudioCatchRuleset().RulesetInfo;
        beatmap.BeatmapInfo.Difficulty = new BeatmapDifficulty { ApproachRate = 5, OverallDifficulty = 5, DrainRate = 5, CircleSize = 4 };
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        beatmap.HitObjects.Add(new Fruit { StartTime = 1000, X = 100 });
        beatmap.HitObjects.Add(new Fruit { StartTime = 1500, X = 400 });
        beatmap.HitObjects.Add(new Fruit { StartTime = 2000, X = 256 });
        foreach (var obj in beatmap.HitObjects)
            obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);
        return beatmap;
    }

    private static IBeatmap generateCatchBeatmapViaGenerator()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-catch-verify-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");
            var settings = new GenerationSettings
            {
                AudioPath = audioPath,
                TargetLevel = DifficultyLevel.Hard,
                TargetStarRating = 3.5,
                OutputDirectory = outputDir,
            };
            var result = new CatchMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            using var reader = new osu.Game.IO.LineBufferedReader(File.OpenRead(result.OutputFilePath!));
            var decoded = new osu.Game.Beatmaps.Formats.LegacyBeatmapDecoder().Decode(reader, Array.Empty<osu.Game.IO.LineBufferedReader>());
            return decoded;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private sealed class FailingWorkingBeatmap : osu.Game.Beatmaps.WorkingBeatmap
    {
        public FailingWorkingBeatmap(IBeatmap beatmap) : base(beatmap.BeatmapInfo, null!) { }
        protected override IBeatmap GetBeatmap() => throw new InvalidOperationException("fail");
        public override osu.Framework.Graphics.Textures.Texture GetBackground() => null!;
        protected override osu.Framework.Audio.Track.Track GetBeatmapTrack() => null!;
        protected override osu.Game.Skinning.ISkin GetSkin() => null!;
        public override Stream GetStream(string storagePath) => null!;
    }
}
