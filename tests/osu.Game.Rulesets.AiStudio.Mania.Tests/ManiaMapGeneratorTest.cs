using System.IO;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.AiStudio.Mania.Synthesis;

namespace osu.Game.Rulesets.AiStudio.Mania.Tests;

[TestFixture]
public class ManiaMapGeneratorTest
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
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-mania-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var settings = CreateSettings(audioPath, outputDir);
            var result = new ManiaMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.OutputFilePath, Is.Not.Null);
            Assert.That(result.OutputFilePath, Does.EndWith(".osu"));
            Assert.That(File.Exists(result.OutputFilePath), Is.True);

            // Roundtrip decode: must have hit objects.
            var decoded = Decode(result.OutputFilePath!);
            Assert.That(decoded.HitObjects, Is.Not.Empty);
            Assert.That(decoded.HitObjects.Count, Is.GreaterThan(0));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void DecodedBeatmapHasObjects()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-mania-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var result = Generate(audioPath, outputDir);
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            var decoded = Decode(result.OutputFilePath!);
            Assert.That(decoded.HitObjects.Count, Is.GreaterThan(10), "Expected many objects from 60-beat grid (dense half-beats).");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void MissingAudioFails()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-mania-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string missing = Path.Combine(tempDir, "does_not_exist.mp3");
            var settings = CreateSettings(missing, tempDir);
            var result = new ManiaMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(settings).GetAwaiter().GetResult();

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorMessage, Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public void TagsContainAiGenerated()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"aistudio-mania-{Guid.NewGuid():N}");
        string audioPath = Path.Combine(tempDir, "test_audio.mp3");
        string outputDir = Path.Combine(tempDir, "out");
        Directory.CreateDirectory(tempDir);

        try
        {
            File.WriteAllText(audioPath, "placeholder");

            var result = Generate(audioPath, outputDir);
            Assert.That(result.Success, Is.True, result.ErrorMessage);

            var decoded = Decode(result.OutputFilePath!);
            // Tags may appear on beatmap metadata line in .osu file; check file content or BeatmapInfo.
            string content = File.ReadAllText(result.OutputFilePath!);
            Assert.That(content, Does.Contain("AI generated"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static GenerationResult Generate(string audioPath, string outputDir)
        => new ManiaMapGenerator(new FakeAudioAnalyzer()).GenerateAsync(CreateSettings(audioPath, outputDir)).GetAwaiter().GetResult();

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
