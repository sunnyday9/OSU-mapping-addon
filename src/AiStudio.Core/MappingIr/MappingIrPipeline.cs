using AiStudio.Core.Analysis;
using AiStudio.Core.MappingIr.Backends;
using AiStudio.Core.MappingIr.Candidates;
using AiStudio.Core.MappingIr.Critique;
using AiStudio.Core.MappingIr.Difficulty;
using AiStudio.Core.MappingIr.Evidence;
using AiStudio.Core.MappingIr.GlobalPlanning;
using AiStudio.Core.MappingIr.LocalPlanning;
using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Patterns;
using AiStudio.Core.MappingIr.Planning;
using AiStudio.Core.MappingIr.Timeline;
using AiStudio.Core.MappingIr.Validation;

namespace AiStudio.Core.MappingIr;

/// <summary>
/// Mapping IR 端到端管线（Mapping Intelligence spec §5/§19）：
/// 音频分析 → 时间线 → 证据 → 全局计划 → 本地意图 → 候选生成 → 排名 → 渲染 → 校验 → Critic → 有界 Revision。
/// ruleset 相关能力经 <see cref="IRulesetMappingBackend"/> 解耦（默认 Mania 4K）。
/// </summary>
public sealed class MappingIrPipeline
{
    /// <summary>Revision 预算（spec §19.1，配置化默认值）。</summary>
    public int MaxRevisionsPerPhrase { get; init; } = 3;

    private readonly IAudioAnalyzer analyzer;
    private readonly MusicTimelineBuilder timelineBuilder;
    private readonly IMappingEvidenceBuilder evidenceBuilder;
    private readonly IGlobalMappingPlanner globalPlanner;
    private readonly ILocalMappingPlanner localPlanner;
    private readonly IPatternCandidateGenerator candidateGenerator;
    private readonly IPatternCandidateRanker candidateRanker;
    private readonly IRulesetMappingBackend backend;
    private readonly IMappingCritic critic;
    private readonly IDifficultyEvaluator difficultyEvaluator;

    /// <summary>兼容旧构造（MVP A 签名）：内部组装新决策链。</summary>
    public MappingIrPipeline(
        IAudioAnalyzer? analyzer = null,
        IMappingPlanner? planner = null,
        IPatternProvider? provider = null,
        IMappingValidator? validator = null)
        : this(
            analyzer,
            new DeterministicEvidenceBuilder(),
            new DeterministicGlobalPlanner(),
            new DeterministicLocalPlanner(),
            new DeterministicCandidateGenerator(),
            new DeterministicCandidateRanker(),
            new Mania4KMappingBackend(provider, validator),
            new BaselineMappingCritic(),
            new UnavailableDifficultyEvaluator())
    {
    }

    public MappingIrPipeline(
        IAudioAnalyzer? analyzer,
        IMappingEvidenceBuilder? evidenceBuilder,
        IGlobalMappingPlanner? globalPlanner,
        ILocalMappingPlanner? localPlanner,
        IPatternCandidateGenerator? candidateGenerator,
        IPatternCandidateRanker? candidateRanker,
        IRulesetMappingBackend? backend,
        IMappingCritic? critic,
        IDifficultyEvaluator? difficultyEvaluator)
    {
        this.analyzer = analyzer ?? new Analysis.SyntheticAudioAnalyzer(180.0, 60000);
        this.timelineBuilder = new MusicTimelineBuilder();
        this.evidenceBuilder = evidenceBuilder ?? new DeterministicEvidenceBuilder();
        this.globalPlanner = globalPlanner ?? new DeterministicGlobalPlanner();
        this.localPlanner = localPlanner ?? new DeterministicLocalPlanner();
        this.candidateGenerator = candidateGenerator ?? new DeterministicCandidateGenerator();
        this.candidateRanker = candidateRanker ?? new DeterministicCandidateRanker();
        this.backend = backend ?? new Mania4KMappingBackend();
        this.critic = critic ?? new BaselineMappingCritic();
        this.difficultyEvaluator = difficultyEvaluator ?? new UnavailableDifficultyEvaluator();
    }

    /// <summary>运行完整管线，返回最终文档（含 concrete_objects + evaluation + critic issues）。</summary>
    public MappingDocument Run(string audioPath, DifficultyProfile difficultyProfile, int seed = 0, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(audioPath);
        ArgumentNullException.ThrowIfNull(difficultyProfile);

        // 1. 音频分析
        BeatGrid grid = analyzer.AnalyseBeatAsync(audioPath, cancellationToken).GetAwaiter().GetResult();
        IReadOnlyList<AudioSection> sections = analyzer.AnalyseSectionsAsync(audioPath, cancellationToken).GetAwaiter().GetResult();

        // 2. 时间线
        var timeline = timelineBuilder.Build(grid, sections);

        // 3. 文档骨架
        var document = MappingDocument.CreateEmpty(
            $"mapir_{Path.GetFileNameWithoutExtension(audioPath)}_{seed}",
            new MapInfo(hashAudio(audioPath), Title: Path.GetFileNameWithoutExtension(audioPath), AudioFilename: Path.GetFileName(audioPath)),
            new RulesetInfo(backend.Ruleset, new Dictionary<string, object?> { ["keys"] = 4 }),
            difficultyProfile);
        document = document with { MusicTimeline = timeline };

        // 4. 证据 → 全局计划 → 逐段本地规划 + 候选 + 排名 + 生成（含 revision loop）
        var evidence = evidenceBuilder.Build(timeline, difficultyProfile);
        var globalPlan = globalPlanner.Plan(timeline, evidence, difficultyProfile, document.Ruleset);

        var intents = new List<MappingIntent>();
        var patterns = new List<PatternIntent>();
        var transitions = new List<PatternTransition>();
        var generated = new List<ConcreteObject>();
        int counter = 1;

        for (int i = 0; i < timeline.Sections.Count; i++)
        {
            var section = timeline.Sections[i];
            if (section.EndTime <= section.StartTime)
                continue;

            var context = new LocalMappingContext(
                section,
                evidence,
                globalPlan,
                patterns,
                Array.Empty<PatternIntent>(), // next patterns：baseline 无未来 pattern（spec §10 future-aware 留待后续）
                difficultyProfile);

            var intent = localPlanner.Plan(context);
            intents.Add(intent);

            // 候选生成 + 排名（有界 revision：critic 软问题 → 重排候选）
            var best = selectBestCandidate(intent, difficultyProfile, seed, document, timeline, generated, counter, out int consumedObjects);
            patterns.Add(best);
            counter += consumedObjects;

            if (i > 0)
                transitions.Add(new PatternTransition(
                    $"transition_{intent.Id}",
                    patterns[i - 1].Id,
                    best.Id,
                    transitionType(patterns[i - 1].Family, best.Family),
                    new TransitionOverlap(intents[i - 1].EndTime - 250, intents[i - 1].EndTime),
                    new Dictionary<string, object?> { ["overlap_policy"] = "no_objects_in_overlap" }));
        }

        document = document with
        {
            MappingPlan = new MappingPlan(intents, patterns, transitions),
            ConcreteObjects = generated,
        };

        // 5. 校验（backend validator）+ Critic
        var validation = backend.Validator.Validate(document);
        var criticReport = critic.Evaluate(document);
        double? observedSr = difficultyEvaluator.TryEvaluateStarRating(document);

        document = document with
        {
            Evaluation = new Evaluation(
                validation.Valid,
                Difficulty: new Dictionary<string, object?>
                {
                    ["object_count"] = generated.Count,
                    ["duration_ms"] = timeline.DurationMs,
                    ["observed_star_rating"] = observedSr,
                },
                MusicAlignmentScore: musicAlignmentScore(timeline, generated),
                // transition_score 留 null：本阶段未计算真实的转换评分（spec §20 evaluation 是观测性的，不编造常量）。
                TransitionScore: null,
                Issues: validation.Issues.Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["code"] = i.Code, ["severity"] = i.Severity, ["message"] = i.Message })
                    .Concat(criticReport.Issues.Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["code"] = i.Code, ["severity"] = i.Severity, ["message"] = i.Message, ["start_time"] = i.StartTime, ["end_time"] = i.EndTime }))
                    .ToList(),
                // spec §25.4：难度评估器不可用 → DifficultyKnown=false，不声称达到目标 SR
                DifficultyKnown: observedSr is not null),
        };

        return document;
    }

    /// <summary>渲染 .osu 文本（经 backend，ruleset 专属）。</summary>
    public string RenderOsu(MappingDocument document)
        => backend.Render(document);

    // ---- internals -------------------------------------------------------

    private PatternIntent selectBestCandidate(MappingIntent intent, DifficultyProfile profile, int seed, MappingDocument document, MusicTimeline timeline, List<ConcreteObject> generated, int counter, out int consumedObjects)
    {
        consumedObjects = 0;
        var candidates = candidateGenerator.Generate(intent, profile, backend.Ruleset, seed, timeline.Tempo.BaseBpm);
        var ranked = candidateRanker.Rank(candidates, intent, profile);

        if (ranked.Count == 0)
        {
            // 无有效候选：回退一个最简 single pattern
            var fallback = fallbackSingle(intent, timeline.Tempo.BaseBpm);
            var ctx = new PatternGenerationContext(timeline, document, generated, profile, seed);
            var result = backend.Provider.Generate(fallback, ctx);
            consumedObjects = appendObjects(result.Objects, generated, counter);
            return fallback;
        }

        // 按排名尝试候选（有界 revision）；无硬问题即接受
        int attempt = 0;
        while (attempt < ranked.Count && attempt < MaxRevisionsPerPhrase)
        {
            var rankedCandidate = ranked[attempt];
            var ctx = new PatternGenerationContext(timeline, document, generated, profile, seed + attempt);
            var result = backend.Provider.Generate(rankedCandidate.Candidate.Intent, ctx);
            if (result.Objects.Count > 0)
            {
                // 局部快速校验：候选本身无 error 级问题即接受
                bool hasHardIssue = result.Issues.Any(i => i.Severity == "error");
                if (!hasHardIssue)
                {
                    consumedObjects = appendObjects(result.Objects, generated, counter);
                    return rankedCandidate.Candidate.Intent;
                }
            }

            attempt++;
        }

        // 预算耗尽：接受最后一个尝试过的候选（宁可出草稿）
        var last = ranked[Math.Min(attempt, ranked.Count - 1)].Candidate.Intent;
        var lastCtx = new PatternGenerationContext(timeline, document, generated, profile, seed);
        var lastResult = backend.Provider.Generate(last, lastCtx);
        consumedObjects = appendObjects(lastResult.Objects, generated, counter);
        return last;
    }

    /// <summary>把对象追加到生成列表，返回实际追加的数量（供外层 counter 精确推进，杜绝 ID 重复）。</summary>
    private static int appendObjects(IReadOnlyList<ConcreteObject> objects, List<ConcreteObject> generated, int counter)
    {
        int added = 0;
        foreach (var obj in objects)
        {
            generated.Add(obj with { Id = $"obj_{counter++}" });
            added++;
        }

        return added;
    }

    private static PatternIntent fallbackSingle(MappingIntent intent, double bpm)
        => new(
            $"candidate_{intent.Id}_fallback",
            RulesetKind.Mania,
            "single",
            intent.StartTime,
            intent.EndTime,
            new Dictionary<string, object?> { ["subdivision"] = "1/4", ["bpm"] = bpm },
            new Dictionary<string, object?> { ["max_consecutive_same_column"] = 1 },
            0.5,
            Rationale: "Fallback: no valid candidate passed ranking.");

    private static string transitionType(string from, string to)
        => from == to
            ? "same_family"
            : from == "jumpstream" && to == "stream" ? "chord_removal"
            : from == "stream" && to == "jumpstream" ? "chord_introduction"
            : from.StartsWith("ln", StringComparison.Ordinal) ? "ln_release"
            : "hand_rebalance";

    /// <summary>音乐对齐分：对象时间落在节奏网格（最小细分 1/16 拍）上的比例 [0,1]。</summary>
    public static double musicAlignmentScore(MusicTimeline timeline, IReadOnlyList<ConcreteObject> objects)
    {
        if (objects.Count == 0)
            return 0.0;

        double beatMs = timeline.Tempo.BaseBpm > 0 ? 60000.0 / timeline.Tempo.BaseBpm : 0;
        if (beatMs <= 0)
            return 0.0;

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
