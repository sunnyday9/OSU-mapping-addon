# ADR-MVP-B-002 — 官方 Difficulty Evaluator adapter（render→decode 路径）

- **日期**：2026-08-28
- **状态**：已采纳
- **背景**：ADR-MVP-A-016 定义 `IDifficultyEvaluator` 接口在 Core、官方 adapter 在 ruleset 程序集，随 MVP-B 交付。需要确定 adapter 内部怎么从 IR 文档算出官方 SR。

## 决策

`ManiaOfficialDifficultyEvaluator`（`osu.Game.Rulesets.AiStudio.Mania/Synthesis/`）实现 `IDifficultyEvaluator`：

```
TryEvaluateStarRating(document)
  → new ManiaOsuRenderer().Render(document)          // IR → .osu 文本（已有，确定性已测）
  → LegacyBeatmapDecoder.Decode(LineBufferedReader)  // 官方 decoder 解析
  → new ManiaInMemoryWorkingBeatmap(beatmap)          // 复用现有包装
  → new ManiaDifficultyCalculator(new ManiaRuleset().RulesetInfo, working).Calculate().StarRating
  → double.IsFinite 检查 + try/catch → null（失败语义）
```

**未选 IR→ManiaBeatmap 内存直转**（虽然 `ManiaMapGenerator.buildBeatmap` 有样板）：渲染→解码免费获得官方 decoder 的全部语义（control point、LN 归一、ApplyDefaults），零新增转换代码，且与"可打开的 .osu"产物天然一致（算的就是玩家会玩到的那个文件）。代价（每次评估一次文本生成+解析，毫秒级）可忽略。

## 理由

1. **Core 零依赖约束**：`IDifficultyEvaluator` 在 Core（无 osu 引用），实现必须在 ruleset 程序集（compile-include 使 Core 接口在插件内可见）——ADR-MVP-A-016 已定；
2. **render→decode 最短路径**：所有组件已验证（`ManiaChecksTest` 的 decode 样板、`OsuMapGeneratorTest` 的 SR 断言样板、`ManiaInMemoryWorkingBeatmap` 现成）；
3. **失败语义**：返回 null（而非抛异常）保持 `Evaluation.DifficultyKnown=false`——"评估器不可用不声称达标"（spec §25.4），空图/坏图不崩管线。

## 影响

- `MappingIrPipeline.Run` 注入官方 evaluator 后 `observed_star_rating` 有值、`DifficultyKnown=true`；
- `ManiaIrCalibratedPipeline` 把 evaluator 注入每次迭代的 pipeline（每轮重算 SR 作为校准反馈）；
- 空对象/不可解码 → null → 校准循环立即返回（不迭代），保持草稿语义。
