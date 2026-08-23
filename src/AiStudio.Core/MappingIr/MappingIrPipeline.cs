using AiStudio.Core.Analysis;
using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Patterns;
using AiStudio.Core.MappingIr.Planning;
using AiStudio.Core.MappingIr.Rendering;
using AiStudio.Core.MappingIr.Timeline;
using AiStudio.Core.MappingIr.Validation;

namespace AiStudio.Core.MappingIr;

/// <summary>
/// Mapping IR 端到端管线（对应详细计划 §26 第 12 项"关闭 LLM 也应跑通"的闭环）：
/// 音频分析 → 时间线 → 规划 → 模式生成 → 校验 → .osu 渲染。
/// </summary>
public sealed class MappingIrPipeline
{
    private readonly IAudioAnalyzer analyzer;
    private readonly IMappingPlanner planner;
    private readonly IPatternProvider provider;
    private readonly IMappingValidator validator;
    private readonly ManiaOsuRenderer renderer;
    private readonly MusicTimelineBuilder timelineBuilder;

    public MappingIrPipeline(
        IAudioAnalyzer? analyzer = null,
        IMappingPlanner? planner = null,
        IPatternProvider? provider = null,
        IMappingValidator? validator = null)
    {
        this.analyzer = analyzer ?? new Analysis.SyntheticAudioAnalyzer(180.0, 60000);
        this.planner = planner ?? new DeterministicMappingPlanner();
        this.provider = provider ?? new Mania4KPatternProvider();
        this.validator = validator ?? new MappingValidator();
        this.renderer = new ManiaOsuRenderer();
        this.timelineBuilder = new MusicTimelineBuilder();
    }

    /// <summary>运行完整管线，返回最终文档（含 concrete_objects + evaluation）。</summary>
    public MappingDocument Run(string audioPath, DifficultyProfile difficultyProfile, int seed = 0, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(audioPath);
        ArgumentNullException.ThrowIfNull(difficultyProfile);

        // 1. 音频分析（BASS 谱通量）
        BeatGrid grid = analyzer.AnalyseBeatAsync(audioPath, cancellationToken).GetAwaiter().GetResult();
        IReadOnlyList<AudioSection> sections = analyzer.AnalyseSectionsAsync(audioPath, cancellationToken).GetAwaiter().GetResult();

        // 2. 时间线
        var timeline = timelineBuilder.Build(grid, sections);

        // 3. 规划（规则型）
        var plan = planner.Plan(timeline, difficultyProfile, seed);

        // 4. 模式生成
        var document = MappingDocument.CreateEmpty(
            $"mapir_{Path.GetFileNameWithoutExtension(audioPath)}_{seed}",
            new MapInfo(hashAudio(audioPath), Title: Path.GetFileNameWithoutExtension(audioPath)),
            new RulesetInfo(RulesetKind.Mania, new Dictionary<string, object?> { ["keys"] = 4 }),
            difficultyProfile);
        document = document with { MusicTimeline = timeline, MappingPlan = plan };

        var generated = new List<ConcreteObject>();
        int counter = 1;
        foreach (var intent in plan.Patterns)
        {
            var ctx = new PatternGenerationContext(timeline, document, generated, difficultyProfile, seed);
            var result = provider.Generate(intent, ctx);
            foreach (var obj in result.Objects)
            {
                // 对象 ID 全局唯一：用文档内递增计数
                generated.Add(obj with { Id = $"obj_{counter++}" });
            }
        }

        document = document with { ConcreteObjects = generated };

        // 5. 校验
        var validation = validator.Validate(document);
        document = document with
        {
            Evaluation = new Evaluation(
                validation.Valid,
                Difficulty: new Dictionary<string, object?> { ["object_count"] = generated.Count, ["duration_ms"] = timeline.DurationMs },
                MusicAlignmentScore: musicAlignmentScore(timeline, generated),
                TransitionScore: plan.Transitions.Count == 0 ? 1.0 : 0.8,
                Issues: validation.Issues.Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["code"] = i.Code, ["severity"] = i.Severity, ["message"] = i.Message }).ToList()),
        };

        return document;
    }

    /// <summary>渲染 .osu 文本（确定性）。</summary>
    public string RenderOsu(MappingDocument document)
        => renderer.Render(document);

    /// <summary>音乐对齐分：对象时间落在节奏网格（最小细分 1/16 拍）上的比例 [0,1]。</summary>
    public static double musicAlignmentScore(MusicTimeline timeline, IReadOnlyList<ConcreteObject> objects)
    {
        if (objects.Count == 0)
            return 0.0;

        double beatMs = timeline.Tempo.BaseBpm > 0 ? 60000.0 / timeline.Tempo.BaseBpm : 0;
        if (beatMs <= 0)
            return 0.0;

        // 节奏网格 = 最小细分（1/16 拍）；对象落在网格上（±2ms）即视为对齐。
        double gridMs = beatMs / 16.0;
        int aligned = 0;
        foreach (var obj in objects)
        {
            double t = obj.Time;
            double nearest = Math.Round(t / gridMs) * gridMs;
            if (Math.Abs(t - nearest) < 2.0)
                aligned++;
        }

        return (double)aligned / objects.Count;
    }

    private static string hashAudio(string audioPath)
    {
        using var stream = File.OpenRead(audioPath);
        using var sha = System.Security.Cryptography.SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
