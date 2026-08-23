# ADR-MVP-A-009 — 强类型 Pattern 参数（ManiaPatternParameters）

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：code-review-v0.1 §15 指出：`PatternIntent.Parameters` 是 `IReadOnlyDictionary<string, object?>`，JSON 反序列化后嵌套值（如 `column_order` 数组）进入 `JsonElement` 表示，而 provider 内 `value is object[]` 判空失败 → **静默回退默认值**——内存 IR 与 JSON 恢复后的 IR 不等价。

## 决策

- 新增 `ManiaPatternParameters`（record）：Subdivision/Density/ColumnStrategy/ColumnOrder/JackTolerance/Bpm/ChordSize/ChordDensity/LnRatio/LnDurationBeats/Count/JackColumn；
- `ManiaPatternParameters.FromDictionary` 统一解析：**同时兼容 CLR 原始类型与 `JsonElement`**（`GetString/GetInt32/GetDouble/EnumerateArray`），缺失/非法回退默认值（宽松解析，validator 把关）；
- `Mania4KPatternProvider` 全部参数读取改走强类型（含 `RhythmTimeline` 构造——它之前直接 `Convert.ToDouble(JsonElement)` 会抛 `InvalidCastException`，由 roundtrip 测试暴露并修复）。

## 理由

1. 单一解析入口消除"内存 vs JSON 恢复不等价"隐患；
2. IR 契约不变（`Parameters` 仍是开放字典），仅消费端强类型化——不破坏 schema；
3. 测试 `PatternIntentRoundTrip_JsonElementCompatible_ProviderOutputEqual` 证明内存与 JSON 恢复后的 provider 输出完全一致。

## 影响

- LLM 产出 JSON → IR → provider 的链路（spec §14.3）现在安全；
- `RhythmTimeline` 不再直接读字典（消除最后一处 JsonElement 脆弱点）。
