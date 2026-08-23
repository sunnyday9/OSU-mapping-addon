# ADR-MVP-A-011 — Mapping Intelligence 决策链落地（Evidence / Global / Local / Candidate / Rank / Backend / Critic / Difficulty）

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：code-review-v0.1 判定当前实现"Mapping Intelligence ⚠️ 规则 placeholder"，差距集中在：无 Evidence 层、Global/Local 未分离、无候选生成/排名、pipeline 硬编码 Mania、无 Critic/Revision、Difficulty 未接入。mapping-intelligence-v0.1-spec §31 给出 P0/P1 实施顺序。

## 决策

按 spec §13（Deterministic Baseline）落地完整决策链，全部组件确定性、接口可替换：

| 组件 | 实现 | spec |
|---|---|---|
| `Evidence/MappingEvidence` + `DeterministicEvidenceBuilder` | 每段一条证据（rhythm/accent/energy/vocal/movement/density/repetition/climax/novelty + sources） | §7/§13.2 |
| `GlobalPlanning/GlobalMappingPlan` + `DeterministicGlobalPlanner` | 难度曲线 + 段落计划 + 全局高潮（future-aware：非最终高潮 ×0.85 余量）+ 对比点 | §9 |
| `LocalPlanning/ILocalMappingPlanner` + `DeterministicLocalPlanner` | 上下文 + 证据 + 全局计划 → MappingIntent（rationale 引用证据与高潮） | §10 |
| `Candidates/IPatternCandidateGenerator` + `DeterministicCandidateGenerator` | 每意图 3–4 个候选（family/subdivision/列策略组合） | §11 |
| `Candidates/IPatternCandidateRanker` + `DeterministicCandidateRanker` | 权重可配置评分：0.30 音乐对齐 + 0.20 难度契合 + 0.20 连续性 + 0.15 可读性 + 0.10 结构契合 + 0.05 合法性；硬无效候选排名前拒绝 | §12 |
| `Backends/IRulesetMappingBackend` + `Mania4KMappingBackend` | provider+validator+renderer 聚合，pipeline 与 ruleset 解耦 | §26 |
| `Critique/IMappingCritic` + `BaselineMappingCritic` | 硬问题（重叠/空对象）阻断；软问题（密度不匹配/重复/对齐）进 revision | §15 |
| `Difficulty/IDifficultyEvaluator` + `UnavailableDifficultyEvaluator` | 官方 calculator adapter 注入点；不可用时 observed_sr=null 且不声称达标 | §16/§25.4 |
| Pipeline revision loop | `MaxRevisionsPerPhrase=3`（配置化），候选按排名尝试，硬错误跳过下一名 | §19.1 |

**保留 `IMappingPlanner` facade**：`MappingIrPipeline` 旧构造签名不变（内部组装新链路），既有 43 测试零改动通过。

## 理由

1. 严格遵循 code-review 建议："完善 Deterministic Baseline → Evidence → Candidate → Scoring → Difficulty feedback 再接 LLM"（§二十三）；
2. 每个组件独立接口 = spec §23 replaceable interfaces，未来 LLM/learned 组件逐个替换不推倒重来；
3. rationale 已引用证据（demo 实测：`Evidence (energy 0.85, rhythm 0.65) + global climax at 20000`）——可解释性从"规则文案"升级为"证据驱动"。

## 影响

- Pipeline 决策链从 `Section → Intent → Pattern`（3 层）扩展为 8 层（Evidence → Global → Local → Candidate → Rank → Provider → Validator/Critic → Revision）；
- demo 产物 pattern 选择变化（chorus 从 jumpstream → stream，因 ranker 权衡结构契合与可读性）——这是评分生效的预期结果；
- 后续 P2（LLM/learned ranker/style/copilot）有明确注入点。

## 遗留（P2，本次不做）

- Copilot context API（spec §20）、preference logging（§21）、LLM planner（§14）、learned ranker（§35）、style conditioning（§22）；
- 完整四 ruleset backend（Osu/Taiko/Catch provider）；
- MusicTimeline 语义事件（kick/snare/vocal）依赖真实音频分析升级。
