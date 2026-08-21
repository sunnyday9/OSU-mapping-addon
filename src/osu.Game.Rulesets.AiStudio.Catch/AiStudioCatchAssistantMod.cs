using osu.Framework.Localisation;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.AiStudio.Catch;

/// <summary>
/// AI Studio assistant marker mod for catch.
/// Ranked is always false.
/// </summary>
public class AiStudioCatchAssistantMod : Mod
{
    public override string Name => "AI Studio Assistant (Catch)";

    public override string Acronym => "AIAC";

    public override LocalisableString Description => "Marks maps assisted by AI Studio (catch). Does not affect gameplay or scoring.";

    public override ModType Type => ModType.Fun;

    public override bool Ranked => false;
}
