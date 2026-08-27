# ADR-MVP-A-012 — Global Mapping Plan（全局映射计划）

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：code-review-v0.1 指出原实现"section-by-section 过短"，缺少全局视角——难度曲线、全局高潮、future-aware 余量。mapping-intelligence-v0.1-spec §9 定义 GlobalMappingPlan。

## 决策

新增 `GlobalPlanning/GlobalMappingPlan` + `IGlobalMappingPlanner` + `DeterministicGlobalPlanner`：

- **GlobalMappingPlan**（spec §9.2）：`DifficultyCurve` / `MappingComplexityCurve` / `SectionPlans[]` / `GlobalClimax` / `ContrastPoints[]`；
- **DeterministicGlobalPlanner**（spec §9.6）：按 section energy/type + difficulty profile 确定全局曲线；识别全局高潮（最高能量候选）；**future-aware**——非最终高潮乘 0.85 密度预算余量，final chorus 保留余量；
- 输出被 `DeterministicLocalPlanner` 消费（`sectionPlan.Role`、`DensityBudget`、`GlobalClimax.TimeMs`）。

## 理由

1. 全局视角是"段落规划与音乐结构一致"的前提（spec §9.1）：climax 只能从全曲能量曲线判定，逐段独立无法做到；
2. future-aware 余量保证高潮段不因前段密度耗尽而平淡（demo 实测 final chorus density budget 高于中间段）。

## 影响

- 本地规划器新增 `GlobalPlan` 输入（`LocalMappingContext`）；`MappingPrimaryIntent.Climax` 只在"能量>0.6 且为全局高潮"时触发；
- rationale 文案引用 `GlobalClimax.TimeMs`（可解释性）。
