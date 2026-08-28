# osu! AI Studio

osu!lazer 的 AI 制图辅助插件（**不改动 osu!lazer 任何核心代码**，纯 ruleset 插件）。

- **音频 → 谱面生成**：上传音频自动生成可游玩的 osu! 标准谱面；
- **制图辅助**：手动制图时实时给出 ranked 合规建议与规划。

**质量定位**：生成/辅助产出的谱面以 **ranked 级质量** 为基准（客观 Ranking Criteria 零错误、难度设置合规、节奏对齐、参数落在真实 ranked 语料分布内、星数校准 ±0.3★），但不以 ranked 投稿为目标（官方 RC 禁止生成式 AI 谱面进 rank），产物默认标注 AI generated。

**模式路线**：osu! 标准（开发中）→ mania → catch → taiko，四模式共享分析层、各用专属生成策略。

## 安装（玩/试用插件）

1. 在 [Releases](../../releases) 下载最新的 zip；
2. 解压出 `osu.Game.Rulesets.AiStudio.Osu.dll`；
3. 放入 osu!lazer 用户数据目录下的 `rulesets/` 文件夹；
4. 启动 osu!lazer，在选歌界面把谱面切换到 **AI Studio (osu!)** 规则集，进入编辑器即可看到 AI Studio 面板。

## 开发

环境要求：.NET 8 SDK（本仓库 `global.json` 固定 8.0.x）。

```bash
dotnet restore
dotnet build
dotnet test
dotnet format --verify-no-changes
```

本地 SDK 可放在仓库 `.dotnet/` 目录（已被 gitignore），或使用系统安装的 SDK。

## 仓库结构

```
src/AiStudio.Core/                    共享核心（纯 .NET 8，源码并入各 ruleset 程序集）
  └── MappingIr/                      MVP A：Mapping IR v0.1 核心层（模型/序列化/时间线/规划/模式/校验/渲染）
src/osu.Game.Rulesets.AiStudio.Osu/   osu! 标准模式插件
src/osu.Game.Rulesets.AiStudio.Mania/ mania 插件
src/osu.Game.Rulesets.AiStudio.Catch/ catch 插件
src/osu.Game.Rulesets.AiStudio.Taiko/ taiko 插件
tests/                                单元测试（NUnit；含 AiStudio.Core.MappingIr.Tests 43 用例）
tools/analysis/                       ranked 语料采集/分布拟合（Python，离线）
tools/mapping-ir-demo/                MVP A 端到端 CLI 演示（合成音频 → .osu/.json）
docs/                                 需求/架构/RC 覆盖矩阵/验收清单
docs/new plan/                        AI Mapper 新计划 + implementation（实施计划与 ADR）
.github/workflows/                    CI/CD（GitHub Actions）
```

## 里程碑状态

| 里程碑 | 状态 | 说明 |
|---|---|---|
| M0 脚手架 | ✅ 完成 | 规则集可被游戏识别，三个编辑器注入点就位 |
| M1 检查引擎 v1 | ✅ 完成 | 4 条 RC 客观检查 + Verify 页/侧栏 + SuggestionEngine |
| M2 生成 v1 | ✅ 完成 | BASS 音频分析（BPM/beat/onset/段落）+ 模板合成 + SR 校准闭环 + 五道门禁 + .osu 导出 + Setup 页生成 UI |
| M3 生成 v2 | ✅ 完成 | 多难度预设/spread、段落/kiai/break、.osz 导出、IDistributionProvider 语料分布 |
| M4–M6 | ✅ 完成 | mania / catch / taiko 独立插件（各有检查集与合成器） |
| **MVP A Mapping IR** | ✅ 完成 | Mapping IR v0.1 核心层：语义 IR + 确定性 Mania 4K 生成 + 校验 + .osu 渲染 + 端到端 CLI；JSON Schema 校验 PASS、43/43 测试、零新依赖 |
| **MVP B SR 校准** | ✅ 完成 | 官方 ManiaDifficultyCalculator 校准闭环：DensityScale 旋钮迭代 → 目标 SR ±0.15★（实测 5.61 ∈ 5.5±0.15）；`IDifficultyEvaluator` adapter + `ManiaIrCalibratedPipeline`；112 Core + 25 Mania 测试 |

详见 `docs/PLAN.md`（§8.1 AI Mapper 路线）、`docs/verification.md`（§7 MVP A 验收）与 `docs/new plan/implementation/`（实施计划 + ADR-MVP-A-001~016 + ADR-MVP-B-001~002）。
