# ADR-MVP-A-015 — Critic + 有界 Revision loop

- **日期**：2026-08-23（修订：2026-08-27 code-review 合入前修复）
- **状态**：已采纳
- **背景**：code-review-v0.1 指出无 Critic / Revision loop（spec §15/§19）。合入前 review 进一步发现初版 revision loop 是"按排名顺序尝试 provider"而非 critic 驱动：critic 在循环后只跑一次、软问题不回馈、第三名候选恒不可达、预算耗尽可能接受带硬错误的对象。

## 决策

- **`Critique/IMappingCritic` + `BaselineMappingCritic`**（spec §15）：硬问题（overlap/空对象/非法 timing）阻断；软问题（density_mismatch / pattern_repetition / rhythm_alignment）进 revision；
- **有界 revision loop**（spec §19.1，`MaxRevisionsPerPhrase=3` 配置化）：每候选生成后立即用 critic 门控——`report.Valid==false`（硬问题）→ 拒绝该候选；软问题存在但无硬问题 → 接受（软问题不阻塞出图，记录进 Evaluation.Issues）；
- **预算耗尽语义**：所有候选被拒或耗尽预算 → 回退最简 single pattern（宁可出草稿，不产出带硬错误对象的文档）；
- **对齐谓词统一**：`MappingIrPipeline.isOnGrid`（`< 2.0` 容差）与 critic 的 `rhythm_alignment` 判定共用同一谓词，消除边界（恰 2.0ms）上 score 与 critic 结论相反的分歧。

## 理由

1. critic 必须在"候选生成后、接受前"参与门控，软问题驱动重试才有意义——初版把 critic 放循环外导致 revision 是死代码（spec §27 第 13 条"detected issue 至少一次 revision pass"）；
2. 预算耗尽接受硬错误对象违反"validator/critic 是质量闸门"的定位；回退 single 是确定性、可渲染、可校验的最简兜底；
3. 谓词统一消除同一对象在 `musicAlignmentScore`（align=1.0）与 critic（off-grid）间结论相反的可观测矛盾。

## 影响

- Pipeline 每段：候选生成 → 排名 → 逐个尝试（critic 门控）→ 预算耗尽回退；
- `Evaluation.Issues` 合并 validator + critic 报告；`Evaluation.Valid` 以 validator 结果为准（critic 软问题不翻转 Valid）；
- 修复后新增测试：`Pipeline_BudgetExhausted_ProducesValidFallback_NotHardErrorObjects`、`Critic_SoftIssue_TriggersRevision_NotHardBlock`、`AlignmentPredicate_IsSymmetricWithCritic`。
