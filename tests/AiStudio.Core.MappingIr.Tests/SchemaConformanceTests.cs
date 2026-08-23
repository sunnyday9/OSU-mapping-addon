using System.Text.Json;
using AiStudio.Core.MappingIr.Serialization;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

/// <summary>
/// Canonical JSON Schema 一致性测试（code-review P0-3 / spec §36 "JSON serialization is schema-valid"）。
/// 断言序列化产物与 <c>mapping-ir-v0.1.schema.json</c> 的键集合/枚举值/约束对齐。
/// 注意：完整 JSON Schema 语义校验在 CI 用 Python jsonschema 执行（demo 产物），
/// 这里做 C# 侧可重复的 shape 断言（顶层键/枚举/非 null 归一化）。
/// </summary>
[TestFixture]
public class SchemaConformanceTests
{
    private static JsonDocument LoadCanonicalSchema()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "MappingIr", "Schema", "mapping-ir-v0.1.schema.json");
        if (!File.Exists(path))
        {
            // 测试项目内嵌路径回退
            path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "AiStudio.Core", "MappingIr", "Schema", "mapping-ir-v0.1.schema.json");
        }

        Assert.That(File.Exists(path), Is.True, $"canonical schema not found at {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    [Test]
    public void SerializedDocument_AllTopLevelKeysMatchSchema()
    {
        string json = JsonMappingIrSerializer.Serialize(TestFixtures.BuildDocument(42));
        using var doc = JsonDocument.Parse(json);
        using var schema = LoadCanonicalSchema();

        var schemaProps = schema.RootElement.GetProperty("properties").EnumerateObject().Select(p => p.Name).ToHashSet();
        var actualKeys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        // 序列化产物键集合 ⊆ schema 定义的属性（不允许 schema 外的键）
        foreach (string key in actualKeys)
            Assert.That(schemaProps, Does.Contain(key), $"serialized key '{key}' not in canonical schema");

        // schema 的 required 键必须全部出现
        var required = schema.RootElement.GetProperty("required").EnumerateArray().Select(e => e.GetString()!).ToArray();
        foreach (string key in required)
            Assert.That(actualKeys, Does.Contain(key), $"required schema key '{key}' missing from serialized doc");
    }

    [Test]
    public void SerializedEnums_MatchSchemaEnumValues()
    {
        string json = JsonMappingIrSerializer.Serialize(TestFixtures.BuildDocument(42));
        using var doc = JsonDocument.Parse(json);
        using var schema = LoadCanonicalSchema();
        var defs = schema.RootElement.GetProperty("$defs");

        // 校验 music_timeline.sections[].type ∈ schema 枚举
        var sectionTypes = doc.RootElement.GetProperty("music_timeline").GetProperty("sections")
            .EnumerateArray().Select(s => s.GetProperty("type").GetString()!).Distinct().ToArray();
        var schemaSectionEnum = defs.GetProperty("section").GetProperty("properties").GetProperty("type").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        foreach (string t in sectionTypes)
            Assert.That(schemaSectionEnum, Does.Contain(t), $"section type '{t}' not in schema enum");

        // 校验 ruleset.ruleset ∈ schema 枚举
        string ruleset = doc.RootElement.GetProperty("ruleset").GetProperty("ruleset").GetString()!;
        var schemaRulesetEnum = defs.GetProperty("ruleset").GetProperty("properties").GetProperty("ruleset").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.That(schemaRulesetEnum, Does.Contain(ruleset), $"ruleset '{ruleset}' not in schema enum");

        // 校验 provenance.origin ∈ schema 枚举
        string origin = doc.RootElement.GetProperty("provenance").GetProperty("origin").GetString()!;
        var schemaProvenanceEnum = defs.GetProperty("provenance").GetProperty("properties").GetProperty("origin").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.That(schemaProvenanceEnum, Does.Contain(origin), $"provenance origin '{origin}' not in schema enum");
    }

    [Test]
    public void SerializedSections_LabelsNeverNull()
    {
        // schema: labels 是 array（不允许 null）；converter 归一化为 []
        string json = JsonMappingIrSerializer.Serialize(TestFixtures.BuildDocument(42));
        using var doc = JsonDocument.Parse(json);

        foreach (var section in doc.RootElement.GetProperty("music_timeline").GetProperty("sections").EnumerateArray())
        {
            var labels = section.GetProperty("labels");
            Assert.That(labels.ValueKind, Is.EqualTo(JsonValueKind.Array), $"section labels must be array, got {labels.ValueKind}");
        }
    }

    [Test]
    public void SerializedEvaluation_DifficultyNeverNull()
    {
        // schema: evaluation.difficulty 是 object（不允许 null）
        string json = JsonMappingIrSerializer.Serialize(TestFixtures.BuildDocument(42));
        using var doc = JsonDocument.Parse(json);

        var difficulty = doc.RootElement.GetProperty("evaluation").GetProperty("difficulty");
        Assert.That(difficulty.ValueKind, Is.EqualTo(JsonValueKind.Object), $"evaluation.difficulty must be object, got {difficulty.ValueKind}");
    }

    [Test]
    public void SchemaFile_IsCanonicalCopy()
    {
        // canonical 副本与 docs/new plan 的 schema 必须一致（防漂移）
        string canonical = Path.Combine(AppContext.BaseDirectory, "MappingIr", "Schema", "mapping-ir-v0.1.schema.json");
        string repo = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docs", "new plan", "mapping-ir-v0.1.schema.json");
        if (File.Exists(repo))
        {
            string a = File.ReadAllText(canonical).Trim();
            string b = File.ReadAllText(repo).Trim();
            Assert.That(a, Is.EqualTo(b), "canonical schema copy drifted from docs/new plan");
        }
    }
}
