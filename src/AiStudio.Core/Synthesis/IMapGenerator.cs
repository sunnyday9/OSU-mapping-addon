using AiStudio.Core.Models;

namespace AiStudio.Core.Synthesis;

/// <summary>
/// 生成结果。Success 为 false 时不得落盘。
/// </summary>
public sealed class GenerationResult
{
    public bool Success { get; init; }

    /// <summary>§3 质量门禁报告（Success=true 时必然全绿）。</summary>
    public QualityGateReport? QualityReport { get; init; }

    /// <summary>导出的 .osu 文件路径（Success=true 时非空）。</summary>
    public string? OutputFilePath { get; init; }

    /// <summary>输出目录中的音频文件路径（拷贝失败时回退为原始输入路径）。</summary>
    public string? AudioOutputPath { get; init; }

    public string? ErrorMessage { get; init; }
}

/// <summary>
/// 模式专属生成器的统一入口（PLAN.md §5）。
/// 每个模式（osu!/mania/taiko/catch）各自实现一个版本，共享分析层输出。
/// </summary>
public interface IMapGenerator
{
    Task<GenerationResult> GenerateAsync(GenerationSettings settings, CancellationToken cancellationToken = default);
}
