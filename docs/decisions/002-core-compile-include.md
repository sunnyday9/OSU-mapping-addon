# ADR-002 共享核心源码编译并入（compile-include）

- 日期：2026-08-16（M0）
- 状态：已采纳

## 背景
需在四个独立 ruleset 程序集（每程序集仅识别一个 Ruleset 子类）间共享分析/合成/校验等核心能力，同时要求部署物可直接放入 `rulesets/` 被扫描加载，不产生额外依赖 dll 的二义性。

## 决策
`AiStudio.Core` 以 `<Compile Include="..\AiStudio.Core\**\*.cs" LinkBase="AiStudio.Core" />` 源码并入各 ruleset 程序集，rulesets 目录只部署自包含 dll；Core 自身零外部依赖，仅作为单元测试目标。

## 后果
- 正：四份拷贝共享同一源码、无漂移；避免加载器对“无 Ruleset 子类 dll”的不确定处理；单 dll 便于分发与安装。
- 负：重复编译，包体略增；Core 变更需四工程联动构建；已由 CI 8 工程矩阵覆盖。

## 取舍
用编译期去重替代运行时共享，优先保证部署正确性与可分发性。
