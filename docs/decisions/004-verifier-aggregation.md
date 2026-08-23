# ADR-004 复用官方 verifier + 自研增量检查

- 日期：2026-08-17（M1）
- 状态：已采纳

## 背景
官方 `OsuBeatmapVerifier`（MIT）已覆盖 offscreen、spinner 长度、few hitsounds 等；官方通用检查列表为 private 不可追加；MapsetVerifier 为 GPL-3.0 不可链代码。

## 决策
以 ruleset verifier 身份聚合官方 verifier 结果，并并行运行自研增量（spread 星距、难度区间、combo 颜色、spinner 间隔等）；每模式独立检查集。

## 后果
- 正：不重复造轮子，覆盖可追溯到 RC 条款。
- 负：需维护增量检查与官方重叠边界，M4–M6 每模式需独立落地检查集。

## 取舍
借鉴思路、重写实现，规避许可证传染同时保证可审计性。
