# ADR-MVP-A-004 — 规划层先确定性规则，LLM 留接口

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：详细计划 §26 交付物第 9 项"一个最小 LLM Planner"，但 MVP A 范围限定为"无 LLM 也能跑通闭环"（计划 §2 非目标）。

## 决策

- 实现 `IMappingPlanner` 接口（输入 `MusicTimeline` + `DifficultyProfile`，输出 `MappingPlan`），当前唯一实现为 **`DeterministicMappingPlanner`**（纯规则）：
  - 段落强度 → `MappingIntent.Primary`（intro→establish、verse→repeat/variation、chorus→escalation/climax、outro→de_escalation/resolution）；
  - 段内密度目标 → `PatternIntent` 的 subdivision / family 选择（高密度段 → stream/jumpstream，低密度 → single/jump）；
  - 列策略：交替/镜像/staircase 按 seed 轮换；
  - LN 政策：`ln_complexity` 维度 → single_ln/ln_rice 比例；
  - 所有决策带 `rationale`（可解释性，为 Milestone 1"AI 可以解释为什么这样作图"打底）。
- 接口保留 `PlannerKind` 标注（`RuleBased`/`LLM`），未来 LLM 实现直接替换 `DeterministicMappingPlanner`。

## 理由

1. MVP A 验收 = "Schema 可校验 + 确定性渲染 + 无 LLM 校验"（spec §27），规则规划即可满足；
2. 规则规划让 golden 测试精确可控（LLM 输出不可精确快照）；
3. 接口先行 = 详细计划 §28"第一个任务不是做 Agent"的落实。

## 备选

- 直接接 LLM API：依赖外部服务、不可复现、测试不稳定，违反 MVP A 的确定性目标。
- 不做规划层、由调用方手写 MappingPlan：违背"系统闭环"目标。

## 影响

- `MappingIrDemo` 默认使用规则规划；`IMappingPlanner` 为未来 LLM Planner 的唯一注入点。
