using System.IO;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.AiStudio.Mania.Checks;
using osu.Game.Rulesets.AiStudio.Mania.Edit;
using osu.Game.Rulesets.AiStudio.Mania.Synthesis;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.AiStudio.Mania.Tests;

[TestFixture]
public class ManiaChecksTest
{
    private sealed class FakeAudioAnalyzer : IAudioAnalyzer
    {
        public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult(new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList()));

        public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudioSection>>(new[] { new AudioSection(0, 30000, 0.6) });
    }

    [Test]
    public void AiStudioManiaBeatmapVerifierRunsWithoutThrowing()
    {
        var beatmap = GenerateManiaBeatmap();

        var verifier = new AiStudioManiaBeatmapVerifier();
        var context = CreateContext(beatmap);

        Assert.DoesNotThrow(() => verifier.Run(context).ToList());
    }

    [Test]
    public void ColumnDistributionJackChordChecksExist()
    {
        // Verify the three mania-specific checks are instantiable and each reports possible templates.
        var columnCheck = new CheckManiaColumnDistribution();
        var jackCheck = new CheckManiaJackLimit();
        var chordCheck = new CheckManiaChordDensity();

        Assert.That(columnCheck.PossibleTemplates, Is.Not.Empty);
        Assert.That(jackCheck.PossibleTemplates, Is.Not.Empty);
        Assert.That(chordCheck.PossibleTemplates, Is.Not.Empty);

        // The verifier aggregates these plus CheckManiaDifficultySettingsRanges.
        var verifier = new AiStudioManiaBeatmapVerifier();
        Assert.That(verifier, Is.Not.Null);

        // Spot-check that the generated beatmap exercises column distribution.
        var beatmap = GenerateManiaBeatmap();
        var ctx = CreateContext(beatmap);

        // These checks should run without throwing even on an AI-generated beatmap.
        Assert.DoesNotThrow(() => columnCheck.Run(ctx).ToList());
        Assert.DoesNotThrow(() => jackCheck.Run(ctx).ToList());
        Assert.DoesNotThrow(() => chordCheck.Run(ctx).ToList());
    }

    [Test]
    public void DifficultySettingsRangeCheckRunsWithoutThrowing()
    {
        var beatmap = GenerateManiaBeatmap();
        var ctx = CreateContext(beatmap);
        var check = new CheckManiaDifficultySettingsRanges();

        Assert.DoesNotThrow(() => check.Run(ctx).ToList());
    }

    // Build a mania beatmap via the generator (FakeAudioAnalyzer) then load as WorkingBeatmap context.
    private static IBeatmap GenerateManiaBeatmap()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-mania-check-{Guid.NewGuid():N}");
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

            var result = new ManiaMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            // Construct a playable beatmap for verifier: reuse simple ManiaBeatmap with generated hit objects.
            // Decode the exported file to get a fully populated beatmap.
            using var reader = new osu.Game.IO.LineBufferedReader(File.OpenRead(result.OutputFilePath!));
            var decoded = new osu.Game.Beatmaps.Formats.LegacyBeatmapDecoder().Decode(reader, Array.Empty<osu.Game.IO.LineBufferedReader>());

            // Wrap decoded hit objects into a ManiaBeatmap for verifier (which checks ManiaHitObject types).
            var mania = new ManiaBeatmap(new StageDefinition(4));
            mania.BeatmapInfo = decoded.BeatmapInfo;
            mania.ControlPointInfo = decoded.ControlPointInfo;
            foreach (var obj in decoded.HitObjects.OfType<ManiaHitObject>())
                mania.HitObjects.Add(obj);

            return mania;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static BeatmapVerifierContext CreateContext(IBeatmap beatmap)
        => new BeatmapVerifierContext(beatmap, new TestWorkingBeatmap(beatmap));

    private sealed class TestWorkingBeatmap : WorkingBeatmap
    {
        private readonly IBeatmap beatmap;

        public TestWorkingBeatmap(IBeatmap beatmap)
            : base(beatmap.BeatmapInfo, null!)
        {
            this.beatmap = beatmap;
        }

        protected override IBeatmap GetBeatmap() => beatmap;

        public override Texture GetBackground() => null!;

        protected override Track GetBeatmapTrack() => null!;

        protected override ISkin GetSkin() => new LegacyBeatmapSkin(BeatmapInfo, null);

        public override Stream GetStream(string storagePath) => null!;
    }
}
