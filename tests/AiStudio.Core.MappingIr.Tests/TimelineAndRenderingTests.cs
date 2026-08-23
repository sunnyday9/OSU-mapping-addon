using AiStudio.Core.Analysis;
using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Rendering;
using AiStudio.Core.MappingIr.Timeline;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

[TestFixture]
public class MusicTimelineBuilderTests
{
    [Test]
    public void Build_ProducesFullTimeline()
    {
        var timeline = TestFixtures.Timeline();

        Assert.That(timeline.DurationMs, Is.EqualTo(TestFixtures.DurationMs));
        Assert.That(timeline.Tempo.BaseBpm, Is.EqualTo(TestFixtures.Bpm));
        Assert.That(timeline.Sections.Count, Is.EqualTo(3));
        Assert.That(timeline.Phrases.Count, Is.EqualTo(3));
        Assert.That(timeline.Events.Count, Is.GreaterThan(0));
    }

    [Test]
    public void Build_SectionsSnapToBeatGrid()
    {
        var timeline = TestFixtures.Timeline();

        double beatMs = 60000.0 / TestFixtures.Bpm;
        foreach (var section in timeline.Sections)
        {
            double startResid = Math.Abs(section.StartTime - Math.Round(section.StartTime / beatMs) * beatMs);
            double endResid = Math.Abs(section.EndTime - Math.Round(section.EndTime / beatMs) * beatMs);
            Assert.That(startResid, Is.LessThan(1.0), $"section '{section.Id}' start off beat grid");
            Assert.That(endResid, Is.LessThan(1.0), $"section '{section.Id}' end off beat grid");
        }
    }

    [Test]
    public void Build_EventsCoverAllBeats()
    {
        var timeline = TestFixtures.Timeline();

        int beatCount = timeline.Events.Count;
        Assert.That(beatCount, Is.GreaterThan(0));
        // 每个 beat 一个事件，且首尾覆盖
        Assert.That(timeline.Events[0].Time, Is.EqualTo(0));
        Assert.That(timeline.Events[^1].Time, Is.GreaterThanOrEqualTo(TestFixtures.DurationMs - 400));
        Assert.That(timeline.Events.All(e => e.Type == MusicEventType.Beat || e.Type == MusicEventType.Onset), Is.True);
    }

    [Test]
    public void Build_EmptyGrid_ProducesEmptyTimeline()
    {
        var builder = new MusicTimelineBuilder();
        var timeline = builder.Build(new BeatGrid(0, 0, Array.Empty<double>()), Array.Empty<AudioSection>());

        Assert.That(timeline.DurationMs, Is.EqualTo(0));
        Assert.That(timeline.Sections, Is.Empty);
        Assert.That(timeline.Events, Is.Empty);
    }
}

[TestFixture]
public class ManiaOsuRendererTests
{
    [Test]
    public void Render_ContainsAllSections()
    {
        var doc = TestFixtures.BuildDocument();
        string osu = new ManiaOsuRenderer().Render(doc);

        Assert.That(osu, Does.StartWith("osu file format v14"));
        Assert.That(osu, Does.Contain("[General]"));
        Assert.That(osu, Does.Contain("[Metadata]"));
        Assert.That(osu, Does.Contain("[Difficulty]"));
        Assert.That(osu, Does.Contain("[TimingPoints]"));
        Assert.That(osu, Does.Contain("[HitObjects]"));
    }

    [Test]
    public void Render_ModeIsMania()
    {
        string osu = new ManiaOsuRenderer().Render(TestFixtures.BuildDocument());
        Assert.That(osu, Does.Contain("Mode: 3"));
        Assert.That(osu, Does.Contain("CircleSize:4"));
    }

    [Test]
    public void Render_HitObjectCountMatchesDocument()
    {
        var doc = TestFixtures.BuildDocument();
        string osu = new ManiaOsuRenderer().Render(doc);

        int body = osu.IndexOf("[HitObjects]", StringComparison.Ordinal);
        var lines = osu[(body + "[HitObjects]".Length)..].Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.That(lines.Length, Is.EqualTo(doc.ConcreteObjects!.Count));
    }

    [Test]
    public void Render_Deterministic()
    {
        var doc = TestFixtures.BuildDocument(seed: 5);
        var renderer = new ManiaOsuRenderer();
        Assert.That(renderer.Render(doc), Is.EqualTo(renderer.Render(doc)));
    }

    [Test]
    public void Render_HoldObjectsUseType128()
    {
        var doc = TestFixtures.BuildDocument();
        // 构造一个含 hold 的最小文档
        doc = doc with
        {
            ConcreteObjects = new[]
            {
                new ConcreteObject("h1", "hold", 1000, EndTime: 1500, Column: 1),
                new ConcreteObject("n1", "hit", 2000, Column: 0),
            },
        };
        string osu = new ManiaOsuRenderer().Render(doc);

        Assert.That(osu, Does.Contain("192,192,1000,128,0,1500:0:0:0:0:"), "hold object must be type 128 with endTime:0 hitSample");
        Assert.That(osu, Does.Contain("64,192,2000,1,0,0:0:0:0:"), "hit object must be type 1");
    }
}

[TestFixture]
public class MappingIrPipelineTests
{
    [Test]
    public void Run_EndToEnd_ProducesValidDocument()
    {
        var pipeline = new MappingIrPipeline(TestFixtures.Analyzer());
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpa_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");

        try
        {
            var doc = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 42);

            Assert.That(doc.MusicTimeline.Sections.Count, Is.GreaterThan(0));
            Assert.That(doc.MappingPlan.Intents.Count, Is.GreaterThan(0));
            Assert.That(doc.ConcreteObjects, Is.Not.Null.And.Count.GreaterThan(0));
            Assert.That(doc.Evaluation.Valid, Is.True);
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    [Test]
    public void Run_DeterministicAcrossSeeds_ExceptRandomFamilies()
    {
        var pipeline = new MappingIrPipeline(TestFixtures.Analyzer());
        string pseudo = Path.Combine(Path.GetTempPath(), $"mvpa_{Guid.NewGuid():N}.mp3");
        File.WriteAllText(pseudo, "placeholder");

        try
        {
            var a = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 1);
            var b = pipeline.Run(pseudo, TestFixtures.BalancedProfile(), seed: 1);

            Assert.That(b.ConcreteObjects!.Count, Is.EqualTo(a.ConcreteObjects!.Count));
            Assert.That(b.ConcreteObjects!.Select(o => o.Time), Is.EqualTo(a.ConcreteObjects!.Select(o => o.Time)));
            Assert.That(b.ConcreteObjects!.Select(o => o.Column), Is.EqualTo(a.ConcreteObjects!.Select(o => o.Column)));
        }
        finally
        {
            File.Delete(pseudo);
        }
    }

    [Test]
    public void MusicAlignment_AllObjectsOnGrid_IsOne()
    {
        var doc = TestFixtures.BuildDocument();
        double score = MappingIrPipeline.musicAlignmentScore(doc.MusicTimeline, doc.ConcreteObjects!);

        Assert.That(score, Is.EqualTo(1.0).Within(0.001));
    }
}
