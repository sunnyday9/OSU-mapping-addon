# ADR-MVP-A-001 — Mapping IR 使用 C# record 类型

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：`docs/new plan/mapping-ir-types.cs` 已给出 IR 的 C# 类型草案（全部为 record），需决定最终 IR 承载类型。

## 决策

Mapping IR v0.1 全部使用 **sealed record**（含 record struct 枚举与 `IReadOnlyList<T>` 集合字段），与草案 `mapping-ir-types.cs` 对齐。

## 理由

1. **值语义**：IR 是"文档"（document），生成/校验/对比均以值等价为主，record 自带 `Equals`/`GetHashCode`/`ToString`，便于 golden 测试与 diff。
2. **不可变性**：AI 规划产物不应被 renderer 意外修改；`init` 属性 + `IReadOnlyList` 强制只读，配合 `with` 表达式做局部变更（如 provenance 更新）。
3. **序列化友好**：System.Text.Json 对 record 的 `init` 属性 + 构造器完美支持，无反射开销。
4. **与上游草案一致**：`mapping-ir-types.cs` 已是 record 风格，避免计划与实现漂移。

## 备选

- `class` + mutable 属性：便于对象图复用，但破坏不可变性约束，validator/generator 之间易产生隐式耦合。
- struct：集合字段语义复杂，不利于嵌套文档结构。

## 影响

- IR 所有变更通过 `with` 表达式；文档级更新由 `MappingDocument` 工厂方法封装。
- JSON 反序列化后对象默认不可变，符合"IR 是数据不是服务"的定位。
