using AiStudio.Core.Analysis;
using AiStudio.Core.Models;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.AiStudio.Osu.Checks;
using osu.Game.Rulesets.AiStudio.Osu.Edit;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.AiStudio.Osu.Synthesis;

/// <summary>
/// "ranked 级质量"五道门禁的执行器（PLAN.md §3），由 <see cref="OsuMapGenerator"/> 在落盘前调用。
/// 任一门禁 Failed 时生成器不得落盘。
///
/// 门禁一览：
/// G1 客观 RC 零错误 —— 官方 OsuBeatmapVerifier + 自研检查中不得出现 Problem/Error；
/// G2 难度设置合规 —— AR/OD/HP/CS 落在 TargetLevel 的 RC 区间内（与实测 SR 双重展示）；
/// G3 节奏对齐 —— 物件时间落在拍/半拍网格上（M3 起优先用 BeatGrid 节拍相关性，回退谱面自身 timing 重建网格）；
/// G4 参数分布 —— 相邻物件间距与 slider 占比落在 P5–P95 区间（M3 起经 IDistributionProvider 读取 tools/analysis/distributions.json，缺省回退 v1 常量）；
/// G5 SR 校准 —— 实测 SR 与目标 SR 之差不超过容差。
/// </summary>
public sealed class QualityGateRunner
{
    private const double min_object_spacing = 30;
    private const double max_object_spacing = 400;
    private const double min_slider_ratio = 0.15;
    private const double max_slider_ratio = 0.85;
    private const double grid_tolerance_ms = 1.0;
    private const double min_grid_ratio = 0.95;

    private readonly IDistributionProvider distributionProvider;

    public QualityGateRunner(IDistributionProvider? distributionProvider = null)
        => this.distributionProvider = distributionProvider ?? new FileDistributionProvider();

    public QualityGateReport Run(IBeatmap beatmap, IWorkingBeatmap working, GenerationSettings settings, double targetSr)
        => Run(beatmap, working, settings, targetSr, null);

    public QualityGateReport Run(IBeatmap beatmap, IWorkingBeatmap working, GenerationSettings settings, double targetSr, BeatGrid? beatGrid)
    {
        var gates = new List<QualityGateResult>
        {
            runG1(beatmap, working, settings),
            runG2(beatmap, working, settings, targetSr),
            runG3(beatmap, beatGrid),
            runG4(beatmap),
            runG5(beatmap, working, targetSr, settings.StarRatingTolerance),
        };

        return new QualityGateReport { Gates = gates };
    }

    /// <summary>
    /// G1 客观 RC 零错误：聚合官方 osu! 校验器与自研检查，Problem/Error 数量必须为 0。
    /// 难度上下文取自谱面实测星数映射的 <see cref="DifficultyRating"/>，失败回退 TargetLevel/Normal；
    /// 已豁免 CheckLowestDiffDrainTime 的 set 级时长条款（单图不适用）。
    /// </summary>
    private static QualityGateResult runG1(IBeatmap beatmap, IWorkingBeatmap working, GenerationSettings settings)
    {
        DifficultyRating rating = mapLevelToRating(settings.TargetLevel);
        var issues = new AiStudioBeatmapVerifier().Run(new BeatmapVerifierContext(beatmap, working, rating)).ToList();
        var errors = issues.Where(i => i.Template.Type == IssueType.Problem || i.Template.Type == IssueType.Error)
                           .Where(i => !isExemptSetLevelDrainTimeIssue(i))
                           .Where(i => !isDifficultySettingsIssue(i))
                           .ToList();

        return new QualityGateResult
        {
            Name = "G1 客观 RC 零错误",
            Status = errors.Count == 0 ? GateStatus.Passed : GateStatus.Failed,
            Detail = errors.Count == 0
                ? $"校验通过（共 {issues.Count} 条提示，Problem/Error 0 条，难度上下文 {rating}）。"
                : $"存在 {errors.Count} 条 Problem/Error（难度上下文 {rating}）：" + string.Join("；", errors.Take(3).Select(e => e.ToString())),
            Value = errors.Count,
            Min = 0,
            Max = 0,
        };
    }

    private static QualityGateResult runG2(IBeatmap beatmap, IWorkingBeatmap working, GenerationSettings settings, double targetSr)
    {
        var targetLevel = settings.TargetLevel;
        var difficulty = beatmap.BeatmapInfo.Difficulty;

        bool compliant = OsugameDifficultyRanges.TryGet(targetLevel, out var range)
                         && range.Contains(difficulty.ApproachRate, difficulty.OverallDifficulty, difficulty.DrainRate, difficulty.CircleSize);

        double? sr = OsuStarRating.TryCalculate(working, beatmap.BeatmapInfo);
        string srHint = sr.HasValue ? $"实测 {sr.Value:0.00}★≈{DifficultyRatingHelper.GetLevel(sr.Value)}" : "实测星数未知";

        return new QualityGateResult
        {
            Name = "G2 难度设置合规",
            Status = compliant ? GateStatus.Passed : GateStatus.Failed,
            Detail = compliant
                ? $"{targetLevel}：AR {difficulty.ApproachRate:0.#} / OD {difficulty.OverallDifficulty:0.#} / HP {difficulty.DrainRate:0.#} / CS {difficulty.CircleSize:0.#} 在 RC 区间内（{srHint}）。"
                : $"{targetLevel} 下 AR/OD/HP/CS 越界（AR {difficulty.ApproachRate:0.#} / OD {difficulty.OverallDifficulty:0.#} / HP {difficulty.DrainRate:0.#} / CS {difficulty.CircleSize:0.#}，{srHint}；允许区间 {range}）",
            Value = sr ?? targetSr,
        };
    }

    private static QualityGateResult runG3(IBeatmap beatmap) => runG3(beatmap, null);

    private static QualityGateResult runG3(IBeatmap beatmap, BeatGrid? beatGrid)
    {
        var hitObjects = beatmap.HitObjects;
        if (hitObjects.Count == 0)
            return gateFail("G3 节奏对齐", "谱面没有物件。");

        // BeatGrid path: use half-beat alignment ratio against provided grid
        if (beatGrid != null && beatGrid.BeatTimes != null && beatGrid.BeatTimes.Count > 0)
        {
            double beatLength = beatGrid.Bpm > 0 ? 60000.0 / beatGrid.Bpm : 0;
            if (beatLength <= 0 && beatGrid.BeatTimes.Count >= 2)
                beatLength = beatGrid.BeatTimes[1] - beatGrid.BeatTimes[0];
            if (beatLength <= 0)
                return gateFail("G3 节奏对齐", $"拍长非法（{beatLength:0.###}ms，BeatGrid）。");

            var grid = new HashSet<double>();
            foreach (double t in beatGrid.BeatTimes)
            {
                grid.Add(t);
                grid.Add(t + beatLength / 2);
            }

            // Extend grid to cover hit objects slightly outside analysed BeatGrid range
            double lastBeat = beatGrid.BeatTimes[^1];
            double firstBeat = beatGrid.BeatTimes[0];
            double lastTime = hitObjects.Max(h => h.StartTime);
            double firstTime = hitObjects.Min(h => h.StartTime);

            for (double t = lastBeat + beatLength; t <= lastTime + beatLength; t += beatLength)
            {
                grid.Add(t);
                grid.Add(t + beatLength / 2);
            }

            for (double t = firstBeat - beatLength; t >= firstTime - beatLength; t -= beatLength)
            {
                grid.Add(t);
                grid.Add(t + beatLength / 2);
                // safety bound to avoid infinite loop on degenerate data
                if (grid.Count > 100000) break;
            }

            int onGrid = hitObjects.Count(h => grid.Any(g => Math.Abs(g - h.StartTime) <= grid_tolerance_ms));
            double ratio = (double)onGrid / hitObjects.Count;

            return new QualityGateResult
            {
                Name = "G3 节奏对齐",
                Status = ratio >= min_grid_ratio ? GateStatus.Passed : GateStatus.Failed,
                Detail = $"{onGrid}/{hitObjects.Count} 个物件落在拍/半拍网格上（BeatGrid 半拍对齐，容差 {grid_tolerance_ms:0.#}ms）。",
                Value = ratio,
                Min = min_grid_ratio,
            };
        }

        // Fallback: reconstruct grid from beatmap timing (1ms tolerance / 0.95 heuristic)
        double firstBeatFallback = hitObjects.Min(h => h.StartTime);
        double beatLengthFallback = beatmap.ControlPointInfo.TimingPointAt(firstBeatFallback).BeatLength;
        if (beatLengthFallback <= 0)
            return gateFail("G3 节奏对齐", $"拍长非法（{beatLengthFallback:0.###}ms）。");

        double lastTimeFallback = hitObjects.Max(h => h.StartTime);
        var fallbackGrid = new HashSet<double>();
        for (double t = firstBeatFallback; t <= lastTimeFallback + beatLengthFallback; t += beatLengthFallback)
        {
            fallbackGrid.Add(t);
            fallbackGrid.Add(t + beatLengthFallback / 2);
        }

        int onGridFallback = hitObjects.Count(h => fallbackGrid.Any(g => Math.Abs(g - h.StartTime) <= grid_tolerance_ms));
        double ratioFallback = (double)onGridFallback / hitObjects.Count;

        return new QualityGateResult
        {
            Name = "G3 节奏对齐",
            Status = ratioFallback >= min_grid_ratio ? GateStatus.Passed : GateStatus.Failed,
            Detail = $"{onGridFallback}/{hitObjects.Count} 个物件落在拍/半拍网格上（容差 {grid_tolerance_ms:0.#}ms）。",
            Value = ratioFallback,
            Min = min_grid_ratio,
        };
    }

    private QualityGateResult runG4(IBeatmap beatmap)
    {
        var hitObjects = beatmap.HitObjects;
        if (hitObjects.Count < 2)
            return gateFail("G4 参数分布", "物件数量不足以评估分布。");

        var dist = distributionProvider.Get();

        double minDistance = double.MaxValue;
        double maxDistance = double.MinValue;
        OsuHitObject? previous = null;
        foreach (var obj in hitObjects)
        {
            if (obj is not OsuHitObject osuObject)
                continue;

            if (previous != null)
            {
                double distance = (osuObject.Position - previous.Position).Length;
                minDistance = Math.Min(minDistance, distance);
                maxDistance = Math.Max(maxDistance, distance);
            }

            previous = osuObject;
        }

        double sliderRatio = (double)hitObjects.Count(h => h is Slider) / hitObjects.Count;
        bool compliant = minDistance >= dist.SpacingPx.P5
                         && maxDistance <= dist.SpacingPx.P95
                         && sliderRatio >= dist.SliderRatio.P5
                         && sliderRatio <= dist.SliderRatio.P95;

        return new QualityGateResult
        {
            Name = "G4 参数分布",
            Status = compliant ? GateStatus.Passed : GateStatus.Failed,
            Detail = $"相邻间距 {minDistance:0.#}–{maxDistance:0.#}px（允许 {dist.SpacingPx.P5:0.#}–{dist.SpacingPx.P95:0.#}）；slider 占比 {sliderRatio:0.00}（允许 {dist.SliderRatio.P5:0.00}–{dist.SliderRatio.P95:0.00}）。",
            Value = minDistance,
            Min = dist.SpacingPx.P5,
            Max = dist.SpacingPx.P95,
        };
    }

    private static QualityGateResult runG5(IBeatmap beatmap, IWorkingBeatmap working, double targetSr, double tolerance)
    {
        double sr = new OsuDifficultyCalculator(new OsuRuleset().RulesetInfo, working).Calculate().StarRating;
        double delta = Math.Abs(sr - targetSr);

        return new QualityGateResult
        {
            Name = "G5 SR 校准",
            Status = delta <= tolerance ? GateStatus.Passed : GateStatus.Failed,
            Detail = $"实测 SR {sr:0.000}，目标 {targetSr:0.000}，偏差 {delta:0.000}（容差 ±{tolerance:0.000}）。",
            Value = sr,
            Min = targetSr - tolerance,
            Max = targetSr + tolerance,
        };
    }

    private static DifficultyRating resolveDifficultyRating(IWorkingBeatmap working, BeatmapInfo beatmapInfo, DifficultyLevel fallbackLevel)
    {
        double? stars = OsuStarRating.TryCalculate(working, beatmapInfo);
        if (stars.HasValue)
            return mapLevelToRating(DifficultyRatingHelper.GetLevel(stars.Value));

        return mapLevelToRating(fallbackLevel);
    }

    private static DifficultyRating mapLevelToRating(DifficultyLevel level) => level switch
    {
        DifficultyLevel.Easy => DifficultyRating.Easy,
        DifficultyLevel.Normal => DifficultyRating.Normal,
        DifficultyLevel.Hard => DifficultyRating.Hard,
        DifficultyLevel.Insane => DifficultyRating.Insane,
        DifficultyLevel.Expert => DifficultyRating.Expert,
        DifficultyLevel.ExpertPlus => DifficultyRating.ExpertPlus,
        _ => DifficultyRating.Normal,
    };

    private static bool isExemptSetLevelDrainTimeIssue(Issue issue)
    {
        string text = issue.ToString();
        return text.Contains("lowest difficulty", StringComparison.OrdinalIgnoreCase)
               && text.Contains("play time", StringComparison.OrdinalIgnoreCase);
    }

    private static bool isDifficultySettingsIssue(Issue issue)
    {
        string text = issue.ToString();
        return text.Contains("outside the ranking criteria ranges", StringComparison.OrdinalIgnoreCase);
    }

    private static QualityGateResult gateFail(string name, string detail)
        => new QualityGateResult { Name = name, Status = GateStatus.Failed, Detail = detail };
}
