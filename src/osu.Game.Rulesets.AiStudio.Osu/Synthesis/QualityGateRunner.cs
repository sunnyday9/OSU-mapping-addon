using AiStudio.Core.Models;
using osu.Game.Beatmaps;
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
/// G2 难度设置合规 —— AR/OD/HP/CS 落在按星数映射的难度等级 RC 区间内；
/// G3 节奏对齐 —— 物件时间落在拍/半拍网格上（v1 用谱面自身 timing 重建网格，代理指标）；
/// G4 参数分布 —— 相邻物件间距与 slider 占比落在合理区间（v1 临时内置区间，
///    语料工具落地前使用；corpus-refresh 工作流届时以真实语料统计替换）；
/// G5 SR 校准 —— 实测 SR 与目标 SR 之差不超过容差。
/// </summary>
public sealed class QualityGateRunner
{
    /// <summary>相邻物件间距下限（px）。</summary>
    private const double min_object_spacing = 30;

    /// <summary>相邻物件间距上限（px）。</summary>
    private const double max_object_spacing = 400;

    /// <summary>slider 占全部物件比例的下限。</summary>
    private const double min_slider_ratio = 0.15;

    /// <summary>slider 占全部物件比例的上限。</summary>
    private const double max_slider_ratio = 0.85;

    /// <summary>网格对齐容差（ms）。</summary>
    private const double grid_tolerance_ms = 1.0;

    /// <summary>G3 要求的最小网格对齐比例。</summary>
    private const double min_grid_ratio = 0.95;

    public QualityGateReport Run(IBeatmap beatmap, IWorkingBeatmap working, GenerationSettings settings, double targetSr)
    {
        var gates = new List<QualityGateResult>
        {
            runG1(beatmap, working),
            runG2(beatmap, targetSr),
            runG3(beatmap),
            runG4(beatmap),
            runG5(beatmap, working, targetSr, settings.StarRatingTolerance),
        };

        return new QualityGateReport { Gates = gates };
    }

    /// <summary>
    /// G1 客观 RC 零错误：聚合官方 osu! 校验器与自研检查，Problem/Error 数量必须为 0。
    /// 注意：以 <see cref="DifficultyRating.Normal"/> 作为解释难度 —— 官方 CheckLowestDiffDrainTime
    /// 的时长条款（Hard 3:30 / Insane 4:15 / Expert 5:00）针对"ranked 提交的谱面集合"的最低难度；
    /// v1 生成的是独立单难度演示谱，该 set 级条款不适用，其余按难度分级早退的检查
    /// （TimeDistanceEquality 等）仍以 Warning 级别正常参与。
    /// </summary>
    private static QualityGateResult runG1(IBeatmap beatmap, IWorkingBeatmap working)
    {
        var issues = new AiStudioBeatmapVerifier().Run(new BeatmapVerifierContext(beatmap, working, DifficultyRating.Normal)).ToList();
        var errors = issues.Where(i => i.Template.Type == IssueType.Problem || i.Template.Type == IssueType.Error).ToList();

        return new QualityGateResult
        {
            Name = "G1 客观 RC 零错误",
            Status = errors.Count == 0 ? GateStatus.Passed : GateStatus.Failed,
            Detail = errors.Count == 0
                ? $"校验通过（共 {issues.Count} 条提示，Problem/Error 0 条）。"
                : $"存在 {errors.Count} 条 Problem/Error：" + string.Join("；", errors.Take(3).Select(e => e.ToString())),
            Value = errors.Count,
            Min = 0,
            Max = 0,
        };
    }

    /// <summary>
    /// G2 难度设置合规：按实测星数映射难度等级，AR/OD/HP/CS 必须落在该等级的 RC 区间内。
    /// </summary>
    private static QualityGateResult runG2(IBeatmap beatmap, double sr)
    {
        var level = DifficultyRatingHelper.GetLevel(sr);
        var difficulty = beatmap.BeatmapInfo.Difficulty;

        bool compliant = OsugameDifficultyRanges.TryGet(level, out var range)
                         && range.Contains(difficulty.ApproachRate, difficulty.OverallDifficulty, difficulty.DrainRate, difficulty.CircleSize);

        return new QualityGateResult
        {
            Name = "G2 难度设置合规",
            Status = compliant ? GateStatus.Passed : GateStatus.Failed,
            Detail = compliant
                ? $"{level}：AR {difficulty.ApproachRate:0.#} / OD {difficulty.OverallDifficulty:0.#} / HP {difficulty.DrainRate:0.#} / CS {difficulty.CircleSize:0.#} 在 RC 区间内。"
                : $"{level} 下 AR/OD/HP/CS 越界（AR {difficulty.ApproachRate:0.#} / OD {difficulty.OverallDifficulty:0.#} / HP {difficulty.DrainRate:0.#} / CS {difficulty.CircleSize:0.#}）。",
            Value = sr,
        };
    }

    /// <summary>
    /// G3 节奏对齐（v1 代理指标）：物件起始时间必须落在拍/半拍网格上（容差 1ms），比例 ≥ 0.95。
    /// 网格由谱面自身的 timing 重建：首拍 = 首个物件时间，拍长 = 该处 TimingControlPoint.BeatLength。
    /// </summary>
    private static QualityGateResult runG3(IBeatmap beatmap)
    {
        var hitObjects = beatmap.HitObjects;
        if (hitObjects.Count == 0)
            return gateFail("G3 节奏对齐", "谱面没有物件。");

        double firstBeat = hitObjects.Min(h => h.StartTime);
        double beatLength = beatmap.ControlPointInfo.TimingPointAt(firstBeat).BeatLength;
        if (beatLength <= 0)
            return gateFail("G3 节奏对齐", $"拍长非法（{beatLength:0.###}ms）。");

        // 网格 = { firstBeat + k * beatLength } ∪ { firstBeat + k * beatLength + beatLength / 2 }
        double lastTime = hitObjects.Max(h => h.StartTime);
        var grid = new HashSet<double>();
        for (double t = firstBeat; t <= lastTime + beatLength; t += beatLength)
        {
            grid.Add(t);
            grid.Add(t + beatLength / 2);
        }

        int onGrid = hitObjects.Count(h => grid.Any(g => Math.Abs(g - h.StartTime) <= grid_tolerance_ms));
        double ratio = (double)onGrid / hitObjects.Count;

        return new QualityGateResult
        {
            Name = "G3 节奏对齐",
            Status = ratio >= min_grid_ratio ? GateStatus.Passed : GateStatus.Failed,
            Detail = $"{onGrid}/{hitObjects.Count} 个物件落在拍/半拍网格上（容差 {grid_tolerance_ms:0.#}ms）。",
            Value = ratio,
            Min = min_grid_ratio,
        };
    }

    /// <summary>
    /// G4 参数分布（v1 临时内置区间，注释说明：语料工具落地前使用，corpus-refresh 工作流届时替换）：
    /// 相邻物件间距 ∈ [30, 400]px，且 slider 占全部物件的比例 ∈ [0.15, 0.85]。
    /// </summary>
    private static QualityGateResult runG4(IBeatmap beatmap)
    {
        var hitObjects = beatmap.HitObjects;
        if (hitObjects.Count < 2)
            return gateFail("G4 参数分布", "物件数量不足以评估分布。");

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
        bool compliant = minDistance >= min_object_spacing
                         && maxDistance <= max_object_spacing
                         && sliderRatio >= min_slider_ratio
                         && sliderRatio <= max_slider_ratio;

        return new QualityGateResult
        {
            Name = "G4 参数分布",
            Status = compliant ? GateStatus.Passed : GateStatus.Failed,
            Detail = $"相邻间距 {minDistance:0.#}–{maxDistance:0.#}px（允许 {min_object_spacing:0.#}–{max_object_spacing:0.#}）；slider 占比 {sliderRatio:0.00}（允许 {min_slider_ratio:0.00}–{max_slider_ratio:0.00}）。",
            Value = minDistance,
            Min = min_object_spacing,
            Max = max_object_spacing,
        };
    }

    /// <summary>
    /// G5 SR 校准：实测星数与目标星数之差不超过 settings.StarRatingTolerance。
    /// </summary>
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

    private static QualityGateResult gateFail(string name, string detail)
        => new QualityGateResult { Name = name, Status = GateStatus.Failed, Detail = detail };
}
