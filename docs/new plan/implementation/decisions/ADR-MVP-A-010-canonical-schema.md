# ADR-MVP-A-010 — Canonical JSON Schema 入库 + 一致性测试

- **日期**：2026-08-23
- **状态**：已采纳
- **背景**：code-review-v0.1 §16 指出：仓库中没有 canonical `mapping-ir-v0.1.schema.json` 作为"协议真相源"——协议目前由 C# 类型 + 测试 + docs 隐式定义，无法独立于实现校验（尤其未来 LLM schema / Python dataset 消费）。

## 决策

- 把 `docs/new plan/mapping-ir-v0.1.schema.json` 复制为 canonical 副本：`src/AiStudio.Core/MappingIr/Schema/mapping-ir-v0.1.schema.json`（随 Core 源码进版本控制；测试 csproj 以 Content 链接到输出目录）；
- 新增 `SchemaConformanceTests`（C# 侧 shape 断言）：
  - 序列化产物键集合 ⊆ schema 属性 + schema required 全出现；
  - section type / ruleset / provenance origin ∈ schema 枚举；
  - labels 永远 array（null 归一化生效）、evaluation.difficulty 永远 object；
  - canonical 副本与 `docs/new plan` 原文件一致（防漂移）。
- 完整 JSON Schema 语义校验仍由 Python jsonschema 在 demo 产物上执行（CI/本地脚本），C# 测试做可重复的快速 shape 守护。

## 理由

1. 满足 spec §36 "JSON serialization is schema-valid" 与 code-review P0"三者 CI 强制一致"；
2. 不引入 JsonSchema.Net NuGet（保持零依赖；语义校验在 Python 侧已覆盖）；
3. canonical 副本使 schema 与 C# 类型同仓演进、单文件 diff 可审查。

## 影响

- 新增/修改 IR 字段时：改 C# 类型 + 改 canonical schema + 改 docs 副本，SchemaConformanceTests 会守住一致性；
- demo 产物每次生成后跑 jsonschema 校验（已在验证脚本中）。
