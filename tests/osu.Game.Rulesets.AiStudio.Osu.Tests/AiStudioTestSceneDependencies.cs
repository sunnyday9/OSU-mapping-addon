using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Configuration;
using osu.Game.Graphics;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

/// <summary>
/// 为裸 <see cref="osu.Framework.Testing.TestScene"/> 注册游戏级 UI 服务
/// （<see cref="SessionStatics"/> / <see cref="OsuColour"/> / <see cref="IBeatSyncProvider"/> 等），
/// 等价于 OsuGameBase 依赖容器的 headless 子集（完整游戏宿主在单测环境过重且会挂起）。
/// 编辑器专属依赖（EditorBeatmap 等）由各场景自行补充。
/// </summary>
internal static class AiStudioTestSceneDependencies
{
    public static DependencyContainer Create(IReadOnlyDependencyContainer parent)
    {
        var dependencies = new DependencyContainer(parent);

        // 会话级静态配置（HoverSampleDebounceComponent 等 UI 组件需要）。
        dependencies.Cache(new SessionStatics());

        // osu! 配色（OsuAnimatedButton/OsuSpriteText 等需要）。
        dependencies.Cache(new OsuColour());

        // 节拍同步（OsuTextBox 内部 BeatSyncedContainer 需要）。
        dependencies.CacheAs<IBeatSyncProvider>(new StubBeatSyncProvider());

        return dependencies;
    }

    /// <summary>IBeatSyncProvider 桩实现：空控制点 + 静止时钟 + 零振幅。</summary>
    private sealed class StubBeatSyncProvider : IBeatSyncProvider
    {
        public ControlPointInfo ControlPoints { get; } = new ControlPointInfo();

        public IClock Clock { get; } = new FramedClock();

        public ChannelAmplitudes CurrentAmplitudes { get; } = new ChannelAmplitudes();
    }
}
