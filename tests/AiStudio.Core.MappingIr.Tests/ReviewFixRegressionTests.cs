using AiStudio.Core.MappingIr.Candidates;
using AiStudio.Core.MappingIr.Critique;
using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Patterns;
using AiStudio.Core.MappingIr.Rendering;
using AiStudio.Core.MappingIr.Serialization;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

/// <summary>
/// code-review 修复回归测试：
/// 1) 对象 ID 唯一性  2) LN ln_duration_beats&gt;1 无同列重叠  3) AudioFilename 同步
/// 4) 零长段过滤+ID 唯一  5) candidates ≥4  6) TransitionScore null  7) DifficultyKnown
/// 8) critic 空对象仍报 pattern 问题  9) SpreadPlanner drain 阶梯不反转
/// </summary>
[TestFixture]
public class ReviewFixRegressionTests
{
    // ---- #1 对象 ID 唯一性 ----

    [Test]
    public void Pipeline_ObjectIdsAreUniqueAcrossSections()
    {
        var pipeline = new MappingIrPipeline(TestFixtures.Analyzer());
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpfix_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 42);
            var ids = doc.ConcreteObjects!.Select(o => o.Id).ToList();

            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "object IDs must be unique across sections");
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    // ---- #3 LN 同列重叠（ln_duration_beats > 1）----

    [Test]
    public void LnDurationBeats_AboveOne_NoSameColumnOverlap()
    {
        var provider = new Mania4KPatternProvider();
        var intent = new PatternIntent(
            "pattern_ln",
            RulesetKind.Mania,
            "single_ln",
            1000,
            20000,
            new Dictionary<string, object?>
            {
                ["subdivision"] = "1/8",
                ["column_order"] = new object[] { 0, 2, 1, 3 },
                ["bpm"] = 174.0,
                ["ln_duration_beats"] = 2.0, // > 1：修复前会重叠
            },
            new Dictionary<string, object?> { ["max_consecutive_same_column"] = 1 },
            0.9);

        var ctx = new PatternGenerationContext(TestFixtures.Timeline(), TestFixtures.BuildDocument(42), Array.Empty<ConcreteObject>(), TestFixtures.BalancedProfile(), 42);
        var result = provider.Generate(intent, ctx);

        // 同列无重叠
        var byColumn = result.Objects.Where(o => o.Column is not null).GroupBy(o => o.Column!.Value);
        foreach (var group in byColumn)
        {
            var ordered = group.OrderBy(o => o.Time).ToList();
            for (int i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1];
                var cur = ordered[i];
                Assert.That(cur.Time >= (prev.EndTime ?? prev.Time), Is.True,
                    $"column {group.Key}: LN overlap after ln_duration_beats>1 fix ({prev.Id}@{prev.Time}-{prev.EndTime} vs {cur.Id}@{cur.Time})");
            }
        }

        // LN 尾部不越过 intent.EndTime（#2 跨段修复）
        foreach (var obj in result.Objects.Where(o => o.Type == "hold"))
            Assert.That(obj.EndTime!.Value, Is.LessThanOrEqualTo(intent.EndTime), "LN end must not exceed intent.EndTime");
    }

    // ---- #4 AudioFilename 同步 ----

    [Test]
    public void Renderer_UsesMapInfoAudioFilename()
    {
        var doc = TestFixtures.BuildDocument(42) with
        {
            Map = TestFixtures.BuildDocument(42).Map with { AudioFilename = "song_174bpm.mp3" },
        };
        string osu = new ManiaOsuRenderer().Render(doc);

        Assert.That(osu, Does.Contain("AudioFilename: song_174bpm.mp3"));
        Assert.That(osu, Does.Not.Contain("AudioFilename: audio.mp3"));
    }

    [Test]
    public void Renderer_FallbackToAudioMp3_WhenFilenameMissing()
    {
        var doc = TestFixtures.BuildDocument(42) with { Map = TestFixtures.BuildDocument(42).Map with { AudioFilename = null } };
        string osu = new ManiaOsuRenderer().Render(doc);

        Assert.That(osu, Does.Contain("AudioFilename: audio.mp3"));
    }

    [Test]
    public void Pipeline_SetsAudioFilenameFromInput()
    {
        var pipeline = new MappingIrPipeline(TestFixtures.Analyzer());
        string pseudo = Path.Combine(Path.GetTempPath(), "my_song.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 42);
            Assert.That(doc.Map.AudioFilename, Is.EqualTo("my_song.mp3"));
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    [Test]
    public void SerializedDocument_ContainsAudioFilename()
    {
        var doc = TestFixtures.BuildDocument(42) with { Map = TestFixtures.BuildDocument(42).Map with { AudioFilename = "x.mp3" } };
        string json = JsonMappingIrSerializer.Serialize(doc);
        Assert.That(json, Does.Contain("audio_filename"));
    }

    // ---- #7 零长段过滤 + ID 唯一 ----

    [Test]
    public void TimelineBuilder_ZeroLengthSections_AreFilteredAndIdsUnique()
    {
        // 两段极短（< 半拍）相邻段 snap 到同一 beat → 零长段被过滤，ID 唯一
        var analyzer = new AiStudio.Core.MappingIr.Analysis.SyntheticAudioAnalyzer(
            174.0,
            20000,
            new[] { 0.0, 100.0, 200.0, 15000.0 },
            new[] { 0.3, 0.4, 0.5, 0.2 });
        var grid = analyzer.AnalyseBeatAsync("x").GetAwaiter().GetResult();
        var sections = analyzer.AnalyseSectionsAsync("x").GetAwaiter().GetResult();
        var timeline = new AiStudio.Core.MappingIr.Timeline.MusicTimelineBuilder().Build(grid, sections);

        var ids = timeline.Sections.Select(s => s.Id).ToList();
        Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "section IDs must be unique");
        foreach (var section in timeline.Sections)
            Assert.That(section.EndTime, Is.GreaterThan(section.StartTime), "no zero-length sections");
    }

    // ---- #5 candidates ≥ 4 ----

    [Test]
    public void CandidateGenerator_ProducesAtLeastFour_ForAllIntents()
    {
        var gen = new DeterministicCandidateGenerator();
        foreach (MappingPrimaryIntent primary in Enum.GetValues<MappingPrimaryIntent>())
        {
            var intent = new MappingIntent(
                $"intent_{primary}", 1000, 8000, primary,
                new[] { "x" }, new[] { "y" },
                new MappingEmphasis(0.7, 0.7, 0.5, 0.6, 0.7, 0.5), 0.6, 0.9);
            var candidates = gen.Generate(intent, TestFixtures.BalancedProfile(), RulesetKind.Mania, 42);
            Assert.That(candidates.Count, Is.GreaterThanOrEqualTo(4), $"intent {primary} should produce >= 4 candidates");
        }
    }

    // ---- #6 TransitionScore null ----

    [Test]
    public void Pipeline_TransitionScoreIsNull_NotFabricated()
    {
        var pipeline = new MappingIrPipeline(TestFixtures.Analyzer());
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpfix_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 42);
            Assert.That(doc.Evaluation.TransitionScore, Is.Null, "transition score must not be fabricated");
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    // ---- #7 DifficultyKnown ----

    [Test]
    public void Pipeline_DifficultyKnownFalse_WhenEvaluatorUnavailable()
    {
        var pipeline = new MappingIrPipeline(TestFixtures.Analyzer());
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpfix_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 42);
            Assert.That(doc.Evaluation.DifficultyKnown, Is.False, "DifficultyKnown must be false when evaluator unavailable (spec §25.4)");
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    // ---- #11 Critic 空对象仍报 pattern 问题 ----

    [Test]
    public void Critic_EmptyObjects_StillReportsPatternRepetition()
    {
        var doc = TestFixtures.BuildDocument(42) with
        {
            ConcreteObjects = Array.Empty<ConcreteObject>(),
            MappingPlan = new MappingPlan(
                TestFixtures.BuildDocument(42).MappingPlan.Intents,
                TestFixtures.BuildDocument(42).MappingPlan.Patterns.Select(p => p with { Family = "stream" }).ToArray(),
                TestFixtures.BuildDocument(42).MappingPlan.Transitions),
        };
        var report = new BaselineMappingCritic().Evaluate(doc);

        Assert.That(report.HardIssues.Select(i => i.Code), Does.Contain("no_objects"));
        Assert.That(report.SoftIssues.Select(i => i.Code), Does.Contain("pattern_repetition"), "critic must not early-return on empty objects");
    }

    // ---- #9 SpreadPlanner drain 阶梯不反转 ----

    [Test]
    public void SpreadPlanner_LongDrain_DoesNotInvertToNormal()
    {
        // 5 分钟以上 drain → 起跳 Insane（修复前 fallthrough 到 Normal）
        var grid = new AiStudio.Core.Analysis.BeatGrid(120, 0, new[] { 0.0, 300000.0 });
        var sections = new[] { new AiStudio.Core.Analysis.AudioSection(0, 300000, 0.5) };
        var settings = new AiStudio.Core.Models.GenerationSettings { TargetLevel = AiStudio.Core.Models.DifficultyLevel.Expert, TargetStarRating = 5.0 };

        var specs = Synthesis.SpreadPlanner.Plan(grid, sections, settings);

        Assert.That(specs.Any(s => s.Level == AiStudio.Core.Models.DifficultyLevel.Insane), Is.True,
            "5-min drain should start at Insane or above, not Normal (drain ladder inversion)");
        Assert.That(specs.All(s => s.Level != AiStudio.Core.Models.DifficultyLevel.Normal || specs.Count == 1), Is.True);
    }
}
