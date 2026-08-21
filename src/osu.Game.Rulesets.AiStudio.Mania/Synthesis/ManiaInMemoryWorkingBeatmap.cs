using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.AiStudio.Mania.Synthesis;

/// <summary>
/// In-memory working beatmap for mania generation/verification.
/// </summary>
public sealed class ManiaInMemoryWorkingBeatmap : WorkingBeatmap
{
    private readonly IBeatmap beatmap;

    public ManiaInMemoryWorkingBeatmap(IBeatmap beatmap)
        : base(beatmap.BeatmapInfo, null!)
    {
        this.beatmap = beatmap;
    }

    protected override IBeatmap GetBeatmap() => beatmap;

    public override Texture GetBackground() => null!;

    protected override Track GetBeatmapTrack() => null!;

    protected override ISkin GetSkin() => new LegacyBeatmapSkin(BeatmapInfo, null);

    public override Stream GetStream(string storagePath) => null!;
}
