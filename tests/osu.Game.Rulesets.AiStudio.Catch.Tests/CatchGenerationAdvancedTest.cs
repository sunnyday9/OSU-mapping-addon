using System.IO;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.AiStudio.Catch.Synthesis;
using osu.Game.Rulesets.Catch;

namespace osu.Game.Rulesets.AiStudio.Catch.Tests;

[TestFixture]
public class CatchGenerationAdvancedTest
{
    private sealed class FakeAudioAnalyzer : IAudioAnalyzer
    {
        private readonly BeatGrid grid;
        private readonly IReadOnlyList<AudioSection> sections;

        public FakeAudioAnalyzer(BeatGrid? grid = null, IReadOnlyList<AudioSection>? sections = null)
        {
            this.grid = grid ?? new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList());
            this.sections = sections ?? new[] { new AudioSection(0, 30000, 0.6) };
        }

        public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult(grid);

        public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult(sections);
    }

    [Test]
    public void GenerateWithRealWavSucceeds()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-catch-adv-{Guid.NewGuid():N}");
        string wavPath = Path.Combine(tempDir, "clicktrack.wav");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            WavTestUtils.CreateClickTrackWav(wavPath, bpm: 120, durationSeconds: 30, amplitude: 0.8);
            Assert.That(File.Exists(wavPath), Is.True);

            var settings = new GenerationSettings
            {
                AudioPath = wavPath,
                TargetLevel = DifficultyLevel.Hard,
                TargetStarRating = 3.5,
                OutputDirectory = outputDir,
            };

            var result = new CatchMapGenerator().GenerateAsync(settings).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.OutputFilePath, Is.Not.Null);
            Assert.That(File.Exists(result.OutputFilePath!), Is.True);
            Assert.That(result.QualityReport, Is.Not.Null);

            string content = File.ReadAllText(result.OutputFilePath!);
            Assert.That(content, Does.Contain("AI generated"));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Test]
    public void GenerateSucceedsForAllTargetLevels()
    {
        foreach (var level in Enum.GetValues<DifficultyLevel>())
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-catch-lvl-{Guid.NewGuid():N}");
            string audioPath = Path.Combine(tempDir, "test_audio.mp3");
            string outputDir = Path.Combine(tempDir, "out");
            Directory.CreateDirectory(tempDir);

            try
            {
                File.WriteAllText(audioPath, "placeholder");

                var settings = new GenerationSettings
                {
                    AudioPath = audioPath,
                    TargetLevel = level,
                    TargetStarRating = 3.5,
                    OutputDirectory = outputDir,
                };

                var result = new CatchMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();

                Assert.That(result.Success, Is.True, $"{level} should succeed: {result.ErrorMessage}");
                Assert.That(File.Exists(result.OutputFilePath!), Is.True, $"{level} output missing");

                var decoded = Decode(result.OutputFilePath!);
                Assert.That(decoded.HitObjects.Count, Is.GreaterThan(0), $"{level} should have objects");
                Assert.That(decoded.BeatmapInfo.DifficultyName, Is.EqualTo(level.ToString()), $"{level} DifficultyName");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Test]
    public void SpreadPlannerWithCatchProducesValidSpread()
    {
        var grid = new BeatGrid(120, 0, Enumerable.Range(0, 120).Select(i => i * 500.0).ToList());
        var sections = new[] { new AudioSection(0, 60000, 0.6) };
        var settings = new GenerationSettings { AudioPath = "dummy.mp3", TargetStarRating = 3.5 };

        var specs = SpreadPlanner.Plan(grid, sections, settings);

        Assert.That(specs.Count, Is.GreaterThanOrEqualTo(2));
        for (int i = 1; i < specs.Count; i++)
        {
            double gap = specs[i].TargetStarRating - specs[i - 1].TargetStarRating;
            Assert.That(gap, Is.LessThanOrEqualTo(2.01), $"gap {gap} between {specs[i - 1].Level} and {specs[i].Level}");
        }

        Assert.That(specs.Select(s => s.Level).Distinct().Count(), Is.EqualTo(specs.Count), "levels should be distinct");
    }

    [Test]
    public void SpreadPlannerExpandSettingsProducesPerDifficultySettings()
    {
        var grid = new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList());
        var sections = new[] { new AudioSection(0, 30000, 0.6) };
        var settings = new GenerationSettings { AudioPath = "dummy.mp3", OutputDirectory = "/tmp/out", IncludeBreakSections = true, TargetStarRating = 3.5 };

        var expanded = SpreadPlanner.ExpandSettings(settings, grid, sections);

        Assert.That(expanded.Count, Is.GreaterThanOrEqualTo(2));
        foreach (var s in expanded)
        {
            Assert.That(s.AudioPath, Is.EqualTo(settings.AudioPath));
            Assert.That(s.OutputDirectory, Is.EqualTo(settings.OutputDirectory));
            Assert.That(s.Difficulties, Is.Not.Null);
            Assert.That(s.Difficulties!.Count, Is.EqualTo(1));
        }
    }

    [Test]
    public void SpreadPlannerRespectsExplicitDifficulties()
    {
        var grid = new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList());
        var sections = new[] { new AudioSection(0, 30000, 0.6) };
        var explicitSpecs = new[]
        {
            new DifficultySpec { Level = DifficultyLevel.Normal, TargetStarRating = 2.3, StarRatingTolerance = 0.3 },
            new DifficultySpec { Level = DifficultyLevel.Hard, TargetStarRating = 3.0, StarRatingTolerance = 0.3 },
        };
        var settings = new GenerationSettings { AudioPath = "dummy.mp3", Difficulties = explicitSpecs };

        var planned = SpreadPlanner.Plan(grid, sections, settings);

        Assert.That(planned.Count, Is.EqualTo(explicitSpecs.Length));
        Assert.That(planned[0].Level, Is.EqualTo(DifficultyLevel.Normal));
        Assert.That(planned[1].Level, Is.EqualTo(DifficultyLevel.Hard));
    }

    [Test]
    public void ConstantDistributionProviderReturnsConfiguredSet()
    {
        var custom = new DistributionSet
        {
            SpacingPx = new DistributionRange(10, 500),
            SliderRatio = new DistributionRange(0.0, 0.95),
            GridRatio = new DistributionRange(0.9, 1.0),
        };
        var provider = new ConstantDistributionProvider(custom);
        var got = provider.Get();

        Assert.That(got.SpacingPx, Is.EqualTo(custom.SpacingPx));
        Assert.That(got.SliderRatio, Is.EqualTo(custom.SliderRatio));
        Assert.That(got.GridRatio, Is.EqualTo(custom.GridRatio));
        Assert.That(provider, Is.InstanceOf<IDistributionProvider>());
    }

    [Test]
    public void ConstantDistributionProviderDefaultsWhenNull()
    {
        var provider = new ConstantDistributionProvider(null);
        var got = provider.Get();
        Assert.That(got, Is.Not.Null);
        Assert.That(got.SpacingPx.P5, Is.EqualTo(DistributionSet.Default.SpacingPx.P5));
    }

    [Test]
    public void FileDistributionProviderFallsBackToDefaultWhenMissing()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.json");
        var provider = new FileDistributionProvider(missing);
        var got = provider.Get();
        Assert.That(got, Is.Not.Null);
        Assert.That(got.SpacingPx.P5, Is.EqualTo(DistributionSet.Default.SpacingPx.P5));
        Assert.That(provider, Is.InstanceOf<IDistributionProvider>());
    }

    [Test]
    public void FileDistributionProviderParsesJson()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-dist-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string jsonPath = Path.Combine(tempDir, "dist.json");

        try
        {
            string json = """{"spacing_px":{"p5":15.0,"p95":350.0},"slider_ratio":{"p5":0.1,"p95":0.9},"grid_ratio":{"p5":0.92,"p95":0.99}}""";
            File.WriteAllText(jsonPath, json);

            var provider = new FileDistributionProvider(jsonPath);
            var got = provider.Get();

            Assert.That(got.SpacingPx.P5, Is.EqualTo(15.0).Within(1e-9));
            Assert.That(got.SpacingPx.P95, Is.EqualTo(350.0).Within(1e-9));
            Assert.That(got.SliderRatio.P5, Is.EqualTo(0.1).Within(1e-9));
            Assert.That(got.GridRatio.P95, Is.EqualTo(0.99).Within(1e-9));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Test]
    public void FileDistributionProviderReturnsDefaultOnInvalidJson()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-dist-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string jsonPath = Path.Combine(tempDir, "bad.json");

        try
        {
            File.WriteAllText(jsonPath, "not json at all {");
            var provider = new FileDistributionProvider(jsonPath);
            var got = provider.Get();
            Assert.That(got.SpacingPx.P5, Is.EqualTo(DistributionSet.Default.SpacingPx.P5));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Test]
    public void DistributionSetFromDictionaryHandlesMissingKeys()
    {
        var dict = new Dictionary<string, IReadOnlyDictionary<string, double>>
        {
            ["spacing_px"] = new Dictionary<string, double> { ["p5"] = 20, ["p95"] = 300 },
        };
        var set = DistributionSet.FromDictionary(dict);
        Assert.That(set.SpacingPx.P5, Is.EqualTo(20));
        Assert.That(set.SpacingPx.P95, Is.EqualTo(300));
        Assert.That(set.SliderRatio.P5, Is.EqualTo(DistributionSet.Default.SliderRatio.P5));
        Assert.That(set.GridRatio.P5, Is.EqualTo(DistributionSet.Default.GridRatio.P5));
    }

    [Test]
    public void DistributionSetFromDictionaryHandlesUpperCaseKeys()
    {
        var dict = new Dictionary<string, IReadOnlyDictionary<string, double>>
        {
            ["spacing_px"] = new Dictionary<string, double> { ["P5"] = 25, ["P95"] = 310 },
        };
        var set = DistributionSet.FromDictionary(dict);
        Assert.That(set.SpacingPx.P5, Is.EqualTo(25));
        Assert.That(set.SpacingPx.P95, Is.EqualTo(310));
    }

    [Test]
    public void InMemoryWorkingBeatmapReturnsBeatmapAndHandlesOverrides()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-catch-wm-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");
            var settings = new GenerationSettings { AudioPath = audioPath, TargetLevel = DifficultyLevel.Hard, TargetStarRating = 3.5, OutputDirectory = outputDir };
            var result = new CatchMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            var decoded = Decode(result.OutputFilePath!);
            var working = new InMemoryWorkingBeatmap(decoded);

            Assert.That(working.GetBackground(), Is.Null);
            Assert.That(working.GetStream("any"), Is.Null);
            Assert.That(working.BeatmapInfo, Is.EqualTo(decoded.BeatmapInfo));
            var trackMethod = typeof(osu.Game.Beatmaps.WorkingBeatmap).GetMethod("GetBeatmapTrack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(trackMethod, Is.Not.Null);
            Assert.That(trackMethod!.Invoke(working, null), Is.Null);
            var skinMethod = typeof(osu.Game.Beatmaps.WorkingBeatmap).GetMethod("GetSkin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(skinMethod, Is.Not.Null);
            Assert.That(skinMethod!.Invoke(working, null), Is.Not.Null);

            double stars = new osu.Game.Rulesets.Catch.Difficulty.CatchDifficultyCalculator(new CatchRuleset().RulesetInfo, working).Calculate().StarRating;
            Assert.That(stars, Is.GreaterThanOrEqualTo(0));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Test]
    public void InMemoryWorkingBeatmapDirectConstruction()
    {
        var beatmap = new Beatmap();
        beatmap.BeatmapInfo.Ruleset = new CatchRuleset().RulesetInfo;
        beatmap.ControlPointInfo.Add(0, new osu.Game.Beatmaps.ControlPoints.TimingControlPoint { BeatLength = 500 });
        beatmap.HitObjects.Add(new osu.Game.Rulesets.Catch.Objects.Fruit { StartTime = 1000, X = 256 });

        var working = new InMemoryWorkingBeatmap(beatmap);

        Assert.That(working.GetBackground(), Is.Null);
        Assert.That(working.GetStream("x"), Is.Null);
        var trackMethod2 = typeof(osu.Game.Beatmaps.WorkingBeatmap).GetMethod("GetBeatmapTrack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(trackMethod2, Is.Not.Null);
        Assert.That(trackMethod2!.Invoke(working, null), Is.Null);
        var skinMethod2 = typeof(osu.Game.Beatmaps.WorkingBeatmap).GetMethod("GetSkin", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(skinMethod2, Is.Not.Null);
        Assert.That(skinMethod2!.Invoke(working, null), Is.Not.Null);
    }

    [Test]
    public void GenerationFailPathsAreDeterministic()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-catch-fail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string missing = Path.Combine(tempDir, "nope.mp3");
            var settings = new GenerationSettings { AudioPath = missing, TargetLevel = DifficultyLevel.Hard, OutputDirectory = tempDir };
            var result = new CatchMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();
            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);

            var emptyAudio = new GenerationSettings { AudioPath = string.Empty, TargetLevel = DifficultyLevel.Hard, OutputDirectory = tempDir };
            var result2 = new CatchMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(emptyAudio).GetAwaiter().GetResult();
            Assert.That(result2.Success, Is.False);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static Beatmap Decode(string osuPath)
    {
        using var reader = new LineBufferedReader(File.OpenRead(osuPath));
        return new LegacyBeatmapDecoder().Decode(reader, Array.Empty<LineBufferedReader>());
    }
}
