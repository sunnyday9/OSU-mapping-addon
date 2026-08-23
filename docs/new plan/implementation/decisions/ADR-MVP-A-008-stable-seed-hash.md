# ADR-MVP-A-008 — 稳定确定性 seed（FNV-1a 替代 string.GetHashCode）

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：code-review-v0.1 §14 指出：`Seed ^ family.GetHashCode()` 依赖 `string.GetHashCode()`——该实现**跨进程/跨 .NET 版本不保证稳定**，与"fixed seed + fixed input → deterministic"的承诺不一致，会破坏可复现性（dataset/AI debugging）。

## 决策

- 新增 `DeterministicHash` 工具类：FNV-1a 64 位稳定字符串哈希（`offset_basis=0xcbf29ce484222325`，`prime=0x100000001b3`）；
- `DeriveSeed(family, seed) = seed ^ (FNV1a64(family) 高 32 位 ^ 低 32 位)`；
- `IPatternGenerationContext.CreateFamilyRandom` 改用 `DeterministicHash.DeriveSeed`。

## 理由

1. FNV-1a 是公开、固定、无随机化的哈希算法，跨进程/跨平台结果一致；
2. 算法简单（~10 行），零依赖，与 MVP A"零新依赖"约束一致；
3. 测试覆盖已知向量（`Fnv1a64("hello") = 0xa430d84680aabd0b`）与跨实例一致性。

## 备选

- SHA-256：更"标准"但对 seed 派生过重（需字节转换），FNV-1a 足够且更快。
- xxHash：需要 NuGet 依赖，违反零依赖约束。

## 影响

- 既有 seed 的生成结果会变化（seed 语义保留，family 派生变了）——golden 测试全部基于相对断言，无需更新快照；
- 跨进程可复现性现在真正成立（spec §2.2 Deterministic rendering）。
