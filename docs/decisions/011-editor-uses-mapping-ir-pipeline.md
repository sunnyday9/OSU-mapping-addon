# ADR-011 编辑器 Mania 生成切换到 Mapping IR 管线

- 日期：2026-08-28
- 状态：已采纳（实施跟踪：spec #16，工单 #17 → #18 → #19）

## 背景

架构走查候选 5 判定两条 Mania 生成路径并存为事故源：编辑器 "Generate (Mania)" 走 M2 旧链（内联收敛公式，参数已与统一收敛核漂移），MVP-B 的 SR 校准闭环只活在测试里——违反「报告 SR = 实测 SR」不变量（见 `CONTEXT.md`）。相关：ADR-010 记录的 M2 时代各生成器内置校准，其收敛实现已由 `DensityScaleSearch` 收敛核统一（PR #15）。

## 决策

编辑器按钮改调实现生成器接口的插件侧适配器：注入 BASS 分析器 → 已校准 Mapping IR 管线 → 渲染写盘 + 音频复制 → GenerationResult（校准元数据 converged/iterations/observed_sr/final_density_scale 显性化进 QualityReport）。

- M2 Mania 生成器删除；osu! 模式 M2 生成器保留（IR 链尚无 osu! backend）
- 设置映射取最小：目标 SR / 容差 / balanced 维度档案；TargetLevel 与键数 ≠ 4 为已知缺口
- 校准不收敛不阻断落盘，元数据显性化供用户判断
- Core 保持纯内存库；文件系统产品环留在插件侧

## 后果

- 正：Mania 单一生成路径；报告与实测一致；谱面质量由 Mapping IR 决策链保证（MVP-A/B 验证）；编辑器 UX 契约不变（一行替换）
- 负：编辑器产出的谱面内容与 M2 时代完全不同（用户可见变化，已接受）；TargetLevel 查表与键数扩展成为新缺口，留待 M3

## 取舍

以一次性的用户可见行为变化，换取单一路径与「报告 SR = 实测 SR」不变量。拒绝双按钮并存方案（让双轨漂移继续存活，正是候选 5 判定的事故源本身）。
