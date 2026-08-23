using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Catch.Beatmaps;
using osu.Game.Rulesets.Catch.Difficulty;
using osu.Game.Rulesets.Catch.UI;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.AiStudio.Catch;

/// <summary>
/// AI Studio (catch) ruleset plugin.
///
/// Design note (PLAN.md §2.3): inherits <see cref="Ruleset"/> (not <see cref="CatchRuleset"/>) and reuses
/// public Catch components — avoids LegacyID=2 silent-not-registered trap.
/// Delegates official behaviour to an inner <see cref="CatchRuleset"/> instance.
/// </summary>
public partial class AiStudioCatchRuleset : Ruleset
{
    public const string SHORT_NAME = "aistudio-catch";

    private readonly CatchRuleset catchRuleset = new CatchRuleset();

    public override string Description => "AI Studio (catch)";

    public override string ShortName => SHORT_NAME;

    public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
        => new DrawableCatchRuleset(this, beatmap, mods);

    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
        => new CatchBeatmapConverter(beatmap, this);

    public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap)
        => new CatchBeatmapProcessor(beatmap);

    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
        => new CatchDifficultyCalculator(RulesetInfo, beatmap);

    public override PerformanceCalculator CreatePerformanceCalculator()
        => new CatchPerformanceCalculator();

    public override HitObjectComposer CreateHitObjectComposer()
        => new Edit.AiStudioCatchHitObjectComposer(this);

    public override IBeatmapVerifier CreateBeatmapVerifier()
        => new Edit.AiStudioCatchBeatmapVerifier();

    public override IEnumerable<Mod> GetModsFor(ModType type)
    {
        var mods = catchRuleset.GetModsFor(type).ToList();

        if (type == ModType.Fun)
            mods.Add(new AiStudioCatchAssistantMod());

        return mods;
    }

    public override IEnumerable<Drawable> CreateEditorSetupSections()
        => base.CreateEditorSetupSections().Append(new Edit.AiStudioCatchSetupSection());
}
