using AiStudio.Core.MappingIr.Model;

namespace AiStudio.Core.MappingIr.GlobalPlanning;

/// <summary>全局计划的难度曲线点（内部规划目标，非官方 SR 计算）。</summary>
public sealed record DifficultyCurvePoint(int TimeMs, double Target);

/// <summary>全局计划的段落计划（每段角色 + 预算）。</summary>
public sealed record SectionPlan(
    string SectionId,
    int StartTime,
    int EndTime,
    string Role,
    double DensityBudget,
    double IntensityBudget);

/// <summary>全局对比点（需要与周围形成对比的时机）。</summary>
public sealed record ContrastPoint(int TimeMs, string Type, string Reason);

/// <summary>
/// 全局映射计划（spec §9.2）：
/// 全局难度曲线 + 段落计划 + 全局高潮 + 对比点。本地 planner 依赖它做 future-aware 决策。
/// </summary>
public sealed record GlobalMappingPlan(
    IReadOnlyList<DifficultyCurvePoint> DifficultyCurve,
    IReadOnlyList<SectionPlan> SectionPlans,
    GlobalClimaxInfo GlobalClimax,
    IReadOnlyList<ContrastPoint> ContrastPoints);

/// <summary>全局高潮信息：候选时间点 + 相对强度 + 是否"最终高潮"（后续段落应保留余量）。</summary>
public sealed record GlobalClimaxInfo(int TimeMs, double Strength, bool IsFinalClimax);
