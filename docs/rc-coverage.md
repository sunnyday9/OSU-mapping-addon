# Ranking Criteria 覆盖矩阵（RC Coverage）

> 版本：v3 · 2026-08-21 · 对应 `docs/PLAN.md` §7 / `docs/requirements.md` FR-1.3 · 状态：M0/M1/M2/M3 完成（M3 生成 v2：多段/Spread/.osz/IDistributionProvider），M4 mania / M5 catch / M6 taiko 已落地（各有独立检查集与合成器）
> 维护约定（PLAN §7"可追溯"）：每条检查注释引用 RC 条款编号；新增/修改检查时必须同步本矩阵，否则检查不得合入。

---

## 1. 图例与口径

**实现位置**五类：

| 标记 | 含义 |
|---|---|
| 官方通用 | osu.Game 内置通用检查（`osu.Game/Rulesets/Edit/Checks/`），Verify 页与 ruleset 校验器**并列运行**，不依赖本插件 |
| 官方 osu/mania/taiko/catch | 各模式官方 BeatmapVerifier，经 `AiStudio*BeatmapVerifier` 聚合 |
| 自研 M1–M3 | 本仓库自研检查（**已实现**） |
| 已实现（M4–M6） | mania/taiko/catch 自研检查与合成器已落地 |
| 主观不可自动化 | 政策或听感条款，不硬编码为报错，作 checklist 提示与合成模板库设计原则（PLAN §3） |

**其他口径**：

- RC 条款核对日期 2026-08；官方检查类名以锁定版 ppy/osu（2026.730.0）源码为准（源码路径见 `docs/PLAN.md` 附录 A）；
- "官方覆盖"= 该项检查已随官方 Verify 生效；"已实现"= 自研检查已落地并有正/反用例测试（requirements.md FR-1.4）；
- 门禁判定口径（PLAN §3 门禁 1）：Error/Problem 级 0 项；Warning/Advice 作提示不阻断。

---

## 2. 覆盖矩阵

### 2.1 General Rules / Guidelines（通用条款）

| RC 分类 | 条款/规则摘要 | 是否客观可算 | 实现位置 | 状态 |
|---|---|---|---|---|
| General Rules | **snap 偏差**：所有物件必须对齐节拍网格，偏差 <2ms | 是 | 官方通用（`CheckUnsnappedObjects`） | 官方覆盖 |
| General Rules | **drain time**：时长下限；且最低难度不得高于 drain time 对应等级（<3:30 → Normal、3:30–4:15 → Hard、4:15–5:00 → Insane） | 是 | 官方通用（`CheckLowestDiffDrainTime`）+ 自研 `CheckSpreadStarRatingGaps`（缺档检查，同阈值） | 官方覆盖 + **已实现** |
| General Rules | **spread 星距与缺档**：难度集不得跳过难度等级、不得出现大幅星距差（RC 无数值，M1 规格：相邻星距 >2.0★ 报 Warning） | 部分（阈值自定，M1 规格） | 自研 `CheckSpreadStarRatingGaps` + M3 `SpreadPlanner`（生成侧约束相邻 ≤2.0★） | **已实现** |
| General Rules | **timing 点冲突**：红/绿线重复、timing 段重叠等 | 是 | 官方通用（Verify 内置 timing 检查，类名以锁定版源码为准）；自研增量已在 M3 支撑多段 timing（绿线 kiai） | 官方覆盖 + M3 多段已实现 |
| General Rules | **音频规格**：音频存在、时长匹配、码率/采样率合规 | 是 | 官方通用（`CheckAudio`：存在性/基本规格）；严格阈值（码率/采样率，门禁 1）由 `QualityGateRunner` 间接覆盖 | 官方覆盖（基本） |
| General Rules | **背景与文件规格**：背景存在、无损坏资源 | 是 | 官方通用（`CheckBackground`） | 官方覆盖 |
| General Rules | **预览点一致性**：preview time 取值合法且与音频时长一致 | 是 | 官方通用（`CheckPreview`） | 官方覆盖 |
| General Rules | **hitsound 要求**：物件有 hitsound、数量充足（few hitsounds） | 部分（数量可算，听感不可算） | 官方通用（`CheckHitSounds`）；听感部分为 checklist（PLAN §3） | 官方覆盖 + 主观项 |
| General Rules（政策） | **AI policy**：ranked 谱面必须 100% 人工输入、禁止生成式 AI（osu-wiki PR #15087，2026-08-13 合并） | 否（政策） | 主观不可自动化；落实为 NFR-3（产物标注 AI generated、不承诺可 rank）+ `AiStudioAssistantMod`（Ranked=false） | 已落实（非检查项） |

### 2.2 osu! 模式（per-mode 条款）

| RC 分类 | 条款/规则摘要 | 是否客观可算 | 实现位置 | 状态 |
|---|---|---|---|---|
| osu! 模式 | **物件间隔/重叠**：物件不重叠、间距合理 | 是 | ✅ 已内建于 OsuMapGenerator 约束求解（PLAN §6.3）+ G1 门禁验证；官方 osu 检查并列 | 已实现（M2；M3 按段间距可变） |
| osu! 模式 | **段落/kiai/break/SV**：段落切分、kiai 区间、break 休息、SV 绿线 | 部分 | M3 `BassAudioAnalyzer` 多段（2–5 段）+ `OsuMapGenerator` kiai/break/SV 绿线 | **已实现（M3）** |
| osu! 模式 | **多难度/集合导出**：难度集 spread、.osz 打包 | 部分 | M3 `SpreadPlanner` + `BeatmapSetExporter` + `.osz` 集合落盘 | **已实现（M3）** |
| osu! 模式 | **spinner 时长**：长度达标且不过长 | 是 | 官方 osu（`OsuBeatmapVerifier` 内置，`AiStudioBeatmapVerifier` 注释确认"spinner 长度已覆盖，不重复实现"） | 官方覆盖 |
| osu! 模式 | **4:3 出屏**：物件不超出 4:3 屏幕边界（offscreen） | 是 | 官方 osu（`OsuBeatmapVerifier` 内置 offscreen 检查） | 官方覆盖 |
| osu! 模式 | **SV 限制**：SV 取值/变化不极端（如 0.5–2.0 区间外需合理） | 部分 | M3 按段 SV 绿线已落地（`EffectControlPoint` kiai 区间 + 按强度 SV）；严格阈值由门禁覆盖 | 已实现（M3） |
| osu! 模式 | **combo 颜色**：至少 2 个不同自定义颜色，除非强制默认皮肤 | 是 | 自研 M1 `CheckComboColourCount`（2026.730.0 起颜色移至谱面皮肤 `SkinConfiguration`） | **已实现** |

### 2.3 mania 模式

| RC 分类 | 条款/规则摘要 | 实现位置 | 状态 |
|---|---|---|---|
| mania 模式 | **列分布**：各列使用均衡，无长期空列 | 自研 `CheckManiaColumnDistribution` | **已实现（M4）** |
| mania 模式 | **jack 限制**：同列连续击打不超过阈值 | 自研 `CheckManiaJackLimit` | **已实现（M4）** |
| mania 模式 | **chord 密度**：同时按键数与难度匹配 | 自研 `CheckManiaChordDensity` | **已实现（M4）** |
| mania 模式 | **难度设置**：OD/HP 区间 | 自研 `CheckManiaDifficultySettingsRanges` + `ManiaDifficultyRanges` | **已实现（M4）** |

### 2.4 taiko 模式

| RC 分类 | 条款/规则摘要 | 实现位置 | 状态 |
|---|---|---|---|
| taiko 模式 | **don/kat 平衡**：don/kat 比例不过度偏斜 | 自研 `CheckTaikoDonKatBalance` | **已实现（M6）** |
| taiko 模式 | **mono 序列**：单色长串限制 | 自研 `CheckTaikoMonoPattern` | **已实现（M6）** |
| taiko 模式 | **难度设置**：OD/HP 区间 | 自研 `TaikoDifficultyRanges` | **已实现（M6）** |

### 2.5 catch 模式

| RC 分类 | 条款/规则摘要 | 实现位置 | 状态 |
|---|---|---|---|
| catch 模式 | **hyperdash 可行性**：连续 hyperdash 间隔 | 自研 `CheckCatchHyperdashFeasibility` | **已实现（M5）** |
| catch 模式 | **出屏**：物件不超出 catch 场地 | 自研 `CheckCatchOffscreen` | **已实现（M5）** |
| catch 模式 | **移动可行性**：位移速度在可达范围内 | 自研 `CheckCatchMovementFeasibility` | **已实现（M5）** |
| catch 模式 | **难度设置**：AR/OD/HP/CS 区间 | 自研 `CatchDifficultyRanges` | **已实现（M5）** |

### 2.6 难度分级（difficulty levels 条款）

| RC 分类 | 条款/规则摘要 | 是否客观可算 | 实现位置 | 状态 |
|---|---|---|---|---|
| 难度分级 | **AR/OD/HP/CS 区间**：各难度等级 "Difficulty setting guidelines" | 是 | 自研 `CheckDifficultySettingsRanges` + 官方通用 | **已实现** |
| 难度分级 | **spinner 前后间隔**：Easy ≥4 拍 / Normal ≥2 拍 / Hard+ ≥1 拍 | 是 | 自研 `CheckSpinnerSpacing` | **已实现** |

---

## 3. 自研检查清单

| 检查类 | RC 条款来源 | 判定与阈值 | IssueType | 测试 |
|---|---|---|---|---|
| `CheckDifficultySettingsRanges` | osu! RC 各难度 "Difficulty setting guidelines" | 星数 → 难度等级 → AR/OD/HP/CS 须落区间；星数失败跳过 | Problem | `AiStudioBeatmapVerifierTest` |
| `CheckSpreadStarRatingGaps` | 通用 RC spread；osu! RC drain time | 相邻 >2.0★ → Warning；缺最低难度 → Warning | Warning | `AiStudioBeatmapVerifierTest` |
| `CheckComboColourCount` | osu! RC combo 颜色 | 去重后 <2 → Problem | Problem | `AiStudioBeatmapVerifierTest` |
| `CheckSpinnerSpacing` | osu! RC spinner 间隔 | 间隔 < requiredBeats × BeatLength → Warning | Warning | `AiStudioBeatmapVerifierTest` |
| `CheckManiaColumnDistribution` | mania RC 列分布 | 空列/不均衡 → Warning | Warning | — |
| `CheckManiaJackLimit` | mania RC jack | 连续同列 ≥4 → Warning | Warning | — |
| `CheckManiaChordDensity` | mania RC chord | 4K>2/7K>3 同时 → Warning | Warning | — |
| `CheckTaikoDonKatBalance` | taiko RC don/kat | 单类 ≥85% → Warning | Warning | — |
| `CheckTaikoMonoPattern` | taiko RC mono | 同色连续 ≥8 → Warning | Warning | — |
| `CheckCatchHyperdashFeasibility` | catch RC hyperdash | 连续 hyperdash 间隔 <80ms → Warning | Warning | — |
| `CheckCatchOffscreen` | catch RC 场地 | OriginalX ∉ [0,512] → Warning | Warning | — |
| `CheckCatchMovementFeasibility` | catch RC 移动 | 所需速度 > dash 阈值 → Warning | Warning | — |

---

## 4. 生成质量门禁 ↔ 检查实现映射

> PLAN §3 五道门禁是"ranked 级质量"的操作化定义；`AiStudio.Core/Models/QualityGateReport.cs`（`AllPassed`）已就位，生成落盘唯一放行条件（PLAN §6-5）。

| 门禁（PLAN §3） | 判定标准 | 对应检查实现（现状） | 备注 |
|---|---|---|---|
| ① 客观 RC 零错误 | Error/Problem 级 0 项 | 官方通用 + 官方 per-mode + 自研各模式 checks（G1 按 TargetLevel 上下文，豁免 set 级 drain time；难度设置类 Warning 已在 G1 豁免，G2 专责） | 已实现 |
| ② 难度设置合规 | AR/OD/HP/CS 落在目标难度 RC 区间 | `CheckDifficultySettingsRanges`（判定侧）+ `OsugameDifficultyRanges.Get(TargetLevel)`（取值侧） | 已实现 |
| ③ 节奏-音乐对齐度 | 物件落格率 ≥0.95（容差 1ms） | `QualityGateRunner` G3：拍/半拍网格对齐率；M3 起谱面自身 timing 重建，数据源与分析层一致 | 已实现（M3 按段密度已使对齐更贴近音乐分段） |
| ④ 参数分布 P5–P95 | 间距与 slider 占比落在 P5–P95 | `QualityGateRunner` G4 经 `IDistributionProvider` 读取 `tools/analysis/distributions.json`（`FileDistributionProvider` 无文件回退默认值）；`corpus.py` 离线合成拟合产出 P5–P95 | **已实现（M3）** |
| ⑤ SR 校准 ±0.3★ | 星数与用户目标偏差 ≤ ±0.3★ | `OsuStarRating.TryCalculate` / 各模式 `*StarRating`，校准闭环 `±0.3` | 已实现 |
