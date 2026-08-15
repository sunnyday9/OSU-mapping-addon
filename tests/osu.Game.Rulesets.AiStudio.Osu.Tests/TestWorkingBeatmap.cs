using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

/// <summary>
/// 最小测试用 <see cref="WorkingBeatmap"/>：仅覆盖 5 个抽象成员。
/// 测试路径不触及皮肤/音频，因此相关成员返回 null（以 null! 表示有意为之）。
/// </summary>
internal class TestWorkingBeatmap : WorkingBeatmap
{
    private readonly IBeatmap beatmap;

    public TestWorkingBeatmap(IBeatmap beatmap)
        : base(beatmap.BeatmapInfo, null!)
    {
        this.beatmap = beatmap;
    }

    protected override IBeatmap GetBeatmap() => beatmap;

    public override Texture GetBackground() => null!;

    protected override Track GetBeatmapTrack() => null!;

    protected override ISkin GetSkin() => null!;

    public override Stream GetStream(string storagePath) => null!;
}
