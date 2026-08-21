using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.AiStudio.Mania;

/// <summary>
/// AI Studio 辅助标记 mod（mania）：仅用于标识"经 AI Studio 辅助/生成的谱面"，
/// 不改变玩法与计分（Ranked 恒为 false，不参与官方排名）。
/// </summary>
public class AiStudioManiaAssistantMod : Mod
{
    public override string Name => "AI Studio Assistant (Mania)";

    public override string Acronym => "AIA";

    public override LocalisableString Description => "Marks maps assisted by AI Studio. Does not affect gameplay or scoring.";

    public override ModType Type => ModType.Fun;

    public override bool Ranked => false;
}
