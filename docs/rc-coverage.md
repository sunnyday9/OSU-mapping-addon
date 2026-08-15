# Ranking Criteria 覆盖矩阵（RC Coverage）

> 版本：v1 · 2026-08-15 · 对应 `docs/PLAN.md` §7 / `docs/requirements.md` FR-1.3 · 状态：M0 完成、M1 进行中
> 维护约定（PLAN §7"可追溯"）：每条检查注释引用 RC 条款编号；新增/修改检查时必须同步本矩阵，否则检查不得合入。

---

## 1. 图例与口径

**实现位置**五类：

| 标记 | 含义 |
|---|---|
| 官方通用 | osu.Game 内置通用检查（`osu.Game/Rulesets/Edit/Checks/`），Verify 页与 ruleset 校验器**并列运行**，不依赖本插件 |
| 官方 osu | `OsuBeatmapVerifier`（osu 模式官方检查），经 `AiStudioBeatmapVerifier` 聚合（M0 起生效） |
| 自研 M1 | 本仓库 `src/osu.Game.Rulesets.AiStudio.Osu/Checks/` 增量检查（**已实现**） |
| 计划 M2+ | 生成管线 / 检查引擎扩展规划（未实现） |
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
| General Rules | **drain time**：时长下限；且最低难度不得高于 drain time 对应等级（<3:30 → Normal、3:30–4:15 → Hard、4:15–5:00 → Insane） | 是 | 官方通用（`CheckLowestDiffDrainTime`）+ 自研 M1 `CheckSpreadStarRatingGaps`（缺档检查，同阈值） | 官方覆盖 + **已实现**（自研） |
| General Rules | **spread 星距与缺档**：难度集不得跳过难度等级、不得出现大幅星距差（RC 无数值，M1 规格：相邻星距 >2.0★ 报 Warning） | 部分（阈值自定，M1 规格） | 自研 M1 `CheckSpreadStarRatingGaps` | **已实现** |
| General Rules | **timing 点冲突**：红/绿线重复、timing 段重叠等 | 是 | 官方通用（Verify 内置 timing 检查，类名以锁定版源码为准）；自研增量 ⏳ M2+ | 官方覆盖 + 待扩展（M2+） |
| General Rules | **音频规格**：音频存在、时长匹配、码率/采样率合规 | 是 | 官方通用（`CheckAudio`：存在性/基本规格）；严格阈值（码率/采样率，门禁 1）⏳ M2+ | 官方覆盖（基本）+ 待实现（严格） |
| General Rules | **背景与文件规格**：背景存在、无损坏资源 | 是 | 官方通用（`CheckBackground`） | 官方覆盖 |
| General Rules | **预览点一致性**：preview time 取值合法且与音频时长一致 | 是 | 官方通用（`CheckPreview`） | 官方覆盖 |
| General Rules | **hitsound 要求**：物件有 hitsound、数量充足（few hitsounds） | 部分（数量可算，听感不可算） | 官方通用（`CheckHitSounds`）；听感部分为 checklist（PLAN §3） | 官方覆盖 + 主观项 |
| General Rules（政策） | **AI policy**：ranked 谱面必须 100% 人工输入、禁止生成式 AI（osu-wiki PR #15087，2026-08-13 合并） | 否（政策） | 主观不可自动化；落实为 NFR-3（产物标注 AI generated、不承诺可 rank）+ `AiStudioAssistantMod`（Ranked=false） | 已落实（非检查项） |

### 2.2 osu! 模式（per-mode 条款）

| RC 分类 | 条款/规则摘要 | 是否客观可算 | 实现位置 | 状态 |
|---|---|---|---|---|
| osu! 模式 | **物件间隔/重叠**：物件不重叠、间距合理 | 是 | 合成器内建 RC 硬约束 ⏳ M2+（PLAN §6.3，约束内建于摆放求解器）；官方 osu 检查如覆盖则并列 | 待实现（M2+） |
| osu! 模式 | **spinner 时长**：长度达标且不过长 | 是 | 官方 osu（`OsuBeatmapVerifier` 内置，`AiStudioBeatmapVerifier` 注释确认"spinner 长度已覆盖，不重复实现"） | 官方覆盖 |
| osu! 模式 | **4:3 出屏**：物件不超出 4:3 屏幕边界（offscreen） | 是 | 官方 osu（`OsuBeatmapVerifier` 内置 offscreen 检查） | 官方覆盖 |
| osu! 模式 | **SV 限制**：SV 取值/变化不极端（如 0.5–2.0 区间外需合理） | 部分 | ⏳ M2+ 自研增量（当前官方 Verify 未见专门 SV 检查，以锁定版源码为准） | 待实现（M2+） |
| osu! 模式 | **combo 颜色**：至少 2 个不同自定义颜色，除非强制默认皮肤 | 是 | 自研 M1 `CheckComboColourCount`（2026.730.0 起颜色移至谱面皮肤 `SkinConfiguration`，官方旧 `CheckComboColours` 已不可用） | **已实现** |

### 2.3 难度分级（difficulty levels 条款）

| RC 分类 | 条款/规则摘要 | 是否客观可算 | 实现位置 | 状态 |
|---|---|---|---|---|
| 难度分级 | **AR/OD/HP/CS 区间**：各难度等级 "Difficulty setting guidelines"（Easy–Expert+，如 Insane：AR 7–9.3、OD 7–9、HP 5–8、CS ≤7） | 是 | 自研 M1 `CheckDifficultySettingsRanges`（星数 → 等级经 `DifficultyRatingHelper`，区间表 `OsugameDifficultyRanges` 含 RC 原文引用）+ 官方通用 `CheckDifficultySettings`（基础合法性） | **已实现** |
| 难度分级 | **spinner 前后间隔**：Easy ≥4 拍 / Normal ≥2 拍 / Hard+ ≥1 拍（Insane/Expert 无条款，M1 规格沿用 1 拍） | 是（1 拍 = 最近 timing 点 BeatLength） | 自研 M1 `CheckSpinnerSpacing`（对前/后相邻物件都检查） | **已实现** |

---

## 3. 自研 M1 检查清单（已实现）

| 检查类 | RC 条款来源 | 判定与阈值 | IssueType | 测试 |
|---|---|---|---|---|
| `CheckDifficultySettingsRanges` | osu! RC 各难度 "Difficulty setting guidelines" | 星数 → 难度等级（阈值同官方 `StarDifficulty.GetDifficultyRating`）→ AR/OD/HP/CS 须落 `OsugameDifficultyRanges` 区间；星数计算失败跳过 | Problem | `OsugameDifficultyRangesTest`、`AiStudioBeatmapVerifierTest` |
| `CheckSpreadStarRatingGaps` | 通用 RC "the spread cannot skip any difficulty levels and there cannot be any drastically large difficulty gaps"；osu! RC drain time 最低难度条款 | 相邻难度星距 >2.0★ → Warning；按集合最大 drain time 缺最低难度 → Warning；集合 <2 难度不报 | Warning | `AiStudioBeatmapVerifierTest` |
| `CheckComboColourCount` | osu! RC "Each beatmap must use at least two different custom combo colours unless the default skin is forced" | 谱面皮肤自定义颜色去重后 <2 → Problem；无自定义颜色（强制默认皮肤）豁免；读不到皮肤跳过不误报 | Problem | `AiStudioBeatmapVerifierTest` |
| `CheckSpinnerSpacing` | osu! RC 各难度 spinner 间隔 Guidelines（Easy ≥4 拍 / Normal ≥2 拍 / Hard ≥1 拍） | 前后相邻物件间隔 < requiredBeats × BeatLength → Warning（含实际/所需拍数） | Warning | `AiStudioBeatmapVerifierTest` |

> 状态口径：以上 4 条均已实现并挂载于 `AiStudioBeatmapVerifier`（与官方 `OsuBeatmapVerifier` 聚合），requirements.md FR-1.1/FR-1.2 达成；FR-1.3（本矩阵）随本文档交付。

---

## 4. 生成质量门禁 ↔ 检查实现映射

> PLAN §3 五道门禁是"ranked 级质量"的操作化定义；`AiStudio.Core/Models/QualityGateReport.cs`（`AllPassed`）已就位，生成落盘唯一放行条件（PLAN §6-5）。

| 门禁（PLAN §3） | 判定标准 | 对应检查实现（现状） | 待实现（M2+） |
|---|---|---|---|
| ① 客观 RC 零错误 | Error/Problem 级 0 项 | 官方通用（snap/音频/背景/预览/drain/hitsound）+ 官方 osu（offscreen/spinner 时长/hitsound）+ 自研 M1 ×4（设置区间/spread/颜色/spinner 间隔） | 物件间隔/重叠、SV 限制、timing 冲突、音频严格规格（码率/采样率）等自研增量；生成器内建 RC 硬约束（PLAN §6.3） |
| ② 难度设置合规 | AR/OD/HP/CS 落在目标难度 RC 区间 | `CheckDifficultySettingsRanges`（**已实现**，判定侧） | 生成器按 `GenerationSettings.TargetLevel` 自动取 `OsugameDifficultyRanges` 区间（取值侧） |
| ③ 节奏-音乐对齐度 | 物件密度曲线与 onset/能量曲线相关性 ≥ 阈值；段落强度分级一致 | 无（需分析层输出） | M2 共享分析层（`IAudioAnalyzer`）落地后新增相关性检查；段落/密度分级作为合成器规划输入 |
| ④ 参数分布 P5–P95 | 间距均值/方差、stream 长度、滑条:圈比例、combo 长度落在 ranked 语料分布 | 无 | `tools/analysis` 语料采集与拟合（`corpus-refresh.yml` 已占位）+ 生成器分布采样约束 + 分布偏差检查 |
| ⑤ SR 校准 ±0.3★ | 星数与用户目标偏差 ≤ ±0.3★ | `OsuStarRating.TryCalculate`（**已实现**，M1 检查与校准共用同一星数入口） | M2 校准闭环：`OsuDifficultyCalculator.Calculate(mods)` 迭代调间距/密度至目标 SR |

> 主观条款（slider 路径清晰度、flow 合理性、hitsound 听感等）不硬编码为报错：作 checklist 提示与合成模板库设计原则（PLAN §3），不进入上表门禁判定。
