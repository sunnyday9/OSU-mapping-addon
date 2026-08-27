# ADR-MVP-A-013 — Planner 分解 + Candidate 生成与排名

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：code-review-v0.1 指出 Global/Local Planner 未分离、无候选生成与排名（原实现单层 section→intent→pattern 直接选定）。spec §10/§11/§12 定义本地规划、候选、排名。

## 决策

- **`LocalPlanning/ILocalMappingPlanner` + `DeterministicLocalPlanner`**（spec §10）：输入 context（section + evidence + globalPlan + previous patterns + difficulty profile）→ 单条 `MappingIntent`（primary/secondary/emphasis/complexity/continuity/rationale）；
- **`Candidates/IPatternCandidateGenerator` + `DeterministicCandidateGenerator`**（spec §11）：每意图生成 4 个候选（family/subdivision/列策略组合），reason codes 标注选择理由；
- **`Candidates/IPatternCandidateRanker` + `DeterministicCandidateRanker`**（spec §12.1）：权重可配置评分 `0.30*MusicAlignment + 0.20*DifficultyFit + 0.20*Continuity + 0.15*Readability + 0.10*StructuralFit + 0.05*Validity`；硬无效候选（未知 family，Validity=0）排名前拒绝；
- **保留 `DeterministicMappingPlanner` facade**：既有测试与外部调用不改（内部委托新链路）。

## 理由

1. 候选 + 排名是 spec 决策链的核心升级：从"直接选定"到"生成多个 → 评分 → 选优"，为未来 learned ranker 留注入点；
2. 权重精确对齐 spec §12.1 baseline 且可配置（`DeterministicCandidateRanker(weights)` 构造）。

## 影响

- Pipeline 每段流程变为 LocalIntent → 4 候选 → 排名 → 生成；
- 既有 43 测试零改动通过（facade 兼容）。
