using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Patterns;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

[TestFixture]
public class Mania4KPatternProviderTests
{
    private static readonly string[] families = { "single", "stream", "burst", "jack", "jump", "jumpstream", "single_ln", "ln_rice", "ln_release" };

    private static PatternIntent Intent(string family, int start = 1000, int end = 8000, Dictionary<string, object?>? parameters = null)
        => new(
            $"pattern_{family}",
            RulesetKind.Mania,
            family,
            start,
            end,
            parameters ?? new Dictionary<string, object?>
            {
                ["subdivision"] = "1/8",
                ["column_order"] = new object[] { 0, 2, 1, 3 },
                ["bpm"] = 174.0,
            },
            new Dictionary<string, object?>
            {
                ["max_consecutive_same_column"] = 1,
                ["allow_chords"] = false,
                ["allow_ln"] = false,
                ["max_chord_size"] = 2,
            },
            0.9);

    private static PatternGenerationContext Context(int seed = 42, IReadOnlyList<ConcreteObject>? previous = null)
        => new(
            TestFixtures.Timeline(),
            TestFixtures.BuildDocument(seed),
            previous ?? Array.Empty<ConcreteObject>(),
            TestFixtures.BalancedProfile(),
            seed);

    [Test]
    public void AllFamilies_GenerateObjects()
    {
        var provider = new Mania4KPatternProvider();
        foreach (string family in families)
        {
            var result = provider.Generate(Intent(family), Context());
            Assert.That(result.Objects.Count, Is.GreaterThan(0), $"family '{family}' produced no objects");
            Assert.That(result.Issues, Is.Empty, $"family '{family}' produced issues");
        }
    }

    [Test]
    public void AllObjects_ColumnsInRange_TimeMonotonic()
    {
        var provider = new Mania4KPatternProvider();
        foreach (string family in families)
        {
            var result = provider.Generate(Intent(family), Context());
            int lastTime = -1;
            foreach (var obj in result.Objects.OrderBy(o => o.Time))
            {
                Assert.That(obj.Column, Is.InRange(0, 3), $"family '{family}' object '{obj.Id}' column out of range");
                Assert.That(obj.Time, Is.GreaterThanOrEqualTo(lastTime), $"family '{family}' not time-monotonic");
                lastTime = obj.Time;
            }
        }
    }

    [Test]
    public void HoldObjects_EndTimeGreaterThanStart()
    {
        var provider = new Mania4KPatternProvider();
        foreach (string family in new[] { "single_ln", "ln_rice", "ln_release" })
        {
            var result = provider.Generate(Intent(family), Context());
            foreach (var obj in result.Objects.Where(o => o.Type == "hold"))
            {
                Assert.That(obj.EndTime, Is.Not.Null, $"family '{family}' hold missing end_time");
                Assert.That(obj.EndTime!.Value, Is.GreaterThan(obj.Time), $"family '{family}' hold end <= start");
            }
        }
    }

    [Test]
    public void NoSameColumnOverlap_AcrossAllFamilies()
    {
        var provider = new Mania4KPatternProvider();
        foreach (string family in families)
        {
            var result = provider.Generate(Intent(family), Context());
            var byColumn = result.Objects.Where(o => o.Column is not null).GroupBy(o => o.Column!.Value);
            foreach (var group in byColumn)
            {
                var ordered = group.OrderBy(o => o.Time).ToList();
                for (int i = 1; i < ordered.Count; i++)
                {
                    var prev = ordered[i - 1];
                    var cur = ordered[i];
                    // 同列对象不允许 start 落在前一个对象的 [start, end) 区间内
                    bool overlaps = cur.Time < (prev.EndTime ?? prev.Time) && cur.Time >= prev.Time;
                    Assert.That(overlaps, Is.False, $"family '{family}' column {group.Key} overlap: {prev.Id}@{prev.Time} vs {cur.Id}@{cur.Time}");
                }
            }
        }
    }

    [Test]
    public void JackFamily_RepeatedColumn()
    {
        var provider = new Mania4KPatternProvider();
        var intent = Intent("jack", parameters: new Dictionary<string, object?>
        {
            ["subdivision"] = "1/8",
            ["column_order"] = new object[] { 0, 2, 1, 3 },
            ["bpm"] = 174.0,
            ["jack_column"] = 1,
            ["count"] = 5,
        });
        var result = provider.Generate(intent, Context());

        Assert.That(result.Objects.Count, Is.EqualTo(5));
        Assert.That(result.Objects.All(o => o.Column == 1), Is.True, "jack must stay on configured column");
        // 同列连续 jack：间隔应 ≥ 最小节拍间隔（1/8 拍）
        var times = result.Objects.Select(o => o.Time).OrderBy(t => t).ToList();
        double beatMs = 60000.0 / 174.0;
        for (int i = 1; i < times.Count; i++)
        {
            Assert.That(times[i] - times[i - 1], Is.GreaterThanOrEqualTo(beatMs / 8 - 1), "jack interval below minimum grid");
        }
    }

    [Test]
    public void Deterministic_SameSeedSameOutput()
    {
        var provider = new Mania4KPatternProvider();
        foreach (string family in families)
        {
            var a = provider.Generate(Intent(family), Context(seed: 42));
            var b = provider.Generate(Intent(family), Context(seed: 42));
            Assert.That(b.Objects.Select(o => o.Id), Is.EqualTo(a.Objects.Select(o => o.Id)), $"family '{family}' not deterministic");
            Assert.That(b.Objects.Select(o => o.Time), Is.EqualTo(a.Objects.Select(o => o.Time)));
            Assert.That(b.Objects.Select(o => o.Column), Is.EqualTo(a.Objects.Select(o => o.Column)));
        }
    }

    [Test]
    public void Deterministic_DifferentSeedDifferentOutput_ForRandomFamilies()
    {
        var provider = new Mania4KPatternProvider();
        // 含随机的 family（jumpstream/ln_rice）应随 seed 变化
        foreach (string family in new[] { "jumpstream", "ln_rice" })
        {
            var a = provider.Generate(Intent(family), Context(seed: 1));
            var b = provider.Generate(Intent(family), Context(seed: 2));
            var colsA = string.Join(",", a.Objects.Select(o => $"{o.Time}:{o.Column}"));
            var colsB = string.Join(",", b.Objects.Select(o => $"{o.Time}:{o.Column}"));
            Assert.That(colsA, Is.Not.EqualTo(colsB), $"family '{family}' should vary across seeds");
        }
    }

    [Test]
    public void WrongRuleset_ReturnsErrorIssue()
    {
        var provider = new Mania4KPatternProvider();
        var intent = Intent("stream") with { Ruleset = RulesetKind.Osu };
        var result = provider.Generate(intent, Context());

        Assert.That(result.Objects, Is.Empty);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("ruleset_mismatch"));
    }

    [Test]
    public void UnknownFamily_ReturnsErrorIssue()
    {
        var provider = new Mania4KPatternProvider();
        var result = provider.Generate(Intent("nonexistent"), Context());

        Assert.That(result.Objects, Is.Empty);
        Assert.That(result.Issues.Select(i => i.Code), Does.Contain("unknown_family"));
    }

    [Test]
    public void AllObjects_LandOnRhythmGrid()
    {
        // 节奏网格 = 1/16 拍（最小细分）；对象必须落在网格上（±2ms）
        var provider = new Mania4KPatternProvider();
        double beatMs = 60000.0 / TestFixtures.Bpm;
        double grid = beatMs / 16.0;

        foreach (string family in families)
        {
            var result = provider.Generate(Intent(family), Context());
            foreach (var obj in result.Objects)
            {
                double nearest = Math.Round(obj.Time / grid) * grid;
                Assert.That(Math.Abs(obj.Time - nearest), Is.LessThan(2.0), $"family '{family}' object '{obj.Id}' off rhythm grid");
            }
        }
    }
}
