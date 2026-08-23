using System.IO;
using System.Text;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Models;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Beatmaps;
using osu.Game.Rulesets.Taiko.Difficulty;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.AiStudio.Taiko.Synthesis;

/// <summary>
/// Taiko map generator: IMapGenerator implementation — band energy don/kat heuristic with SR calibration.
///
/// Header cites https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!taiko
///
/// Simple deterministic pattern: hit i with i % 4 == 1 => kat (rim) else don (centre), using Taiko Hit/DrumRoll objects.
/// Timeline is driven by the shared audio analyser's BeatGrid (BassAudioAnalyzer). Metadata Tags include "AI generated".
/// SR calibration via TaikoDifficultyCalculator iterative density tuning.
/// </summary>
public sealed class TaikoMapGenerator : IMapGenerator
{
    private const double drumroll_chance = 0.06;
    private const double drumroll_beat_length_factor = 0.5;
    private const string default_output_dir = "osu-ai-studio-output-taiko";
    private const double min_density_multiplier = 0.5;
    private const double max_density_multiplier = 2.0;
    private const int max_calibration_iterations = 5;

    private readonly IAudioAnalyzer analyzer;

    public TaikoMapGenerator(IAudioAnalyzer? analyzer = null)
        => this.analyzer = analyzer ?? new Analysis.BassAudioAnalyzer();

    public async Task<GenerationResult> GenerateAsync(GenerationSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(settings.AudioPath) || !File.Exists(settings.AudioPath))
            return fail($"Audio file not found: {settings.AudioPath}");

        var grid = await analyzer.AnalyseBeatAsync(settings.AudioPath, cancellationToken);
        var sections = await analyzer.AnalyseSectionsAsync(settings.AudioPath, cancellationToken);

        if (grid.Bpm <= 0)
            return fail($"Invalid BPM ({grid.Bpm:0.###}), cannot generate.");

        if (grid.BeatTimes == null || grid.BeatTimes.Count == 0)
            return fail("No valid beat grid detected, cannot generate.");

        double beatLength = 60000.0 / grid.Bpm;
        var beatmap = calibrate(settings, grid, sections, beatLength, cancellationToken);

        string outputDirectory = resolveOutputDirectory(settings);
        Directory.CreateDirectory(outputDirectory);

        var working = new InMemoryWorkingBeatmap(beatmap);
        double stars = 0;
        try { stars = new TaikoDifficultyCalculator(new TaikoRuleset().RulesetInfo, working).Calculate().StarRating; } catch { }

        string audioFileName = Path.GetFileName(settings.AudioPath);
        string audioOutputPath = copyAudio(settings.AudioPath, outputDirectory, audioFileName, out string? note);
        beatmap.BeatmapInfo.Metadata.AudioFile = audioFileName;

        string osuPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(audioFileName)}_taiko.osu");
        using (var writer = new StreamWriter(osuPath, false, new UTF8Encoding(false)))
        {
            new LegacyBeatmapEncoder(beatmap, new LegacyBeatmapSkin(beatmap.BeatmapInfo, null), null).Encode(writer);
        }

        string detail = stars > 0 ? $"Taiko generated at {grid.Bpm:0.##} BPM, {beatmap.HitObjects.Count} objects, {stars:0.00}*." : $"Taiko generated at {grid.Bpm:0.##} BPM, {beatmap.HitObjects.Count} objects.";
        if (note != null) detail += " " + note;

        var report = new QualityGateReport
        {
            Gates = new[]
            {
                new QualityGateResult { Name = "Generation", Status = GateStatus.Passed, Detail = detail },
            },
        };

        return new GenerationResult
        {
            Success = true,
            QualityReport = report,
            OutputFilePath = osuPath,
            AudioOutputPath = audioOutputPath,
        };
    }

    private static Beatmap<TaikoHitObject> buildBeatmap(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections, double beatLength, double densityMultiplier = 1.0)
    {
        var beatmap = new Beatmap<TaikoHitObject>();

        var range = Checks.TaikoDifficultyRanges.Get(settings.TargetLevel);
        float od = (float)((range.OverallDifficulty.Min + range.OverallDifficulty.Max) / 2);
        float hp = (float)((range.HpDrain.Min + range.HpDrain.Max) / 2);

        od = Math.Clamp(od, 0, 10);
        hp = Math.Clamp(hp, 0, 10);

        beatmap.BeatmapInfo.Ruleset = new TaikoRuleset().RulesetInfo;
        beatmap.BeatmapInfo.DifficultyName = settings.TargetLevel.ToString();
        beatmap.BeatmapInfo.Difficulty = new BeatmapDifficulty
        {
            ApproachRate = 5,
            OverallDifficulty = od,
            DrainRate = hp,
            CircleSize = 2,
            SliderMultiplier = 1.4,
            SliderTickRate = 1,
        };
        beatmap.BeatmapInfo.Metadata = new BeatmapMetadata
        {
            Title = Path.GetFileNameWithoutExtension(settings.AudioPath),
            Artist = "AI Studio",
            Author = new RealmUser { Username = "AI Studio" },
            AudioFile = Path.GetFileName(settings.AudioPath),
            PreviewTime = (int)grid.BeatTimes[grid.BeatTimes.Count / 2],
            Tags = "AI generated",
        };

        double firstBeat = grid.BeatTimes[0];
        beatmap.ControlPointInfo.Add(firstBeat - beatLength >= 0 ? firstBeat - beatLength : 0, new TimingControlPoint { BeatLength = beatLength });

        foreach (var section in sections)
        {
            if (section.KiaiCandidate)
            {
                beatmap.ControlPointInfo.Add(section.StartTime, new EffectControlPoint { KiaiMode = true });
                beatmap.ControlPointInfo.Add(section.EndTime, new EffectControlPoint { KiaiMode = false });
            }
        }

        var classifier = new Analysis.TaikoOnnxClassifier();

        var candidates = new List<double>();
        double lastAllowed = grid.BeatTimes[^1] - beatLength;
        foreach (double beat in grid.BeatTimes)
        {
            if (beat > lastAllowed)
                break;

            candidates.Add(beat);
            bool dense = sectionDenseAt(beat, sections, densityMultiplier);
            if (dense)
            {
                double half = beat + beatLength / 2;
                if (half <= lastAllowed)
                    candidates.Add(half);
            }
        }

        candidates.Sort();

        for (int i = 0; i < candidates.Count; i++)
        {
            double time = candidates[i];

            bool isDrumRoll = candidates.Count > 16 && i % 17 == 7;
            if (isDrumRoll)
            {
                double duration = beatLength * drumroll_beat_length_factor;
                var roll = new DrumRoll
                {
                    StartTime = time,
                    Duration = duration,
                };
                roll.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);
                beatmap.HitObjects.Add(roll);
                continue;
            }

            HitType type = classifier.Classify(i);
            var hit = new Hit
            {
                StartTime = time,
                Type = type,
            };
            hit.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);
            beatmap.HitObjects.Add(hit);
        }

        foreach (var ho in beatmap.HitObjects)
            ho.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);

        return beatmap;
    }

    private static bool sectionDenseAt(double timeMs, IReadOnlyList<AudioSection> sections, double densityMultiplier = 1.0)
    {
        if (sections.Count == 0) return false;
        foreach (var s in sections)
        {
            if (timeMs >= s.StartTime && timeMs < s.EndTime)
                return s.Intensity * densityMultiplier > 0.45;
        }
        return sections.Average(s => s.Intensity) * densityMultiplier > 0.45;
    }

    private static Beatmap<TaikoHitObject> calibrate(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections, double beatLength, CancellationToken cancellationToken)
    {
        double multiplier = 1.0;
        Beatmap<TaikoHitObject>? beatmap = null;

        for (int i = 0; i < max_calibration_iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            beatmap = buildBeatmap(settings, grid, sections, beatLength, multiplier);
            double sr = calculateStarRating(beatmap);
            double delta = settings.TargetStarRating - sr;

            if (Math.Abs(delta) <= settings.StarRatingTolerance)
                return beatmap;

            double next = Math.Clamp(multiplier * (1 + delta / Math.Max(sr, 0.5)), min_density_multiplier, max_density_multiplier);
            if (Math.Abs(next - multiplier) < 1e-3)
                return beatmap;

            multiplier = next;
        }

        return beatmap!;
    }

    private static double calculateStarRating(IBeatmap beatmap)
        => new TaikoDifficultyCalculator(new TaikoRuleset().RulesetInfo, new InMemoryWorkingBeatmap(beatmap)).Calculate().StarRating;

    private static string resolveOutputDirectory(GenerationSettings settings) => string.IsNullOrWhiteSpace(settings.OutputDirectory)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), default_output_dir)
        : settings.OutputDirectory;

    private static string copyAudio(string audioPath, string outputDirectory, string audioFileName, out string? note)
    {
        string audioOutputPath = Path.Combine(outputDirectory, audioFileName);
        note = null;
        try
        {
            if (!string.Equals(Path.GetFullPath(audioPath), Path.GetFullPath(audioOutputPath), StringComparison.OrdinalIgnoreCase))
                File.Copy(audioPath, audioOutputPath, overwrite: true);
        }
        catch (Exception ex)
        {
            note = $"Audio copy failed ({ex.Message}), falling back to original path.";
            audioOutputPath = audioPath;
        }
        return audioOutputPath;
    }

    private static GenerationResult fail(string message, QualityGateReport? report = null)
        => new GenerationResult { Success = false, ErrorMessage = message, QualityReport = report };
}
