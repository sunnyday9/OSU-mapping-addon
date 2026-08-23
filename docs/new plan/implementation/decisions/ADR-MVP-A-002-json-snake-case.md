# ADR-MVP-A-002 — JSON 序列化采用 snake_case 映射 + 独立 DTO 层

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：`mapping-ir-v0.1.schema.json` 的所有字段均为 snake_case（`start_time`、`music_timeline`），而 C# 类型草案为 PascalCase。需决定序列化策略。

## 决策

- 使用 System.Text.Json 的 **`JsonNamingPolicy.SnakeCaseLower`**（.NET 8 内置）做全局命名映射，C# 类型保持 PascalCase；
- 枚举序列化为 snake_case 字符串（自定义 `JsonStringEnumConverter` + 命名策略），与 schema 的 enum 值（`pre_chorus`、`de_escalation`）对齐；
- `object?` 属性（`Features`、`Variant`、`Parameters` 等）保持 `JsonElement`-friendly 序列化（默认 `object` 序列化为原始 JSON）；
- **不引入独立 DTO 层**：IR record 直接作为 JSON 模型，靠命名策略对齐 schema。

## 理由

1. 零额外依赖（纯 System.Text.Json，符合 MVP A 非目标"不引入新 NuGet"）；
2. 一个模型两用（内存 IR = JSON 文档），避免 DTO 映射层成为漂移源；
3. SnakeCaseLower 是 .NET 8 内置策略，无需手写转换器。

## 备选

- 手写 DTO + AutoMapper：增加维护面，MVP 阶段不值得。
- 属性级 `[JsonPropertyName]`：枚举 20+ 值、字段 60+ 个，标注爆炸且易错。

## 影响

- JSON 文件名/字段名与 schema 严格一致，schema 校验可直接用于序列化产物；
- 反序列化时未知字段默认忽略（schema `additionalProperties: false` 的校验由验证器负责，不在反序列化层报错）。
