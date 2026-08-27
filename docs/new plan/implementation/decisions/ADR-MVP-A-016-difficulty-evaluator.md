# ADR-MVP-A-016 — Difficulty Evaluator（官方 calculator adapter 注入点）

- **日期**：2026-08-23（修订：2026-08-27 code-review 合入前确认范围）
- **状态**：已采纳（接口 + Core 骨架）；ruleset adapter 交付列为后续任务
- **背景**：code-review-v0.1 指出 Difficulty feedback 未接入（spec §16/§25.4）。官方 `ManiaDifficultyCalculator` 依赖 osu! 类型，只能在 ruleset 程序集使用，Core 不能引用。

## 决策

- **`Difficulty/IDifficultyEvaluator`**：`double? TryEvaluateStarRating(MappingDocument)`——评估整图 SR；不可用返回 null；
- **Core 默认 `UnavailableDifficultyEvaluator`**：恒返回 null；pipeline 据 null 设置 `Evaluation.DifficultyKnown=false`，不声称达到目标 SR（spec §25.4 观测性原则）；
- **官方 adapter 位置**：ruleset 程序集（Mania 插件）实现 `IDifficultyEvaluator`，内部用官方 `ManiaDifficultyCalculator` 对渲染文档（或 IR ConcreteObjects）计算 SR——**本次范围仅接口 + 骨架**，adapter 随 SR 校准闭环（MVP-B）交付。

## 理由

1. Core 零依赖约束（不引用 ppy.osu.Game）⇒ 接口必须在 Core、实现必须在 ruleset 程序集；
2. `DifficultyKnown=false` 语义保证"评估器不可用时不声称达标"——诚实性优先于功能完整。

## 影响

- Pipeline `Evaluation.Difficulty["observed_star_rating"]` 现为 null、`DifficultyKnown=false`；
- MVP-B（SR 校准闭环 ±0.3★）的接入点已就绪：注入 adapter 后 pipeline 自动填 observed SR。
