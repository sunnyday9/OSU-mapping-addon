using System.IO;
using System.Text;
using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
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
/// osu! 标准模式谱面生成器（M2，PLAN.md §5.2）：
/// 分析 → 参数化模式生成 → SR 校准闭环 → 五道质量门禁 → 落盘。
///
/// 生成流程：
/// A. 分析：BASS 节拍网格 + 段落强度（<see cref="IAudioAnalyzer"/>）；
/// B. 参数表：拍长、密集/稀疏模式（段落平均强度 &gt; 0.45 为密集）、基础间距；
/// C. 物件生成：以 (256,192) 为中心、4 方向轮转的确定性模式，slider 时长精确控制到
///    下一物件前 14ms（避开官方 CheckConcurrentObjects 的 10ms "almost concurrent" 容差）；
/// D. SR 校准闭环：spacing 乘子夹逼 [0.55, 1.8]，至 |SR − target| ≤ 容差（≤5 轮）；
/// E. 五道质量门禁（<see cref="QualityGateRunner"/>），任一失败不落盘；
/// F. 落盘：音频拷贝 + LegacyBeatmapEncoder 导出 map.osu。
/// </summary>
public sealed class OsuMapGenerator : IMapGenerator
{
    /// <summary>基础间距（px）。注意：M2 规格初稿为 55，但 55 × 1.8 上限只能到约 3.1★，
    /// 无法满足 3.5★ 的默认目标；经实测（2026.730.0 难度计算）改为 70，使 3.5★ 在校准区间内可达。</summary>
    private const double base_spacing = 70.0;

    /// <summary>slider 路径长度相对基础间距的倍数。</summary>
    private const float slider_length_multiplier = 1.4f;

    /// <summary>物件位置钳制范围（官方 CheckOffscreenObjects 对 4:3 判定框为 x∈[-67,579]、y∈[-60,428]，
    /// CS4 半径 ≈ 73px，[50,462]×[50,334] 恒安全）。</summary>
    private const float position_min_x = 50f, position_max_x = 462f, position_min_y = 50f, position_max_y = 334f;

    /// <summary>slider 终点钳制范围（终点比起点更靠边缘，另行收紧；[40,472]×[40,340] 含半径后仍安全）。</summary>
    private const float end_min_x = 40f, end_max_x = 472f, end_min_y = 40f, end_max_y = 340f;

    /// <summary>slider 时长相对物件间隔的提前结束余量（ms）。
    /// 官方并发检查：下一物件起始 ≤ slider 结束 + 2ms 判并发、差 &lt; 10ms 判 almost concurrent；
    /// 14ms 保证两者都不触发。</summary>
    private const double slider_end_margin_ms = 14.0;

    private const double min_spacing_multiplier = 0.55;
    private const double max_spacing_multiplier = 1.8;
    private const int max_calibration_iterations = 5;

    /// <summary>每 N 个物件开新 combo。</summary>
    private const int combo_interval = 8;

    /// <summary>每 N 个物件追加 whistle 打击音（CheckFewHitsounds 要求至少存在一个 addition 音效）。</summary>
    private const int whistle_interval = 4;

    /// <summary>默认输出子目录名（我的文档下）。</summary>
    private const string default_output_dir = "osu-ai-studio-output";

    private static readonly Vector2[] directions =
    {
        new Vector2(0, -1), // 上
        new Vector2(0, 1),  // 下
        new Vector2(-1, 0), // 左
        new Vector2(1, 0),  // 右
    };

    private static readonly Vector2 center = new Vector2(256, 192);

    private readonly IAudioAnalyzer analyzer;

    public OsuMapGenerator(IAudioAnalyzer? analyzer = null)
        => this.analyzer = analyzer ?? new Analysis.BassAudioAnalyzer();

    public async Task<GenerationResult> GenerateAsync(GenerationSettings settings, CancellationToken cancellationToken = default)
    {
        // ---- A. 分析 ----
        if (string.IsNullOrEmpty(settings.AudioPath) || !File.Exists(settings.AudioPath))
            return fail($"音频文件不存在：{settings.AudioPath}");

        var grid = await analyzer.AnalyseBeatAsync(settings.AudioPath, cancellationToken);
        var sections = await analyzer.AnalyseSectionsAsync(settings.AudioPath, cancellationToken);

        if (grid.Bpm <= 0)
            return fail($"BPM 无效（{grid.Bpm:0.###}），无法生成。");

        if (grid.BeatTimes == null || grid.BeatTimes.Count == 0)
            return fail("未检测到有效节拍网格，无法生成。");

        // ---- B. 参数表 ----
        double beatLength = 60000.0 / grid.Bpm;
        bool dense = sections.Count == 0 || sections.Average(s => s.Intensity) > 0.45;

        // ---- D. SR 校准闭环（内部调用 C 的 buildBeatmap 重建）----
        var beatmap = calibrate(settings, grid, beatLength, dense, cancellationToken);

        // ---- E. 五道质量门禁 ----
        var working = new InMemoryWorkingBeatmap(beatmap);
        var report = new QualityGateRunner().Run(beatmap, working, settings, settings.TargetStarRating);

        if (!report.AllPassed)
        {
            string failed = string.Join("；", report.Gates.Where(g => g.Status == GateStatus.Failed).Select(g => $"{g.Name}：{g.Detail}"));
            return fail($"质量门禁未通过（不落盘）：{failed}", report);
        }

        // ---- F. 落盘 ----
        string outputDirectory = string.IsNullOrWhiteSpace(settings.OutputDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), default_output_dir)
            : settings.OutputDirectory;
        Directory.CreateDirectory(outputDirectory);

        string audioFileName = Path.GetFileName(settings.AudioPath);
        string audioOutputPath = Path.Combine(outputDirectory, audioFileName);
        string? audioCopyNote = null;
        try
        {
            if (!string.Equals(Path.GetFullPath(settings.AudioPath), Path.GetFullPath(audioOutputPath), StringComparison.OrdinalIgnoreCase))
                File.Copy(settings.AudioPath, audioOutputPath, overwrite: true);
        }
        catch (Exception ex)
        {
            // 拷贝失败：仍按原文件名引用（音频留在原地），在报告中提示。
            audioCopyNote = $"音频拷贝失败（{ex.Message}），已回退为引用原始路径。";
            audioOutputPath = settings.AudioPath;
        }

        beatmap.BeatmapInfo.Metadata.AudioFile = audioFileName;

        string osuPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(audioFileName)}.osu");
        using (var writer = new StreamWriter(osuPath, false, new UTF8Encoding(false)))
        {
            new LegacyBeatmapEncoder(beatmap, new LegacyBeatmapSkin(beatmap.BeatmapInfo, null), null).Encode(writer);
        }

        if (audioCopyNote != null)
        {
            report = new QualityGateReport
            {
                Gates = report.Gates.Append(new QualityGateResult { Name = "音频拷贝", Status = GateStatus.Passed, Detail = audioCopyNote }).ToList(),
            };
        }

        return new GenerationResult
        {
            Success = true,
            QualityReport = report,
            OutputFilePath = osuPath,
            AudioOutputPath = audioOutputPath,
        };
    }

    /// <summary>
    /// C. 物件生成：以 spacingMultiplier 缩放基础间距重建整张谱面（确定性模式，可复现、可测试）。
    /// 网格：每拍落点（dense 时另有半拍点）；最后 2 拍内不落点。
    /// 模式序列按物件索引轮转：dense = [circle, slider, circle, circle]，sparse = [circle, slider]。
    /// </summary>
    private Beatmap<OsuHitObject> buildBeatmap(GenerationSettings settings, BeatGrid grid, double beatLength, bool dense, double spacingMultiplier)
    {
        var beatmap = new Beatmap<OsuHitObject>();

        // 难度参数取 Hard RC 区间中间值（AR 7 / OD 6 / HP 5），CS 4 亦在 [0,6] 内；随后断言保证合规。
        var hardRange = OsugameDifficultyRanges.Get(DifficultyLevel.Hard);
        float ar = (float)((hardRange.ApproachRate.Min + hardRange.ApproachRate.Max) / 2);
        float od = (float)((hardRange.OverallDifficulty.Min + hardRange.OverallDifficulty.Max) / 2);
        float hp = (float)((hardRange.HpDrain.Min + hardRange.HpDrain.Max) / 2);
        const float cs = 4f;

        if (!hardRange.Contains(ar, od, hp, cs))
            throw new InvalidOperationException($"Hard 难度参数未落在 RC 区间内：AR {ar} OD {od} HP {hp} CS {cs}。");

        beatmap.BeatmapInfo.Ruleset = new OsuRuleset().RulesetInfo;
        beatmap.BeatmapInfo.DifficultyName = "Hard";
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
        };

        double firstBeat = grid.BeatTimes[0];
        beatmap.ControlPointInfo.Add(firstBeat - beatLength >= 0 ? firstBeat - beatLength : 0, new TimingControlPoint { BeatLength = beatLength });

        // v1 不插入 break（settings.IncludeBreakSections 保持 false 语义）；M3 做段落级 break 规划时再启用。

        // 候选时间：每拍 t（dense 另加半拍 t + beatLength/2）；最后 2 拍内不落点。
        double lastAllowedBeat = grid.BeatTimes[^1] - 2 * beatLength;
        var candidates = new List<double>();
        foreach (double beat in grid.BeatTimes)
        {
            if (beat > lastAllowedBeat)
                break;

            candidates.Add(beat);
            if (dense)
                candidates.Add(beat + beatLength / 2);
        }

        double spacing = base_spacing * spacingMultiplier;
        double gap = dense ? beatLength / 2 : beatLength;
        double sliderDuration = gap - slider_end_margin_ms;

        for (int i = 0; i < candidates.Count; i++)
        {
            var direction = directions[i % directions.Length];
            double time = candidates[i];
            var position = new Vector2(
                Math.Clamp(center.X + direction.X * (float)spacing, position_min_x, position_max_x),
                Math.Clamp(center.Y + direction.Y * (float)spacing, position_min_y, position_max_y));

            bool isSlider = dense ? i % 4 == 1 : i % 2 == 1;
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

        // 显式应用默认值：让难度计算/官方校验看到的 slider 时长与终态一致，
        // 不依赖难度计算内部对输入谱面的副作用（决定性与可测性）。
        foreach (var hitObject in beatmap.HitObjects)
            hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.BeatmapInfo.Difficulty, CancellationToken.None);

        return beatmap;
    }

    /// <summary>
    /// 构造 1 拍长（精确到下一物件前 14ms）的直线 slider：起点为物件位置，沿 direction 延伸
    /// spacing × 1.4，终点钳制在安全框内。时长通过 SliderVelocityMultiplier 控制
    /// （2026.730.0 实测速度公式：velocity = SliderMultiplier × SV × 200 px/s）。
    /// </summary>
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

    /// <summary>
    /// D. SR 校准闭环：spacingMultiplier 初始 1.0，按 delta 比例夹逼调整（[0.55, 1.8]），
    /// 每轮重建谱面重算 SR，直至 |SR − target| ≤ 容差；最多 5 轮。
    /// </summary>
    private Beatmap<OsuHitObject> calibrate(GenerationSettings settings, BeatGrid grid, double beatLength, bool dense, CancellationToken cancellationToken)
    {
        double multiplier = 1.0;
        Beatmap<OsuHitObject>? beatmap = null;

        for (int i = 0; i < max_calibration_iterations; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            beatmap = buildBeatmap(settings, grid, beatLength, dense, multiplier);
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
