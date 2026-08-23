using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Validation;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

[TestFixture]
public class MappingValidatorTests
{
    [Test]
    public void ValidDocument_Passes()
    {
        var doc = TestFixtures.BuildDocument(seed: 42);
        var result = new MappingValidator().Validate(doc);

        Assert.That(result.Valid, Is.True);
        Assert.That(result.Issues, Is.Empty);
    }

    [Test]
    public void SchemaMismatch_Fails()
    {
        var doc = TestFixtures.BuildDocument() with { Schema = "wrong-schema" };
        var result = new MappingValidator().Validate(doc);

        Assert.That(result.Valid, Is.False);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("schema_mismatch"));
    }

    [Test]
    public void VersionMismatch_Fails()
    {
        var doc = TestFixtures.BuildDocument() with { Version = "9.9.9" };
        var result = new MappingValidator().Validate(doc);

        Assert.That(result.Valid, Is.False);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("version_mismatch"));
    }

    [Test]
    public void InvalidColumn_Fails()
    {
        var doc = TestFixtures.BuildDocument() with
        {
            ConcreteObjects = new[]
            {
                new ConcreteObject("bad", "hit", 100, Column: 4), // 4 > 3
            },
        };
        var result = new MappingValidator().Validate(doc);

        Assert.That(result.Valid, Is.False);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("invalid_column"));
    }

    [Test]
    public void HoldWithEndBeforeStart_Fails()
    {
        var doc = TestFixtures.BuildDocument() with
        {
            ConcreteObjects = new[]
            {
                new ConcreteObject("bad", "hold", 500, EndTime: 400, Column: 0),
            },
        };
        var result = new MappingValidator().Validate(doc);

        Assert.That(result.Valid, Is.False);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("invalid_ln"));
    }

    [Test]
    public void SameColumnOverlap_Fails()
    {
        var doc = TestFixtures.BuildDocument() with
        {
            ConcreteObjects = new[]
            {
                new ConcreteObject("a", "hold", 100, EndTime: 400, Column: 0),
                new ConcreteObject("b", "hit", 250, Column: 0), // 落在 a 的 [100,400) 内
                new ConcreteObject("c", "hit", 200, Column: 1),
            },
        };
        var result = new MappingValidator().Validate(doc);

        Assert.That(result.Valid, Is.False);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("column_overlap"));
    }

    [Test]
    public void NonManiaRuleset_Warns()
    {
        var baseDoc = TestFixtures.BuildDocument();
        var taikoPlan = baseDoc.MappingPlan with
        {
            Patterns = baseDoc.MappingPlan.Patterns.Select(p => p with { Ruleset = RulesetKind.Taiko }).ToArray(),
        };
        var doc = baseDoc with
        {
            Ruleset = new RulesetInfo(RulesetKind.Taiko, new Dictionary<string, object?>()),
            MappingPlan = taikoPlan,
        };
        var result = new MappingValidator().Validate(doc);

        // 结构仍合法（warning 不阻断），但 unsupported_ruleset 标记
        Assert.That(result.Valid, Is.True);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("unsupported_ruleset"));
    }

    [Test]
    public void EmptyObjects_Fails()
    {
        var doc = TestFixtures.BuildDocument() with { ConcreteObjects = Array.Empty<ConcreteObject>() };
        var result = new MappingValidator().Validate(doc);

        Assert.That(result.Valid, Is.False);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("no_objects"));
    }

    [Test]
    public void PatternRulesetMismatch_Fails()
    {
        var doc = TestFixtures.BuildDocument();
        var badPattern = doc.MappingPlan.Patterns[0] with { Ruleset = RulesetKind.Osu };
        doc = doc with
        {
            MappingPlan = new MappingPlan(doc.MappingPlan.Intents, new[] { badPattern }, doc.MappingPlan.Transitions),
        };
        var result = new MappingValidator().Validate(doc);

        Assert.That(result.Valid, Is.False);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("pattern_ruleset_mismatch"));
    }

    [Test]
    public void InvalidIntentRange_Fails()
    {
        var doc = TestFixtures.BuildDocument();
        var badIntent = doc.MappingPlan.Intents[0] with { EndTime = doc.MappingPlan.Intents[0].StartTime };
        doc = doc with
        {
            MappingPlan = new MappingPlan(new[] { badIntent }, doc.MappingPlan.Patterns, doc.MappingPlan.Transitions),
        };
        var result = new MappingValidator().Validate(doc);

        Assert.That(result.Valid, Is.False);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("invalid_intent_range"));
    }
}
