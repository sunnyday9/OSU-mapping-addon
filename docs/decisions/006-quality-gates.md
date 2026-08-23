# ADR-006 不以 ranked 为目标但以五道质量门禁为硬标准

- 日期：2026-08-17（M1）
- 状态：已采纳

## 背景
RC AI policy（osu-wiki PR #15087, 2026-08-13）要求 ranked 谱面 100% 人工输入、禁止生成式 AI 痕迹。

## 决策
本项目不冲 rank，但生成与辅助产出的质量基准与 ranked 一致：PLAN §3 五道门禁全绿才是“完成”的硬定义；产物默认标注 `AI generated`，`AiStudioAssistantMod.Ranked=false`。

## 后果
- 正：符合社区与官方合规预期，避免 ranked 投稿风险。
- 负：需以客观可量化门禁持续约束生成质量，门禁即迭代终点。

## 取舍
以质量对齐替代名义 rank，用可验证门禁定义完成度。
