# ADR-003 NuGet 精确锁版 + api-compat 定时对抗

- 日期：2026-08-16（M0）
- 状态：已采纳

## 背景
ppy 按周发 NuGet，API 面漂移会导致构建断裂或静默行为变化；`RulesetAPIVersionSupported` 亦需跟随。

## 决策
csproj 精确锁版 `ppy.osu.Game` / `ppy.osu.Game.Rulesets.*` 为 `2026.730.0`，保证可复现构建；`api-compat.yml` 每周用最新包编译探针，失败自动开 Issue 驱动升级决策。

## 后果
- 正：构建稳定可复现，漂移可感知。
- 负：需人工跟进 ppy 发版，探针失败即产生维护工单。

## 取舍
以固定版本换稳定性，接受周级跟进成本。
