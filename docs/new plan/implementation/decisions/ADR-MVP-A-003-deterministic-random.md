# ADR-MVP-A-003 — 确定性随机：seed 驱动的共享 Random 注入

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：`mapping-ir-v0.1-spec.md` §21 要求"固定 seed + 固定输入 → 固定输出"，Pattern Generator 内部需要随机性（列选择、间距抖动等）。

## 决策

- `PatternGenerationContext` 增加 `int Seed`（来自文档 `provenance`/生成上下文，缺省 0）；
- 每个 pattern family 生成器接收一个 `Random` 实例（由 provider 从 `Seed` 派生，且**每个 family 用独立派生 seed**：`Seed ^ familyName.GetHashCode()`），避免跨 family 的随机序列相互影响；
- **禁用** `System.Random.Shared`/静态随机：全局状态会破坏可复现性；
- 随机序列的行为（如"从合法列集中随机选"）封装在 `DeterministicRandom` 帮助类中，便于单测精确控制。

## 理由

1. 派生 seed 让"只改一个 family 的随机行为"不影响其他 family 的输出——对 golden 测试与增量开发友好；
2. 共享 `Random` 实例传入而非每步 new，保证同 seed 下序列完全一致（`new Random(seed)` 的序列是确定的，但重复创建相同 seed 的 Random 在 .NET 中序列相同——仍统一用单实例以消除歧义）。

## 备选

- 静态 `Random(seed)` 全局：测试间污染，多文档并行生成时不可复现。
- 不注入随机、纯规则生成：会丧失 pattern 多样性（variation 目标无法达成）。

## 影响

- `IPatternProvider.Generate` 签名不变，`Seed` 放入 `PatternGenerationContext`；
- Golden 测试固定 seed 即可精确快照对象序列。
