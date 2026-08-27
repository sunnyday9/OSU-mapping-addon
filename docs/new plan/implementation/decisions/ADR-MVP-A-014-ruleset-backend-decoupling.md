# ADR-MVP-A-014 — Ruleset Mapping Backend 解耦

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：code-review-v0.1 指出 Pipeline/Validator/Renderer 硬编码 Mania，未来四 ruleset（osu!/taiko/catch）无法复用。spec §26 要求 ruleset 能力经接口解耦。

## 决策

- **`Backends/IRulesetMappingBackend`**：`RulesetKind Ruleset` + `IPatternProvider Provider` + `IMappingValidator Validator` + `Render(MappingDocument)`；
- **`Backends/Mania4KMappingBackend`**：聚合 `Mania4KPatternProvider` + `MappingValidator` + `ManiaOsuRenderer`；
- `MappingIrPipeline` 构造接受 `IRulesetMappingBackend`（默认 Mania4K），内部不再硬编码 Mania 分支。

## 理由

1. pipeline 与 ruleset 解耦后，新增 Osu/Taiko/Catch backend 只需实现接口，pipeline 零改动（spec §26 允许 Mania 4K 为首个实现目标）；
2. 单一聚合点 = 每个 ruleset 的 provider/validator/renderer 版本一致性由 backend 保证。

## 影响

- Pipeline 中所有 ruleset 专属操作（provider 生成、校验、渲染）都经 `backend` 访问；
- **非 Mania ruleset 仍只支持到接口层**：候选生成器对非 Mania 返回空、validator 报 warning——明确"不支持"而非静默产出错误语义（MVP 范围限制）。
