using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Difficulty;
using osu.Game.Rulesets.Mania.Edit;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.AiStudio.Mania;

/// <summary>
/// AI Studio (mania) ruleset plugin.
///
/// Delegates to an internal <see cref="ManiaRuleset"/> instance for converter/processor/mods to avoid LegacyID trap.
/// Inherits <see cref="Ruleset"/> directly, not <see cref="ManiaRuleset"/>.
/// </summary>
public partial class AiStudioManiaRuleset : Ruleset
{
    public const string SHORT_NAME = "aistudio-mania";

    private readonly ManiaRuleset maniaRuleset = new ManiaRuleset();

    public override string Description => "AI Studio (mania)";

    public override string ShortName => SHORT_NAME;

    public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
        => new DrawableManiaRuleset(this, beatmap, mods);

    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
        => new ManiaBeatmapConverter(beatmap, this);

    public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap)
        => maniaRuleset.CreateBeatmapProcessor(beatmap)!;

    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
        => new ManiaDifficultyCalculator(RulesetInfo, beatmap);

    public override PerformanceCalculator CreatePerformanceCalculator()
        => new ManiaPerformanceCalculator();

    public override HitObjectComposer CreateHitObjectComposer()
        => new Edit.AiStudioManiaHitObjectComposer(this);

    public override IBeatmapVerifier CreateBeatmapVerifier()
        => new Edit.AiStudioManiaBeatmapVerifier();

    public override IEnumerable<Mod> GetModsFor(ModType type)
    {
        var mods = maniaRuleset.GetModsFor(type).ToList();

        if (type == ModType.Fun)
            mods.Add(new AiStudioManiaAssistantMod());

        return mods;
    }

    public override IEnumerable<Drawable> CreateEditorSetupSections()
        => base.CreateEditorSetupSections().Append(new Edit.AiStudioManiaSetupSection());
}
