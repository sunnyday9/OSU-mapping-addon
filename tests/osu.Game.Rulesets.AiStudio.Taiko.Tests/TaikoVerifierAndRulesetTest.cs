using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.AiStudio.Taiko.Checks;
using osu.Game.Rulesets.AiStudio.Taiko.Edit;
using osu.Game.Rulesets.AiStudio.Taiko.Synthesis;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Beatmaps;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.AiStudio.Taiko.Tests;

[TestFixture]
public class TaikoVerifierAndRulesetTest
{
    private sealed class FakeAudioAnalyzer : IAudioAnalyzer
    {
        public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult(new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList()));

        public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudioSection>>(new[] { new AudioSection(0, 30000, 0.6) });
    }

    private AiStudioTaikoRuleset ruleset = null!;

    [SetUp]
    public void Setup()
    {
        ruleset = new AiStudioTaikoRuleset();
    }

    [Test]
    public void ShortNameIsAistudioTaiko()
    {
        Assert.That(ruleset.ShortName, Is.EqualTo(AiStudioTaikoRuleset.SHORT_NAME));
        Assert.That(ruleset.ShortName, Is.EqualTo("aistudio-taiko"));
        Assert.That(ruleset.RulesetInfo.ShortName, Is.EqualTo("aistudio-taiko"));
    }

    [Test]
    public void DescriptionIsCorrect()
    {
        Assert.That(ruleset.Description, Is.EqualTo("AI Studio (taiko)"));
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
        var assistant = ruleset.CreateAllMods().OfType<AiStudioTaikoAssistantMod>().Single();
        Assert.That(assistant.Ranked, Is.False);
        Assert.That(assistant.Type, Is.EqualTo(ModType.Fun));
        Assert.That(assistant.Acronym, Is.EqualTo("AIAT"));
        Assert.That(assistant.Name, Is.EqualTo("AI Studio Assistant (Taiko)"));
        Assert.That(assistant.Description.ToString(), Does.Contain("taiko").IgnoreCase);
    }

    [Test]
    public void GetModsForFunContainsAssistant()
    {
        var funMods = ruleset.GetModsFor(ModType.Fun).ToList();
        Assert.That(funMods.OfType<AiStudioTaikoAssistantMod>().Any(), Is.True);
    }

    [Test]
    public void GetModsForNonFunDoesNotContainAssistant()
    {
        foreach (var type in new[] { ModType.DifficultyReduction, ModType.DifficultyIncrease, ModType.Automation, ModType.Conversion, ModType.System })
        {
            var mods = ruleset.GetModsFor(type).ToList();
            Assert.That(mods.OfType<AiStudioTaikoAssistantMod>().Any(), Is.False, $"{type} should not contain AIAT");
        }
    }

    [Test]
    public void FactoryCreateBeatmapConverter()
    {
        var beatmap = createEmptyTaikoBeatmap();
        var converter = ruleset.CreateBeatmapConverter(beatmap);
        Assert.That(converter, Is.Not.Null);
        Assert.That(converter.CanConvert(), Is.True);
        var converted = converter.Convert();
        Assert.That(converted, Is.Not.Null);
    }

    [Test]
    public void FactoryCreateBeatmapConverterWithHit()
    {
        var beatmap = new Beatmap<TaikoHitObject>();
        beatmap.BeatmapInfo.Ruleset = new TaikoRuleset().RulesetInfo;
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        beatmap.HitObjects.Add(new Hit { StartTime = 1000, Type = HitType.Centre });
        foreach (var obj in beatmap.HitObjects)
            obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);

        var converter = ruleset.CreateBeatmapConverter(beatmap);
        var converted = converter.Convert();
        Assert.That(converted.HitObjects.Count, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void FactoryCreateDifficultyCalculator()
    {
        var beatmap = createTaikoBeatmapWithHits();
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
        Assert.That(verifier, Is.InstanceOf<AiStudioTaikoBeatmapVerifier>());
    }

    [Test]
    public void FactoryCreateDrawableRuleset()
    {
        var beatmap = createTaikoBeatmapWithHits();
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
        var beatmap = createTaikoBeatmapWithHits();
        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));
        var verifier = new AiStudioTaikoBeatmapVerifier();
        Assert.DoesNotThrow(() => verifier.Run(context).ToList());
    }

    [Test]
    public void VerifierRunsWithoutThrowOnGeneratedBeatmap()
    {
        var beatmap = generateTaikoBeatmapViaGenerator();
        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));
        var verifier = new AiStudioTaikoBeatmapVerifier();
        Assert.DoesNotThrow(() => verifier.Run(context).ToList());
    }

    [Test]
    public void ChecksRunWithoutThrow()
    {
        var beatmap = generateTaikoBeatmapViaGenerator();
        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));

        Assert.DoesNotThrow(() => new CheckTaikoDonKatBalance().Run(context).ToList());
        Assert.DoesNotThrow(() => new CheckTaikoMonoPattern().Run(context).ToList());
    }

    [Test]
    public void DonKatBalanceFlagsImbalance()
    {
        var beatmap = new Beatmap<TaikoHitObject>();
        beatmap.BeatmapInfo.Ruleset = new TaikoRuleset().RulesetInfo;
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        for (int i = 0; i < 20; i++)
            beatmap.HitObjects.Add(new Hit { StartTime = 1000 + i * 500, Type = HitType.Centre });
        foreach (var obj in beatmap.HitObjects)
            obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);

        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));
        var issues = new CheckTaikoDonKatBalance().Run(context).ToList();
        Assert.That(issues.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(issues.All(i => i.Template is CheckTaikoDonKatBalance.IssueTemplateImbalance), Is.True);
    }

    [Test]
    public void DonKatBalanceDoesNotFlagBalanced()
    {
        var beatmap = new Beatmap<TaikoHitObject>();
        beatmap.BeatmapInfo.Ruleset = new TaikoRuleset().RulesetInfo;
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        for (int i = 0; i < 20; i++)
            beatmap.HitObjects.Add(new Hit { StartTime = 1000 + i * 500, Type = i % 2 == 0 ? HitType.Centre : HitType.Rim });
        foreach (var obj in beatmap.HitObjects)
            obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);

        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));
        var issues = new CheckTaikoDonKatBalance().Run(context).ToList();
        Assert.That(issues.Count, Is.EqualTo(0));
    }

    [Test]
    public void MonoPatternFlagsLongRun()
    {
        var beatmap = new Beatmap<TaikoHitObject>();
        beatmap.BeatmapInfo.Ruleset = new TaikoRuleset().RulesetInfo;
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        for (int i = 0; i < 10; i++)
            beatmap.HitObjects.Add(new Hit { StartTime = 1000 + i * 500, Type = HitType.Centre });
        foreach (var obj in beatmap.HitObjects)
            obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);

        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));
        var issues = new CheckTaikoMonoPattern().Run(context).ToList();
        Assert.That(issues.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(issues.All(i => i.Template is CheckTaikoMonoPattern.IssueTemplateMonoPattern), Is.True);
    }

    [Test]
    public void MonoPatternDoesNotFlagShortRun()
    {
        var beatmap = new Beatmap<TaikoHitObject>();
        beatmap.BeatmapInfo.Ruleset = new TaikoRuleset().RulesetInfo;
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        for (int i = 0; i < 10; i++)
            beatmap.HitObjects.Add(new Hit { StartTime = 1000 + i * 500, Type = i % 2 == 0 ? HitType.Centre : HitType.Rim });
        foreach (var obj in beatmap.HitObjects)
            obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);

        var context = new BeatmapVerifierContext(beatmap, new InMemoryWorkingBeatmap(beatmap));
        var issues = new CheckTaikoMonoPattern().Run(context).ToList();
        Assert.That(issues.Count, Is.EqualTo(0));
    }

    [Test]
    public void VerifierWithInvalidWorkingDoesNotThrow()
    {
        var beatmap = createEmptyTaikoBeatmap();
        var working = new FailingWorkingBeatmap(beatmap);
        // TaikoStarRating is internal, but its null-return path is exercised via verifier with a failing working beatmap.
        // Ensure verifier handles invalid working gracefully without throwing.
        var context = new BeatmapVerifierContext(beatmap, working);
        var verifier = new AiStudioTaikoBeatmapVerifier();
        Assert.DoesNotThrow(() => verifier.Run(context).ToList());
    }

    private static Beatmap<TaikoHitObject> createEmptyTaikoBeatmap()
    {
        var beatmap = new Beatmap<TaikoHitObject>();
        beatmap.BeatmapInfo.Ruleset = new AiStudioTaikoRuleset().RulesetInfo;
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        return beatmap;
    }

    private static Beatmap<TaikoHitObject> createTaikoBeatmapWithHits()
    {
        var beatmap = new Beatmap<TaikoHitObject>();
        beatmap.BeatmapInfo.Ruleset = new AiStudioTaikoRuleset().RulesetInfo;
        beatmap.BeatmapInfo.Difficulty = new BeatmapDifficulty { ApproachRate = 5, OverallDifficulty = 5, DrainRate = 5, CircleSize = 2 };
        beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
        beatmap.HitObjects.Add(new Hit { StartTime = 1000, Type = HitType.Centre });
        beatmap.HitObjects.Add(new Hit { StartTime = 1500, Type = HitType.Rim });
        beatmap.HitObjects.Add(new Hit { StartTime = 2000, Type = HitType.Centre });
        foreach (var obj in beatmap.HitObjects)
            obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);
        return beatmap;
    }

    private static IBeatmap generateTaikoBeatmapViaGenerator()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-taiko-verify-{Guid.NewGuid():N}");
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
            var result = new TaikoMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();
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
