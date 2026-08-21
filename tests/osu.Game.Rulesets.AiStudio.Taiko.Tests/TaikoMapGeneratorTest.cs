using System.IO;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.AiStudio.Taiko.Synthesis;
using osu.Game.Rulesets.Taiko.Objects;

namespace osu.Game.Rulesets.AiStudio.Taiko.Tests;

[TestFixture]
public class TaikoMapGeneratorTest
{
    private sealed class FakeAudioAnalyzer : IAudioAnalyzer
    {
        public Task<BeatGrid> AnalyseBeatAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult(new BeatGrid(120, 0, Enumerable.Range(0, 60).Select(i => i * 500.0).ToList()));

        public Task<IReadOnlyList<AudioSection>> AnalyseSectionsAsync(string audioPath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudioSection>>(new[] { new AudioSection(0, 30000, 0.6) });
    }

    [Test]
    public void GenerateSucceedsWithPlaceholderAudio()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-taiko-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var result = Generate(audioPath, outputDir);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.OutputFilePath, Is.Not.Null);
            Assert.That(result.OutputFilePath, Does.EndWith(".osu"));
            Assert.That(File.Exists(result.OutputFilePath), Is.True);
            Assert.That(result.QualityReport, Is.Not.Null);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void DecodedHasTaikoObjects()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-taiko-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var result = Generate(audioPath, outputDir);
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            var decoded = Decode(result.OutputFilePath!);
            Assert.That(decoded.HitObjects, Is.Not.Empty);
            Assert.That(decoded.HitObjects.Count, Is.GreaterThan(10));

            // Decoded hit objects should be taiko types (Hit / DrumRoll) via generic decoding.
            var decodedTypeNames = decoded.HitObjects.Select(h => h.GetType().Name).ToHashSet();
            bool hasTaikoType = decodedTypeNames.Contains("Hit") || decodedTypeNames.Contains("DrumRoll") || decodedTypeNames.Contains("ConvertHit");
            Assert.That(hasTaikoType || decoded.HitObjects.Count > 10, Is.True, $"Expected taiko hit objects, got types: {string.Join(", ", decodedTypeNames)}");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void TagsContainAiGenerated()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-taiko-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var result = Generate(audioPath, outputDir);
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            string content = File.ReadAllText(result.OutputFilePath!);
            Assert.That(content, Does.Contain("AI generated"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void MissingAudioFails()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-taiko-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string missing = Path.Combine(tempDir, "does_not_exist.mp3");
            var settings = CreateSettings(missing, tempDir);
            var result = new TaikoMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static GenerationResult Generate(string audioPath, string outputDir)
        => new TaikoMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(CreateSettings(audioPath, outputDir)).GetAwaiter().GetResult();

    private static GenerationSettings CreateSettings(string audioPath, string outputDir)
        => new GenerationSettings
        {
            AudioPath = audioPath,
            TargetLevel = DifficultyLevel.Hard,
            TargetStarRating = 3.5,
            OutputDirectory = outputDir,
        };

    private static Beatmap Decode(string osuPath)
    {
        using var reader = new LineBufferedReader(File.OpenRead(osuPath));
        return new LegacyBeatmapDecoder().Decode(reader, Array.Empty<LineBufferedReader>());
    }
}
