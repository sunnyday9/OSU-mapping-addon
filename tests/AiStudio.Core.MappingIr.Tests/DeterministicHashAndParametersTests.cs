using System.Text.Json;
using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Patterns;
using AiStudio.Core.MappingIr.Serialization;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

[TestFixture]
public class DeterministicHashTests
{
    [Test]
    public void Fnv1a64_KnownVector()
    {
        // FNV-1a 64 位标准测试向量（"hello" 已知值）
        Assert.That(DeterministicHash.Fnv1a64("hello"), Is.EqualTo(0xa430d84680aabd0bUL));
    }

    [Test]
    public void Fnv1a64_DifferentInputs_DifferentHashes()
    {
        Assert.That(DeterministicHash.Fnv1a64("stream"), Is.Not.EqualTo(DeterministicHash.Fnv1a64("burst")));
        Assert.That(DeterministicHash.Fnv1a64("single_ln"), Is.Not.EqualTo(DeterministicHash.Fnv1a64("ln_rice")));
    }

    [Test]
    public void DeriveSeed_StableAcrossCalls()
    {
        int a = DeterministicHash.DeriveSeed("stream", 42);
        int b = DeterministicHash.DeriveSeed("stream", 42);
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void DeriveSeed_DifferentFamilies_Differ()
    {
        Assert.That(DeterministicHash.DeriveSeed("stream", 42), Is.Not.EqualTo(DeterministicHash.DeriveSeed("jack", 42)));
    }

    [Test]
    public void DeriveSeed_DifferentSeeds_Differ()
    {
        Assert.That(DeterministicHash.DeriveSeed("stream", 1), Is.Not.EqualTo(DeterministicHash.DeriveSeed("stream", 2)));
    }

    [Test]
    public void CreateFamilyRandom_IsDeterministic_AcrossInstances()
    {
        // 两个独立 context（同 seed）→ family random 序列一致（验证 FNV-1a 跨实例稳定）
        var ctx1 = new PatternGenerationContext(TestFixtures.Timeline(), TestFixtures.BuildDocument(7), Array.Empty<ConcreteObject>(), TestFixtures.BalancedProfile(), 7);
        var ctx2 = new PatternGenerationContext(TestFixtures.Timeline(), TestFixtures.BuildDocument(7), Array.Empty<ConcreteObject>(), TestFixtures.BalancedProfile(), 7);

        var r1 = ctx1.CreateFamilyRandom("jumpstream");
        var r2 = ctx2.CreateFamilyRandom("jumpstream");
        var seq1 = Enumerable.Range(0, 10).Select(_ => r1.Next()).ToArray();
        var seq2 = Enumerable.Range(0, 10).Select(_ => r2.Next()).ToArray();

        Assert.That(seq2, Is.EqualTo(seq1));
    }
}

[TestFixture]
public class ManiaPatternParametersTests
{
    private static IReadOnlyDictionary<string, object?> MemoryDict() => new Dictionary<string, object?>
    {
        ["subdivision"] = "1/16",
        ["density"] = 0.72,
        ["column_strategy"] = "mirror",
        ["column_order"] = new object[] { 3, 1, 2, 0 },
        ["jack_tolerance"] = 0.05,
        ["bpm"] = 174.0,
        ["chord_size"] = 2,
        ["chord_density"] = 0.4,
        ["ln_ratio"] = 0.5,
        ["ln_duration_beats"] = 2.0,
        ["count"] = 6,
        ["jack_column"] = 2,
    };

    [Test]
    public void FromDictionary_MemoryValues_Parsed()
    {
        var parsed = ManiaPatternParameters.FromDictionary(MemoryDict(), 180.0);

        Assert.That(parsed.Subdivision, Is.EqualTo("1/16"));
        Assert.That(parsed.Density, Is.EqualTo(0.72));
        Assert.That(parsed.ColumnStrategy, Is.EqualTo("mirror"));
        Assert.That(parsed.ColumnOrder, Is.EqualTo(new[] { 3, 1, 2, 0 }));
        Assert.That(parsed.Bpm, Is.EqualTo(174.0));
        Assert.That(parsed.ChordDensity, Is.EqualTo(0.4));
        Assert.That(parsed.LnRatio, Is.EqualTo(0.5));
        Assert.That(parsed.LnDurationBeats, Is.EqualTo(2.0));
        Assert.That(parsed.Count, Is.EqualTo(6));
        Assert.That(parsed.JackColumn, Is.EqualTo(2));
    }

    [Test]
    public void FromDictionary_JsonElementValues_Parsed()
    {
        // 模拟 JSON 反序列化后的字典（嵌套值全是 JsonElement）
        string json = JsonSerializer.Serialize(MemoryDict(), JsonMappingIrSerializer.Options);
        var jsonDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonMappingIrSerializer.Options)!;

        var parsed = ManiaPatternParameters.FromDictionary(jsonDict, 180.0);

        Assert.That(parsed.Subdivision, Is.EqualTo("1/16"));
        Assert.That(parsed.Density, Is.EqualTo(0.72));
        Assert.That(parsed.ColumnStrategy, Is.EqualTo("mirror"));
        Assert.That(parsed.ColumnOrder, Is.EqualTo(new[] { 3, 1, 2, 0 }));
        Assert.That(parsed.Bpm, Is.EqualTo(174.0));
        Assert.That(parsed.ChordDensity, Is.EqualTo(0.4));
        Assert.That(parsed.LnRatio, Is.EqualTo(0.5));
        Assert.That(parsed.Count, Is.EqualTo(6));
        Assert.That(parsed.JackColumn, Is.EqualTo(2));
    }

    [Test]
    public void FromDictionary_MissingKeys_FallbackToDefaults()
    {
        var parsed = ManiaPatternParameters.FromDictionary(new Dictionary<string, object?>(), 174.0);

        Assert.That(parsed.Subdivision, Is.EqualTo(ManiaPatternParameters.DefaultSubdivision));
        Assert.That(parsed.Density, Is.EqualTo(1.0), "density defaults to 1.0 = full rhythm points (SR calibration knob, MVP-B)");
        Assert.That(parsed.ColumnOrder, Is.EqualTo(new[] { 0, 2, 1, 3 }));
        Assert.That(parsed.Bpm, Is.EqualTo(174.0));
        Assert.That(parsed.JackColumn, Is.Null);
    }

    [Test]
    public void PatternIntentRoundTrip_JsonElementCompatible_ProviderOutputEqual()
    {
        // review 指出的核心缺口：内存构造的 intent 与 JSON 反序列化后的 intent，
        // 经过 provider 应产生完全一致的输出。
        var provider = new Mania4KPatternProvider();
        var intentMemory = new PatternIntent(
            "pattern_test",
            RulesetKind.Mania,
            "jumpstream",
            1000,
            8000,
            MemoryDict(),
            new Dictionary<string, object?> { ["allow_chords"] = true },
            0.9);

        // 序列化整个文档 → 反序列化 → 取 pattern 参数
        var doc = TestFixtures.BuildDocument(42) with
        {
            MappingPlan = new MappingPlan(
                TestFixtures.BuildDocument(42).MappingPlan.Intents,
                new[] { intentMemory },
                Array.Empty<PatternTransition>()),
        };
        string json = JsonMappingIrSerializer.Serialize(doc);
        var back = JsonMappingIrSerializer.Deserialize(json)!;
        var intentJson = back.MappingPlan.Patterns[0];

        var ctx = new PatternGenerationContext(TestFixtures.Timeline(), doc, Array.Empty<ConcreteObject>(), TestFixtures.BalancedProfile(), 42);
        var resultMemory = provider.Generate(intentMemory, ctx);
        var resultJson = provider.Generate(intentJson, ctx);

        Assert.That(resultJson.Objects.Count, Is.EqualTo(resultMemory.Objects.Count));
        Assert.That(resultJson.Objects.Select(o => o.Time), Is.EqualTo(resultMemory.Objects.Select(o => o.Time)));
        Assert.That(resultJson.Objects.Select(o => o.Column), Is.EqualTo(resultMemory.Objects.Select(o => o.Column)));
        Assert.That(resultJson.Objects.Select(o => o.Type), Is.EqualTo(resultMemory.Objects.Select(o => o.Type)));
        Assert.That(resultJson.Objects.Select(o => o.EndTime), Is.EqualTo(resultMemory.Objects.Select(o => o.EndTime)));
    }
}
