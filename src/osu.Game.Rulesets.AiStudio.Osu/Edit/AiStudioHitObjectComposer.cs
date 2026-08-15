using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Osu.Edit;

namespace osu.Game.Rulesets.AiStudio.Osu.Edit;

/// <summary>
/// Compose 页作曲器：继承官方 <see cref="OsuHitObjectComposer"/> 保留全部 osu! 编辑能力，
/// 并向右工具箱追加 AI Studio 面板（PLAN.md §2.2 注入点 1）。
/// </summary>
public partial class AiStudioHitObjectComposer : OsuHitObjectComposer
{
    public AiStudioHitObjectComposer(Ruleset ruleset)
        : base(ruleset)
    {
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        RightToolbox.AddRange(new Drawable[]
        {
            new AiStudioToolboxGroup(),
        });
    }
}
