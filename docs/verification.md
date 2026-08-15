# 验收检查清单（Verification）

> 版本：2026-08-15（M0 + M1 阶段验收）
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
| L0-3 | 全部单元测试 | `dotnet test tests/osu.Game.Rulesets.AiStudio.Osu.Tests/osu.Game.Rulesets.AiStudio.Osu.Tests.csproj -c Release` | 28/28 通过 | ✅ 28/28（含 4 检查正/反用例、ruleset 兼容性冒烟、RC 区间表） |
| L0-4 | 格式校验 | `dotnet format <csproj> --verify-no-changes --no-restore`（两个项目） | 退出码 0 | ✅ 两个项目均通过 |
| L0-5 | dll 产物命名 | 检查 `bin/Release/net8.0/` 下产物 | 文件名以 `osu.Game.Rulesets.` 开头（加载器硬性要求） | ✅（见下方 L0-6） |
| L0-6 | 产物自包含 | 检查 ruleset dll 是否仅依赖官方包 | rulesets 目录只需部署 1 个 dll（AiStudio.Core 源码已编译并入） | ✅ 共享核心零独立 dll |

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

## 3. L2 — 真机冒烟（需真实 osu!lazer，本环境无法执行，步骤供用户执行）

1. 构建：`dotnet build src/osu.Game.Rulesets.AiStudio.Osu/osu.Game.Rulesets.AiStudio.Osu.csproj -c Release`；
2. 复制 `bin/Release/net8.0/osu.Game.Rulesets.AiStudio.Osu.dll` 到 osu!lazer 用户数据目录 `rulesets/` 文件夹；
3. 启动 osu!lazer，确认设置/选歌界面未报"自定义规则集异常"；
4. 选歌 → 左侧规则集列表出现 **AI Studio (osu!)**；
5. 把一张 osu! 谱面切换到 AI Studio 规则集 → 进入编辑器；
6. 检查三个注入点：
   - Compose 页右工具箱出现 "AI Studio" 面板；
   - Setup 页出现 "AI Studio" 分区；
   - Verify 页出现 AI Studio 的 4 项检查（连同官方检查一起列出）；
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
| CI/CD | 5 工作流 + dependabot + 模板 | L1-7 ✅；真实 GitHub 运行待仓库推送后验证 |

## 5. 已知限制与后续

- **L2 真机冒烟未执行**：本环境无 osu!lazer 图形环境，三项"游戏内可见"验收（L2-3~L2-6）需用户按 §3 步骤执行后回填；
- 覆盖率 ≥70% 门禁（NFR-6）：当前为功能性测试优先，覆盖率达到后由 CI（Codecov）度量——ci.yml 已预留上传配置，M2 起纳入；
- M2 生成管线（BASS_FX 分析/SR 校准/语料分布）未开始，相关检查标记"计划"见 `rc-coverage.md`。

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
