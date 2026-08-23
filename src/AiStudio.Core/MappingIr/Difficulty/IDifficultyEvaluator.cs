using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.Difficulty;

/// <summary>
/// 难度评估器契约（code-review P1 / spec §16 Difficulty Feedback Loop）。
/// 官方 DifficultyCalculator adapter 在 ruleset 程序集实现（依赖 osu! 类型）；
/// Core 内提供接口 + null 安全骨架（不可用时系统仍可出草稿，但须标注 DifficultyKnown=false）。
/// </summary>
public interface IDifficultyEvaluator
{
    /// <summary>评估整图 SR；不可用时返回 null（Evaluation.DifficultyKnown=false）。</summary>
    double? TryEvaluateStarRating(MappingDocument document);
}

/// <summary>
/// Core 内默认实现：不可用（null）。ruleset 程序集注入官方 adapter 后替换。
/// </summary>
public sealed class UnavailableDifficultyEvaluator : IDifficultyEvaluator
{
    public double? TryEvaluateStarRating(MappingDocument document) => null;
}
