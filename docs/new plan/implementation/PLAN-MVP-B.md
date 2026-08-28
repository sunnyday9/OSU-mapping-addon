# MVP-B — SR 校准闭环实施计划

> 版本：v1 · 2026-08-28 · 状态：**已完成**（探针 → 旋钮 → 闭环 → 验收全绿）· 分支：main（直接合入）
> 依据：`docs/new plan/mapping-intelligence-v0.1-spec.md` §16（Difficulty Feedback Loop）、§25.4（DifficultyKnown 语义）、ADR-MVP-A-016（官方 evaluator adapter 交付点）、PLAN-MVP-A2 §7 后续项 1（SR 校准闭环 ±0.3★）

## 0. 交付摘要（2026-08-28）

| 交付物 | 状态 | 验证 |
|---|---|---|
| `StarRatingCalibrationLoop`（Core 纯算法校准循环） | ✅ | 7 单测（收敛/预算/不可用/停滞/确定性/不可达） |
| `DeterministicCandidateGenerator.DensityScale` 密度旋钮 | ✅ | sweep 实测：scale 1.0→2.76★, 2.0→4.99★, 3.0→7.55★ 连续单调 |
| `Mania4KPatternProvider` 连续密度采样（`RhythmTimeline.Keep`） | ✅ | `floor((i+1)r) > floor(ir)` 确定性均匀采样，消除平台区 |
| `ManiaOfficialDifficultyEvaluator`（官方 calculator adapter） | ✅ | render→decode→calc 纯内存链路，失败返回 null |
| `ManiaIrCalibratedPipeline`（开箱即用门面） | ✅ | 端到端 **SR=5.61 ∈ 5.5±0.15**，确定性成立 |
| Mania 端到端测试 | ✅ | 7 测试（SR 容差/DifficultyKnown/确定性/空图/解码一致性/sweep 单调） |
| 决策记录 | ✅ | ADR-MVP-B-001（密度旋钮）、ADR-MVP-B-002（官方 adapter） |

## 1. 目标

在 Mapping IR 管线之上实现 **G5 门禁（SR 校准）**：让管线产出文档的实测 SR（官方 `ManiaDifficultyCalculator`）落在 `DifficultyProfile.TargetStarRating ± Tolerance` 内，并让 `Evaluation.DifficultyKnown=true` 生效。这是五道质量门禁（PLAN.md §3）中唯一未闭环的一项。

## 2. 非目标（MVP-B 不做）

- 不做 Osu/Taiko/Catch 的 SR 校准（仅 Mania 4K——四模式 backend 是后续 MVP）
- 不改 `MappingIrPipeline` 的公开 API（`StarRatingCalibrationLoop` 是独立组件，由门面组装）
- 不做 LLM/learned 组件
- demo 保持 Core-only（不引官方包；校准是 ruleset 层能力）

## 3. 架构

```
ManiaIrCalibratedPipeline（ruleset 程序集门面）
  ├── MappingIrPipeline（Core，注入 DensityScale 的 candidate generator）
  ├── ManiaOfficialDifficultyEvaluator（ruleset：render→decode→ManiaDifficultyCalculator）
  └── StarRatingCalibrationLoop（Core 纯算法）
        └── 迭代：scale → 重跑管线 → 读 observed_sr → |target−sr| ≤ tol? 收敛 : 更新 scale
```

**密度旋钮链路**（关键设计，ADR-MVP-B-001）：

```
DensityScale（校准循环调节）
  → DeterministicCandidateGenerator：选 subdivision 档（1/4/1/8/1/16/1/24）+ 档内 density 参数
  → PatternIntent.Parameters["density"]
  → Mania4KPatternProvider.RhythmTimeline.Keep(i) = floor((i+1)r) > floor(ir)
  → 对象数连续单调 → 官方 SR 连续单调
```

探针发现（驱动设计）：`DifficultyProfile.Dimensions.Density` **不是有效旋钮**——它只经 `Emphasis.Density = evidence×0.7 + profile×0.3` 影响 subdivision 档位阈值（0.5/0.7），档内无变化，实测三个 profile 值产出相同 SR。真正的密度控制点是 **subdivision 档 + 档内节奏点保留比例**。

## 4. 实施步骤（SDLC：探针 → 设计 → 实现 → 测试 → 验收）

| 步骤 | 内容 | 验收 |
|---|---|---|
| P1 | SR 探针：render→decode→官方 calculator 实测默认产物 SR | density 0.72 → SR 2.76（目标 5.5 需上调）；sweep 1.0/2.0/3.0 → 2.76/4.99/7.55 单调可达 |
| P2 | `DeterministicCandidateGenerator.DensityScale` 旋钮 + 档位映射 | sweep 单调；scale=1.0 保持既有行为（回归测试） |
| P3 | `Mania4KPatternProvider` 连续密度采样（`RhythmTimeline.Keep`） | 对象数随 density 参数连续变化；确定性（同 seed 同输出） |
| P4 | `StarRatingCalibrationLoop`（Core） | 收敛公式 `next = clamp(scale×(1+delta/max(sr,0.5)), 0.2, 4.0)`；预算 6 次；停滞保护 |
| P5 | `ManiaOfficialDifficultyEvaluator` + `ManiaIrCalibratedPipeline` | 端到端 SR ∈ 目标±容差；DifficultyKnown=true |
| P6 | 测试 + 文档 | 14 新测试（7 Core + 7 Mania）；ADR×2；PLAN/README 更新 |

## 5. 测试策略

- **Core 单测**（stub evaluator，无官方依赖）：收敛、迭代上限、评估器不可用、无目标 SR、停滞、确定性、不可达目标 clamp
- **Mania 端到端**（真官方 calculator）：SR 容差（5.5±0.15）、DifficultyKnown、确定性、空图不抛、解码对象数一致、sweep 单调性
- **回归**：既有 284 测试零改动通过（DensityScale 默认 1.0 = 既有行为；`ManiaPatternParameters.Density` 默认 1.0 = 全量）

## 6. 决策记录

- `ADR-MVP-B-001-density-calibration-knob.md`：为什么调 DensityScale（subdivision 档 + 档内采样）而非 profile.Dimensions.Density
- `ADR-MVP-B-002-official-evaluator-adapter.md`：adapter 放 ruleset 程序集、走 render→decode 路径的理由

## 7. 最终提交前检查清单

- [x] `dotnet build`（Release）全绿、warnings-as-errors
- [x] `dotnet test` 全绿（MappingIr **112**（+7 校准）；Mania **25**（+7 端到端）；Osu 50 / Catch 53 / Taiko 58 无回归）
- [x] `dotnet format --verify-no-changes` 通过
- [x] 端到端：`ManiaIrCalibratedPipeline.Run` 产物 SR=5.61 ∈ 5.5±0.15、`DifficultyKnown=true`
- [x] 确定性：同 seed 两次运行对象序列完全一致
- [x] 决策记录 ADR-MVP-B-001/002 补齐

## 8. 后续（非本次范围）

- MVP-C：LLM Mapping Planner 替换 `DeterministicMappingPlanner`（接口已就绪）
- 完整四 ruleset backend + 各自 SR 校准（Osu/Taiko/Catch）
- Copilot 模式（spec §20）——校准后的 SR 可作为 Copilot 建议的难度约束
