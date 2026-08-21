# ADR-007 四模式共享分析层、每模式专属合成器

- 日期：2026-08-21（M3–M6）
- 状态：已采纳

## 背景
osu!/mania/taiko/catch 各有独立生成方式与模型（2D 摆放、音符矩阵+人体工学约束、don-kat 序列、std 派生+移动可行性），但共享模式无关的分析层（BPM/beat/onset/能量/段落）。

## 决策
每模式一个 ruleset 程序集 + 独立检查集与语料分布，共享同一 `IAudioAnalyzer`/`BeatGrid`/`AudioSection` 与 `AiStudio.Core` 合成基架；Taiko 的 ONNX 分类器以无依赖存根形式预留未来接入点。

## 后果
- 正：复用分析能力，四模式独立演进，CI 生成回归防退步。
- 负：每模式需独立的 `BassAudioAnalyzer`（或复用）与 SR 校准迭代，新增模式时需复制分析器与合成器骨架。

## 取舍
以共享分析换一致性，以专属合成换模式适配；ONNX 仅在 Taiko 按需增强，不引入全局原生依赖。
