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
src/osu.Game.Rulesets.AiStudio.Osu/   osu! 标准模式插件（当前开发重点）
tests/                                单元测试（NUnit）
docs/                                 需求/架构/RC 覆盖矩阵/验收清单
.github/workflows/                    CI/CD（GitHub Actions）
```

## 里程碑状态

| 里程碑 | 状态 | 说明 |
|---|---|---|
| M0 脚手架 | ✅ 完成 | 规则集可被游戏识别，三个编辑器注入点就位 |
| M1 检查引擎 v1 | 🚧 进行中 | RC 客观规则 + Verify 页/侧栏 |
| M2 生成 v1 | ⏳ | 音频分析 + Hard 预设生成 + SR 校准 |
| M3–M7 | ⏳ | 多难度/导出 / mania / catch / taiko / 打磨 |

详见 `docs/PLAN.md` 与 `docs/verification.md`。
