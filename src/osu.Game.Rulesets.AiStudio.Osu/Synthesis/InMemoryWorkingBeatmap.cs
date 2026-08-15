using System.IO;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.AiStudio.Osu.Synthesis;

/// <summary>
/// 生成管线专用的内存 <see cref="WorkingBeatmap"/>：直接持有已构造的 <see cref="IBeatmap"/>，
/// 不触碰存储/音频/背景（相关成员返回 null，与测试用 TestWorkingBeatmap 同款约定）。
/// 皮肤返回 <see cref="LegacyBeatmapSkin"/>（空资源），供难度计算与官方校验器读取谱面皮肤配置。
/// 公开（而非 internal）：生成器、编辑器与集成测试共用。
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
