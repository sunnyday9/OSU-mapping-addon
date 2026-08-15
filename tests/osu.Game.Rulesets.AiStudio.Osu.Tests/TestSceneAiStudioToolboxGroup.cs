using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.AiStudio.Osu.Edit;

namespace osu.Game.Rulesets.AiStudio.Osu.Tests;

/// <summary>
/// AI Studio 编辑器右工具箱面板（<see cref="AiStudioToolboxGroup"/>）的 headless 渲染冒烟测试。
///
/// <see cref="AiStudioToolboxGroup"/> 继承 <see cref="osu.Game.Rulesets.Edit.EditorToolboxGroup"/>，
/// 其 UI 组件（HoverSampleDebounceComponent）依赖 <see cref="SessionStatics"/>，在
/// <see cref="CreateChildDependencies"/> 中手工缓存（裸 TestScene 不注册该游戏级服务）。
/// </summary>
[TestFixture]
public partial class TestSceneAiStudioToolboxGroup : TestScene
{
    /// <summary>被测试的工具箱面板。</summary>
    private AiStudioToolboxGroup toolboxGroup = null!;

    public TestSceneAiStudioToolboxGroup()
    {
        toolboxGroup = new AiStudioToolboxGroup();
        Add(toolboxGroup);
    }

    /// <summary>缓存 UI 组件所需的游戏级会话服务。</summary>
    protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        => AiStudioTestSceneDependencies.Create(base.CreateChildDependencies(parent));

    /// <summary>
    /// 冒烟：面板加载后应渲染出占位说明文本（M1/M2 接入提示，含 "AI"）。
    /// </summary>
    [Test]
    public void ToolboxShowsPlaceholder()
    {
        AddUntilStep("工具箱面板已加载", () => toolboxGroup.IsLoaded);

        AddAssert("面板存在子元素", () => toolboxGroup.Children.Count > 0);
        AddAssert("存在 AI 占位说明文本", () => toolboxGroup.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains("AI")));
        // SettingsToolboxGroup 头部标题会 ToUpper() 渲染为 OsuSpriteText（位于 InternalChildren，ChildrenOfType 可递归到）。
        AddAssert("面板标题为 AI STUDIO", () => toolboxGroup.ChildrenOfType<SpriteText>().Any(t => t.Text.ToString().Contains("AI STUDIO")));
    }
}
