# ADR-MVP-A-007 — LN family 节奏步长约束（防同列重叠）

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：初版 `single_ln`/`ln_rice`/`ln_release` 按 1/8 拍间隔生成 LN，但 LN 时长默认 1 拍——同列 LN 必然重叠（测试 `NoSameColumnOverlap_AcrossAllFamilies` 暴露）。

## 决策

`RhythmTimeline` 对 LN family 强制**步长 = 1 个整拍**（`step = subdivision`，即 1/8 细分下每 8 个细分点放一个对象），非 LN family 步长 = 1 个细分点。LN 时长默认 1 拍 → 相邻同列 LN 首尾相接（不重叠、合法）。

## 理由

1. mania 合法性：同列对象 start 不得落在前一个对象的 [start, end) 内；1 拍步长 + ≤1 拍时长保证该不变式；
2. LN 是"持续音"语义，1/8 拍间隔的 LN 在 4K 下不可读且无法游玩；
3. 步长约束放在节奏时间轴层，所有 LN family 共享，避免每个生成器重复实现。

## 影响

- LN family 对象密度 = 1 拍 1 个（可经 `ln_duration_beats` 参数调整）；
- 不变式测试 `NoSameColumnOverlap_AcrossAllFamilies` 全 family 通过。
