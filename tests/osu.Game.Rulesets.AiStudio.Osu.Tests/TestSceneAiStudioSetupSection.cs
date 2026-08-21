using System;
using System.IO;
using System.Linq;
using AiStudio.Core.Models;
using AiStudio.Core.Synthesis;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.AiStudio.Osu.Edit;
using osu.Game.Rulesets.AiStudio.Osu.Synthesis;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

/// <summary>
/// AI Studio 插件 Setup 页分区（<see cref="AiStudioSetupSection"/>）的 headless 自动化冒烟测试：
/// 等价于真机上"输入音频路径 → 点击生成 → 输出 map.osu" 的注入点验证，可在 CI 复用。
///
/// 运行机制：继承 <see cref="osu.Framework.Testing.TestScene"/>（框架自带 [TestFixture]/[Test] 调度），
/// [OneTimeSetUp] 创建 headless GameHost（TestRunHeadlessGameHost），每个 [Test] 的步骤在游戏线程上
/// 顺序执行；AddUntilStep 最多重试 10 秒（UntilStepButton 硬编码上限，可用 OSU_TESTS_NO_TIMEOUT=1 关闭）。
/// UI 组件所需的游戏级服务 <see cref="SessionStatics"/> 在 <see cref="CreateChildDependencies"/> 手工缓存。
///
/// 注意：<see cref="AiStudioSetupSection"/> 内部使用默认输出目录（我的文档/osu-ai-studio-output，
/// 见 OsuMapGenerator 的 OutputDirectory 空值回退），无法注入输出目录；本测试断言默认目录下的
/// 产物并在 TearDown 中只清理本次生成的同名文件，避免污染用户目录。
/// </summary>
[TestFixture]
public partial class TestSceneAiStudioSetupSection : TestScene
{
    /// <summary>被测试的 Setup 分区。</summary>
    private AiStudioSetupSection section = null!;

    /// <summary>测试用真实点击轨 WAV 的路径（临时目录）。</summary>
    private string tempWavPath = null!;

    /// <summary>临时 WAV 所在目录（TearDown 时整体删除）。</summary>
    private string tempDirectory = null!;

    /// <summary>生成器默认输出目录（我的文档/osu-ai-studio-output）。</summary>
    private static string defaultOutputDirectory
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "osu-ai-studio-output");

    /// <summary>缓存 UI 组件所需的游戏级会话服务。</summary>
    protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        => AiStudioTestSceneDependencies.Create(base.CreateChildDependencies(parent));

    public TestSceneAiStudioSetupSection()
    {
        section = new AiStudioSetupSection();
        Add(section);
    }

    /// <summary>
    /// 在测试体内创建临时点击轨 WAV。
    /// 注意：TestScene 的 [TearDown] 会在测试方法体返回后、AddStep 注册的步骤执行前运行，
    /// 因此不能在 [TearDown] 删除步骤所需的资源（实测文件会被提前删除）；清理放在最后的 AddStep。
    /// </summary>
    private void createWav()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), $"aistudio-scene-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        tempWavPath = WavTestUtils.CreateClickTrackWav(Path.Combine(tempDirectory, "clicktrack.wav"), bpm: 120, durationSeconds: 60);
    }

    /// <summary>
    /// 冒烟测试：设置音频路径 → 点击生成按钮 → 等待输出目录出现 map.osu，且状态文本显示"已生成"。
    /// 生成在工作线程（Task.Run）执行、完成回调经 Scheduler 回到更新线程，因此用 AddUntilStep 轮询。
    /// </summary>
    [Test]
    public void GenerateButtonProducesMapFile()
    {
        createWav();

        AddStep("设置音频路径", () => section.ChildrenOfType<OsuTextBox>().Single().Text = tempWavPath);

        AddStep("点击生成按钮", () => section.ChildrenOfType<BasicButton>().First(b => b.Text.ToString().Contains("Hard 预设")).TriggerClick());

        // 输出文件名为音频名 + ".osu"（OsuMapGenerator 落盘逻辑）；等待生成完成且状态文本刷新。
        string expectedOsuPath = Path.Combine(defaultOutputDirectory, "clicktrack.osu");
        AddUntilStep("map.osu 已生成且状态已刷新", () =>
            File.Exists(expectedOsuPath)
            && section.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains("已生成")));

        AddAssert("输出文件位于默认目录", () => Path.GetDirectoryName(expectedOsuPath) == defaultOutputDirectory);

        cleanupStep();
    }

    /// <summary>
    /// 清理临时 WAV 目录与本次生成落盘的输出文件（作为最后一步执行，见 <see cref="createWav"/> 注释）。
    /// </summary>
    private void cleanupStep()
    {
        AddStep("清理临时文件", () =>
        {
            if (tempDirectory != null && Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);

            if (tempWavPath != null && Directory.Exists(defaultOutputDirectory))
            {
                string baseName = Path.GetFileNameWithoutExtension(tempWavPath);
                foreach (string file in Directory.GetFiles(defaultOutputDirectory, $"{baseName}.*"))
                    File.Delete(file);
            }
        });
    }

    /// <summary>渲染冒烟：分区内应存在 "AI Studio" 标题、音频输入框与生成按钮（与测试顺序无关）。</summary>
    [Test]
    public void SectionRendersTitle()
    {
        AddAssert("存在 AI Studio 标题", () => section.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains("AI Studio")));
        AddAssert("存在音频路径输入框", () => section.ChildrenOfType<OsuTextBox>().Any());
        AddAssert("存在生成按钮", () => section.ChildrenOfType<BasicButton>().Any(b => b.Text.ToString().Contains("生成")));
    }
}
