using AiStudio.Core.MappingIr.Candidates;
using AiStudio.Core.MappingIr.Critique;
using AiStudio.Core.MappingIr.Evidence;
using AiStudio.Core.MappingIr.GlobalPlanning;
using AiStudio.Core.MappingIr.LocalPlanning;
using AiStudio.Core.MappingIr.Model;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

[TestFixture]
public class EvidenceBuilderTests
{
    [Test]
    public void Build_OneEvidencePerSection()
    {
        var timeline = TestFixtures.Timeline();
        var evidence = new DeterministicEvidenceBuilder().Build(timeline, TestFixtures.BalancedProfile());

        Assert.That(evidence.Count, Is.EqualTo(timeline.Sections.Count));
    }

    [Test]
    public void Build_AllDimensionsInRange()
    {
        var evidence = new DeterministicEvidenceBuilder().Build(TestFixtures.Timeline(), TestFixtures.BalancedProfile());

        foreach (var e in evidence)
        {
            foreach (double v in new[] { e.Rhythm, e.Accent, e.Energy, e.Vocal, e.Movement, e.Density, e.Repetition, e.Climax, e.Novelty, e.BeatConfidence, e.Confidence })
            {
                Assert.That(v, Is.InRange(0.0, 1.0), $"evidence '{e.Id}' dimension out of range");
            }

            Assert.That(e.Sources, Is.Not.Empty, $"evidence '{e.Id}' missing sources");
            Assert.That(e.Sources, Does.Contain("audio.energy"));
        }
    }

    [Test]
    public void Build_HighestEnergySection_HasHighestClimax()
    {
        var evidence = new DeterministicEvidenceBuilder().Build(TestFixtures.Timeline(), TestFixtures.BalancedProfile());

        var chorus = evidence.Single(e => e.StartTime >= 20000 && e.StartTime < 40000);
        var others = evidence.Where(e => e != chorus);
        Assert.That(chorus.Climax, Is.GreaterThanOrEqualTo(others.Max(o => o.Climax)), "highest-energy section must have highest climax");
        Assert.That(chorus.Energy, Is.EqualTo(0.85));
    }

    [Test]
    public void Build_Deterministic()
    {
        var builder = new DeterministicEvidenceBuilder();
        var a = builder.Build(TestFixtures.Timeline(), TestFixtures.BalancedProfile());
        var b = builder.Build(TestFixtures.Timeline(), TestFixtures.BalancedProfile());

        Assert.That(b.Select(e => e.Energy), Is.EqualTo(a.Select(e => e.Energy)));
        Assert.That(b.Select(e => e.Climax), Is.EqualTo(a.Select(e => e.Climax)));
    }
}

[TestFixture]
public class GlobalPlannerTests
{
    [Test]
    public void Plan_ProducesCurveAndSectionPlans()
    {
        var timeline = TestFixtures.Timeline();
        var evidence = new DeterministicEvidenceBuilder().Build(timeline, TestFixtures.BalancedProfile());
        var plan = new DeterministicGlobalPlanner().Plan(timeline, evidence, TestFixtures.BalancedProfile(), new RulesetInfo(RulesetKind.Mania, new Dictionary<string, object?> { ["keys"] = 4 }));

        Assert.That(plan.DifficultyCurve.Count, Is.EqualTo(timeline.Sections.Count));
        Assert.That(plan.SectionPlans.Count, Is.EqualTo(timeline.Sections.Count));
        Assert.That(plan.GlobalClimax.TimeMs, Is.EqualTo(20000)); // chorus 段起点
    }

    [Test]
    public void Plan_GlobalClimaxIsHighestEnergySection()
    {
        var timeline = TestFixtures.Timeline();
        var evidence = new DeterministicEvidenceBuilder().Build(timeline, TestFixtures.BalancedProfile());
        var plan = new DeterministicGlobalPlanner().Plan(timeline, evidence, TestFixtures.BalancedProfile(), new RulesetInfo(RulesetKind.Mania, new Dictionary<string, object?> { ["keys"] = 4 }));

        var chorus = timeline.Sections.Single(s => s.Type == SectionType.Chorus);
        Assert.That(plan.GlobalClimax.Strength, Is.EqualTo(chorus.Energy));
    }

    [Test]
    public void Plan_FinalClimax_NoReserve()
    {
        // 仅一段高能量段（最后一段）→ isFinalClimax，density budget 不打折
        var timeline = TestFixtures.Timeline();
        var evidence = new DeterministicEvidenceBuilder().Build(timeline, TestFixtures.BalancedProfile());
        var plan = new DeterministicGlobalPlanner().Plan(timeline, evidence, TestFixtures.BalancedProfile(), new RulesetInfo(RulesetKind.Mania, new Dictionary<string, object?> { ["keys"] = 4 }));

        var chorusPlan = plan.SectionPlans.Single(p => p.Role == "climax");
        Assert.That(chorusPlan.DensityBudget, Is.GreaterThan(0.7), "final climax should not be discounted");
    }

    [Test]
    public void Plan_Deterministic()
    {
        var timeline = TestFixtures.Timeline();
        var evidence = new DeterministicEvidenceBuilder().Build(timeline, TestFixtures.BalancedProfile());
        var planner = new DeterministicGlobalPlanner();
        var ruleset = new RulesetInfo(RulesetKind.Mania, new Dictionary<string, object?> { ["keys"] = 4 });

        var a = planner.Plan(timeline, evidence, TestFixtures.BalancedProfile(), ruleset);
        var b = planner.Plan(timeline, evidence, TestFixtures.BalancedProfile(), ruleset);

        Assert.That(b.DifficultyCurve.Select(p => p.Target), Is.EqualTo(a.DifficultyCurve.Select(p => p.Target)));
        Assert.That(b.SectionPlans.Select(p => p.Role), Is.EqualTo(a.SectionPlans.Select(p => p.Role)));
    }

    [Test]
    public void Plan_EmptyTimeline_EmptyPlan()
    {
        var plan = new DeterministicGlobalPlanner().Plan(MusicTimeline.Empty, Array.Empty<MappingEvidence>(), TestFixtures.BalancedProfile(), new RulesetInfo(RulesetKind.Mania, new Dictionary<string, object?>()));
        Assert.That(plan.SectionPlans, Is.Empty);
    }
}

[TestFixture]
public class CandidateRankerTests
{
    private static IReadOnlyList<PatternCandidate> Candidates()
    {
        var gen = new DeterministicCandidateGenerator();
        var intent = new MappingIntent(
            "intent_test", 1000, 8000, MappingPrimaryIntent.Climax,
            new[] { "rhythm_emphasis" }, new[] { "snare" },
            new MappingEmphasis(0.9, 0.8, 0.3, 0.7, 0.8, 0.6), 0.7, 0.9);
        return gen.Generate(intent, TestFixtures.BalancedProfile(), RulesetKind.Mania, 42);
    }

    [Test]
    public void Generate_ProducesAtLeastThreeCandidates()
    {
        Assert.That(Candidates().Count, Is.GreaterThanOrEqualTo(3), "spec §11.1 requires 3-5 candidates");
    }

    [Test]
    public void Rank_OrdersByScoreDescending()
    {
        var intent = new MappingIntent("intent_test", 1000, 8000, MappingPrimaryIntent.Climax, new[] { "x" }, new[] { "y" }, new MappingEmphasis(0.9, 0.8, 0.3, 0.7, 0.8, 0.6), 0.7, 0.9);
        var ranked = new DeterministicCandidateRanker().Rank(Candidates(), intent, TestFixtures.BalancedProfile());

        Assert.That(ranked.Count, Is.GreaterThanOrEqualTo(3));
        for (int i = 1; i < ranked.Count; i++)
            Assert.That(ranked[i].Score, Is.LessThanOrEqualTo(ranked[i - 1].Score));
    }

    [Test]
    public void Rank_RejectsHardInvalidCandidates()
    {
        var intent = new MappingIntent("intent_test", 1000, 8000, MappingPrimaryIntent.Establish, new[] { "x" }, new[] { "y" }, new MappingEmphasis(0.5, 0.5, 0.5, 0.5, 0.5, 0.5), 0.5, 0.9);
        var invalid = new PatternCandidate("bad", new PatternIntent("bad", RulesetKind.Mania, "nonexistent_family", 1000, 8000, new Dictionary<string, object?>(), new Dictionary<string, object?>(), 0.5), 0.5, 0.5, 0.5, 0.5, new[] { "x" });

        var ranked = new DeterministicCandidateRanker().Rank(new[] { invalid }, intent, TestFixtures.BalancedProfile());
        Assert.That(ranked, Is.Empty, "hard-invalid candidate must be rejected before ranking (spec §12.7)");
    }

    [Test]
    public void Rank_WeightsAreConfigurable()
    {
        var customWeights = new Dictionary<string, double>
        {
            ["music_alignment"] = 1.0,
            ["difficulty_fit"] = 0.0,
            ["continuity"] = 0.0,
            ["readability"] = 0.0,
            ["structural_fit"] = 0.0,
            ["validity"] = 0.0,
        };
        var ranker = new DeterministicCandidateRanker(customWeights);
        var intent = new MappingIntent("intent_test", 1000, 8000, MappingPrimaryIntent.Climax, new[] { "x" }, new[] { "y" }, new MappingEmphasis(0.9, 0.8, 0.3, 0.7, 0.8, 0.6), 0.7, 0.9);

        var ranked = ranker.Rank(Candidates(), intent, TestFixtures.BalancedProfile());
        Assert.That(ranked, Is.Not.Empty);
    }
}

[TestFixture]
public class CriticTests
{
    [Test]
    public void Evaluate_CleanDocument_NoIssues()
    {
        var doc = TestFixtures.BuildDocument(42);
        var report = new BaselineMappingCritic().Evaluate(doc);

        Assert.That(report.Valid, Is.True, "clean document should pass critic");
        Assert.That(report.Issues, Is.Empty);
    }

    [Test]
    public void Evaluate_OverlappingObjects_HardIssue()
    {
        var doc = TestFixtures.BuildDocument(42) with
        {
            ConcreteObjects = new[]
            {
                new ConcreteObject("a", "hold", 1000, EndTime: 2000, Column: 0),
                new ConcreteObject("b", "hit", 1500, Column: 0),
            },
        };
        var report = new BaselineMappingCritic().Evaluate(doc);

        Assert.That(report.Valid, Is.False);
        Assert.That(report.HardIssues.Select(i => i.Code), Does.Contain("column_overlap"));
    }

    [Test]
    public void Evaluate_EnergyDensityMismatch_SoftIssue()
    {
        // 高能量段但几乎无对象 → density_mismatch warning
        var doc = TestFixtures.BuildDocument(42);
        var chorus = doc.MusicTimeline.Sections.Single(s => s.Type == SectionType.Chorus);
        doc = doc with
        {
            ConcreteObjects = new[]
            {
                new ConcreteObject("n1", "hit", chorus.StartTime, Column: 0),
            },
        };
        var report = new BaselineMappingCritic().Evaluate(doc);

        Assert.That(report.SoftIssues.Select(i => i.Code), Does.Contain("density_mismatch"));
        Assert.That(report.Valid, Is.True, "soft issues must not block acceptance (spec §15.3)");
    }

    [Test]
    public void Evaluate_RepeatedPatterns_SoftIssue()
    {
        var doc = TestFixtures.BuildDocument(42);
        // 构造连续相同 family 的 plan
        var repeated = doc.MappingPlan.Patterns.Select((p, i) => p with { Family = "stream" }).ToArray();
        doc = doc with { MappingPlan = new MappingPlan(doc.MappingPlan.Intents, repeated, doc.MappingPlan.Transitions) };

        var report = new BaselineMappingCritic().Evaluate(doc);
        Assert.That(report.SoftIssues.Select(i => i.Code), Does.Contain("pattern_repetition"));
    }

    [Test]
    public void Evaluate_EmptyObjects_HardIssue()
    {
        var doc = TestFixtures.BuildDocument(42) with { ConcreteObjects = Array.Empty<ConcreteObject>() };
        var report = new BaselineMappingCritic().Evaluate(doc);

        Assert.That(report.Valid, Is.False);
        Assert.That(report.HardIssues.Select(i => i.Code), Does.Contain("no_objects"));
    }
}

[TestFixture]
public class LocalPlannerTests
{
    private static LocalMappingContext ContextFor(int sectionIndex)
    {
        var timeline = TestFixtures.Timeline();
        var evidence = new DeterministicEvidenceBuilder().Build(timeline, TestFixtures.BalancedProfile());
        var ruleset = new RulesetInfo(RulesetKind.Mania, new Dictionary<string, object?> { ["keys"] = 4 });
        var globalPlan = new DeterministicGlobalPlanner().Plan(timeline, evidence, TestFixtures.BalancedProfile(), ruleset);
        var section = timeline.Sections[sectionIndex];

        return new LocalMappingContext(section, evidence, globalPlan, Array.Empty<PatternIntent>(), Array.Empty<PatternIntent>(), TestFixtures.BalancedProfile());
    }

    [Test]
    public void Plan_ChorusGetsClimax()
    {
        var intent = new DeterministicLocalPlanner().Plan(ContextFor(1));
        Assert.That(intent.Primary, Is.EqualTo(MappingPrimaryIntent.Climax));
        Assert.That(intent.Rationale, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void Plan_OutroGetsResolution()
    {
        var intent = new DeterministicLocalPlanner().Plan(ContextFor(2));
        Assert.That(intent.Primary, Is.EqualTo(MappingPrimaryIntent.Resolution));
    }

    [Test]
    public void Plan_Deterministic()
    {
        var planner = new DeterministicLocalPlanner();
        var a = planner.Plan(ContextFor(1));
        var b = planner.Plan(ContextFor(1));

        Assert.That(b.Primary, Is.EqualTo(a.Primary));
        Assert.That(b.Emphasis, Is.EqualTo(a.Emphasis));
    }
}
