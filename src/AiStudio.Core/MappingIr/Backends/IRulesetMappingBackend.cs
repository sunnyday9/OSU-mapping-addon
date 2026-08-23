using AiStudio.Core.MappingIr.Model;
using AiStudio.Core.MappingIr.Patterns;
using AiStudio.Core.MappingIr.Rendering;
using AiStudio.Core.MappingIr.Validation;

namespace AiStudio.Core.MappingIr.Backends;

/// <summary>
/// ruleset 映射后端契约（code-review P1-4 / spec §26）：
/// 聚合某 ruleset 的 Pattern Provider + Validator + Renderer，使 pipeline 与具体 ruleset 解耦。
/// </summary>
public interface IRulesetMappingBackend
{
    RulesetKind Ruleset { get; }

    IPatternProvider Provider { get; }

    IMappingValidator Validator { get; }

    /// <summary>把 ConcreteObject[] 渲染为 .osu 文本（ruleset 专属）。</summary>
    string Render(MappingDocument document);
}

/// <summary>
/// Mania 4K 映射后端：聚合 Mania4KPatternProvider + MappingValidator + ManiaOsuRenderer。
/// </summary>
public sealed class Mania4KMappingBackend : IRulesetMappingBackend
{
    public Mania4KMappingBackend(IPatternProvider? provider = null, IMappingValidator? validator = null)
    {
        Provider = provider ?? new Mania4KPatternProvider();
        Validator = validator ?? new MappingValidator();
    }

    public RulesetKind Ruleset => RulesetKind.Mania;

    public IPatternProvider Provider { get; }

    public IMappingValidator Validator { get; }

    public string Render(MappingDocument document)
        => new ManiaOsuRenderer().Render(document);
}
