using AiStudio.Core.MappingIr.Backends;
using AiStudio.Core.MappingIr.Difficulty;
using AiStudio.Core.MappingIr.Model;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

[TestFixture]
public class BackendTests
{
    [Test]
    public void ManiaBackend_ExposesManiaRuleset()
    {
        var backend = new Mania4KMappingBackend();
        Assert.That(backend.Ruleset, Is.EqualTo(RulesetKind.Mania));
        Assert.That(backend.Provider, Is.Not.Null);
        Assert.That(backend.Validator, Is.Not.Null);
    }

    [Test]
    public void ManiaBackend_RendersOsu()
    {
        var backend = new Mania4KMappingBackend();
        string osu = backend.Render(TestFixtures.BuildDocument(42));

        Assert.That(osu, Does.Contain("osu file format v14"));
        Assert.That(osu, Does.Contain("Mode: 3"));
        Assert.That(osu, Does.Contain("[HitObjects]"));
    }

    [Test]
    public void Pipeline_WithBackendInjection_ProducesValidDocument()
    {
        // 新式构造：backend 注入
        var pipeline = new MappingIrPipeline(
            TestFixtures.Analyzer(),
            evidenceBuilder: null,
            globalPlanner: null,
            localPlanner: null,
            candidateGenerator: null,
            candidateRanker: null,
            backend: new Mania4KMappingBackend(),
            critic: null,
            difficultyEvaluator: new UnavailableDifficultyEvaluator());

        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpa2_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");
        try
        {
            var doc = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 42);

            Assert.That(doc.ConcreteObjects, Is.Not.Null.And.Count.GreaterThan(0));
            Assert.That(doc.Evaluation.Valid, Is.True);
            Assert.That(doc.MappingPlan.Intents.Count, Is.EqualTo(doc.MusicTimeline.Sections.Count));
            // evaluation 含 observed_star_rating（Unavailable → null）
            Assert.That(doc.Evaluation.Difficulty!.ContainsKey("observed_star_rating"), Is.True);
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    [Test]
    public void Pipeline_DifficultyKnown_FalseWhenEvaluatorUnavailable()
    {
        // UnavailableDifficultyEvaluator → observed_star_rating null → 不声称达到目标 SR
        var evaluator = new UnavailableDifficultyEvaluator();
        Assert.That(evaluator.TryEvaluateStarRating(TestFixtures.BuildDocument(42)), Is.Null);
    }

    [Test]
    public void Pipeline_RenderOsu_GoesThroughBackend()
    {
        var pipeline = new MappingIrPipeline(TestFixtures.Analyzer());
        string osu = pipeline.RenderOsu(TestFixtures.BuildDocument(42));
        Assert.That(osu, Does.Contain("[HitObjects]"));
    }
}
