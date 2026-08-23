# MVP A.2 — Mapping Intelligence 基线实施计划

> 版本：v2 · 2026-08-23 · 状态：**已完成**（P0 全修 + P1 决策链全部落地，验证全绿）· 分支：`feat/mvp-a-mapping-ir`（续）· 前置：MVP A（Mapping IR v0.1 核心层，commit d84b55d）
> 依据：
> - `docs/new plan/code-review-v0.1.md`（第三方审计：差距清单 + P0/P1/P2 优先级）
> - `docs/new plan/mapping-intelligence-v0.1-spec.md`（Mapping Intelligence v0.1 规范：Evidence / Global / Local / Candidate / Ranking / Critic / Difficulty）
> - `docs/new plan/mapping-ir-v0.1-spec.md` §27（IR v0.1 一致性）

## 1. 目标

在 MVP A 已交付的 Mapping IR 核心层之上，按 code-review 的 P0/P1 优先级补齐差距，把 `DeterministicMappingPlanner` 从"单层 section→intent→pattern"演进为 spec §13/§31 定义的 **deterministic baseline intelligence** 决策链：

```
MusicTimeline
  → MappingEvidence（新）
  → GlobalMappingPlan（新）
  → Local MappingIntent（重构）
  → PatternCandidate[]（新）
  → Ranking（新，deterministic scoring）
  → PatternIntent
  → IPatternProvider（经 IRulesetMappingBackend 解耦）
  → ConcreteObjects
  → Validator + Critic（新）→ 有限 Revision loop
```

**验收基线**（spec §27 的 15 条 Baseline Implementation Requirements + §36 Definition of Done 中属于本阶段的部分）。

## 2. 差距清单 → 本次任务映射（来自 code-review-v0.1.md）

| Review 项 | 优先级 | 本次实施 |
|---|---|---|
| 修复 `string.GetHashCode()` 确定性缺陷 | **P0** | FNV-1a 稳定哈希替代（Task 2） |
| JSON `object?` 参数强类型化（`column_order` JsonElement 陷阱） | **P0** | `ManiaPatternParameters` 强类型 + 序列化兼容（Task 3） |
| Canonical JSON Schema 入库 + CI 强制一致 | **P0** | schema 校验测试入库（Task 4） |
| MappingEvidence 层缺失 | **P1** | `Evidence/MappingEvidence.cs` + `EvidenceBuilder`（Task 5） |
| Global/Local Planner 未分离（section-by-section 过短） | **P1** | `IGlobalMappingPlanner` + `ILocalMappingPlanner` 分解（Task 6/7） |
| Candidate generation + ranking 缺失 | **P1** | `IPatternCandidateGenerator` + `IPatternCandidateRanker`（Task 7） |
| Pipeline/Validator/Renderer 硬编码 Mania | **P1** | `IRulesetMappingBackend` + `Mania4KMappingBackend`（Task 8） |
| Critic / Revision loop 缺失 | **P1** | `IMappingCritic` + 有界 revision（Task 9） |
| Difficulty feedback 未接入 | **P1** | `IDifficultyEvaluator` 接口 + adapter 骨架（Task 10） |
| 测试缺口（schema validation / pattern parameter round-trip / transition consistency / cross-ruleset） | P0/P1 | 随各 Task 补齐；专项：schema 校验、参数 roundtrip、transition 一致性 |

**明确不在本次范围**（review 也建议不要现在做）：
- LLM Planner / 任何 learned 组件（spec §14 为可选，P2）
- Style conditioning（P2，spec §22）
- Copilot 上下文 API（P2，spec §20）
- Standard/Taiko/Catch 的完整 Pattern Provider（仅解耦接口 + Mania backend；spec §26 允许 Mania 4K 为首个实现目标）
- 完整 MusicTimeline 语义增强（kick/snare/vocal 事件，依赖真实音频分析，独立后续）

## 3. 实施步骤（SDLC：设计 → 实现 → 测试 → 验收）

### Task 2 — P0：稳定确定性 seed（ADR-MVP-A-008）

- `IPatternGenerationContext.CreateFamilyRandom` 的 `Seed ^ family.GetHashCode()` → FNV-1a 64 位稳定哈希：`StableHash(family) ^ seed`
- 新增 `DeterministicHash` 工具类（FNV-1a 实现，纯算法，测试覆盖已知向量）
- 测试：`Deterministic_SameSeedSameOutput` 保持通过；新增跨进程稳定断言（同 seed 输出哈希一致）

### Task 3 — P0：强类型 Pattern 参数（ADR-MVP-A-009）

- 新增 `ManiaPatternParameters`（record）：Subdivision / Density / ColumnStrategy / ColumnOrder / JackTolerance / Bpm / ChordSize / ChordDensity / LnRatio / LnDurationBeats / Count / JackColumn
- `PatternIntent.Parameters` 保持 `IReadOnlyDictionary<string, object?>`（IR 契约不变），但 provider 内用 `ManiaPatternParameters.FromDictionary` 解析；**roundtrip 安全**：解析时兼容 `JsonElement`（`GetInt32/GetString/GetArray`）与原始 CLR 类型
- 测试：`PatternParametersRoundTrip_JsonElementCompatible` —— 序列化 → 反序列化 → provider 生成结果与内存构造一致（这正是 review 指出的缺口）

### Task 4 — P0：Canonical Schema 对齐（ADR-MVP-A-010）

- `docs/new plan/mapping-ir-v0.1.schema.json` 复制到 `src/AiStudio.Core/MappingIr/Schema/mapping-ir-v0.1.schema.json` 作为 canonical 副本（随 Core 编译进资源，测试引用）
- 新增 `MappingIrSchemaValidator`：调用方注入（测试用 Python/jsonschema 等价断言；C# 侧做键集合/枚举/类型 shape 断言，不引入 JsonSchema.Net 依赖）
- 测试：`Schema_AllTopLevelKeysPresent`、`Schema_EnumValuesMatch`、`Schema_NullNormalized`（labels/difficulty 非 null）

### Task 5 — P1：MappingEvidence（ADR-MVP-A-011）

- `Evidence/MappingEvidence.cs`（record，spec §7.2 shape）：Id / StartTime / EndTime / Rhythm / Accent / Energy / Vocal / Movement / Density / Repetition / Climax / Novelty / BeatConfidence / Confidence / Sources[]
- `Evidence/IMappingEvidenceBuilder.cs` + `DeterministicEvidenceBuilder`（spec §13.2）：从 MusicTimeline（section 能量/类型/事件）派生证据——MVP 基线从 energy/section type/beat density 计算，sources 标注（`audio.energy`、`structure.chorus` 等）
- 测试：evidence 值域 [0,1]、sources 非空、climax 在最高能量段最高、确定性

### Task 6 — P1：Global Mapping Plan（ADR-MVP-A-012）

- `GlobalPlanning/GlobalMappingPlan.cs`（spec §9.2）：DifficultyCurve / MappingComplexityCurve / SectionPlans[] / GlobalClimax / ContrastPoints[]
- `GlobalPlanning/IGlobalMappingPlanner.cs` + `DeterministicGlobalPlanner`（spec §9.6）：按 section energy/type + difficulty profile 确定全局曲线；识别 global climax（最高能量候选）；**future-aware**：final chorus 保留余量
- 测试：curve 单调性符合能量、climax 正确、确定性

### Task 7 — P1：Planner 分解 + Candidate + Ranking（ADR-MVP-A-013）

- `LocalPlanning/ILocalMappingPlanner.cs`（spec §10）：输入 context + evidence + globalPlan → `MappingIntent`
- `Candidates/IPatternCandidateGenerator.cs`（spec §11）：对每个 intent 生成 **3–5 个候选**（family 参数组合）
- `Candidates/IPatternCandidateRanker.cs`（spec §12.1 权重）：`0.30*MusicAlignment + 0.20*DifficultyFit + 0.20*Continuity + 0.15*Readability + 0.10*StructuralFit + 0.05*Validity`
- 保留 `DeterministicMappingPlanner` 作为 facade（内部走新链路），既有测试不破坏
- 测试：候选数 ≥3、硬无效候选被拒、rank 确定性、权重可配置

### Task 8 — P1：Ruleset 解耦（ADR-MVP-A-014）

- `Backends/IRulesetMappingBackend.cs`：`RulesetKind Ruleset` + `IPatternProvider Provider` + `IMappingValidator Validator` + `IManiaOsuRenderer`-style `IRenderer`（`Render(MappingDocument)` → string）
- `Backends/Mania4KMappingBackend.cs`：聚合现有 Mania4KPatternProvider + ManiaValidator + ManiaOsuRenderer
- `MappingIrPipeline` 构造改为接受 `IRulesetMappingBackend`（默认 Mania4K）；删除 pipeline 内硬编码 Mania 分支
- 测试：pipeline 用 backend 注入仍全绿；backend 暴露的 Ruleset 与文档一致

### Task 9 — P1：Critic + Revision（ADR-MVP-A-015）

- `Critique/IMappingCritic.cs` + `BaselineMappingCritic`（spec §15）：硬问题（invalid timing/overlap/unsupported object）阻断；软问题（weak alignment/continuity）进 revision 排序
- `MappingIrPipeline` 增加有界 revision loop（spec §19.1：`max_revisions_per_phrase=3`，配置化）：critic 报软问题 → 重新生成候选 → 重排 → 重渲染；硬问题直接失败
- 测试：critic 正反例；revision loop 在预算内终止；超预算不无限循环

### Task 10 — P1：Difficulty feedback（ADR-MVP-A-016）

- `Difficulty/IDifficultyEvaluator.cs`：`TryEvaluate(MappingDocument) → double? StarRating`（官方 ManiaDifficultyCalculator adapter 在 ruleset 程序集实现，Core 内提供接口 + null 返回骨架）
- `Evaluation.Difficulty` 填入 observed SR（有则填，无则保持 object_count/duration）
- 测试：接口契约 + 空实现返回 null 不炸

## 4. 测试策略（沿用 MVP A + 补齐 review 缺口）

- 既有 43 测试保持通过（facade 兼容）
- 新增测试文件：`DeterministicHashTests`、`PatternParametersTests`、`SchemaConformanceTests`、`EvidenceBuilderTests`、`GlobalPlannerTests`、`CandidateRankerTests`、`BackendTests`、`CriticTests`、`DifficultyEvaluatorTests`、`PipelineRevisionTests`
- 覆盖 review 指出的缺口：JSON→IR→JSON 语义等价（含 Parameters）、pattern parameter roundtrip、transition consistency、cross-ruleset 校验（backend 层）

## 5. 决策记录

新 ADR：ADR-MVP-A-008（稳定哈希）、009（强类型参数）、010（canonical schema）、011（Evidence）、012（Global Plan）、013（Planner 分解）、014（Backend 解耦）、015（Critic/Revision）、016（Difficulty adapter）。

## 6. 最终提交前检查清单

- [x] `dotnet build`（Release）全绿、warnings-as-errors
- [x] `dotnet test` 全绿（MappingIr **84/84**（既有 43 + 新增 41）；既有四模式 Osu 50 / Mania 18 / Taiko 58 / Catch 53 无回归）
- [x] `dotnet format --verify-no-changes` 通过（Core/测试/demo）
- [x] schema 校验测试通过（canonical schema 副本 + 键集合/枚举断言；demo 产物 jsonschema PASS）
- [x] 端到端 demo 可运行（evidence → global → local → candidate/rank → backend → critic 全链路，valid=True alignment=1.0 deterministic=True）
- [x] 决策记录 ADR-MVP-A-008~011 补齐（稳定哈希/强类型参数/canonical schema/决策链）
- [ ] 文档同步（PLAN/architecture/verification/requirements 待更新）

## 7. 后续（非本次范围）

- P2：Copilot context API、preference logging、LLM planner、learned ranker、style conditioning（spec §31 P2）
- 完整四 ruleset backend（Osu/Taiko/Catch Pattern Provider）
- MusicTimeline 语义增强（kick/snare/vocal/melody 事件，依赖真实音频分析）
