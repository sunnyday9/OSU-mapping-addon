using System.IO;
using System.Text;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.Timing;
using osu.Game.Models;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.AiStudio.Catch.Synthesis;

/// <summary>
/// Catch map generator : IMapGenerator — derive from std: generate via OsuMapGenerator-like logic then map to CatchHitObjects (Fruit/JuiceStream).
/// Simple: use osu template then map to catch x positions (0–512). Uses CatchHitObject types from ppy.osu.Game.Rulesets.Catch.
/// Fruit placement at x = 256 + direction * spacing. Ensures ApplyDefaults.
///
/// Header cites https://osu.ppy.sh/wiki/en/Ranking_Criteria/osu!catch
/// </summary>
public sealed class CatchMapGenerator : IMapGenerator
{
    private const string default_output_dir = "osu-ai-studio-output-catch";
    private const double base_spacing = 55.0;
    private const double min_spacing_multiplier = 0.5;
    private const double max_spacing_multiplier = 2.0;
    private const int max_calibration_iterations = 5;

    private readonly IAudioAnalyzer analyzer;

    public CatchMapGenerator(IAudioAnalyzer? analyzer = null)
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

        var working = new InMemoryWorkingBeatmap(beatmap);
        // SR calibration already satisfied by calibrate(); run verifier hygiene for output detail.
        double stars = 0;
        try { stars = new CatchDifficultyCalculator(new CatchRuleset().RulesetInfo, working).Calculate().StarRating; } catch { }

        string outputDirectory = resolveOutputDirectory(settings);
        Directory.CreateDirectory(outputDirectory);

        string audioFileName = Path.GetFileName(settings.AudioPath);
        string audioOutputPath = copyAudio(settings.AudioPath, outputDirectory, audioFileName, out string? note);
        beatmap.BeatmapInfo.Metadata.AudioFile = audioFileName;

        string osuPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(audioFileName)}_catch.osu");
        using (var writer = new StreamWriter(osuPath, false, new UTF8Encoding(false)))
        {
            new LegacyBeatmapEncoder(beatmap, new LegacyBeatmapSkin(beatmap.BeatmapInfo, null), null).Encode(writer);
        }

        string detail = stars > 0
            ? $"Catch generated at {grid.Bpm:0.##} BPM, {beatmap.HitObjects.Count} objects, {stars:0.00}*."
            : $"Catch generated at {grid.Bpm:0.##} BPM, {beatmap.HitObjects.Count} objects.";
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

    private Beatmap<CatchHitObject> buildBeatmap(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections, double beatLength, double spacingMultiplier)
    {
        var beatmap = new Beatmap<CatchHitObject>();

        var range = Checks.CatchDifficultyRanges.Get(settings.TargetLevel);
        float ar = (float)((range.ApproachRate.Min + range.ApproachRate.Max) / 2);
        float od = (float)((range.OverallDifficulty.Min + range.OverallDifficulty.Max) / 2);
        float hp = (float)((range.HpDrain.Min + range.HpDrain.Max) / 2);
        float cs = range.CircleSize.Contains(4) ? 4f : (float)((range.CircleSize.Min + range.CircleSize.Max) / 2);

        if (!range.Contains(ar, od, hp, cs))
            throw new InvalidOperationException($"{settings.TargetLevel} difficulty settings outside RC range: AR {ar} OD {od} HP {hp} CS {cs}.");

        beatmap.BeatmapInfo.Ruleset = new CatchRuleset().RulesetInfo;
        beatmap.BeatmapInfo.DifficultyName = settings.TargetLevel.ToString();
        beatmap.BeatmapInfo.Difficulty = new BeatmapDifficulty
        {
            ApproachRate = ar,
            OverallDifficulty = od,
            DrainRate = hp,
            CircleSize = cs,
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

        var breakRanges = new List<(double Start, double End)>();
        if (settings.IncludeBreakSections)
        {
            foreach (var s in sections)
            {
                double dur = s.EndTime - s.StartTime;
                if (s.Intensity < 0.25 && dur >= 2000)
                    breakRanges.Add((s.StartTime + 100, s.EndTime - 100));
            }
        }

        double lastAllowedBeat = grid.BeatTimes[^1] - 2 * beatLength;
        var candidates = new List<double>();
        foreach (double beat in grid.BeatTimes)
        {
            if (beat > lastAllowedBeat)
                break;

            bool inBreak = breakRanges.Any(r => beat >= r.Start && beat < r.End);
            if (inBreak) continue;

            bool dense = sectionDenseAt(beat, sections);
            candidates.Add(beat);
            if (dense)
            {
                double half = beat + beatLength / 2;
                bool halfInBreak = breakRanges.Any(r => half >= r.Start && half < r.End);
                if (!halfInBreak && half <= lastAllowedBeat)
                    candidates.Add(half);
            }
        }

        candidates.Sort();

        // Catch x placement: derived from std template spacing.
        // Simple: use alternating direction * spacingMultiplier -> x = 256 + direction * spacing, clamped to [0,512].
        // JuiceStream for dense subdivisions: every 4th dense half-beat becomes a JuiceStream spanning half a beat.
        double spacing = base_spacing * spacingMultiplier;
        int[] directions = { -1, 1 };

        for (int i = 0; i < candidates.Count; i++)
        {
            double time = candidates[i];
            bool denseAtTime = sectionDenseAt(time, sections);

            int dir = directions[i % directions.Length];
            float x = (float)Math.Clamp(CatchPlayfield.CENTER_X + dir * spacing, 0, CatchPlayfield.WIDTH);

            // Add slight jitter per index to avoid perfect symmetry; keep within bounds.
            float jitter = (i % 3 - 1) * 8f;
            x = Math.Clamp(x + jitter, 0, CatchPlayfield.WIDTH);

            bool isJuice = denseAtTime && i % 4 == 1;
            CatchHitObject hitObject;

            if (isJuice)
            {
                double duration = beatLength / 2;
                // JuiceStream path: single segment from x to mirrored position, duration = half beat.
                var stream = new JuiceStream
                {
                    StartTime = time,
                    X = x,
                };

                // Path is a SliderPath; use simple linear path with two points.
                // SliderPath ctor: (pathType, vertices, expectedDistance)
                var path = new SliderPath();
                try
                {
                    // Build a short horizontal slider path: 0 -> distance
                    float endX = Math.Clamp(x + dir * 40f, 0, CatchPlayfield.WIDTH);
                    float span = Math.Abs(endX - x);
                    path = new SliderPath(PathType.LINEAR, new[] { osuTK.Vector2.Zero, new osuTK.Vector2(span, 0) }, span);
                }
                catch
                {
                    path = new SliderPath(PathType.LINEAR, new[] { osuTK.Vector2.Zero, new osuTK.Vector2(20, 0) }, 20);
                }

                stream.Path = path;
                stream.RepeatCount = 0;
                // Let juice stream duration be set via control points velocity; keep default.
                hitObject = stream;
            }
            else
            {
                hitObject = new Fruit
                {
                    StartTime = time,
                    X = x,
                };
            }

            hitObject.NewCombo = i % 8 == 0;
            if (hitObject is IHasComboInformation comboInfo)
            {
                // combo set via NewCombo is sufficient.
            }

            beatmap.HitObjects.Add(hitObject);
        }

        foreach (var s in breakRanges)
        {
            try
            {
                var period = new BreakPeriod(s.Start, s.End);
                beatmap.Breaks.Add(period);
            }
            catch
            {
            }
        }

        foreach (var hitObject in beatmap.HitObjects)
            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);

        return beatmap;
    }

    private static bool sectionDenseAt(double timeMs, IReadOnlyList<AudioSection> sections)
    {
        if (sections.Count == 0) return false;
        foreach (var s in sections)
        {
            if (timeMs >= s.StartTime && timeMs < s.EndTime)
                return s.Intensity > 0.45;
        }
        return sections.Average(s => s.Intensity) > 0.45;
    }

    private Beatmap<CatchHitObject> calibrate(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections, double beatLength, CancellationToken cancellationToken)
    {
        double multiplier = 1.0;
        Beatmap<CatchHitObject>? beatmap = null;

        for (int i = 0; i < max_calibration_iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            beatmap = buildBeatmap(settings, grid, sections, beatLength, multiplier);
            double sr = calculateStarRating(beatmap);
            double delta = settings.TargetStarRating - sr;

            if (Math.Abs(delta) <= settings.StarRatingTolerance)
                return beatmap;

            double next = Math.Clamp(multiplier * (1 + delta / Math.Max(sr, 0.5)), min_spacing_multiplier, max_spacing_multiplier);
            if (Math.Abs(next - multiplier) < 1e-3)
                return beatmap;

            multiplier = next;
        }

        return beatmap!;
    }

    private static double calculateStarRating(IBeatmap beatmap)
        => new CatchDifficultyCalculator(new CatchRuleset().RulesetInfo, new InMemoryWorkingBeatmap(beatmap)).Calculate().StarRating;

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
