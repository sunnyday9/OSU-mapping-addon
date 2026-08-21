# ADR-008 BASS 解码 + spectral-flux 自研节拍

- 日期：2026-08-19（M2–M3）
- 状态：已采纳

## 背景
曾尝试 `BASS_FX_BPM_DecodeGet`，实测对合成点击轨返回 0 且无错误码，不可靠。

## 决策
改用 `Bass.ChannelGetData` FFT 的 spectral-flux 包络 + 64 采样块能量精化（≈1.45ms）做 onset/节拍检测，峰值拾取 + IOI 中位数求 BPM，回退自相关；段落则在谱通量基础上做能量包络滑动窗口+z-score 多段切分（2–5 段，`KiaiCandidate`）。

## 后果
- 正：零新增原生依赖、离线可用、headless/CI 可用；`Bass.Init(0)` 无声设备模式稳定。
- 负：自研阈值与窗长需持续调参；短音频回退为单段 Verse。

## 取舍
以可控的自研 DSP 换对不可靠原生扩展的依赖，保留 BASS_FX 仅作决策记录。
