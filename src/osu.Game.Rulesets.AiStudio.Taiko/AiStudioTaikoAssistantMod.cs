using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.AiStudio.Taiko;

/// <summary>
/// AI Studio assistant marker mod for taiko.
/// Ranked is always false.
/// </summary>
public class AiStudioTaikoAssistantMod : Mod
{
    public override string Name => "AI Studio Assistant (Taiko)";

    public override string Acronym => "AIAT";

    public override LocalisableString Description => "Marks maps assisted by AI Studio (taiko). Does not affect gameplay or scoring.";

    public override ModType Type => ModType.Fun;

    public override bool Ranked => false;
}
