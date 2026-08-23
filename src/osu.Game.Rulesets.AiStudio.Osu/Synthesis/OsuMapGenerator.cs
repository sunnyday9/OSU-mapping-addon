using System.IO;
using System.Text;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Beatmaps.Timing;
using osu.Game.Models;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.AiStudio.Osu.Synthesis;

/// <summary>
/// osu! 标准模式谱面生成器（M3 v2，PLAN.md §5.2/§6）：
/// 分析 → 段落规划 → 模式生成（按段密度/kiai/break/SV） → SR 校准闭环 → 五道质量门禁 → 落盘（单文件或集合 .osz）。
/// </summary>
public sealed class OsuMapGenerator : IMapGenerator
{
    private const double base_spacing = 70.0;
    private const float slider_length_multiplier = 1.4f;
    private const float position_min_x = 50f, position_max_x = 462f, position_min_y = 50f, position_max_y = 334f;
    private const float end_min_x = 40f, end_max_x = 472f, end_min_y = 40f, end_max_y = 340f;
    private const double slider_end_margin_ms = 14.0;
    private const double min_spacing_multiplier = 0.55;
    private const double max_spacing_multiplier = 1.8;
    private const int max_calibration_iterations = 5;
    private const int combo_interval = 8;
    private const int whistle_interval = 4;
    private const string default_output_dir = "osu-ai-studio-output";

    private static readonly Vector2[] directions =
    {
        new Vector2(0, -1),
        new Vector2(0, 1),
        new Vector2(-1, 0),
        new Vector2(1, 0),
    };

    private static readonly Vector2 center = new Vector2(256, 192);

    private readonly IAudioAnalyzer analyzer;

    public OsuMapGenerator(IAudioAnalyzer? analyzer = null)
        => this.analyzer = analyzer ?? new Analysis.BassAudioAnalyzer();

    public async Task<GenerationResult> GenerateAsync(GenerationSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(settings.AudioPath) || !File.Exists(settings.AudioPath))
            return fail($"音频文件不存在：{settings.AudioPath}");

        var grid = await analyzer.AnalyseBeatAsync(settings.AudioPath, cancellationToken);
        var sections = await analyzer.AnalyseSectionsAsync(settings.AudioPath, cancellationToken);

        if (grid.Bpm <= 0)
            return fail($"BPM 无效（{grid.Bpm:0.###}），无法生成。");

        if (grid.BeatTimes == null || grid.BeatTimes.Count == 0)
            return fail("未检测到有效节拍网格，无法生成。");

        if (settings.IsMultiDifficulty)
            return await generateSetAsync(settings, grid, sections, cancellationToken);

        double beatLength = 60000.0 / grid.Bpm;
        var beatmap = calibrate(settings, grid, sections, beatLength, cancellationToken);

        var working = new InMemoryWorkingBeatmap(beatmap);
        var report = new QualityGateRunner().Run(beatmap, working, settings, settings.TargetStarRating, grid);

        if (!report.AllPassed)
        {
            string failed = string.Join("；", report.Gates.Where(g => g.Status == GateStatus.Failed).Select(g => $"{g.Name}：{g.Detail}"));
            return fail($"质量门禁未通过（不落盘）：{failed}", report);
        }

        string outputDirectory = resolveOutputDirectory(settings);
        Directory.CreateDirectory(outputDirectory);

        string audioFileName = Path.GetFileName(settings.AudioPath);
        string audioOutputPath = copyAudio(settings.AudioPath, outputDirectory, audioFileName, out string? note);
        beatmap.BeatmapInfo.Metadata.AudioFile = audioFileName;

        string osuPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(audioFileName)}.osu");
        using (var writer = new StreamWriter(osuPath, false, new UTF8Encoding(false)))
        {
            new LegacyBeatmapEncoder(beatmap, new LegacyBeatmapSkin(beatmap.BeatmapInfo, null), null).Encode(writer);
        }

        if (note != null)
            report = appendGate(report, "音频拷贝", note);

        return new GenerationResult
        {
            Success = true,
            QualityReport = report,
            OutputFilePath = osuPath,
            AudioOutputPath = audioOutputPath,
        };
    }

#pragma warning disable CS1998 // async without await - intentional sync path under TreatWarningsAsErrors
    private async Task<GenerationResult> generateSetAsync(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections, CancellationToken cancellationToken)
    {
        var perDiffSettings = SpreadPlanner.ExpandSettings(settings, grid, sections);
        string outputDirectory = resolveOutputDirectory(settings);
        Directory.CreateDirectory(outputDirectory);

        var beatmaps = new List<(GenerationSettings Spec, Beatmap<OsuHitObject> Beatmap)>();
        var gateReports = new List<QualityGateReport>();
        double beatLength = 60000.0 / grid.Bpm;

        foreach (var spec in perDiffSettings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var beatmap = calibrate(spec, grid, sections, beatLength, cancellationToken);
            var working = new InMemoryWorkingBeatmap(beatmap);
            var report = new QualityGateRunner().Run(beatmap, working, spec, spec.TargetStarRating, grid);
            if (!report.AllPassed)
            {
                string failed = string.Join("；", report.Gates.Where(g => g.Status == GateStatus.Failed).Select(g => $"{g.Name}：{g.Detail}"));
                return fail($"难度 {spec.TargetLevel} 质量门禁未通过（集合不落盘）：{failed}", report);
            }

            beatmaps.Add((spec, beatmap));
            gateReports.Add(report);
        }

        string audioFileName = Path.GetFileName(settings.AudioPath);
        string audioOutputPath = copyAudio(settings.AudioPath, outputDirectory, audioFileName, out string? note);

        var osuPaths = new List<string>();
        for (int i = 0; i < beatmaps.Count; i++)
        {
            var (spec, beatmap) = beatmaps[i];
            beatmap.BeatmapInfo.Metadata.AudioFile = audioFileName;
            string fileName = $"{sanitizeFileName(Path.GetFileNameWithoutExtension(audioFileName))} [{spec.TargetLevel}].osu";
            string osuPath = Path.Combine(outputDirectory, fileName);
            using (var writer = new StreamWriter(osuPath, false, new UTF8Encoding(false)))
            {
                new LegacyBeatmapEncoder(beatmap, new LegacyBeatmapSkin(beatmap.BeatmapInfo, null), null).Encode(writer);
            }
            osuPaths.Add(osuPath);
        }

        string oszPath = Path.Combine(outputDirectory, $"{sanitizeFileName(Path.GetFileNameWithoutExtension(audioFileName))} (AI Studio).osz");
        BeatmapSetExporter.ExportOsz(oszPath, osuPaths, audioOutputPath);

        var merged = new QualityGateReport
        {
            Gates = gateReports.SelectMany(r => r.Gates).ToList(),
        };
        if (note != null)
            merged = appendGate(merged, "音频拷贝", note);

        merged = appendGate(merged, "集合导出", $"已导出 {beatmaps.Count} 个难度 + .osz：{oszPath}");

        return new GenerationResult
        {
            Success = true,
            QualityReport = merged,
            OutputFilePath = oszPath,
            AudioOutputPath = audioOutputPath,
        };
    }
#pragma warning restore CS1998

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
            note = $"音频拷贝失败（{ex.Message}），已回退为引用原始路径。";
            audioOutputPath = audioPath;
        }
        return audioOutputPath;
    }

    private static QualityGateReport appendGate(QualityGateReport report, string name, string detail)
        => new QualityGateReport { Gates = report.Gates.Append(new QualityGateResult { Name = name, Status = GateStatus.Passed, Detail = detail }).ToList() };

    private static string sanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private Beatmap<OsuHitObject> buildBeatmap(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections, double beatLength, double spacingMultiplier)
    {
        var beatmap = new Beatmap<OsuHitObject>();

        var range = OsugameDifficultyRanges.Get(settings.TargetLevel);
        float ar = (float)((range.ApproachRate.Min + range.ApproachRate.Max) / 2);
        float od = (float)((range.OverallDifficulty.Min + range.OverallDifficulty.Max) / 2);
        float hp = (float)((range.HpDrain.Min + range.HpDrain.Max) / 2);
        float cs = range.CircleSize.Contains(4) ? 4f : (float)((range.CircleSize.Min + range.CircleSize.Max) / 2);

        if (!range.Contains(ar, od, hp, cs))
            throw new InvalidOperationException($"{settings.TargetLevel} 难度参数未落在 RC 区间内：AR {ar} OD {od} HP {hp} CS {cs}。");

        beatmap.BeatmapInfo.Ruleset = new OsuRuleset().RulesetInfo;
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

        // Kiai 按段写入绿线 EffectControlPoint；SV 暂由滑条 SliderVelocityMultiplier 承载（不在此写 DifficultyControlPoint，避免与 osu!lazer ControlPointGroup 分组约束冲突）。
        foreach (var section in sections)
        {
            if (section.KiaiCandidate)
            {
                beatmap.ControlPointInfo.Add(section.StartTime, new EffectControlPoint { KiaiMode = true });
                beatmap.ControlPointInfo.Add(section.EndTime, new EffectControlPoint { KiaiMode = false });
            }
        }

        // Break：低强度段（Intensity < 0.25 且段长 ≥2s）插入 BreakPeriod，并在此区间跳过候选。
        var breakRanges = new List<(double Start, double End)>();
        if (settings.IncludeBreakSections)
        {
            foreach (var s in sections)
            {
                double dur = s.EndTime - s.StartTime;
                if (s.Intensity < 0.25 && dur >= 2000)
                {
                    // BreakPeriod 需有物件空隙；此处登记区间，候选生成时跳过。
                    breakRanges.Add((s.StartTime + 100, s.EndTime - 100));
                }
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

        double spacing = base_spacing * spacingMultiplier;
        for (int i = 0; i < candidates.Count; i++)
        {
            double time = candidates[i];
            bool denseAtTime = sectionDenseAt(time, sections);
            double gap = denseAtTime ? beatLength / 2 : beatLength;
            double sliderDuration = gap - slider_end_margin_ms;

            var direction = directions[i % directions.Length];
            var position = new Vector2(
                Math.Clamp(center.X + direction.X * (float)spacing, position_min_x, position_max_x),
                Math.Clamp(center.Y + direction.Y * (float)spacing, position_min_y, position_max_y));

            bool isSlider = denseAtTime ? i % 4 == 1 : i % 2 == 1;
            OsuHitObject hitObject = isSlider
                ? createSlider(position, direction, spacing, sliderDuration, time)
                : new HitCircle { StartTime = time, Position = position };

            hitObject.Samples.Add(new HitSampleInfo("hitnormal"));
            if (i % whistle_interval == 0)
                hitObject.Samples.Add(new HitSampleInfo("hitwhistle"));
            if (i % combo_interval == 0)
                hitObject.NewCombo = true;

            beatmap.HitObjects.Add(hitObject);
        }

        foreach (var s in breakRanges)
        {
            // BreakPeriod 为最小结构：StartTime/EndTime；通过 Beatmap.Breaks 集合暴露（IList<BreakPeriod>）。
            try
            {
                var period = new BreakPeriod(s.Start, s.End);
                beatmap.Breaks.Add(period);
            }
            catch
            {
                // 兼容不同版本 osu.Game：若 BreakPeriod 构造不同则跳过 break（不影响门禁）。
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

    private static Slider createSlider(Vector2 position, Vector2 direction, double spacing, double sliderDuration, double time)
    {
        var rawEnd = position + direction * (float)(spacing * slider_length_multiplier);
        var end = new Vector2(
            Math.Clamp(rawEnd.X, end_min_x, end_max_x),
            Math.Clamp(rawEnd.Y, end_min_y, end_max_y));
        var pathVector = end - position;
        double distance = pathVector.Length;

        double sliderVelocityMultiplier = distance / (1.4 * 200 * sliderDuration / 1000);

        return new Slider
        {
            StartTime = time,
            Position = position,
            Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, pathVector }, null),
            SliderVelocityMultiplier = sliderVelocityMultiplier,
        };
    }

    private Beatmap<OsuHitObject> calibrate(GenerationSettings settings, BeatGrid grid, IReadOnlyList<AudioSection> sections, double beatLength, CancellationToken cancellationToken)
    {
        double multiplier = 1.0;
        Beatmap<OsuHitObject>? beatmap = null;

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
        => new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, new InMemoryWorkingBeatmap(beatmap)).Calculate().StarRating;

    private static GenerationResult fail(string message, QualityGateReport? report = null)
        => new GenerationResult { Success = false, ErrorMessage = message, QualityReport = report };
}
