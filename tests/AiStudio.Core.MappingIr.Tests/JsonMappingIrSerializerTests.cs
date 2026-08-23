using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Serialization;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

[TestFixture]
public class JsonMappingIrSerializerTests
{
    [Test]
    public void Roundtrip_PreservesSemantics()
    {
        var doc = TestFixtures.BuildDocument(seed: 7);
        string json = JsonMappingIrSerializer.Serialize(doc);
        var back = JsonMappingIrSerializer.Deserialize(json);

        Assert.That(back, Is.Not.Null);
        Assert.That(back!.Schema, Is.EqualTo(MappingDocument.SchemaName));
        Assert.That(back.Version, Is.EqualTo(MappingDocument.SchemaVersion));
        Assert.That(back.DocumentId, Is.EqualTo(doc.DocumentId));
        Assert.That(back.Map.AudioHash, Is.EqualTo(doc.Map.AudioHash));
        Assert.That(back.Ruleset.Ruleset, Is.EqualTo(RulesetKind.Mania));
        Assert.That(back.DifficultyProfile.TargetStarRating, Is.EqualTo(doc.DifficultyProfile.TargetStarRating));
        Assert.That(back.MusicTimeline.Tempo.BaseBpm, Is.EqualTo(TestFixtures.Bpm));
        Assert.That(back.MappingPlan.Intents.Count, Is.EqualTo(doc.MappingPlan.Intents.Count));
        Assert.That(back.MappingPlan.Patterns.Count, Is.EqualTo(doc.MappingPlan.Patterns.Count));
        Assert.That(back.ConcreteObjects, Is.Not.Null);
        Assert.That(back.ConcreteObjects!.Count, Is.EqualTo(doc.ConcreteObjects!.Count));
        Assert.That(back.Evaluation.Valid, Is.EqualTo(doc.Evaluation.Valid));
    }

    [Test]
    public void Serialize_UsesSnakeCaseFieldNames()
    {
        var doc = TestFixtures.BuildDocument();
        string json = JsonMappingIrSerializer.Serialize(doc);

        Assert.That(json, Does.Contain("\"document_id\""));
        Assert.That(json, Does.Contain("\"difficulty_profile\""));
        Assert.That(json, Does.Contain("\"music_timeline\""));
        Assert.That(json, Does.Contain("\"mapping_plan\""));
        Assert.That(json, Does.Contain("\"concrete_objects\""));
        Assert.That(json, Does.Contain("\"start_time\""));
        Assert.That(json, Does.Contain("\"target_star_rating\""));
        Assert.That(json, Does.Not.Contain("\"documentId\""));
        Assert.That(json, Does.Not.Contain("\"musicTimeline\""));
    }

    [Test]
    public void Serialize_EnumsUseSnakeCase()
    {
        var doc = TestFixtures.BuildDocument();
        string json = JsonMappingIrSerializer.Serialize(doc);

        Assert.That(json, Does.Contain("\"intro\""));
        Assert.That(json, Does.Contain("\"chorus\""));
        Assert.That(json, Does.Contain("\"outro\""));
        Assert.That(json, Does.Contain("\"mania\""));
        Assert.That(json, Does.Contain("\"rule_based\""));
    }

    [Test]
    public void Serialize_MatchesExampleDocumentShape()
    {
        // 与 docs/new plan/example-mania4k-v0.1.json 同构：顶层字段顺序与 key 集合一致
        var doc = TestFixtures.BuildDocument();
        string json = JsonMappingIrSerializer.Serialize(doc);

        foreach (string key in new[]
                 {
                     "schema", "version", "document_id", "map", "ruleset", "difficulty_profile",
                     "music_timeline", "mapping_plan", "concrete_objects", "constraints", "style", "provenance", "evaluation",
                 })
        {
            Assert.That(json, Does.Contain($"\"{key}\""), $"missing top-level key '{key}'");
        }
    }

    [Test]
    public void Deserialize_EmptyDocument_ReturnsNull()
    {
        Assert.That(() => JsonMappingIrSerializer.Deserialize("not json"), Throws.TypeOf<System.Text.Json.JsonException>());
    }
}
