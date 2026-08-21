using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.AiStudio.Catch.Synthesis;

/// <summary>
/// In-memory WorkingBeatmap for Catch generation pipeline.
/// </summary>
public sealed class InMemoryWorkingBeatmap : WorkingBeatmap
{
    private readonly IBeatmap beatmap;

    public InMemoryWorkingBeatmap(IBeatmap beatmap)
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
