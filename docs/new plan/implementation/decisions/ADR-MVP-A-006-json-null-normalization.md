# ADR-MVP-A-006 — JSON 可空字段的 schema 归一化（HandleNull converter）

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：`mapping-ir-v0.1.schema.json` 要求 `music_timeline.sections[].labels` 为 array、`evaluation.difficulty` 为 object——两者都**不允许 null**；而 IR 模型字段可空（`Labels`、`Difficulty`）。直接序列化 null 导致 JSON Schema 校验失败。

## 决策

- 定义 `NullToEmptyStringArrayConverter` / `NullToEmptyDictionaryConverter`：null → `[]` / `{}`；
- **关键实现细节**：converter 类型参数必须用**非空** `JsonConverter<IReadOnlyList<string>>` 并重写 `HandleNull => true`——实测 .NET 8 对"可空类型参数的属性级 converter"会绕过 converter 直接把 null 写出去（最小复现验证），非空类型 + HandleNull 才接管 null。
- `MusicSection` 因需要 `Labels` 归一化且要支持反序列化构造器绑定，从 record 改为 sealed class（保留 Deconstruct/ToString，语义等价）。

## 理由

1. schema 是 v0.1 的权威契约，序列化产物必须可被 schema 校验通过（验收标准 spec §27-1"可序列化且 schema 校验"）；
2. 归一化在序列化层解决，模型层保持可空（内部语义"无标签" vs "空标签"不混淆）；
3. 实测驱动：不经过最小复现无法发现 .NET 8 的 HandleNull 行为。

## 备选

- 模型层强制非空（`Labels = Array.Empty<string>()` 默认值）：可行但对所有构造点侵入，且丢失"未设置 vs 空"的区分。
- 自定义整个对象的 converter：过度设计。

## 影响

- `JsonMappingIrSerializer` 产物通过官方 JSON Schema 校验（实测 PASS）；
- 未来新增 schema 非空字段沿用同一 converter 模式。
