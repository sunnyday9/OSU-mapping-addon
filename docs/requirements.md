# 需求文档（Requirements）

> 来源：`docs/PLAN.md`（v3）· 状态：M0/M1/M2/M3/M4/M5/M6 已交付（见 docs/verification.md L3）

## 1. 用户故事

| 编号 | 角色 | 故事 | 对应里程碑 |
|---|---|---|---|
| US-1 | 玩家 | 上传音频文件后，一键生成可游玩的 osu! 标准谱面 | M2/M3 |
| US-2 | 制图者 | 制图过程中实时看到 ranked 合规问题与修复建议 | M1/M3（实时侧栏） |
| US-3 | 制图者 | 根据建议一键生成缺失难度的草稿（难度梯度补全） | M3（SpreadPlanner + 集合生成） |
| US-4 | 玩家 | 生成的谱面在任何原版 osu!lazer/stable 中可正常游玩 | M3（.osz 集合导出，原版可玩） |
| US-5 | 开发者 | 插件随 osu!lazer 版本演进保持兼容（自动侦测上游破坏性变更） | 持续（api-compat.yml 周级探针） |
| US-6 | 维护者 | 每个里程碑的改动通过自动化检查（构建/测试/格式）才能合入 | M0 起（ci.yml 四程序集矩阵 + 覆盖率 70%） |
| US-7 | 制图者 | 切换 mania/taiko/catch 模式时获得对应模式的生成与检查能力 | M4–M6（已交付） |

## 2. 功能需求（按里程碑）

### M0 脚手架（已完成，验收于 verification.md）
- FR-0.1 规则集插件被 osu!lazer 识别（dll 命名、API 版本门禁、非 legacy 注册）；
- FR-0.2 Compose 页右工具箱出现 AI Studio 面板；
- FR-0.3 Setup 页出现 AI Studio 分区；
- FR-0.4 Verify 页规则集校验器挂载（聚合官方 osu! 检查）；
- FR-0.5 构建/测试/格式检查在本地与 CI 全绿（四程序集）。

### M1 检查引擎 v1
- FR-1.1 客观 RC 检查 ≥ 4 条（难度设置区间、spread 星距、combo 颜色、spinner 间隔）；
- FR-1.2 检查结果接入 Verify 页（与官方检查并列展示）；
- FR-1.3 每条检查注释引用 RC 条款编号，`rc-coverage.md` 维护覆盖矩阵；
- FR-1.4 每条检查有正/反用例测试。

### M2 生成 v1
- FR-2.1 BASS 解码 + spectral-flux/块能量分析：BPM 误差 ≤±0.5（合成 click track golden 测试；BASS_FX BPMDecodeGet 已验证不可靠并弃用，见 `BassAudioAnalyzer`）；
- FR-2.2 ranked 语料参数分布拟合（tools/analysis，M3 经 IDistributionProvider 接入 distributions.json）；
- FR-2.3 Hard 预设单难度生成 + SR 校准 ±0.3★；
- FR-2.4 生成结果通过 §3 全部门禁才可落盘。

### M3 生成 v2
- FR-3.1 多难度预设与 spread 规划（相邻 ≤2.0★，不跳级；`SpreadPlanner`）；
- FR-3.2 段落/kiai/break/SV（多段 AudioSection + EffectControlPoint + BreakPeriod）；
- FR-3.3 质量门禁 G3/G4 接入 `tools/analysis` 真实 P5–P95（`IDistributionProvider` + `distributions.json`）；
- FR-3.4 集合导出 `.osz`（`BeatmapSetExporter` Zip，Tags AI generated）；
- FR-3.5 编辑器实时订阅与 Generate Set（`EditorBeatmap` 事件 → `SuggestionEngine` → `ToolboxGroup.UpdateSummary`）。

### M4 mania / M5 catch / M6 taiko
- FR-4.1 各模式独立 ruleset（`Ruleset` 基类，规避 LegacyID 陷阱）；
- FR-4.2 各模式难度区间与 star rating（`*DifficultyRanges` + `*StarRating`）；
- FR-4.3 各模式检查集（mania 列分布/jack/chord；catch hyperdash/offscreen/移动；taiko don/kat/mono）；
- FR-4.4 各模式合成器（mania 音符矩阵；catch 由 std 派生；taiko don/kat 序列）；
- FR-4.5 各模式通过对应门禁与 L0 自动化。

## 3. 非功能需求

- **NFR-1** 不改动 osu!lazer 任何核心代码（唯一插件机制 = ruleset dll；四模式各一 dll）；
- **NFR-2** 生成/难度计算全部后台执行，不阻塞编辑器 UI；
- **NFR-3** 生成产物标注 AI generated，不承诺可 rank；
- **NFR-4** 许可证合规：不链接 GPL 代码（MapsetVerifier 仅借鉴思路）；BASS 商业化需授权；
- **NFR-5** 无凭据硬编码：所有密钥/令牌只经环境变量或密钥服务注入（含 CI 工作流；tools/analysis 离线合成不依赖密钥）；
- **NFR-6** 测试覆盖率 ≥70%（M1 起度量），warnings-as-errors 构建、dotnet format 校验（四程序集矩阵）。

## 4. 验收标准摘要

| 里程碑 | 硬性验收 |
|---|---|
| M0 | 游戏内冒烟：装 dll → 切规则集 → 三个注入点可见；构建/测试/格式全绿 |
| M1 | fixtures 谱面全部命中、无漏报；正/反用例全绿 |
| M2 | 10 首测试曲成功率 >95%；产出通过全部质量门禁 |
| M3 | spread 星距满足 RC 梯度；多段/kiai/break 可见；G3/G4 接真实分布；.osz 导出原版可玩 |
| M4–M6 | 对应模式版 M2 门禁（列分布/jack/chord；don/kat；hyperdash 等） |

详细质量门禁定义见 `docs/PLAN.md` §3；逐项验收执行见 `docs/verification.md`。
