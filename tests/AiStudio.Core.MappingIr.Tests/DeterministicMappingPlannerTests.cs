using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Planning;
using NUnit.Framework;

namespace AiStudio.Core.MappingIr.Tests;

[TestFixture]
public class DeterministicMappingPlannerTests
{
    [Test]
    public void Plan_CoversAllSections()
    {
        var timeline = TestFixtures.Timeline();
        var plan = new DeterministicMappingPlanner().Plan(timeline, TestFixtures.BalancedProfile(), seed: 0);

        Assert.That(plan.Intents.Count, Is.EqualTo(timeline.Sections.Count));
        Assert.That(plan.Patterns.Count, Is.EqualTo(timeline.Sections.Count));
        Assert.That(plan.Transitions.Count, Is.EqualTo(timeline.Sections.Count - 1));
    }

    [Test]
    public void Plan_Deterministic()
    {
        var timeline = TestFixtures.Timeline();
        var planner = new DeterministicMappingPlanner();
        var a = planner.Plan(timeline, TestFixtures.BalancedProfile(), seed: 42);
        var b = planner.Plan(timeline, TestFixtures.BalancedProfile(), seed: 42);

        Assert.That(b.Intents.Select(i => i.Id), Is.EqualTo(a.Intents.Select(i => i.Id)));
        Assert.That(b.Patterns.Select(p => p.Family), Is.EqualTo(a.Patterns.Select(p => p.Family)));
    }

    [Test]
    public void Plan_ChorusGetsClimaxOrEscalation()
    {
        var timeline = TestFixtures.Timeline();
        var plan = new DeterministicMappingPlanner().Plan(timeline, TestFixtures.BalancedProfile(), seed: 0);

        var chorus = plan.Intents[1];
        Assert.That(chorus.Primary, Is.AnyOf(MappingPrimaryIntent.Climax, MappingPrimaryIntent.Escalation));
    }

    [Test]
    public void Plan_EveryPatternHasRationale()
    {
        var plan = new DeterministicMappingPlanner().Plan(TestFixtures.Timeline(), TestFixtures.BalancedProfile(), seed: 0);

        foreach (var intent in plan.Intents)
        {
            Assert.That(intent.Rationale, Is.Not.Null.And.Not.Empty, $"intent '{intent.Id}' missing rationale");
        }

        foreach (var pattern in plan.Patterns)
        {
            Assert.That(pattern.Rationale, Is.Not.Null.And.Not.Empty, $"pattern '{pattern.Id}' missing rationale");
        }
    }

    [Test]
    public void Plan_TransitionsUseValidLabels()
    {
        var plan = new DeterministicMappingPlanner().Plan(TestFixtures.Timeline(), TestFixtures.BalancedProfile(), seed: 0);

        string[] valid = { "same_family", "rhythm_increase", "rhythm_decrease", "density_increase", "density_decrease", "hand_rebalance", "column_rotation", "chord_introduction", "chord_removal", "ln_introduction", "ln_release", "pattern_break", "reset" };
        foreach (var transition in plan.Transitions)
        {
            Assert.That(valid, Does.Contain(transition.TransitionType), $"invalid transition type '{transition.TransitionType}'");
        }
    }

    [Test]
    public void Plan_SubdivisionInAllowedSet()
    {
        var plan = new DeterministicMappingPlanner().Plan(TestFixtures.Timeline(), TestFixtures.BalancedProfile(), seed: 0);

        string[] allowed = { "1/1", "1/2", "1/4", "1/8", "1/12", "1/16", "1/24" };
        foreach (var pattern in plan.Patterns)
        {
            string? subdivision = Convert.ToString(pattern.Parameters["subdivision"]);
            Assert.That(allowed, Does.Contain(subdivision), $"invalid subdivision '{subdivision}'");
        }
    }
}
