# osu!lazer AI 制图辅助插件 — 项目计划（PLAN）

> 版本：v4 · 2026-08-21 · 状态：M0–M6 已交付（M3 v2 多段/Spread/.osz/IDistributionProvider + M4/M5/M6 四模式独立插件） · 工作目录：feat/m3-m6-ai-studio-complete
> 工作目录：`F:\zcode-harness\OSU-mapping-addon`
> v3 变更：① CI/CD 全面适配 GitHub；② 生成与辅助产出的谱面统一以"ranked 级质量"为基准（不冲 rank，质量对齐）；③ 四模式（osu!/mania/taiko/catch）各有独立生成方式/模型，共享分析层 + 每模式专属合成器。

---

## 1. 项目目标与定位

在**不改动 osu!lazer 任何核心代码**的前提下，以官方 ruleset 插件机制交付一个插件：

- **A. 音频 → 谱面生成**：玩家上传音频，自动生成可游玩的谱面；
- **B. 制图辅助**：玩家手动制图时，实时给出建议与规划，使谱面贴近 ranked 标准。

**质量定位（用户明确要求）**：官方 Ranking Criteria 的 AI policy 规定 ranked 谱面必须 100% 人工输入、禁止生成式 AI 痕迹，因此本项目**不以 ranked 为目标**；但**生成谱面与辅助产出的谱面，质量基准与 ranked 谱面保持一致**——即"ranked 级质量"（§3 给出可量化定义）。产物默认标注 "AI generated"，不做"可 rank"承诺。

**模式范围（用户明确要求）**：osu! 四个模式（osu! 标准 / mania / taiko / catch）**各需不同的谱面生成方式/模型**。本计划设计"共享分析层 + 每模式专属生成策略"的统一架构（§4/§5），MVP 从 osu! 标准起步，逐模式交付（§8 里程碑）。

**产出物形态**：插件生成/编辑的是**普通的标准 per-mode .osu 文件**（Mode=0/1/2/3），任何人在原版 osu!lazer/stable 中即可游玩，游玩不依赖本插件；插件只是"制图工作室"载体。

**既定技术决策**（可评审推翻）：
1. AI 管线 = C# 进程内 BASS 解码 + spectral-flux/块能量分析 + 规则/模板合成（零新增原生依赖、离线可用；ONNX 神经网络增强为后续选项；BASS_FX BPMDecodeGet 已验证不可靠并弃用，详见 §5.1 / `BassAudioAnalyzer`）；

2. **CI/CD 全面适配 GitHub**：托管 GitHub，GitHub Actions 构建/测试/发布，GitHub Releases 分发；
3. 开发顺序 = ranked 检查引擎先行（它是生成器的质量闸门，同时先验证编辑器注入点是否畅通）。

---

## 2. 可行性核心结论（基于 ppy/osu master 源码逐文件核实）

### 2.1 官方插件机制

- 自定义 ruleset 是 osu!lazer 唯一官方插件机制：dll 命名 `osu.Game.Rulesets.*.dll`，放入用户数据目录 `rulesets/` 即被 `RulesetStore` 扫描加载（游戏内可拖拽 dll 安装）；
- 每个程序集只识别**一个** public `Ruleset` 子类；`RulesetAPIVersionSupported` 必须等于 `Ruleset.CURRENT_RULESET_API_VERSION`（= `"2022.822.0"`），否则启动一致性检查将其标记不可用；启动时还会以空 Beatmap 试跑 converter/processor/mods，崩溃则 dll 被改名 `.dll.broken` 禁用。

### 2.2 能做到 / 不能做到

| 能力 | 结论 | 依据 |
|---|---|---|
| 编辑器注入作曲工具/面板 | ✅ | `Ruleset.CreateHitObjectComposer()` → `HitObjectComposer<T>` 的 `CompositionTools`、`LeftToolbox`/`RightToolbox`（`EditorToolboxGroup.AttachToToolbox`）、`LayerBelowRuleset` 覆盖层 |
| Setup 页追加分区（音频上传入口） | ✅ | `Ruleset.CreateEditorSetupSections()` |
| Verify 页追加自定义检查 | ✅ | `Ruleset.CreateBeatmapVerifier()` 返回 `IBeatmapVerifier`，与内置检查并列展示 |
| 实时谱面事件订阅 | ✅ | `EditorBeatmap` 的 public 事件（HitObjectAdded/Updated/Removed/BeatmapReprocessed） |
| 调用官方难度/成绩计算 | ✅ | `new OsuDifficultyCalculator(RulesetInfo, workingBeatmap).Calculate(mods)`（public ctor、同步、内置 10s 超时、无内部缓存） |
| 程序化导入/导出谱面 | ✅ | `BeatmapManager.Import/ExportLegacy`、`LegacyBeatmapEncoder`、`LegacyBeatmapExporter`（.osz） |
| 注册自定义 mod | ✅ | override `GetModsFor`（自定义 mod 默认 `Ranked=false`） |
| 音频解码与 DSP | ✅ | osu.Framework 自带 ppy.ManagedBass / .Fx（BASS 解码 + FFT；BASS_FX BPM/beat 已验证不可靠并弃用，改用 spectral-flux + 块能量精化，见 `BassAudioAnalyzer`），插件零新增原生依赖 |
| 新增编辑器页签/替换 Editor 屏幕 | ❌ | Editor 硬编码 5 页签（SongSetup/Compose/Design/Timing/Verify），功能全部收纳进 Compose 面板 + Setup 分区 + Verify 检查 |
| 追加官方通用 BeatmapVerifier 检查 | ❌ | 其 checks 列表为 private，只能以 ruleset verifier 身份并行运行 |
| 自定义 ruleset 参与官方 ranked | ❌ | 服务端只认 4 个 legacy ruleset（与本项目"不冲 rank"定位一致） |

### 2.3 关键陷阱与规避

1. **LegacyID 静默不注册陷阱（最关键）**：官方四个 ruleset（OsuRuleset/TaikoRuleset/CatchRuleset/ManiaRuleset）都实现 `ILegacyRuleset`，其 `LegacyID`（0/1/2/3）是**非 virtual** 的。第三方继承它们会带着冲突的 LegacyID 走 legacy 注册分支，与内置 ruleset 撞车而被静默跳过——**插件永远不出现在游戏里且不报错**。→ **规避：四个模式的插件一律继承 `Ruleset` 基类**，通过 NuGet `ppy.osu.Game` + `ppy.osu.Game.Rulesets.Osu/.Taiko/.Catch/.Mania` 复用官方公开组件（converter/processor/difficulty/performance/verifier/mods），这是社区主流做法（sentakki 同模式）。
2. **自定义 composer 只在谱面 Ruleset 解析到本插件时出现**：用户需在选歌界面把谱面切到本插件规则集（osu 谱面对象均带 `IHasPosition`，`OsuBeatmapConverter.CanConvert` 通过，互转无障碍）；编辑器打开期间 `DisallowExternalBeatmapRulesetChanges=true` 禁止中途切换。
3. **编辑器无 mod UI**：编辑器不使用 mod（`ApplyModTrackAdjustments=false`），自研 AI 辅助 mod 仅供玩法侧/难度计算使用。
4. **API 漂移**：ppy 按周发 NuGet（如 2026.730.0），需精确锁版（sentakki 实践）并用 api-compat 定时任务对抗（§10）。
5. **数据库是 Realm**（非 EF Core），自定义数据经 `RealmAccess.Run/Write` 访问且只在主线程使用。

---

## 3. "ranked 级质量"的可量化门禁

**生成谱面与辅助产出谱面共用同一套闸门**——这是本项目对"与 ranked 谱面同等质量"的操作化定义，每项均有验收阈值：

1. **客观 RC 零错误**：通过全部可程序化的 Ranking Criteria 条款（通用 + 对应模式 + 难度分级）：物件时间间隔/重叠、snap 偏差（<2ms）、drain time、难度集 spread 星距、AR/OD/HP/CS 合法区间、spinner 时长与前后间隔、4:3 出屏、SV 限制、timing 点冲突、音频规格（码率/采样率）、背景与文件规格等。Error/Problem 级 0 项；
2. **难度设置合规**：AR/OD/HP/CS 自动落在目标难度对应的 RC 区间（如 Insane：AR 7–9.3、OD 7–9、HP 5–8）；
3. **节奏-音乐对齐度**：物件密度曲线与 onset/能量曲线的相关性 ≥ 阈值；段落强度分级（intro/verse/chorus）与间距/密度分级一致；
4. **参数分布落在 ranked 语料范围内**：离线采集一批真实 ranked 谱面（osu! API，仅统计参数不随包分发），按 BPM 分箱拟合间距均值/方差、stream 长度、滑条:圈比例、combo 长度等分布；生成谱面的对应参数必须落在 **P5–P95** 区间（防"机器人感"与极端值）；
5. **SR 校准**：星数与用户目标偏差 ≤ ±0.3★（官方难度计算器反馈闭环）。

**主观条款处理**：RC 中不可量化项（slider 路径清晰度、flow 合理性、hitsound 听感等）不硬编码为报错，作为 checklist 提示与合成模板库的设计原则（§6）。

---

## 4. 总体架构（多模式）

### 4.1 仓库结构（monorepo）

```
OSU-mapping-addon/
├── src/
│   ├── AiStudio.Core/                      # 共享核心：分析/合成基架/校准/检查/模型/工具（源码编译并入各 ruleset 程序集）
│   ├── osu.Game.Rulesets.AiStudio.Osu/     # osu! 标准模式插件（MVP）
│   ├── osu.Game.Rulesets.AiStudio.Mania/   # mania 插件（M4）
│   ├── osu.Game.Rulesets.AiStudio.Taiko/   # taiko 插件（M6）
│   └── osu.Game.Rulesets.AiStudio.Catch/   # catch 插件（M5）
├── tests/
│   ├── AiStudio.Core.Tests/
│   ├── osu.Game.Rulesets.AiStudio.Osu.Tests/   # （其余模式同构）
├── tools/analysis/                         # Python 离线：ranked 语料采集、参数分布拟合、模型训练（不进运行链路）
├── docs/                                   # requirements.md / architecture.md / rc-coverage.md / quality-gates.md
├── .github/                                # workflows / ISSUE_TEMPLATE / CODEOWNERS / dependabot.yml
└── Directory.Build.props, .editorconfig, global.json
```

### 4.2 多程序集策略与共享核心

- 四模式 = **四个 ruleset 程序集**（加载器每程序集只认一个 Ruleset 子类；编辑器内容按谱面所属 ruleset 解析）；
- `AiStudio.Core` 以**共享源码编译并入**方式进入四个程序集（csproj `<Compile Include="..\AiStudio.Core\**\*.cs" LinkBase="AiStudio.Core" />`），rulesets 目录下只有 4 个自包含 dll——规避加载器对"无 Ruleset 子类 dll"的处理风险，同时四份拷贝共享同一源码、无漂移；
- 各模式 ruleset 复用对应官方公开组件（converter/processor/difficulty/performance/verifier/mods），NuGet 精确锁版（`ppy.osu.Game` + `ppy.osu.Game.Rulesets.*`）；
- 每个插件内置：AI 面板（上传音频/预设/一键生成）、实时建议侧栏、Verify 检查集、生成管线——四模式 UI 骨架统一，仅合成器与检查集不同。

### 4.3 数据流总览

```
音频文件
  → [共享分析层] 解码/BPM/beat/onset/downbeat/能量/频谱/段落（模式无关）
  → [段落规划] 强度分级 → kiai/SV/break/密度预算
  → [模式专属合成器] std: 2D 摆放 / mania: 音符矩阵 / taiko: don-kat 序列 / catch: std 派生+可行性
  → [校准闭环] 官方难度计算器迭代 → 目标 SR ±0.3★
  → [质量闸门] §3 五道门禁全绿（复用 §7 检查引擎）
  → [落盘] Beatmap<T> 注入 EditorBeatmap 供人工预览修改
  → [导出] LegacyBeatmapEncoder / BeatmapManager.ExportLegacy → 普通 per-mode .osu/.osz + AI 标注
```

---

## 5. 四模式生成方案（不同的方式/模型）

### 5.1 共享分析层（模式无关）

- 解码：ppy.ManagedBass `BassFlags.Decode` 离线解码（osu.Framework 自带，零新增原生依赖、跨平台）；
- 节拍：曾尝试 `BASS_FX_BPM_DecodeGet` / `BASS_FX_BPM_BeatDecodeGet`，实测 `BPMDecodeGet` 对合成点击轨返回 0 且无错误码、不可靠，已**弃用该路径**；当前实现（`BassAudioAnalyzer`）改用 `Bass.ChannelGetData` FFT 的 **spectral-flux + 64 采样块能量精化（≈1.45ms 分辨率）** 自研节拍/onset 检测（阈值+最小间隔 pick、IOI 中位数求 BPM，回退自相关），加能量包络/RMS/频带能量；BASS_FX 仍作为曾尝试路径保留于文档以记录决策；
- 段落：能量曲线 + 重复度估计切分 intro/verse/chorus/bridge/outro；
- **增强选项（M7）**：ONNX Runtime（Microsoft.ML.OnnxRuntime，MIT）进程内跑 BeatNet CRNN（CC-BY-4.0，PyTorch 训练后 `torch.onnx` 导出）提升 beat/downbeat 精度，四个模式共享收益。

### 5.2 osu! 标准（MVP，M1–M3）

2D 摆放合成：节奏模板库（jump 间距、stream 密度、slider/spinner 模板）+ 间距/密度/滑条比按 ranked 语料分布（BPM 分箱）采样 + 官方难度计算器 SR 校准闭环 + RC 客观规则内建为硬约束。这是四模式中最成熟、参考最多（MapsetVerifier 检查、社区模板库）的一条管线。

### 5.3 mania（M4）

音符矩阵合成：onset 网格 → 键位分配（keycount 4K/7K 参数化），**人体工学约束求解**（手型切换、锚点、jack 上限、chord 密度、ABA 过滤），列密度曲线随段落强度变化，按 keycount 校准难度。几何上最简单、最独立，故排在 std 之后先行（开源先例：ManiaMapper、osu-mania-beatmap-generator）。

### 5.4 taiko（M6）

don/kat 序列合成：频谱带能量区分低频鼓点（don）与高频边击（kat）→ 节奏模式模板（mono stream、ddk/kkd 组合）→ BPM 驱动滚动速度与密度；**可选小型 ONNX 分类器**（训练数据与模型产出放 tools/analysis，Python 离线训练）提升鼓点分类精度。因需要全新的打击乐声源分析能力，排位靠后。

### 5.5 catch（M5）

由标准模式合成输出**转换派生**：std 物件 → fruit/stream 映射 + **移动可行性约束求解**（像素位移可达性、edge/hyperdash 控制、屏幕边界）。复用 std 管线，新增约束层即可，故排在 mania 之后。

---

## 6. 生成管线（通用流水线）

1. **分析**（§5.1，后台 Task 异步执行，防卡 UI）；
2. **分段规划**：段落 → kiai、SV 变化、break 位置、密度预算、滑条:圈比例；
3. **约束内合成**：RC 客观规则（无重叠、snap、设置区间、spinner 前后间隔等）**内建于摆放求解器**，而非事后修补；参数从 ranked 语料分布采样，保证"像人做的图"；
4. **校准闭环**：`DifficultyCalculator.Calculate(mods)` 迭代调间距/密度至目标 SR ±0.3★（同步 API，后台线程调用，复用官方内置 10s 超时）；
5. **质量闸门**：§3 五道门禁全绿才算生成成功，任一未过 → 携带诊断信息返回重试/降级；
6. **落盘**：构造 timing 点（红/绿线、kiai）、break、hitsound → 注入 `EditorBeatmap` 供人工预览修改；
7. **导出**：经官方 `LegacyBeatmapEncoder`/`BeatmapManager.ExportLegacy` 导出标准 .osu/.osz，Tags 写入 "AI generated" 标注。

---

## 7. Ranked 合规引擎（每模式检查集 + 建议系统）

- **双用途**：① 人工制图辅助（Verify 页一次性深度检查 + Compose 实时侧栏订阅 EditorBeatmap 事件）；② 生成质量闸门（§3 门禁 1 的实现）。
- **分层**：只做客观可算条款自动报错；主观条款做 checklist 提示。
- **复用**：官方已内置约 24 条通用检查 + 各模式各自若干条（osu 9 条等，MIT 框架），自研 verifier **聚合官方结果 + 增量规则**（spread、AR/OD 区间、语料分布偏差、生成物质量等）；每模式独立检查集（RC 有 per-mode 条款）。参考 MapsetVerifier 检查思路与测试数据，但**全部重写**（其 GPL-3.0 不可链代码）。
- **可追溯**：每条检查注释引用 RC 条款编号，`docs/rc-coverage.md` 维护"条款 → 检查实现"覆盖矩阵。
- **建议系统**：检查结果翻译为可执行建议（如"难度梯度缺口：drain 3:20 需 Normal~Insane 梯度，建议一键生成 4.2★ Insane 草稿"），与生成引擎形成闭环；建议目标始终是**ranked 级质量**（§3 门禁）。

---

## 8. SDLC 全流程与里程碑

需求→设计→实现→测试→部署映射到 GitHub 工程化：`docs/requirements.md`（用户故事+验收标准）、Issue 模板（feature/bug/RC 条款）、architecture.md 设计评审、主干+功能分支、conventional commits、semver（tag v*）、PR 模板（改动说明/测试/截图）。

### 里程碑（含验收标准）

| 里程碑 | 周期（单人全职参考） | 内容 | 验收标准 |
|---|---|---|---|
| **M0 脚手架** | 1 周 | git init + 骨架 + NuGet 锁版 + AiStudio.Osu ruleset 最小实现（converter/difficulty/composer 空面板） | 真实游戏内冒烟：装 dll → 选歌切规则集 → 编辑器三个注入点可见 |
| **M1 检查引擎 v1（osu）** | 3–4 周 | 15–20 条客观检查 + Verify 页 + 实时侧栏 + rc-coverage 初版 | 已知问题谱面 fixtures 全部命中，无漏报 |
| **M2 std 生成 v1** | 5–7 周 | 语料采集与参数拟合（tools/analysis）+ 音频分析 + Hard 预设单难度生成 + SR 校准 + 编辑器预览 | 10 首测试曲生成成功率 >95%；产出通过 §3 全部门禁（0 error、设置合规、节奏相关性达标、参数在语料 P5–P95、SR ±0.3★） |
| **M3 std 生成 v2** | 4–6 周 | 多难度预设（难度集 spread 规划）、段落/kiai/break、滑条质量、建议系统、.osz 导出 | 难度集 spread 星距满足 RC 梯度；导出文件在原版 osu! 可正常游玩 |
| **M4 mania** | 4–6 周 | 矩阵合成 + 人体工学约束 + mania 检查集 + 语料分布 | 同 M2 门禁（mania 版） |
| **M5 catch** | 3–4 周 | std 派生转换 + 移动可行性约束 + catch 检查集 | 同 M2 门禁（catch 版） |
| **M6 taiko** | 5–7 周 | 鼓点分类（频谱特征，可选 ONNX 分类器）+ 模式模板 + taiko 检查集 | 同 M2 门禁（taiko 版） |
| **M7 打磨（可选）** | 视情况 | ONNX BeatNet 节拍增强（四模式共享）、性能优化、多语言、用户文档 | 回归全绿 + 性能基准 |

**质量闸门是"完成"的硬定义**：门禁不达绿即持续迭代，不以"能跑"为交付标准。

---

## 9. 测试策略

- **单元测试**：
  - 分析器：合成音频信号 golden 测试（已知 BPM 的 click track 误差 ≤±0.5 BPM；onset 位置命中率）；
  - 合成器：输出不变式（无重叠、snap 正确、参数在语料分布内、RC 设置区间内）；
  - 检查引擎：每条规则正/反用例（fixtures 谱面）；
  - .osu 编解码 roundtrip；导出文件格式合法性；
- **集成测试**：难度计算器一致性（同谱面 SR 与游戏内一致）、EditorBeatmap 注入、verifier 聚合（官方+自研）；
- **生成回归测试（CI 可重复）**：对抽样测试曲跑完整生成，自动跑 §3 门禁（限定 N 首控制 CI 时长）；
- **UI/冒烟测试**：osu.Framework headless（`OSU_EXECUTION_MODE` 环境变量 + VisualTestRunner，参考 tau/sentakki），覆盖 composer 面板加载、事件订阅、建议侧栏渲染；
- **手工验收清单**：安装 → 选歌 → 编辑器 → 生成 → 试玩全流程（每里程碑必做）；
- **质量门**：覆盖率 ≥70%（coverlet）、`dotnet format --verify-no-changes`、warnings-as-errors。

---

## 10. CI/CD（GitHub 适配）

整个工程托管 **GitHub**，CI/CD 全部基于 **GitHub Actions**，分发走 **GitHub Releases**：

| 工作流 | 触发 | 内容 |
|---|---|---|
| **ci.yml** | push / PR | 矩阵 ubuntu+windows；`actions/setup-dotnet@v4`（8.0.x，对齐 osu global.json 8.0.100）+ NuGet 缓存；restore（锁版）→ build 4 个 ruleset 项目（Release）→ `dotnet format --verify-no-changes` → `dotnet test --logger trx`（headless，`OSU_EXECUTION_MODE`）→ coverlet + Codecov 报告 → 上传 4 个 ruleset dll 为 artifacts |
| **release.yml** | tag `v*` 推送（semver） | Release 构建（`-p:version=$tag`）→ 打包 zip（每模式 dll + 安装说明：放入 lazer `rulesets/` 目录）→ `softprops/action-gh-release` 创建 **draft GitHub Release**（人工复核后发布），changelog 自动生成 |
| **api-compat.yml** | schedule（每周）+ workflow_dispatch | 用最新版 `ppy.osu.Game`/`ppy.osu.Game.Rulesets.*` NuGet 试编译，失败自动开 GitHub Issue 提醒跟进上游破坏性变更（对抗 ppy 周级发版） |
| **corpus-refresh.yml** | schedule（月度）+ 手动 | 重新采集 ranked 语料刷新参数表，自动开 PR 供人工复核合并 |
| **ai-tools.yml** | tools/analysis 变更 | Python 侧 ruff + pytest |

**GitHub 工程化配套**：
- Dependabot（NuGet 自动升级 PR）；
- 分支保护规则：main 需 PR + CI 全绿方可合并；
- PR / Issue 模板（.github/）；CODEOWNERS 指定核心目录评审人；
- CodeQL 安全扫描（可选，周级）；
- 文档托管：README + docs/（GitHub Pages 可选）。

---

## 11. 风险与对策

1. **上游 API 漂移**（ppy 周级发版）→ 精确锁版 + api-compat 定时任务 + `RulesetAPIVersionSupported` 跟随升级；
2. **规则集静默不注册** → 规避 LegacyID 陷阱（四模式一律继承 `Ruleset` 基类），M0 验收必须含"游戏内可见"；
3. **生成质量达不到 ranked 级** → 语料分布采样 + RC 硬约束合成 + SR 校准把下限钉在 §3 门禁上；门禁全绿是"完成"的硬定义，不达绿即迭代；上限靠模板库迭代与 M7 ONNX 增强；
4. **合规风险**（RC AI policy，2026-08-13 合并）→ 不冲 rank、产物标注 AI、检查引擎只做建议不做裁判；宣传口径明确"质量对齐 ranked，但不用于 ranked 投稿"；
5. **许可** → MapsetVerifier GPL-3.0 只借鉴思路不链代码；lazer ICheck 框架 MIT 可放心实现；BASS 个人使用免费、**商业化需购买授权**（如商业化需预算）；madmom 权重非商用 → 不进运行链路；BeatNet CC-BY-4.0 可用于导出 ONNX；
6. **性能** → 生成/难度计算全部后台 Task + 缓存；难度计算复用官方 10s 超时；Realm 对象只在主线程访问；
7. **多模式维护成本** → 共享核心源码编译并入四程序集避免拷贝漂移；每模式检查集/语料分布独立维护，靠 CI 生成回归防退步；
8. **编辑器限制** → 不能新增页签/替换 Editor，功能收纳进 Compose 面板 + Setup 分区 + Verify 检查（§2.2）。

---

## 12. 批准后首批执行步骤

1. `git init` + 目录骨架 + `AiStudio.Core`/`osu.Game.Rulesets.AiStudio.Osu` 项目（锁版 NuGet）+ `Directory.Build.props`/`.editorconfig`/`global.json`；
2. `AiStudioOsuRuleset` 最小实现（复用官方 converter/difficulty/composer 空面板），本地构建后手动装入 lazer 验证可见性（M0 验收）；
3. 搭建 `ci.yml` 跑通构建/测试；
4. 启动 M1：RC 客观规则检查引擎 + Verify 页/侧栏 UI。

---

## 附录 A：关键 API 速查（ppy/osu master 源码路径）

| 用途 | 类/成员 | 路径 |
|---|---|---|
| 插件基类 | `Ruleset`（CreateHitObjectComposer / CreateBeatmapVerifier / CreateEditorSetupSections / CreateBeatmapConverter / CreateBeatmapProcessor / CreateDifficultyCalculator / CreatePerformanceCalculator / GetModsFor / ShortName / RulesetAPIVersionSupported） | `osu.Game/Rulesets/Ruleset.cs` |
| 插件加载 | `RulesetStore`（dll 前缀 `osu.Game.Rulesets.`、用户 `rulesets/` 目录）、`RealmRulesetStore`（API 版本门禁/兼容性试跑） | `osu.Game/Rulesets/RulesetStore.cs`、`RealmRulesetStore.cs` |
| 编辑器作曲 | `HitObjectComposer<T>`（CompositionTools / LeftToolbox / RightToolbox / LayerBelowRuleset / PlayfieldContentContainer）、`EditorToolboxGroup.AttachToToolbox`、范例 `OsuHitObjectComposer` | `osu.Game/Rulesets/Edit/HitObjectComposer.cs`、`osu.Game.Rulesets.Osu/Edit/OsuHitObjectComposer.cs` |
| 编辑器屏幕 | `Editor`（5 页签硬编码）、`EditorScreenWithTimeline`、`ComposeScreen`（按谱面 Ruleset 解析 composer） | `osu.Game/Screens/Edit/` |
| 谱面事件 | `EditorBeatmap`（HitObjectAdded/Updated/Removed/BeatmapReprocessed） | `osu.Game/Screens/Edit/EditorBeatmap.cs` |
| 难度计算 | `OsuDifficultyCalculator(IRulesetInfo, IWorkingBeatmap).Calculate(mods)`（public、同步、10s 超时）、`DifficultyAttributes`/`OsuDifficultyAttributes`、`PerformanceCalculator` | `osu.Game/Rulesets/Difficulty/`、`osu.Game.Rulesets.Osu/Difficulty/` |
| 谱面校验 | `IBeatmapVerifier` / `ICheck` / `Issue` / `IssueTemplate` / 官方 `OsuBeatmapVerifier`、`BeatmapVerifier`；`VerifyScreen.IssueList` 聚合 ruleset verifier | `osu.Game/Rulesets/Edit/`、`osu.Game/Screens/Edit/Verify/` |
| 导入导出 | `BeatmapManager`（Import/ExportLegacy/CreateNewDifficulty）、`LegacyBeatmapEncoder`、`LegacyBeatmapExporter`（.osz） | `osu.Game/Beatmaps/BeatmapManager.cs`、`osu.Game/Beatmaps/Formats/`、`osu.Game/Database/` |
| Mods | `Mod`（Ranked 默认 false）、`Ruleset.GetModsFor`、`IApplicableMod` 族接口 | `osu.Game/Rulesets/Mods/Mod.cs` |
| 音频 | ppy.ManagedBass/.Fx（BASS_FX_BPM_DecodeGet、BASS_FX_BPM_BeatDecodeGet、ChannelGetData FFT） | 依赖 osu.Framework（osu.Framework.csproj） |
| 数据库 | `RealmAccess`（Realm，非 EF Core） | `osu.Game/Database/RealmAccess.cs` |
| 模板 | ruleset 项目模板（`dotnet new install ppy.osu.Game.Templates`） | `ppy/osu` 仓库 `Templates/` 目录 |

## 附录 B：参考资料

- ppy/osu 源码：https://github.com/ppy/osu
- osu-templates（ruleset 模板）：ppy/osu `Templates/`；社区范例：sentakki（https://github.com/LumpBloom7/sentakki）、tau（https://github.com/taulazer/tau，headless 测试与 CI 参考）
- Ranking Criteria：https://osu.ppy.sh/wiki/en/Ranking_Criteria （及 /osu!、/osu!mania、/osu!taiko、/osu!catch）；AI policy 节（osu-wiki PR #15087，2026-08-13 合并）
- 谱面格式：https://osu.ppy.sh/wiki/en/Client/File_formats/osu_(file_format)
- MapsetVerifier（GPL-3.0，仅借鉴思路）：https://github.com/Naxesss/MapsetVerifier
- AI 谱面生成参考：osumapper（https://github.com/kotritrona/osumapper，Apache-2.0）、Dance Dance Convolution（ICML 2017，arXiv:1703.06891）、ManiaMapper、osu-mania-beatmap-generator
- 节拍模型：BeatNet（https://github.com/MJhydri/BeatNet，CC-BY-4.0，可导出 ONNX）；ONNX Runtime（Microsoft.ML.OnnxRuntime，MIT）
- BASS 许可：非商业免费，商业化需授权（https://www.un4seen.com/）
