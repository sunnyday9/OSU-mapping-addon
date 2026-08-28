# ADR-MVP-B-001 — 密度校准旋钮：DensityScale（subdivision 档 + 档内采样）

- **日期**：2026-08-28
- **状态**：已采纳
- **背景**：MVP-B 需要"调节什么来逼近目标 SR"的答案。最初假设 `DifficultyProfile.Dimensions.Density` 是自然旋钮（它驱动 `Emphasis.Density`，进而经 `DeterministicCandidateGenerator` 的 0.5/0.7 阈值选 subdivision 档）。**探针实测推翻了这个假设**：三个 profile 密度值（0.4/0.72/1.0）产出完全相同 SR（2.76★）——因为 `Emphasis.Density = evidence.Density×0.7 + profile.Density×0.3`，evidence 主导且值域落在同一档位阈值内，profile 贡献被吞掉。

## 决策

新增 `DeterministicCandidateGenerator.DensityScale`（默认 1.0 = 既有行为），语义 = **相对"意图默认档全量"的目标密度倍数**：

1. **档位选择**：`target = clamp(scale × levelDensity[baseLevel], 0.02, 4.0)`，选满足 `档位密度 ≥ target` 的最低 subdivision 档（1/4 → 1/8 → 1/16 → 1/24，相对密度 0.25/0.5/1.0/2.0）；
2. **档内采样**：`densityParam = target / levelDensity[level]`（∈[0.05, 1.0]）写入 `PatternIntent.Parameters["density"]`；
3. **Provider 消费**：`Mania4KPatternProvider.RhythmTimeline.Keep(i) = floor((i+1)×r) > floor(i×r)`——确定性均匀采样（无 rng、无浮点累积），r 单调 → 保留数单调 → SR 单调。

实测曲线（174BPM 三段式，目标 5.5）：scale 1.0→2.76★, 1.5→3.95★, 2.0→4.99★, 2.25→5.76★, 3.0→7.55★——**全程连续单调**，5.5 可达。

## 理由

1. **连续性是校准收敛的前提**：纯 stride 取整（`ceil(1/r)`）产生平台区（实测 1.25–1.75 全为 3.26★），校准公式会在平台内停滞；`floor((i+1)r) > floor(ir)` 是精确比例采样，无平台；
2. **默认行为不变**：`DensityScale=1.0` 时 target=档位全量、densityParam=1.0、Keep 恒真 → 与既有生成完全一致（回归测试守护）；
3. **旋钮正交于意图**：不改 Evidence/LocalPlanner/决策链，只缩放候选生成的密度表达——校准是"生成后处理"而非"决策层改动"。

## 影响

- `ManiaPatternParameters.Density` 从"未使用字段"变为**校准旋钮**（默认 1.0 = 全量，原默认 0.5 会意外稀疏化，已修正并更新测试）；
- 候选生成器同档内 4 个候选 subdivision 相同（原为混合档），family 仍是主要区分维度——ranker 行为略有变化但测试无回归；
- 校准后的文档在 `Evaluation.Difficulty` 记录 `observed_star_rating` + `DifficultyKnown=true`（spec §25.4）。
