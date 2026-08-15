using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Difficulty;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.AiStudio.Osu;

/// <summary>
/// AI Studio（osu! 标准模式）规则集插件。
///
/// 设计要点（PLAN.md §2.3）：不继承 <see cref="OsuRuleset"/>，而是继承 <see cref="Ruleset"/>
/// 并复用 osu! 官方公开组件 —— 规避 LegacyID=0 导致的"静默不注册"陷阱。
/// 本规则集是"制图工作室"载体；产出物是普通 osu! 标准 .osu 文件，原版游戏即可游玩。
/// </summary>
public partial class AiStudioRuleset : Ruleset
{
    public const string SHORT_NAME = "aistudio";

    /// <summary>用于委托官方 osu! 行为的实例（mods 列表等）。</summary>
    private readonly OsuRuleset osuRuleset = new OsuRuleset();

    public override string Description => "AI Studio (osu!)";

    public override string ShortName => SHORT_NAME;

    public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
        => new DrawableOsuRuleset(this, beatmap, mods);

    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
        => new OsuBeatmapConverter(beatmap, this);

    public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap)
        => new OsuBeatmapProcessor(beatmap);

    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
        => new OsuDifficultyCalculator(RulesetInfo, beatmap);

    public override PerformanceCalculator CreatePerformanceCalculator()
        => new OsuPerformanceCalculator();

    public override HitObjectComposer CreateHitObjectComposer()
        => new Edit.AiStudioHitObjectComposer(this);

    public override IBeatmapVerifier CreateBeatmapVerifier()
        => new Edit.AiStudioBeatmapVerifier();

    public override IEnumerable<Mod> GetModsFor(ModType type)
    {
        var mods = osuRuleset.GetModsFor(type).ToList();

        if (type == ModType.Fun)
            mods.Add(new AiStudioAssistantMod());

        return mods;
    }

    public override IEnumerable<Drawable> CreateEditorSetupSections()
        => base.CreateEditorSetupSections().Append(new Edit.AiStudioSetupSection());
}
