namespace AiStudio.Core.Models;

public enum GateStatus
{
    Passed,
    Failed,
    NotApplicable,
}

/// <summary>
/// 单道质量门禁的判定结果。
/// </summary>
public sealed class QualityGateResult
{
    public required string Name { get; init; }

    public required GateStatus Status { get; init; }

    /// <summary>失败/通过时的人类可读说明。</summary>
    public string? Detail { get; init; }

    /// <summary>实测值（如有）。</summary>
    public double? Value { get; init; }

    /// <summary>允许区间（如有）。</summary>
    public double? Min { get; init; }

    public double? Max { get; init; }
}

/// <summary>
/// "ranked 级质量"五道门禁的汇总报告（PLAN.md §3）。
/// 生成管线与辅助检查共用；AllPassed 为 false 时生成不得落盘。
/// </summary>
public sealed class QualityGateReport
{
    public IReadOnlyList<QualityGateResult> Gates { get; init; } = Array.Empty<QualityGateResult>();

    public bool AllPassed => Gates.All(g => g.Status != GateStatus.Failed);
}
