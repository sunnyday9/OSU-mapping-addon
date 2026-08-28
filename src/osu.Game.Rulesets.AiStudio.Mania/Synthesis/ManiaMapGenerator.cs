using System.IO;
using System.Text;
using AiStudio.Core.Analysis;
using AiStudio.Core.MappingIr.Calibration;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Models;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.AiStudio.Mania.Synthesis;

/// <summary>
/// Mania map generator: IMapGenerator using IAudioAnalyzer with SR calibration.
/// Deterministic: beat grid per section dense, column = i % keyCount (4K default), hold every 4th object duration gap-14ms.
/// SR calibration via ManiaDifficultyCalculator iterative density tuning.
/// </summary>
public sealed class ManiaMapGenerator : IMapGenerator
{
    private const double hold_gap_margin_ms = 14.0;
    private const double min_density_multiplier = 0.5;
    private const double max_density_multiplier = 2.0;

    private readonly IAudioAnalyzer analyzer;

    public ManiaMapGenerator(IAudioAnalyzer? analyzer = null)
        => this.analyzer = analyzer ?? new Analysis.BassAudioAnalyzer();

    public async Task<GenerationResult> GenerateAsync(GenerationSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(settings.AudioPath) || !File.Exists(settings.AudioPath))
            return fail($"Audio file not found: {settings.AudioPath}");

        BeatGrid grid;
        IReadOnlyList<AudioSection> sections;
        try
        {
            grid = await analyzer.AnalyseBeatAsync(settings.AudioPath, cancellationToken);
            sections = await analyzer.AnalyseSectionsAsync(settings.AudioPath, cancellationToken);
        }
        catch (Exception ex)
        {
            return fail($"Audio analysis failed: {ex.Message}");
        }

        if (grid.Bpm <= 0 || grid.BeatTimes.Count == 0)
            return fail($"Invalid BPM ({grid.Bpm:0.###}) or no beats detected.");

        int keyCount = 4;

        double beatLength = 60000.0 / grid.Bpm;
        var beatmap = calibrate(settings, grid, sections, keyCount, beatLength, cancellationToken);

        string outputDirectory = resolveOutputDirectory(settings);
        Directory.CreateDirectory(outputDirectory);

        string audioFileName = Path.GetFileName(settings.AudioPath);
        string audioOutputPath = copyAudio(settings.AudioPath, outputDirectory, audioFileName, out string? note);
        beatmap.BeatmapInfo.Metadata.AudioFile = audioFileName;

        string osuPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(audioFileName)}_mania.osu");
        using (var writer = new StreamWriter(osuPath, false, new UTF8Encoding(false)))
        {
            new LegacyBeatmapEncoder(beatmap, new LegacyBeatmapSkin(beatmap.BeatmapInfo, null), null).Encode(writer);
        }

        double stars = 0;
        try { stars = calculateStarRating(beatmap); } catch { }

        string detail = stars > 0
            ? $"Mania generated at {grid.Bpm:0.##} BPM, {beatmap.HitObjects.Count} objects ({keyCount}K), {stars:0.00}*."
            : $"Mania generated at {grid.Bpm:0.##} BPM, {beatmap.HitObjects.Count} objects ({keyCount}K) Tags=AI generated";
        if (note != null) detail += " " + note;

        var report = new QualityGateReport
        {
            Gates = new[]
            {
                new QualityGateResult { Name = "Mania generation", Status = GateStatus.Passed, Detail = detail },
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

    private ManiaBeatmap buildBeatmap(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections, int keyCount, double densityMultiplier = 1.0)
    {
        var beatmap = new ManiaBeatmap(new StageDefinition(keyCount));

        var range = Models.ManiaDifficultyRanges.Get(settings.TargetLevel);
        float od = (float)((range.OverallDifficulty.Min + range.OverallDifficulty.Max) / 2);
        float hp = (float)((range.HpDrain.Min + range.HpDrain.Max) / 2);
        float ar = (float)((range.ApproachRate.Min + range.ApproachRate.Max) / 2);
        float cs = (float)((range.CircleSize.Min + range.CircleSize.Max) / 2);

        beatmap.BeatmapInfo.Ruleset = new ManiaRuleset().RulesetInfo;
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

        double beatLength = 60000.0 / grid.Bpm;
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

        double lastAllowed = grid.BeatTimes[^1] - beatLength;
        var candidates = new List<double>();
        foreach (double beat in grid.BeatTimes)
        {
            if (beat > lastAllowed) break;

            bool dense = sectionDenseAt(beat, sections, densityMultiplier);
            candidates.Add(beat);
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
            double gap = i + 1 < candidates.Count ? candidates[i + 1] - time : beatLength;
            int column = i % keyCount;

            ManiaHitObject hitObject;
            bool isHold = i % 4 == 3;
            if (isHold)
            {
                double duration = Math.Max(50, gap - hold_gap_margin_ms);
                hitObject = new HoldNote
                {
                    StartTime = time,
                    Column = column,
                    EndTime = time + duration,
                };
            }
            else
            {
                hitObject = new Note
                {
                    StartTime = time,
                    Column = column,
                };
            }

            hitObject.Samples.Add(new HitSampleInfo("hitnormal"));
            beatmap.HitObjects.Add(hitObject);
        }

        foreach (var hitObject in beatmap.HitObjects)
            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);

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

    private ManiaBeatmap calibrate(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections, int keyCount, double beatLength, CancellationToken cancellationToken)
    {
        // 收敛数值搜索统一走 Core 的 DensityScaleSearch（唯一实现，架构走查候选 5）；
        // 密度乘子的领域 clamp 范围保留在此。
        ManiaBeatmap? beatmap = null;
        var search = new DensityScaleSearch { MinScale = min_density_multiplier, MaxScale = max_density_multiplier };
        search.Search(
            settings.TargetStarRating,
            settings.StarRatingTolerance,
            scale =>
            {
                beatmap = buildBeatmap(settings, grid, sections, keyCount, scale);
                return calculateStarRating(beatmap);
            },
            cancellationToken);

        return beatmap!;
    }

    private static double calculateStarRating(IBeatmap beatmap)
        => new ManiaDifficultyCalculator(new ManiaRuleset().RulesetInfo, new ManiaInMemoryWorkingBeatmap(beatmap)).Calculate().StarRating;

    private static string resolveOutputDirectory(GenerationSettings settings) => string.IsNullOrWhiteSpace(settings.OutputDirectory)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "osu-ai-studio-output")
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

    private static GenerationResult fail(string message)
        => new GenerationResult { Success = false, ErrorMessage = message };
}
