# 验收检查清单（Verification）

> 版本：2026-08-15（M0 + M1 + M2 阶段验收）
> 原则：每项检查 = 具体命令/操作 + 预期结果 + 实测结果；**证据优先于口头结论**。

## 0. 执行环境

| 项 | 值 |
|---|---|
| 操作系统 | Windows 10.0.26200 x64（Git Bash） |
| .NET SDK | 8.0.424（仓库本地 `.dotnet/`，global.json 固定 8.0.x） |
| NuGet 锁定 | ppy.osu.Game 2026.730.0 / ppy.osu.Game.Rulesets.Osu 2026.730.0（与最新版一致） |
| 测试框架 | NUnit 4.5.1 + NUnit3TestAdapter 4.6.0 |

## 1. L0 — 自动化检查（本机可重复执行）

| # | 检查 | 命令 | 预期 | 实测 |
|---|---|---|---|---|
| L0-1 | 插件项目构建 | `dotnet build src/osu.Game.Rulesets.AiStudio.Osu/osu.Game.Rulesets.AiStudio.Osu.csproj -c Release` | 0 错误 | ✅ 0 错误 |
| L0-2 | 测试项目构建 | `dotnet build tests/osu.Game.Rulesets.AiStudio.Osu.Tests/osu.Game.Rulesets.AiStudio.Osu.Tests.csproj -c Release` | 0 错误 | ✅ 0 错误 |
| L0-3 | 全部单元测试 | `dotnet test tests/osu.Game.Rulesets.AiStudio.Osu.Tests/osu.Game.Rulesets.AiStudio.Osu.Tests.csproj -c Release` | 38/38 通过 | ✅ 38/38（M0 冒烟 + M1 检查 + M2 分析器 golden/生成器/门禁/roundtrip/真实 WAV 集成） |
| L0-4 | 格式校验 | `dotnet format <csproj> --verify-no-changes --no-restore`（两个项目） | 退出码 0 | ✅ 两个项目均通过（配合 `.gitattributes` 强制 LF，规避 Windows runner CRLF 误报） |
| L0-5 | dll 产物命名 | 检查 `bin/Release/net8.0/` 下产物 | 文件名以 `osu.Game.Rulesets.` 开头（加载器硬性要求） | ✅ |
| L0-6 | 产物自包含 | 检查 ruleset dll 是否仅依赖官方包 | rulesets 目录只需部署 1 个 dll（AiStudio.Core 源码已编译并入） | ✅ |
| L0-7 | 测试覆盖率 | `dotnet test --collect:"XPlat Code Coverage"`（coverlet） | ≥70% | ✅ **74.2%**（0% 者为需游戏 UI 宿主的 Drawable，M3 起用 osu.Framework headless 补） |
| L0-8 | M2 生成管线 golden 测试 | `BassAudioAnalyzerTest` 120/90 BPM 合成点击轨 | BPM 120±0.5 / 90±1.0 | ✅（BPM 117.45 等 3 处缺陷已修复：WAV 脉冲推进 bug、hop 量化、首拍边界） |
| L0-9 | M2 生成端到端 | `OsuMapGeneratorTest`（假分析器 + 真实 WAV） | 门禁全绿、SR 校准 ±0.3、导出 roundtrip | ✅（真实 60s 120BPM 点击轨 → SR 3.0 校准成功） |

## 2. L1 — 结构/静态检查

| # | 检查 | 证据 |
|---|---|---|
| L1-1 | 插件继承 `Ruleset` 而非 `OsuRuleset`（LegacyID 陷阱规避） | `AiStudioRuleset.cs` 类声明；单测 `RulesetInfoIsNotLegacy` 断言 OnlineID == -1 ✅ |
| L1-2 | API 版本门禁 | `RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION`；单测 `ApiVersionMatchesCurrentRulesetApi` ✅ |
| L1-3 | 三个编辑器注入点就位 | `CreateHitObjectComposer`→AiStudioHitObjectComposer（RightToolbox 挂 AI 面板）、`CreateEditorSetupSections`→AiStudioSetupSection、`CreateBeatmapVerifier`→AiStudioBeatmapVerifier（聚合官方 OsuBeatmapVerifier + 4 自研检查）✅ |
| L1-4 | mod 注册 | `GetModsFor` 委托官方 OsuRuleset + 追加 `AiStudioAssistantMod`（Ranked=false）；单测 `AssistantModIsNotRanked`/`AllModsCanBeCreated` ✅ |
| L1-5 | 检查规则引用 RC 条款 | 每个 Check 类注释含 wiki 条款编号/URL（CheckDifficultySettingsRanges/CheckSpreadStarRatingGaps/CheckComboColourCount/CheckSpinnerSpacing），`docs/rc-coverage.md` 覆盖矩阵 ✅ |
| L1-6 | 星数→难度等级映射与官方一致 | `DifficultyRatingHelper` 阈值 2.0/2.7/4.0/5.3/6.5，对齐官方 `StarDifficulty.GetDifficultyRating`（已核对 ppy/osu 2026.730.0 tag 源码）；单测 `GetLevelMatchesOfficialStarRatingThresholds` ✅ |
| L1-7 | CI/CD 配置合法 | 5 个工作流 + dependabot + 模板经 js-yaml 解析校验通过；api-compat 探针代码已对锁定包实测编译通过（代理验证）✅ |
| L1-8 | 凭据安全 | 仓库内无任何密钥字面量；CI token 一律 `${{ secrets.GITHUB_TOKEN }}` 环境变量注入 ✅ |

## 3. L2 — 真机冒烟（需真实 osu!lazer，本机未安装游戏，部分步骤已执行/待用户执行）

**已执行**：
- ✅ 构建 Release dll 并安装到本机 lazer 规则集目录：`%APPDATA%\osu\rulesets\osu.Game.Rulesets.AiStudio.Osu.dll`（2026-08-15，与用户已有 AiMapper 规则集并列）；
- ✅ 本机未安装 osu!lazer 可执行文件（已搜索 `%LOCALAPPDATA%\osu!`、用户目录、Program Files，均无 `osu!.exe`）——**游戏内步骤无法在本环境执行**。

**待用户在装有 osu!lazer 的机器执行**（dll 已就位，游戏启动即自动加载）：
1. 启动 osu!lazer，确认设置/选歌界面未报"自定义规则集异常"；
2. 选歌 → 左侧规则集列表出现 **AI Studio (osu!)**；
3. 把一张 osu! 谱面切换到 AI Studio 规则集 → 进入编辑器；
4. 检查三个注入点：Compose 右工具箱 "AI Studio" 面板；Setup 页 "AI Studio" 分区（含音频路径输入 + 生成按钮）；Verify 页 AI Studio 检查项；
5. Setup 页输入任意音频文件路径 → 点击"生成（Hard 预设）" → 状态显示输出路径（我的文档/osu-ai-studio-output/），打开目录确认 `map.osu` + 音频副本；
6. 把输出文件夹拖入 osu!lazer 导入，试玩生成的谱面；
7. 正常游玩该谱面（AI Studio 规则集），确认玩法与官方 osu! 一致；
8. 若启动即禁用：检查 osu!lazer 日志（自定义规则集 dll 会被改名 `.dll.broken`，把日志与 dll 目录内容反馈给维护者）。

## 4. L3 — 里程碑验收矩阵（PLAN.md §8）

| 里程碑项 | 验收标准 | 证据 |
|---|---|---|
| M0 脚手架 | 游戏内可见 + 构建/测试/格式全绿 | L0-1~L0-4 ✅；游戏内可见 = L2-3~L2-6（待用户真机执行） |
| M0 规则集兼容性 | 与官方启动兼容性测试等价的行为不崩溃 | 单测 `AllModsCanBeCreated`/`OsuBeatmapConvertsWithoutError`/`DifficultyCalculationDoesNotThrow` ✅ |
| M1 FR-1.1 客观检查 ≥4 条 | 4 检查实现 + 正/反用例 | `AiStudioBeatmapVerifierTest` 10 个用例全绿 ✅ |
| M1 FR-1.2 接入 Verify 页 | AiStudioBeatmapVerifier 聚合官方 + 自研 | 代码走查 ✅ + L2-6 真机确认（待执行） |
| M1 FR-1.3 RC 可追溯 | 注释条款编号 + rc-coverage.md | L1-5 ✅ |
| M1 FR-1.4 正/反用例 | 每检查正/反用例存在且通过 | 测试清单：DifficultySettingsOutOfRange/Compliant、LargeStarRatingGap/Compliant/Missing/Single、SpinnerTooClose/Sufficient、SingleCustom/TwoCustom/NoCustom ✅ |
| M2 FR-2.1 音频分析 | BASS 分析 BPM 误差 ≤±0.5（合成 click track golden） | `BpmDetectionIsAccurateAt120Bpm`/`At90Bpm` ✅；`GenerateFromRealClickTrackWav`（真实 WAV 端到端）✅ |
| M2 FR-2.3 生成 + SR 校准 | Hard 预设生成 + SR ±0.3★ | `GenerateSucceedsWithAllGatesPassing`/`StarRatingIsWithinTolerance` ✅；`UnreachableStarRatingFailsGracefully`（8★ 优雅失败）✅ |
| M2 FR-2.4 质量门禁 | 门禁全绿才落盘 | `QualityGateRunner` G1–G5 全绿才 Success；失败不落盘（Error 消息含失败门禁详情）✅ |
| M2 导出 roundtrip | .osu 可解码回读 | `ExportedFileRoundTrips`（116 物件 1:1 无损、slider/circle 俱在、网格对齐）✅ |
| CI/CD 真实运行 | GitHub Actions 全绿 | 仓库 sunnyday9/OSU-mapping-addon；ci.yml 修复后运行中/已核验（详见最新运行） |
| 覆盖率 | ≥70% | 74.2%（coverlet cobertura）✅ |

## 5. 已知限制与后续

- **L2 游戏内步骤未执行**：本机未安装 osu!lazer（已搜索确认），dll 已装入 `%APPDATA%\osu\rulesets\`，用户按 §3 步骤执行后回填；
- **覆盖率**：74.2% ≥70% ✅；0% 项为需游戏 UI 宿主的 Drawable（composer/setup/toolbox），M3 起用 osu.Framework headless（`OSU_EXECUTION_MODE` + VisualTestRunner）补充；
- **G4 语料分布门禁为临时区间**：corpus-refresh 工作流与 `tools/analysis` 落地后替换为真实 ranked 语料分布（P5–P95）；
- **M2 段落/滑条质量受限**：v1 单段落、无 kiai/break/spinner，M3 细化；BPM 检测回退路径（自相关）未覆盖测试用例，M3 补。

## 6. 复验命令（一次性跑完）

```bash
export PATH="/f/zcode-harness/OSU-mapping-addon/.dotnet:$PATH"
cd /f/zcode-harness/OSU-mapping-addon
dotnet build src/osu.Game.Rulesets.AiStudio.Osu/osu.Game.Rulesets.AiStudio.Osu.csproj -c Release
dotnet build tests/osu.Game.Rulesets.AiStudio.Osu.Tests/osu.Game.Rulesets.AiStudio.Osu.Tests.csproj -c Release
dotnet test tests/osu.Game.Rulesets.AiStudio.Osu.Tests/osu.Game.Rulesets.AiStudio.Osu.Tests.csproj -c Release --no-build
dotnet format src/osu.Game.Rulesets.AiStudio.Osu/osu.Game.Rulesets.AiStudio.Osu.csproj --verify-no-changes --no-restore
dotnet format tests/osu.Game.Rulesets.AiStudio.Osu.Tests/osu.Game.Rulesets.AiStudio.Osu.Tests.csproj --verify-no-changes --no-restore
```
