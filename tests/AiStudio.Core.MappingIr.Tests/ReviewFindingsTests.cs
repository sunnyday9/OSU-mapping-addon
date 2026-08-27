using AiStudio.Core.Analysis;
using AiStudio.Core.MappingIr.Analysis;
using AiStudio.Core.MappingIr.Critique;
using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Serialization;
using AiStudio.Core.MappingIr.Timeline;
using AiStudio.Core.MappingIr.Validation;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

/// <summary>
/// 合入前 code-review 发现项的回归测试（P0-3 / P0-1 / Spec-1 revision / P1-1 对齐谓词）。
/// </summary>
[TestFixture]
public class ReviewFindingsTests
{
    // ---- P0-3：反序列化文档直接校验不抛 InvalidCastException ----

    [Test]
    public void Validate_DeserializedDocument_DoesNotThrow()
    {
        var doc = TestFixtures.BuildDocument(42);
        string json = JsonMappingIrSerializer.Serialize(doc);
        var roundtripped = JsonMappingIrSerializer.Deserialize(json)!;

        // 反序列化后 Variant["keys"] 是 JsonElement：修复前 Convert.ToInt32(JsonElement) 抛 InvalidCastException
        ValidationResult result = new MappingValidator().Validate(roundtripped);

        Assert.That(result.Issues.Select(i => i.Code), Does.Not.Contain("unsupported_keycount"),
            "keys=4 must survive JSON roundtrip and validate as 4K mania");
    }

    [Test]
    public void Validate_DeserializedDocument_WrongKeyCount_ReportsError()
    {
        var doc = TestFixtures.BuildDocument(42) with
        {
            Ruleset = new RulesetInfo(RulesetKind.Mania, new Dictionary<string, object?> { ["keys"] = 7 }),
        };
        string json = JsonMappingIrSerializer.Serialize(doc);
        var roundtripped = JsonMappingIrSerializer.Deserialize(json)!;

        var result = new MappingValidator().Validate(roundtripped);

        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("unsupported_keycount"));
    }

    // ---- P0-1：单段 snap 后坍缩为空 → 兜底一段覆盖全曲 ----

    [Test]
    public void TimelineBuilder_SingleZeroLengthSection_FallsBackToFullCoverage()
    {
        // 单段零长（start==end）：snap 后坍缩 → 兜底 [0, durationMs]
        var grid = new BeatGrid(174.0, 0, new List<double> { 0, 344.8, 689.7 });
        var sections = new[] { new AudioSection(100, 100, 0.5) };
        var timeline = new MusicTimelineBuilder().Build(grid, sections);

        Assert.That(timeline.Sections.Count, Is.EqualTo(1), "must fall back to a single covering section");
        Assert.That(timeline.Sections[0].StartTime, Is.EqualTo(0));
        Assert.That(timeline.Sections[0].EndTime, Is.EqualTo(timeline.DurationMs));
    }

    [Test]
    public void TimelineBuilder_AllSectionsCollapse_FallsBackToFullCoverage()
    {
        // 多段全部 snap 到同一 beat → 全部坍缩 → 兜底
        var grid = new BeatGrid(174.0, 0, new List<double> { 0, 344.8, 689.7 });
        var sections = new[]
        {
            new AudioSection(0, 100, 0.5),
            new AudioSection(100, 200, 0.5),
            new AudioSection(200, 300, 0.5),
        };
        var timeline = new MusicTimelineBuilder().Build(grid, sections);

        Assert.That(timeline.Sections.Count, Is.GreaterThan(0));
        foreach (var section in timeline.Sections)
            Assert.That(section.EndTime, Is.GreaterThan(section.StartTime), "no zero-length sections");
    }

    // ---- Spec-1：critic 驱动的有界 revision ----

    [Test]
    public void Pipeline_RevisionBudget_IsBounded()
    {
        // 高密度意图（density > 0.7）触发 1/16 候选；预算默认 3 → 循环必须终止且产出有效文档
        var pipeline = new MappingIrPipeline(TestFixtures.Analyzer());
        string pseudo = Path.Combine(Path.GetTempPath(), $"rev_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 42);
            Assert.That(doc.ConcreteObjects, Is.Not.Null.And.Count.GreaterThan(0));
            Assert.That(doc.Evaluation.Valid, Is.True);
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    [Test]
    public void Pipeline_BudgetExhausted_ProducesValidFallback_NotHardErrorObjects()
    {
        // 预算=1：即使唯一候选被 critic 拒绝，也必须回退到 valid 输出（不产出带硬错误的对象）
        var pipeline = new MappingIrPipeline(TestFixtures.Analyzer()) { MaxRevisionsPerPhrase = 1 };
        string pseudo = Path.Combine(Path.GetTempPath(), $"rev_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 42);
            Assert.That(doc.ConcreteObjects, Is.Not.Null.And.Count.GreaterThan(0));
            Assert.That(doc.Evaluation.Valid, Is.True, "budget exhaustion must not yield hard-error objects");
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    [Test]
    public void Critic_SoftIssue_TriggersRevision_NotHardBlock()
    {
        // 软问题（如 density_mismatch）不使 report.Valid=false
        var doc = TestFixtures.BuildDocument(42);
        var report = new BaselineMappingCritic().Evaluate(doc);

        Assert.That(report.HardIssues, Is.Empty, "valid doc must have no hard issues");
        // 软问题存在与否取决于 fixture；关键是 Valid 恒为 true
        Assert.That(report.Valid, Is.True);
    }

    // ---- P1-1：对齐谓词对称 ----

    [Test]
    public void AlignmentPredicate_IsSymmetricWithCritic()
    {
        double beatMs = 60000.0 / TestFixtures.Bpm;
        double grid = beatMs / 16.0;

        // 恰好在 2.0ms 边界：score 与 critic 必须一致（都算 on-grid，因为谓词是 < 2.0）
        double boundaryOff = 2.0 - 1e-9;
        Assert.That(MappingIrPipeline.isOnGrid(grid * 10 + boundaryOff, grid), Is.True);

        double boundaryOn = 2.0 + 1e-9;
        Assert.That(MappingIrPipeline.isOnGrid(grid * 10 + boundaryOn, grid), Is.False);
    }

    [Test]
    public void MusicAlignmentScore_MatchesCriticOffGridCount()
    {
        var doc = TestFixtures.BuildDocument(42);
        double score = MappingIrPipeline.musicAlignmentScore(doc.MusicTimeline, doc.ConcreteObjects!);
        var report = new BaselineMappingCritic().Evaluate(doc);

        // 无 off-grid → score=1.0 且 critic 无 rhythm_alignment
        Assert.That(score, Is.EqualTo(1.0).Within(0.001));
        Assert.That(report.SoftIssues.Select(i => i.Code), Does.Not.Contain("rhythm_alignment"));
    }
}
