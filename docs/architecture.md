# 软件架构说明（Architecture）

> 版本：v1 · 2026-08-15 · 对应 `docs/PLAN.md` v3 · 状态：M0 完成、M1 进行中（4 检查已实现）、M2–M7 未开始
> 本文档是 PLAN.md 的架构落地视图，所有设计决策引用 PLAN.md 章节号；RC 覆盖情况见 `docs/rc-coverage.md`。

---

## 1. 总体架构

图例：✅ 已实现（M0/M1） · ⏳ 计划（M2+，接口/模型已就位则标注）

```
┌────────────────────────────────────────────────────────────────────────────────┐
│                      osu!lazer 编辑器（5 页签硬编码，不可扩展）                   │
│                                                                                  │
│  Compose 页 ──► AiStudioHitObjectComposer ──► 右工具箱 AiStudioToolboxGroup  ✅  │
│  Setup 页   ──► AiStudioSetupSection（音频上传分区）                     ✅ 占位 │
│  Verify 页  ──► AiStudioBeatmapVerifier（与官方内置检查并列展示）         ✅     │
└──────────────────────────────────────┬───────────────────────────────────────────┘
                                       │ 唯一插件机制：ruleset dll 扫描加载
                                       │ （dll 前缀 osu.Game.Rulesets.，放入 rulesets/ 目录）
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│        osu.Game.Rulesets.AiStudio.Osu.dll（自包含单 dll，rulesets/ 唯一部署物）   │
│                                                                                  │
│  AiStudioRuleset（继承 Ruleset 基类，规避 LegacyID 陷阱）             ✅ M0       │
│  ├─ 委托内部 OsuRuleset 实例：converter / processor / mods 列表                   │
│  ├─ 官方组件直通：OsuDifficultyCalculator / OsuPerformanceCalculator             │
│  └─ AiStudioAssistantMod（AIA，Fun 类，Ranked=false）                  ✅ M0       │
│                                                                                  │
│  Edit/       AiStudioHitObjectComposer ─ AiStudioToolboxGroup   ✅ M0 → M1/M2 填充│
│              AiStudioSetupSection（M2 起音频上传/一键生成）            ✅ 占位     │
│              AiStudioBeatmapVerifier = OsuBeatmapVerifier + 自研 ×4   ✅ M0/M1    │
│  Checks/     CheckDifficultySettingsRanges / CheckSpreadStarRatingGaps            │
│              CheckComboColourCount / CheckSpinnerSpacing（+OsuStarRating）✅ M1   │
│  Suggestions/ SuggestionEngine（Issue → 可执行建议）                   ✅ M1       │
└──────────────────────────────────────┬───────────────────────────────────────────┘
                                       │ <Compile Include> 源码编译并入
                                       │ （四模式程序集共用同一 Core 源码，无漂移）
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│              AiStudio.Core（共享核心，纯 .NET 8，零外部依赖）                      │
│                                                                                  │
│  Analysis/   IAudioAnalyzer（接口 + BeatGrid/AudioSection 模型）   ✅ 接口，⏳ M2│
│  Synthesis/  IMapGenerator（接口 + GenerationResult 模型）         ✅ 接口，⏳ M2+│
│  Models/     难度区间表 / 质量门禁报告 / 建议 / 生成设置            ✅             │
└──────────────────────────────────────┬───────────────────────────────────────────┘
                                       ▼
┌──────────────────────────────────────────────────────────────────────────────────┐
│                        AI 生成管线（全部 ⏳ M2+，数据流见 §4）                      │
│  音频 → BASS_FX 分析 → 段落规划 → 模式专属合成 → SR 校准                            │
│       → 五道质量门禁（QualityGateReport）→ 落盘 → 导出                             │
└──────────────────────────────────────────────────────────────────────────────────┘

├─ tools/analysis/      ⏳ M2 计划：ranked 语料采集 / 分布拟合 / 模型训练
│                       （离线 Python，不进运行链路；ai-tools.yml 已就位）
└─ .github/workflows/   ✅ M0 起：ci / release / api-compat / corpus-refresh（占位）/ ai-tools
```

要点（对应 PLAN §4.2 / §2.1）：

- **四模式 = 四个 ruleset 程序集**（加载器每程序集只认一个 public `Ruleset` 子类），当前仅 `AiStudio.Osu`（MVP）；Mania/Taiko/Catch 程序集随 M4–M6 加入，共享同一 Core 源码；
- **部署物只有自包含 dll**：Core 以源码编译并入各 ruleset 程序集，rulesets/ 目录无需额外依赖 dll；
- **产出物是普通 per-mode .osu 文件**：原版 osu!lazer/stable 可直接游玩，插件只是"制图工作室"载体（PLAN §1）。

---

## 2. 组件职责

### 2.1 组件职责表

| 组件 | 职责 | 关键实现细节 | 状态 |
|---|---|---|---|
| `AiStudioRuleset` | 插件唯一入口；override 全部 `Ruleset` 工厂方法，把官方 osu! 行为委托/直通给官方组件 | 继承 `Ruleset` 而非 `OsuRuleset`（ADR-001）；`ShortName="aistudio"`；`RulesetAPIVersionSupported=CURRENT_RULESET_API_VERSION`；`GetModsFor` 委托内部 `osuRuleset` 实例并追加 `AiStudioAssistantMod` | ✅ M0 |
| `AiStudioHitObjectComposer` | Compose 页注入（注入点 1） | 继承官方 `OsuHitObjectComposer` 保留全部 osu! 编辑能力；`RightToolbox.AddRange(AiStudioToolboxGroup)` | ✅ M0（面板占位）→ M1 检查摘要 / M2 生成入口 |
| `AiStudioToolboxGroup` | 编辑器右工具箱 "AI Studio" 面板 | `EditorToolboxGroup` 子类；当前占位文案 | ✅ M0 占位，待 M1/M2 填充 |
| `AiStudioSetupSection` | Setup 页追加 "AI Studio" 分区（注入点 2） | M2 起提供音频上传与一键生成入口 | ✅ M0 占位，待 M2 填充 |
| `AiStudioBeatmapVerifier` | Verify 页规则集校验器（注入点 3） | 聚合官方 `OsuBeatmapVerifier` + 自研 `Checks` ×4，与内置检查并列展示；官方已覆盖项（few hitsounds / offscreen / spinner 长度等）不重复实现 | ✅ M0/M1 |
| `Checks/`（×4 + `OsuStarRating`） | 自研增量检查（M1 交付物） | `CheckDifficultySettingsRanges` / `CheckSpreadStarRatingGaps` / `CheckComboColourCount` / `CheckSpinnerSpacing`；`OsuStarRating.TryCalculate` 为星数统一入口（失败返回 null，宁可少报不误报） | ✅ M1 已实现（详见 rc-coverage.md §3） |
| `SuggestionEngine` | 把 Verify 页 `Issue` 翻译为面向制图者的 `Suggestion` | 严重度映射：Problem/Error → Warning；Warning → Advice；Negligible → Info；与生成引擎形成闭环（PLAN §7） | ✅ M1（骨架） |
| `AiStudioAssistantMod` | 标识"经 AI Studio 辅助/生成"的谱面 | Fun 类 mod，`Ranked=false`，不改变玩法与计分；落实 NFR-3 | ✅ M0 |
| `AiStudio.Core` | 共享核心：分析 / 合成 / 模型 | `Analysis.IAudioAnalyzer`、`Synthesis.IMapGenerator` 接口已定义；`Models/` 提供难度区间表、质量门禁报告、建议、生成设置；零外部依赖 | 接口/模型 ✅，实现 ⏳ M2+ |
| `tools/analysis`（计划） | 离线 ranked 语料采集、参数分布拟合、模型训练 | Python，不进运行链路；由 `ai-tools.yml`（ruff+pytest）守护 | ⏳ M2 计划 |
| CI/CD 工作流 ×5 | 构建/测试/格式/发布/兼容探针/语料刷新 | `ci.yml`（双平台矩阵）、`release.yml`（tag 发 draft Release）、`api-compat.yml`（周级探针）、`corpus-refresh.yml`（月度占位）、`ai-tools.yml` | ✅ M0 起（corpus-refresh 为占位） |

### 2.2 组件分层原则

- **检查引擎双用途**（PLAN §7）：① Verify 页一次性深度检查 + Compose 实时侧栏（订阅 `EditorBeatmap` 事件，M1 侧栏待接）；② M2 起作为生成质量闸门（门禁 1）的实现；
- **只做客观可算条款**：主观条款（slider 路径清晰度、flow、hitsound 听感）不硬编码报错，作 checklist 提示与合成模板库设计原则（PLAN §3）；
- **可追溯**：每条检查注释引用 RC 条款编号，矩阵维护于 `docs/rc-coverage.md`（PLAN §7）。

---

## 3. 关键设计决策（ADR 风格）

| 编号 | 决策 | 说明（背景 → 选择 → 后果） | 依据 |
|---|---|---|---|
| ADR-001 | 继承 `Ruleset` 基类，而非 `OsuRuleset` | 官方四个模式 ruleset 均实现 `ILegacyRuleset`，其 `LegacyID`（0/1/2/3）为**非 virtual**；第三方继承会带着冲突的 LegacyID 走 legacy 注册分支，与内置 ruleset 撞车而被**静默跳过**——插件不出现且不报错。因此四个模式的插件一律继承 `Ruleset` 基类，经 NuGet 复用官方公开组件（converter/processor/difficulty/verifier/mods），这是社区主流做法（sentakki 同款）。 | PLAN §2.3-1 |
| ADR-002 | 共享核心源码编译并入（compile-include） | 各 ruleset 程序集通过 `<Compile Include="..\AiStudio.Core\**\*.cs">` 把 Core 源码编入自身，rulesets/ 目录只部署自包含 dll，规避加载器对"无 Ruleset 子类 dll"的处理风险；四份拷贝共享同一源码、无漂移。Core 自身零外部依赖，仅作为单元测试目标（`AiStudio.Core.csproj` 注释）。 | PLAN §4.2 |
| ADR-003 | NuGet 精确锁版 + api-compat 定时对抗 | ppy 按周发 NuGet（本仓锁定 `2026.730.0`），依赖漂移会造成构建断裂或静默行为变化；csproj 精确锁版保证可复现构建，`api-compat.yml` 每周用最新 ppy 包编译探针，失败自动开 Issue 驱动升级决策，`RulesetAPIVersionSupported` 跟随升级。 | PLAN §10、§11-1 |
| ADR-004 | 复用官方 verifier + 自研增量检查 | 官方 `OsuBeatmapVerifier`（MIT）已覆盖 offscreen、spinner 长度、few hitsounds 等，直接聚合复用不重复造轮子；官方通用检查列表为 private 不可追加，故以 ruleset verifier 身份**并行**运行自研增量（spread 星距、参数区间、combo 颜色、spinner 间隔）。MapsetVerifier（GPL-3.0）仅借鉴检查思路与测试数据，全部重写、不链代码。 | PLAN §2.2、§7、§11-5 |
| ADR-005 | 编辑器 5 页签限制下，功能收纳进三个注入点 | 官方 Editor 硬编码 5 页签（SongSetup/Compose/Design/Timing/Verify），不可新增页签或替换 Editor 屏幕；全部功能收纳进 Compose 右工具箱面板、Setup 分区（音频上传）、Verify 检查（合规）三处，三注入点已在 M0 打通并列入 M0 验收。 | PLAN §2.2 |
| ADR-006 | 不以 ranked 为目标，但以五道质量门禁为硬标准 | RC AI policy（osu-wiki PR #15087，2026-08-13 合并）规定 ranked 谱面必须 100% 人工输入、禁止生成式 AI 痕迹，故本项目不冲 rank；但生成与辅助产出的质量基准与 ranked 一致——PLAN §3 五道门禁全绿才是"完成"的硬定义，产物默认标注 "AI generated"、`AiStudioAssistantMod.Ranked=false`，宣传口径"质量对齐 ranked，但不用于 ranked 投稿"。 | PLAN §1、§3、§11-4 |
| ADR-007 | 四模式共享分析层、每模式专属合成器 | osu!/mania/taiko/catch 各有独立的生成方式与模型（2D 摆放、音符矩阵+人体工学约束、don-kat 序列、std 派生+移动可行性），但共享模式无关的分析层（BPM/beat/onset/能量/段落）；每模式一个 ruleset 程序集 + 独立检查集与语料分布，复用同一 Core 源码，CI 生成回归防退步。 | PLAN §4、§5、§11-7 |

---

## 4. 生成管线数据流（⏳ M2+ 计划）

> 接口与模型已就位（`IAudioAnalyzer` / `IMapGenerator` / `GenerationSettings` / `QualityGateReport`），实现随 M2 落地。

```
[1] 音频上传        Setup 分区（AiStudioSetupSection）── M2 起接入
      │
[2] 共享分析层      BASS_FX 离线解码 → BPM/beat/onset/downbeat/能量/频谱/段落
      │             （IAudioAnalyzer；M7 可选 ONNX BeatNet 增强，四模式共享）
[3] 段落规划        强度分级 → kiai / SV / break / 密度预算（§5.2 模板）
      │
[4] 模式专属合成器   IMapGenerator：std 2D 摆放 / mania 音符矩阵 / taiko don-kat /
      │             catch std 派生+移动可行性（RC 客观规则内建于求解器，非事后修补）
[5] 校准闭环        OsuDifficultyCalculator.Calculate(mods) 迭代 → 目标 SR ±0.3★
      │             （同步 API + 官方 10s 超时，后台线程执行不卡 UI）
[6] 质量闸门        五道门禁全绿（QualityGateReport.AllPassed），复用检查引擎；
      │             任一未过 → 携带诊断返回重试/降级
[7] 落盘            Beatmap<T> 注入 EditorBeatmap，供人工预览修改
      │
[8] 导出            LegacyBeatmapEncoder / BeatmapManager.ExportLegacy
                    → 标准 per-mode .osu/.osz，Tags 写入 "AI generated"
```

对应 PLAN §4.3 数据流总览与 §6 通用流水线；门禁定义见 PLAN §3，门禁与检查实现的映射见 `docs/rc-coverage.md` §4。

---

## 5. 扩展点速查表

### 5.1 Ruleset 扩展点（virtual 成员 → 我们的实现 → 官方源码路径）

| Ruleset virtual 成员 | 我们的实现 | 官方源码路径（ppy/osu master） |
|---|---|---|
| `CreateDrawableRulesetWith` | `DrawableOsuRuleset` 直通（保持官方绘制/输入） | `osu.Game.Rulesets.Osu/UI/DrawableOsuRuleset.cs` |
| `CreateBeatmapConverter` | `OsuBeatmapConverter` 直通（`CanConvert` 通过，互转无障碍） | `osu.Game.Rulesets.Osu/Beatmaps/OsuBeatmapConverter.cs` |
| `CreateBeatmapProcessor` | `OsuBeatmapProcessor` 直通 | `osu.Game.Rulesets.Osu/Beatmaps/OsuBeatmapProcessor.cs` |
| `CreateDifficultyCalculator` | `OsuDifficultyCalculator` 直通（检查引擎与 M2 校准闭环共用） | `osu.Game.Rulesets.Osu/Difficulty/OsuDifficultyCalculator.cs` |
| `CreatePerformanceCalculator` | `OsuPerformanceCalculator` 直通 | `osu.Game.Rulesets.Osu/Difficulty/OsuPerformanceCalculator.cs` |
| `CreateHitObjectComposer` | `AiStudioHitObjectComposer`（注入点 1） | `osu.Game.Rulesets.Osu/Edit/OsuHitObjectComposer.cs`、`osu.Game/Rulesets/Edit/HitObjectComposer.cs` |
| `CreateBeatmapVerifier` | `AiStudioBeatmapVerifier`（注入点 3） | `osu.Game.Rulesets.Osu/Edit/OsuBeatmapVerifier.cs`、`osu.Game/Rulesets/Edit/`（IBeatmapVerifier/ICheck/Issue） |
| `CreateEditorSetupSections` | `base` + `AiStudioSetupSection`（注入点 2） | `osu.Game/Rulesets/Ruleset.cs` |
| `GetModsFor` | 委托 `osuRuleset` 列表 + 追加 `AiStudioAssistantMod` | `osu.Game/Rulesets/Mods/Mod.cs` |
| `ShortName` / `Description` / `RulesetAPIVersionSupported` | `"aistudio"` / `"AI Studio (osu!)"` / `CURRENT_RULESET_API_VERSION` | `osu.Game/Rulesets/Ruleset.cs`、`RulesetStore.cs` |

### 5.2 编辑器/框架扩展点

| 能力 | 官方 API | 官方源码路径 | 用途 |
|---|---|---|---|
| 实时谱面事件 | `EditorBeatmap`：HitObjectAdded/Updated/Removed/BeatmapReprocessed | `osu.Game/Screens/Edit/EditorBeatmap.cs` | M1 实时检查侧栏（待接入） |
| 工具箱面板 | `EditorToolboxGroup.AttachToToolbox` | `osu.Game/Rulesets/Edit/EditorToolboxGroup.cs` | `AiStudioToolboxGroup` 右工具箱 |
| 校验框架 | `ICheck` / `IssueTemplate` / `CheckCategory` | `osu.Game/Rulesets/Edit/Checks/Components/` | 自研 4 检查 |
| 谱面皮肤颜色 | `SkinConfiguration`（`IHasComboColours`）经 `IWorkingBeatmap.Skin` | `osu.Game/Skinning/` | `CheckComboColourCount`（2026.730.0 起颜色不在 BeatmapInfo） |
| 音频 DSP | ppy.ManagedBass / `.Fx`（BASS_FX BPM/beat、FFT） | 依赖 osu.Framework | M2 分析层（零新增原生依赖） |
| 导入导出 | `LegacyBeatmapEncoder` / `BeatmapManager.ExportLegacy` | `osu.Game/Beatmaps/Formats/`、`osu.Game/Database/` | M3 导出 .osz |
| 数据库 | `RealmAccess`（Realm，仅主线程） | `osu.Game/Database/RealmAccess.cs` | 自定义数据持久化（后续） |

---

## 6. 目录结构与文件对照

```
OSU-mapping-addon/
├── docs/
│   ├── PLAN.md                        # 核心计划（v3）：架构决策/门禁/里程碑/风险
│   ├── requirements.md                # 需求：用户故事 + 功能/非功能需求（FR/NFR）
│   ├── architecture.md                # 本文档：架构落地视图
│   └── rc-coverage.md                 # RC 覆盖矩阵：条款 → 检查实现（PLAN §7）
├── src/
│   ├── AiStudio.Core/                 # 共享核心（源码编译并入各 ruleset，零外部依赖）
│   │   ├── Analysis/IAudioAnalyzer.cs #   分析接口 + BeatGrid/AudioSection（实现 ⏳ M2）
│   │   ├── Synthesis/IMapGenerator.cs #   生成接口 + GenerationResult（实现 ⏳ M2+）
│   │   ├── Models/                    #   DifficultyLevel / DifficultyRatingHelper /
│   │   │                              #   OsugameDifficultyRanges / DifficultySettingsRange /
│   │   │                              #   QualityGateReport / GenerationSettings / Suggestion
│   │   └── AiStudio.Core.csproj       #   仅作测试目标（Core.Tests），IsPackable=false
│   └── osu.Game.Rulesets.AiStudio.Osu/  # osu! 标准模式插件（MVP，M1 进行中）
│       ├── AiStudioRuleset.cs         #   插件入口（继承 Ruleset，委托官方组件）
│       ├── AiStudioAssistantMod.cs    #   AIA 标记 mod（Fun，Ranked=false）
│       ├── Checks/                    #   M1 自研检查 ×4 + OsuStarRating（星数统一入口）
│       ├── Edit/                      #   Composer / ToolboxGroup / SetupSection / Verifier
│       ├── Suggestions/SuggestionEngine.cs  # Issue → 可执行建议
│       └── osu.Game.Rulesets.AiStudio.Osu.csproj  # 锁版 NuGet + Compile Include 并入 Core
├── tests/osu.Game.Rulesets.AiStudio.Osu.Tests/   # NUnit：verifier / ruleset / 区间表 /
│                                                 # SuggestionEngine / TestWorkingBeatmap
├── tools/analysis/                    # ⏳ M2 计划：ranked 语料采集、分布拟合、模型训练（Python，不进运行链路）
├── .github/workflows/
│   ├── ci.yml                         # 双平台矩阵：restore/build/format/test + dll artifact
│   ├── release.yml                    # tag v* → 打包 zip + draft GitHub Release
│   ├── api-compat.yml                 # 周级探针：最新 ppy 包试编译，失败自动开 Issue
│   ├── corpus-refresh.yml             # 月度语料刷新（M2 占位工作流）
│   └── ai-tools.yml                   # tools/analysis 变更时 ruff + pytest
├── Directory.Build.props / global.json / .editorconfig
└── README.md                          # 安装/开发/里程碑状态
```

**文件对照**（关注点 → 文件）：

| 关注点 | 文件 |
|---|---|
| 插件入口与工厂 | `src/osu.Game.Rulesets.AiStudio.Osu/AiStudioRuleset.cs` |
| 三注入点 | `Edit/AiStudioHitObjectComposer.cs`（Compose）、`Edit/AiStudioSetupSection.cs`（Setup）、`Edit/AiStudioBeatmapVerifier.cs`（Verify） |
| M1 检查实现 | `Checks/`（4 检查 + `OsuStarRating.cs`） |
| 建议系统 | `Suggestions/SuggestionEngine.cs` |
| 难度区间表 / 门禁报告 | `AiStudio.Core/Models/OsugameDifficultyRanges.cs`、`QualityGateReport.cs` |
| 生成/分析接口 | `AiStudio.Core/Analysis/IAudioAnalyzer.cs`、`Synthesis/IMapGenerator.cs` |
| 版本锁定 | `src/osu.Game.Rulesets.AiStudio.Osu/osu.Game.Rulesets.AiStudio.Osu.csproj`（2026.730.0） |
| 质量门禁定义 | `docs/PLAN.md` §3；实现映射见 `docs/rc-coverage.md` §4 |
