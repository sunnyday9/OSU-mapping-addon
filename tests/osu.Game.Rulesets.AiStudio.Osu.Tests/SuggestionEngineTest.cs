using AiStudio.Core.Models;
using NUnit.Framework;
using osu.Game.Rulesets.AiStudio.Osu.Suggestions;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Checks.Components;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

[TestFixture]
public class SuggestionEngineTest
{
    private TestCheck check = null!;

    [SetUp]
    public void Setup()
    {
        check = new TestCheck();
    }

    [Test]
    public void ProblemMapsToWarning()
    {
        var suggestions = SuggestionEngine.FromIssues(new[] { new Issue(check.ProblemTemplate, 42) });

        Assert.That(suggestions, Has.Count.EqualTo(1));
        Assert.That(suggestions[0].Severity, Is.EqualTo(SuggestionSeverity.Warning));
        Assert.That(suggestions[0].Title, Is.EqualTo(check.Metadata.Description));
        Assert.That(suggestions[0].RelatedCheck, Is.EqualTo(check.Metadata.Description));
        Assert.That(suggestions[0].Detail, Does.Contain("42"));
    }

    [Test]
    public void WarningMapsToAdvice()
    {
        var suggestions = SuggestionEngine.FromIssues(new[] { new Issue(check.WarningTemplate, 1) });

        Assert.That(suggestions, Has.Count.EqualTo(1));
        Assert.That(suggestions[0].Severity, Is.EqualTo(SuggestionSeverity.Advice));
    }

    [Test]
    public void ErrorMapsToWarning()
    {
        var suggestions = SuggestionEngine.FromIssues(new[] { new Issue(check.ErrorTemplate, 1) });

        Assert.That(suggestions, Has.Count.EqualTo(1));
        Assert.That(suggestions[0].Severity, Is.EqualTo(SuggestionSeverity.Warning));
    }

    [Test]
    public void NegligibleMapsToInfo()
    {
        var suggestions = SuggestionEngine.FromIssues(new[] { new Issue(check.NegligibleTemplate, 1) });

        Assert.That(suggestions, Has.Count.EqualTo(1));
        Assert.That(suggestions[0].Severity, Is.EqualTo(SuggestionSeverity.Info));
    }

    [Test]
    public void TimeIsMappedFromIssue()
    {
        var suggestions = SuggestionEngine.FromIssues(new[] { new Issue(1234.5, check.ProblemTemplate, 1) });

        Assert.That(suggestions, Has.Count.EqualTo(1));
        Assert.That(suggestions[0].Time, Is.EqualTo(1234.5));
    }

    [Test]
    public void EmptyIssuesProduceEmptySuggestions()
    {
        Assert.That(SuggestionEngine.FromIssues(Array.Empty<Issue>()), Is.Empty);
    }

    private class TestCheck : ICheck
    {
        public TestCheck()
        {
            ProblemTemplate = new IssueTemplateTest(this, IssueType.Problem, "Test problem {0}");
            WarningTemplate = new IssueTemplateTest(this, IssueType.Warning, "Test warning {0}");
            ErrorTemplate = new IssueTemplateTest(this, IssueType.Error, "Test error {0}");
            NegligibleTemplate = new IssueTemplateTest(this, IssueType.Negligible, "Test negligible {0}");
        }

        public CheckMetadata Metadata { get; } = new CheckMetadata(CheckCategory.Settings, "Test check");

        public IssueTemplate ProblemTemplate { get; }

        public IssueTemplate WarningTemplate { get; }

        public IssueTemplate ErrorTemplate { get; }

        public IssueTemplate NegligibleTemplate { get; }

        public IEnumerable<IssueTemplate> PossibleTemplates => new IssueTemplate[]
        {
            ProblemTemplate,
            WarningTemplate,
            ErrorTemplate,
            NegligibleTemplate,
        };

        public IEnumerable<Issue> Run(BeatmapVerifierContext context) => Array.Empty<Issue>();

        private class IssueTemplateTest : IssueTemplate
        {
            public IssueTemplateTest(ICheck check, IssueType type, string message)
                : base(check, type, message)
            {
            }
        }
    }
}
