using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Taiko;
using osu.Game.Rulesets.Taiko.Beatmaps;
using osu.Game.Rulesets.Taiko.Difficulty;
using osu.Game.Rulesets.Taiko.Objects;
using osu.Game.Rulesets.Taiko.UI;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.AiStudio.Taiko;

/// <summary>
/// AI Studio (taiko) ruleset plugin.
///
/// Design note (PLAN.md §2.3): inherits <see cref="Ruleset"/> (not <see cref="TaikoRuleset"/>) and reuses
/// public Taiko components — avoids LegacyID=1 silent-not-registered trap.
/// Delegates official behaviour to an inner <see cref="TaikoRuleset"/> instance where needed.
/// </summary>
public partial class AiStudioTaikoRuleset : Ruleset
{
    public const string SHORT_NAME = "aistudio-taiko";

    private readonly TaikoRuleset taikoRuleset = new TaikoRuleset();

    public override string Description => "AI Studio (taiko)";

    public override string ShortName => SHORT_NAME;

    public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
        => new DrawableTaikoRuleset(this, beatmap, mods);

    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
        => new AiStudioTaikoBeatmapConverter(beatmap, this);

    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
        => new TaikoDifficultyCalculator(RulesetInfo, beatmap);

    public override PerformanceCalculator CreatePerformanceCalculator()
        => new TaikoPerformanceCalculator();

    public override HitObjectComposer CreateHitObjectComposer()
        => new Edit.AiStudioTaikoHitObjectComposer(this);

    public override IBeatmapVerifier CreateBeatmapVerifier()
        => new Edit.AiStudioTaikoBeatmapVerifier();

    public override IEnumerable<Mod> GetModsFor(ModType type)
    {
        var mods = taikoRuleset.GetModsFor(type).ToList();

        if (type == ModType.Fun)
            mods.Add(new AiStudioTaikoAssistantMod());

        return mods;
    }

    public override IEnumerable<Drawable> CreateEditorSetupSections()
        => base.CreateEditorSetupSections().Append(new Edit.AiStudioTaikoSetupSection());
}

/// <summary>
/// Local taiko beatmap converter: wraps official TaikoRuleset conversion via composition,
/// avoiding direct dependency on the internal TaikoBeatmapConverter type.
/// </summary>
internal class AiStudioTaikoBeatmapConverter : BeatmapConverter<TaikoHitObject>
{
    private readonly IBeatmapConverter inner;

    public AiStudioTaikoBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
        : base(beatmap, ruleset)
    {
        inner = new TaikoRuleset().CreateBeatmapConverter(beatmap);
    }

    public override bool CanConvert() => inner.CanConvert();

    protected override IEnumerable<TaikoHitObject> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken)
    {
        if (original is TaikoHitObject taikoHitObject)
            yield return taikoHitObject;
        else
            yield break;
    }

    protected override Beatmap<TaikoHitObject> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
    {
        var converted = inner.Convert(cancellationToken);
        if (converted is Beatmap<TaikoHitObject> typed)
            return typed;

        var result = new TaikoBeatmap();
        result.HitObjects.AddRange(converted.HitObjects.OfType<TaikoHitObject>());
        result.BeatmapInfo = converted.BeatmapInfo;
        result.ControlPointInfo = converted.ControlPointInfo;
        result.Breaks.AddRange(converted.Breaks);
        return result;
    }
}
