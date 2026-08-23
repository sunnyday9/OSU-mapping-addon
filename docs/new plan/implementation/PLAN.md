# MVP A — Mapping IR 核心层实施计划

> 版本：v2 · 2026-08-23 · 状态：**已完成**（S1–S7 全部交付，验证全绿）· 分支：`feat/mvp-a-mapping-ir`
> 依据：`docs/new plan/osu_lazer_ai_mapper_detailed_plan.md` §26/§28（第一阶段交付物 + 首个开发任务）、`docs/new plan/mapping-ir-v0.1-spec.md` §27（v0.1 验收标准）、`docs/new plan/mania-pattern-grammar-v0.1.md`（4K MVP Pattern 集）

## 0. 交付摘要（2026-08-23）

| 交付物 | 状态 | 验证 |
|---|---|---|
| Mapping IR v0.1 类型（13 顶层键，与 schema 对齐） | ✅ | JSON Schema 校验 **PASS**（`mapping-ir-v0.1.schema.json`） |
| JSON 序列化（snake_case + 枚举字符串 + null 归一化） | ✅ | roundtrip 测试 + 与示例文档同构测试 |
| MusicTimelineBuilder（BeatGrid/AudioSection → IR 时间线） | ✅ | 拍对齐/段落/事件测试 |
| DeterministicMappingPlanner（规则型，带 rationale） | ✅ | 确定性 + 段落意图 + 转换标签测试 |
| Mania4KPatternProvider（9 family） | ✅ | 不变式（列/单调/无重叠/LN 合法性/节奏网格）+ 确定性测试 |
| MappingValidator（结构/ruleset/对象/计划一致性） | ✅ | 正反例测试 |
| ManiaOsuRenderer（.osu v14 导出） | ✅ | 段完整性 + 对象数 + 确定性测试 |
| MappingIrPipeline（端到端闭环） | ✅ | demo：3 段 → 1848 对象 → valid=True alignment=1.0 deterministic=True |
| mapping-ir-demo CLI | ✅ | `tools/mapping-ir-demo` 可运行，输出 .osu + .json |
| 单元测试 | ✅ | **43/43 通过**（新）+ 既有 Osu 50 / Mania 18 / Taiko 58 / Catch 53 无回归 |

## 1. 目标

在现有 osu! AI Studio 仓库（M0–M6 已交付）之上，实现 AI Mapper 的**地基层**（详细计划 §26"第一阶段交付物" 的 1–6 项）：

1. `Mapping IR v0.1` — 语义优先的中间表示（C# record 类型 + JSON 序列化 + JSON Schema 校验）
2. `MusicTimeline` 构建器 — 从现有 `IAudioAnalyzer` 的 `BeatGrid`/`AudioSection` 产出 IR 时间线（含 MusicEvent/Phrase/Section）
3. `MappingIntent` 规划器 — 确定性规则规划（无 LLM 时也能跑通闭环）
4. Mania 4K Pattern Grammar — 4K MVP Pattern 集（single/stream/burst/jack/jump/jumpstream/single_ln/ln_rice/ln_release）
5. 确定性 Pattern Generator（`IPatternProvider`）— 固定 seed + 固定输入 → 固定输出
6. Pattern Validator（`IMappingValidator`）— 无 LLM 可检查的约束（列合法性/重叠/密度/手部策略/jack 上限/LN 约束）
7. Mania 4K `.osu` 渲染器 — 由 `ConcreteObject[]` 渲染为可打开的 mania .osu（确定性）
8. 端到端 CLI 演示入口（`MappingIrDemo`）— 一首歌 → 时间线 → 计划 → 生成 → 校验 → .osu

## 2. 非目标（MVP A 不做）

- 不接入 LLM（MappingIntent/PatternIntent 由确定性规则规划产生；接口保留未来 LLM 注入点）
- 不做 Star Rating 校准闭环（属于后续 MVP，本阶段仅验证输出可被 osu!lazer 打开）
- 不做 Standard/Taiko/Catch 的 Pattern Provider（仅保留统一接口 `IPatternProvider`）
- 不改动现有 M0–M6 代码路径（新增 `AiStudio.Core/MappingIr/` 命名空间，独立于现有 Analysis/Synthesis）
- 不引入任何新 NuGet 依赖（纯 .NET 8，JSON 序列化用 System.Text.Json）

## 3. 架构位置

```
src/AiStudio.Core/MappingIr/
├── Model/                  # Mapping IR v0.1 类型（与 docs/new plan/mapping-ir-types.cs 对齐）
├── Serialization/          # JsonMappingIrSerializer（System.Text.Json，snake_case 对齐 schema）
├── Timeline/               # MusicTimelineBuilder（BeatGrid/AudioSection → IR Timeline）
├── Planning/               # DeterministicMappingPlanner（Timeline + DifficultyProfile → MappingPlan）
├── Patterns/               # Mania4KPatternProvider + 各 family 生成算法 + seed 化随机
├── Validation/             # MappingValidator（Pattern 级 + 文档级）
└── Rendering/              # ManiaOsuRenderer（ConcreteObject[] → .osu 文本）
```

## 4. 实施步骤（SDLC：设计 → 实现 → 测试 → 验收）

| 步骤 | 内容 | 验收 |
|---|---|---|
| S1 | IR 类型 + 序列化 + schema 校验 | `mapping-ir-v0.1.schema.json` 校验通过示例文档；roundtrip 测试 |
| S2 | MusicTimelineBuilder | 合成 beat grid → 正确 timeline（beat 事件/小节/段落/phrase） |
| S3 | DeterministicMappingPlanner | 段落 → MappingIntent（强度/能量驱动）；Intent → PatternIntent（节奏/列策略/LN 政策） |
| S4 | Mania4KPatternProvider | 9 个 family 全部可生成；固定 seed 输出稳定；无非法列/重叠/密度越界 |
| S5 | MappingValidator | 对已知坏输入报错（列越界、重叠、密度尖峰、jack 超限） |
| S6 | ManiaOsuRenderer | 输出可解析的 .osu（Roundtrip 测试：对象数/时间/列一致） |
| S7 | 端到端演示 + 文档 | 生成一张完整 4K 谱面（demo），决策记录补齐，提交 |

## 5. 测试策略

- **不变式测试**：每个 pattern family 生成结果满足（列 ∈ [0,3]、时间单调、无重叠、密度在预算内、jack 上限、LN end>start）
- **确定性测试**：同 seed 两次生成完全一致
- **Golden 测试**：固定输入（合成 timeline）+ 固定 seed → 精确对象序列快照
- **Validator 测试**：正例全过 / 反例（坏列、重叠、超密度、长 jack）全报
- **Roundtrip 测试**：JSON 序列化 → 反序列化 → 语义等价；.osu 渲染 → 文本可解析且对象数一致

## 6. 决策记录

所有框架/方案选择（record vs class、snake_case JSON、seed 策略、规划规则、对齐度量、null 归一化、LN 步长约束等）记录在
`docs/new plan/implementation/decisions/`，编号 ADR-MVP-A-001~007。

## 7. 最终提交前检查清单

- [x] `dotnet build`（Release）全绿、warnings-as-errors（0 警告 0 错误）
- [x] `dotnet test` 全绿（MappingIr 43/43 + 既有 179 无回归）
- [x] `dotnet format --verify-no-changes` 通过（Core/测试/demo）
- [x] schema 校验示例文档通过（`mapping-ir-v0.1.schema.json` validate PASS）
- [x] 端到端 demo 生成 .osu 可解析（valid=True alignment=1.0）
- [x] 决策记录完整（ADR-MVP-A-001~007）

## 8. 后续（非 MVP A 范围）

- MVP-B：Difficulty 校准闭环（官方 ManiaDifficultyCalculator 迭代 → 目标 SR ±0.3★）
- MVP-C：LLM Mapping Planner 替换 `DeterministicMappingPlanner`（接口已就绪）
- MVP-D：Standard Pattern Provider + 2D 几何渲染
- Copilot 模式（对象/模式/段落三粒度建议）

