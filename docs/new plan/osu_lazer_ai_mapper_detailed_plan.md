# osu!lazer AI Mapper：详细系统设计与实施计划

## 0. 项目目标

目标是构建一个面向 osu!lazer 的 AI 作图辅助系统，第一阶段以“稳定、合理、可解释”的通用 Mapping AI（A）为核心，同时从架构上预留未来接入 Mapper/User Style Layer（C）的能力。

系统最终支持两种产品模式：

1. **Auto Mapper**：用户导入音频，选择 ruleset、难度星级、key 数/可选 mapping profile 后，系统自动生成完整可玩的 beatmap。
2. **AI Copilot**：用户自己作图时，AI 实时分析音乐和当前谱面，提供下一个 object、下一组 pattern、下一段 mapping direction、段落级重构等建议。

核心原则：

- 第一版不训练从音频直接生成 `.osu` 的端到端大模型。
- LLM 负责音乐语义解释、mapping planning、pattern selection、critique 和交互；确定性的算法负责 timing、几何/column placement、规则约束、difficulty 校准和最终序列渲染。
- 所有 ruleset 都共享统一的 Mapping IR（Intermediate Representation），但每个 ruleset 拥有自己的 Pattern Grammar、Renderer、Validator 和 Difficulty Adapter。
- Star Rating 是结果约束而不是唯一作图策略；系统内部使用 Difficulty Profile。
- 未来 Style Layer 与 Base Mapping Intelligence 解耦，避免为了个性化重新训练整个模型。

---

# 1. 总体架构

```text
Audio
  │
  ▼
Music Analysis Layer
  │  BPM / beat / onset / energy / spectral / vocal / drums / sections
  ▼
Music Representation
  │  Musical Timeline / MusicEvent / Phrase / Section
  ▼
Mapping Context Builder
  │  ruleset / keys / target SR / difficulty profile / current map / style
  ▼
Mapping Agent
  │
  ├── Song Analyst
  ├── Mapping Planner
  ├── Pattern Selector
  ├── Critic
  └── Copilot Dialogue Layer
  │
  ▼
Mapping IR
  │
  ├── Rhythm Intent
  ├── Phrase Intent
  ├── Pattern Intent
  ├── Movement/Hand Policy
  └── Difficulty Budget
  │
  ▼
Ruleset-specific Pattern Engine
  │
  ├── osu!standard
  ├── Mania
  ├── Taiko
  └── Catch
  │
  ▼
Beatmap Renderer
  │
  ▼
Validation + Difficulty
  │  timing / legality / playability / SR / local difficulty
  ▼
Critic / Optimization Loop
  │
  ├── repair
  ├── rerank alternatives
  └── accept candidate
  │
  ▼
.osu / in-editor proposal
  │
  ▼
Human feedback / playtest / edits
  │
  └───────────────→ future Preference & Style Layer
```

osu!lazer 本身提供 ruleset-specific editor 架构，`HitObjectComposer` 是编辑器核心组件之一；Mania 也有独立的 `ManiaRuleset`、`ManiaHitObjectComposer` 和 `ManiaDifficultyCalculator`。因此推荐把 AI 系统设计成独立的 mapping engine/service，再通过 Lazer editor integration 注入建议和生成结果，而不是重写规则集编辑器。citeturn823616search1turn823616search3

osu! 当前的 `DifficultyCalculator` 还支持完整 difficulty calculation 与 timed/progressive difficulty calculation，这非常适合做 AI 生成后的局部 SR、strain 和段落级反馈环。citeturn823616search0

---

# 2. 最重要的设计：不要让 AI 直接生成 HitObjects

## 2.1 推荐的数据流

不要：

```text
Audio → LLM → .osu
```

而应该：

```text
Audio
 ↓
Music Events
 ↓
Mapping Intent
 ↓
Pattern Grammar
 ↓
Mapping IR
 ↓
Deterministic Generator
 ↓
HitObjects
```

这样做的原因是：

- LLM 擅长规划，不擅长保证大量离散数值永远合法。
- Pattern 的时间量化、column/坐标约束、LN release、slider geometry 等应当确定性处理。
- Difficulty calculator 可以作为外部 evaluator。
- Agent 可以解释“为什么选择这个 pattern”。
- 以后替换 LLM、训练小模型或增加 Style Adapter，都不需要重写 renderer。

---

# 3. Mapping IR：整个项目的核心数据结构

这是第一阶段最值得投入精力的部分。

建议把 Mapping IR 设计为四层。

## 3.1 Song Layer

描述音乐本身：

```text
SongContext
- duration
- bpm_map
- time_signature
- key / tonal hints
- beat_grid
- onset_density
- energy_curve
- spectral_features
- vocal_activity
- drum_activity
- bass_activity
- section timeline
- phrase boundaries
```

## 3.2 Mapping Intent Layer

描述“这一段想表达什么”：

```text
MappingIntent
- section_type: intro / verse / prechorus / chorus / drop / bridge / outro
- musical_focus: vocal / melody / kick / snare / bass / harmony / texture
- emphasis
- density_target
- rhythm_complexity_target
- movement_target
- readability_target
- continuity_target
- transition_target
```

## 3.3 Pattern Layer

描述“用什么 mapping language”：

```text
PatternIntent
- primary_pattern
- secondary_pattern
- rhythm_grid
- repetition_policy
- variation_policy
- transition_in
- transition_out
- hand_or_movement_policy
- LN_policy
- difficulty_budget
```

## 3.4 Object Layer

最终渲染为规则集对象：

```text
ConcreteObject
- start_time
- end_time
- position / column
- object_type
- attributes
```

关键原则：**LLM 尽量停留在 Intent / Pattern 层，不直接操作 Object 层。**

---

# 4. Difficulty Profile：不要把 Star Rating 直接喂给 Agent

用户界面可以让用户输入：

```text
Mania 4K
5.5★
```

但内部应该转换成：

```text
DifficultyProfile
- target_star_rating
- target_density
- target_rhythm_complexity
- target_stamina
- target_readability
- target_chord_complexity
- target_ln_ratio
- target_technicality
- target_movement
- target_consistency
- target_peak_intensity
```

不同 profile 可以产生不同但接近相同 SR 的谱面：

- stream-focused
- chordjack-focused
- LN-focused
- technical
- readable
- balanced

因此 SR 应作为最终约束之一，而非 Mapping Planner 的全部目标。

osu! 的 difficulty calculation 已经是规则集专属的；例如当前 Mania calculator 会根据其规则集的 difficulty preprocessing 与 strain skill 计算 StarRating，因此建议直接把官方/现有 difficulty calculator 当作 evaluator，而不是重新训练“预测星级”模型。citeturn823616search0turn823616search2

---

# 5. Pattern Grammar：真正需要构建的知识库

第一版不要追求让模型凭空发明所有 pattern。建立一个结构化 Pattern Library。

## 5.1 Mania 第一批 Pattern

```text
Rhythm
- 1/1
- 1/2
- 1/4
- 1/8
- 1/16
- burst
- jump burst

Hand / column
- alternate
- stream
- jumpstream
- chord
- jack
- chordjack
- handstream
- anchor

LN
- single LN
- LN + rice
- LN chord
- LN transition
- release pattern

Phrase
- build-up
- escalation
- release
- repetition
- variation
- climax
- resolution
```

每种 pattern 必须包含：

- Preconditions
- Input parameters
- Generation algorithm
- Difficulty impact
- Playability constraints
- Common failure modes
- Compatible previous patterns
- Compatible next patterns
- Musical situations where it is commonly appropriate

## 5.2 Standard 第一批 Pattern

```text
Rhythm
- single tap
- burst
- stream
- jump
- jump burst

Movement
- linear movement
- angular movement
- wide movement
- alternating movement
- rotational movement

Slider
- simple slider
- repeated slider
- stream slider
- movement slider
- pattern transition slider

Phrase
- buildup
- climax
- release
- repetition
```

## 5.3 Taiko / Catch

第一阶段只要求统一 IR 和接口存在，不需要与 Standard/Mania 同时做到同等深度。

---

# 6. Agent 设计

建议不要做“万能单 Agent”，而是做一个 Orchestrator + 专业工具。

## 6.1 Orchestrator

负责：

- 当前状态
- 当前段落
- 目标 difficulty
- 用户请求
- 调用哪个 tool
- 什么时候重新评估

## 6.2 Song Analyst

输入音频分析结果，输出：

- sections
- phrases
- musical events
- dominant instruments
- intensity
- repetition
- transitions

## 6.3 Mapping Planner

输入：

- SongContext
- previous mapping
- ruleset
- DifficultyProfile
- optional style

输出：

- phrase-level strategy
- pattern sequence
- density schedule
- transitions

## 6.4 Pattern Selector

不生成最终 object，只选择 Pattern Grammar，并填充参数。

## 6.5 Generator

确定性生成 HitObjects。

## 6.6 Critic

检查：

- musical correspondence
- pattern continuity
- readability
- awkward transitions
- timing
- difficulty drift
- ruleset legality
- local difficulty spikes

## 6.7 Copilot Layer

把 Agent 的内部规划转换为用户能理解的动作：

```text
[Suggest]
下一组建议：1/16 alternating stream
原因：当前 4 小节进入 build-up，snare density 上升。

[Insert]
[Try another]
[Generate next 2 bars]
```

---

# 7. Tool Interface 设计

Agent 应通过稳定 API 操作谱面，而不是直接修改内部文件。

核心工具：

```text
analyze_audio()
get_song_sections()
get_music_events(time_range)
get_current_mapping(time_range)
get_mapping_context()
find_nearby_patterns()
score_pattern()
generate_pattern()
preview_pattern()
validate_mapping()
calculate_difficulty()
calculate_timed_difficulty()
compare_candidates()
insert_objects()
replace_objects()
undo_agent_change()
```

其中 `calculate_timed_difficulty()` 特别有价值：可以做“这一段为什么突然变成 6.3★？”这种局部问题检测，而不必每次只看整首歌最终 SR。osu! 的 difficulty calculator 已经提供 timed/progressive calculation 能力。citeturn823616search0

---

# 8. 自动作图模式 Auto Mapper

## 阶段流程

```text
1. 导入歌曲
2. 选择 ruleset
3. 选择 key 数/游戏变体
4. 选择目标 SR
5. 选择 difficulty profile
6. Audio Analysis
7. Section Segmentation
8. Mapping Plan
9. Pattern Generation
10. Difficulty Evaluation
11. Repair / Replan
12. Global Consistency Pass
13. Export / Open in Lazer
```

## Candidate Search

不要每个位置只有一个候选。

例如一个 chorus 可以生成：

```text
Candidate A: stream-heavy
Candidate B: burst-heavy
Candidate C: LN-heavy
```

然后 Critic 对它们打分：

```text
music_match
+ readability
+ continuity
+ target_difficulty
+ pattern_quality
+ variation
```

最后选最高分。

这会比“LLM 一次生成最终答案”稳定很多。

---

# 9. Copilot 模式

Copilot 必须支持至少三个粒度。

## 9.1 Object Level

用户正在编辑：

> “下一个 note 放哪里？”

AI 根据：

- 当前 note
- 前 N 个对象
- 当前 rhythm
- 当前音乐事件
- movement/hand constraints

生成 3 个候选。

## 9.2 Pattern Level

例如：

> “接下来 2 小节怎么作？”

AI 推荐：

```text
1/16 stream
→ short burst
→ release
```

并可一键插入。

## 9.3 Section Level

例如：

> “副歌这一段应该怎么处理？”

AI 输出：

```text
Chorus strategy:
- density ↑
- rhythm complexity ↑
- keep motif repetition
- add movement
- reserve max intensity for last 4 bars
```

这三层可以共用同一个 Mapping Planner，只是 Context Window 不同。

---

# 10. A→C 的 Style Layer

第一版不要做 mapper 风格训练，但接口从第一天保留：

```text
StyleProfile
- density_preference
- rhythm_preference
- pattern_preference
- LN_preference
- movement_preference
- repetition_preference
- variation_preference
- readability_preference
```

未来增加：

```text
StyleProfile
      ↓
Style Encoder / LoRA / Preference Model
      ↓
Base Mapping Planner
```

此时 Base Model 仍负责：

> “什么是合理 mapping？”

Style Layer 负责：

> “在合理范围内，我喜欢怎样 mapping？”

这是从 A 扩展 C 最安全的边界。

---

# 11. 数据路线：第一阶段不训练大模型

## Phase 1

使用现有 pretrained audio models/tools + LLM + 手写 Pattern Grammar。

目标：证明：

> 系统能生成比纯随机/纯规则方案更像“人类合理 mapping”的谱面。

## Phase 2

开始收集结构化数据：

```text
Map
 ├── Audio features
 ├── Section features
 ├── HitObjects
 ├── Pattern labels
 ├── Difficulty attributes
 └── Transition labels
```

从高质量谱面开始，而不是无差别吃所有 beatmap。

## Phase 3

训练轻量模型：

- Pattern ranking model
- Difficulty prediction model
- Pattern transition model
- Candidate preference model

## Phase 4

加入 Style Adapter / Preference Learning。

---

# 12. 如何构建训练数据而不手工标注一切

这是项目是否能长期发展的关键。

可以从已有谱面自动抽取：

```text
raw .osu
 ↓
parser
 ↓
feature extractor
 ↓
pattern recognizer
 ↓
section alignment
 ↓
training sample
```

例如自动把一段 Mania 对象序列识别为：

```text
[1,3,2,4,1,3,2,4]
→ alternating stream
```

再生成：

```text
Music context
→ observed human pattern
```

于是后续可以训练成：

```text
P(pattern | music, ruleset, current_mapping, difficulty)
```

而不需要人工为每个 note 打标签。

---

# 13. 数据质量控制

Base Mapping AI 最重要的是质量，不是数据量。

建议建立 Map Quality Score：

```text
QualityScore =
    legality
  + timing_quality
  + playability
  + consistency
  + musicality
  + difficulty_integrity
  + human_signal
```

第一阶段优先训练/参考高质量、经过人工审核或具有较强社区质量信号的谱面。

同时保存 negative examples：

```text
good pattern
vs
bad pattern
```

这样未来非常适合 preference/ranking learning。

---

# 14. Validation Pipeline

生成一个候选谱面后，不应该只检查“能不能打开”。

至少分五层。

## Level 1：格式

- `.osu` valid
- object types valid
- timing valid

## Level 2：Ruleset legality

- column validity
- LN start/end
- object overlap legality
- slider constraints

## Level 3：Pattern validity

- broken transition
- unreasonable jack
- awkward movement
- excessive density spike
- accidental repeated pattern

## Level 4：Difficulty

- overall SR
- local SR
- strain spike
- target profile

## Level 5：Musicality

- onset alignment
- phrase alignment
- emphasis alignment
- repetition/variation consistency

前四层尽量 deterministic，第五层可以用模型/LLM/ranking model 辅助。

---

# 15. Difficulty Optimizer

建议把自动作图变成一个搜索问题。

例如：

```text
Target SR = 5.50

Candidate 1 → 4.92
Candidate 2 → 5.27
Candidate 3 → 5.61
Candidate 4 → 5.48
```

但不是简单选择绝对接近 5.50 的谱面，而是：

```text
Objective =
  SR error
+ musicality penalty
+ readability penalty
+ pattern quality penalty
+ difficulty profile penalty
```

这样不会为了追星级而生成明显不合理的 pattern。

可以采用：

- beam search
- candidate ranking
- local search
- simulated annealing（后期）

第一阶段推荐 beam search + deterministic mutation。

---

# 16. MVP 开发路线

## MVP-0：研究原型

目标：证明整个闭环可行。

范围：

- 仅 Mania 4K
- 仅 note/rice/stream/chord/LN 基础 pattern
- 仅歌曲级 BPM + beat + onset + energy
- LLM 做 planning
- deterministic pattern generator
- difficulty calculator
- 输出 `.osu`

验收：

> 对至少 10 首不同类型歌曲，系统可以生成结构完整、可打开、可游玩的 4K 谱面。

---

## MVP-1：Musical Timeline

加入：

- section segmentation
- phrase detection
- drum/vocal/bass activity
- energy curve

验收：

> AI 能解释每个主要段落“为什么使用这种 mapping strategy”。

---

## MVP-2：Difficulty Profile

加入：

- target SR
- density
- stamina
- rhythm complexity
- LN ratio
- readability

验收：

> 给定目标 4.5–6.0★，系统能通过多轮生成/修正使最终 SR 达到目标附近，同时不过度牺牲 mapping quality。

---

## MVP-3：Copilot

加入：

- next object suggestion
- next pattern suggestion
- 2-bar generation
- section recommendation
- accept/reject/edit

验收：

> 用户可以一边手工作图一边调用 AI，而不需要离开 Lazer。

---

## MVP-4：Standard

加入：

- 2D movement
- jump
- stream
- slider
- angle/distance constraints

重点难点是“音乐表达 + 空间移动”的统一，而不是简单复制 Mania architecture。

---

## MVP-5：完整四 ruleset

扩展：

- Taiko
- Catch
- Mania 5K–10K
- Standard

底层只新增：

- Pattern Grammar
- Renderer
- Validator
- Difficulty Adapter

不要改 Mapping IR。

---

# 17. Style Layer 版本

达到稳定 Base Mapping 后，再加入：

### Style V1

规则型 style profile。

### Style V2

从用户自己的历史谱面提取 style embedding。

### Style V3

Preference learning：

```text
AI candidate A
AI candidate B
↓
Mapper chooses B
↓
training preference pair
```

### Style V4

LoRA / adapter / small specialized model。

目标是：

```text
Base quality 不下降
          ↓
Style similarity 增加
```

这应该成为 Style Layer 的硬约束。

---

# 18. Copilot 的用户交互设计

不要让 AI 只能聊天。

推荐把建议设计成操作卡片：

```text
┌──────────────────────────────┐
│ AI Suggestion                │
│                              │
│ 1/16 alternating stream      │
│ 8 objects / 500 ms           │
│                              │
│ Reason                       │
│ ↑ energy + snare density     │
│                              │
│ [Insert] [Preview] [Modify]  │
└──────────────────────────────┘
```

同时支持：

```text
Accept
Reject
Regenerate
Make easier
Make harder
More LN
More rhythmic
More movement
More readable
```

这些按钮将来本身就是非常宝贵的 preference training signal。

---

# 19. MVP 技术栈建议

## Lazer Integration

- C#
- osu!lazer ruleset/editor APIs

官方文档明确把 `HitObjectComposer` 作为 ruleset editor 的核心组成部分，并允许针对 ruleset 实现专用编辑器逻辑。citeturn823616search1

## AI Service

建议第一阶段独立进程：

- Python
- FastAPI
- Pydantic
- PyTorch（仅在需要本地模型时）

原因：

- AI stack 和 Lazer C# 解耦
- 可以快速换模型
- 可以单独测试 Agent
- 将来可以本地模型 / 云端 API 二选一

## Data

- JSON/JSONL：训练与中间表示
- SQLite：MVP
- Parquet：大规模离线数据集

## Search / Retrieval

后期可以加入 vector DB，但 MVP 不必。

---

# 20. Repository 建议结构

```text
osu-ai-mapper/
├── docs/
│   ├── architecture.md
│   ├── mapping-ir.md
│   ├── pattern-grammar.md
│   └── evaluation.md
│
├── core/
│   ├── mapping_ir/
│   ├── music_events/
│   ├── difficulty_profile/
│   ├── patterns/
│   ├── validators/
│   └── optimization/
│
├── rulesets/
│   ├── mania/
│   ├── osu_standard/
│   ├── taiko/
│   └── catch/
│
├── agent/
│   ├── orchestrator/
│   ├── song_analyzer/
│   ├── planner/
│   ├── critic/
│   └── tools/
│
├── audio/
├── datasets/
├── evaluation/
├── services/
└── lazer-plugin/
```

---

# 21. 测试策略

每个 Pattern 都要有 unit test。

例如：

```text
StreamPatternTests
- timing correctness
- monotonic ordering
- no illegal overlap
- correct density
- alternating rule
```

另外建立 golden maps：

```text
Input music
+ fixed MappingPlan
= expected objects
```

Agent 层建立 snapshot tests：

```text
Given context
→ expected valid plan schema
```

不要测试 LLM 的具体语言文字，而测试：

- schema
- selected pattern
- parameters
- constraints
- tool calls

---

# 22. Evaluation：不要只看 SR

建议建立至少 7 个核心指标：

```text
1. Validity
2. Target SR error
3. Musical alignment
4. Pattern continuity
5. Readability/playability
6. Human preference
7. Generation stability
```

最终应该有一个离线 benchmark：

```text
100 songs × rulesets × target difficulties
```

然后固定比较：

```text
Rule baseline
LLM baseline
Hybrid Agent
Future trained model
```

这样才知道每一次架构升级到底有没有变好。

---

# 23. 最重要的里程碑

## Milestone 1

**“AI 可以解释为什么这样作图。”**

完成：Song Representation + Mapping IR + Planner。

## Milestone 2

**“AI 可以稳定生成一段合法 pattern。”**

完成：Pattern Grammar + Generator + Validator。

## Milestone 3

**“AI 可以完成一张达到指定 SR 的谱面。”**

完成：Difficulty Optimizer + Critic Loop。

## Milestone 4

**“AI 可以在 Lazer 中当作 Copilot 使用。”**

完成：Editor integration。

## Milestone 5

**“系统能从用户反馈中变得更像用户喜欢的作图风格。”**

完成：Preference / Style Layer。

---

# 24. 最容易失败的几个地方

## 失败点 1：直接 Audio → LLM → `.osu`

规避：必须使用 Mapping IR + deterministic renderer。

## 失败点 2：只追求 SR

规避：使用 Difficulty Profile + musicality/playability objective。

## 失败点 3：一开始训练大模型

规避：先建立 Pattern Grammar 与 evaluator，再决定哪里需要模型。

## 失败点 4：四种 ruleset 一起做到最深

规避：统一 IR，先把 Mania 4K 和 Standard 做深。

## 失败点 5：太早做 Style

规避：Base Mapping Quality 是硬约束；Style 是后置 layer。

## 失败点 6：只有自动生成，没有人机反馈

规避：Copilot 的 Accept/Reject/Edit 从 MVP-3 就开始记录。

---

# 25. 推荐的实际执行顺序

```text
Phase 0
项目范围 + IR
        ↓
Phase 1
Mania 4K Pattern Grammar
        ↓
Phase 2
Audio → Musical Timeline
        ↓
Phase 3
LLM Mapping Planner
        ↓
Phase 4
Deterministic Generator
        ↓
Phase 5
Difficulty Evaluator + Optimizer
        ↓
Phase 6
Auto Mapper MVP
        ↓
Phase 7
Lazer Copilot MVP
        ↓
Phase 8
Standard
        ↓
Phase 9
Taiko / Catch
        ↓
Phase 10
Preference / Style Layer
```

其中最推荐的开发顺序不是“先 AI”，而是：

```text
Representation
→ Pattern Grammar
→ Generator
→ Validator
→ Difficulty Loop
→ Agent
→ UI
→ Learning
```

原因是这样每一步都能独立测试，而且即使以后把 LLM 换成别的模型，核心 Mapping Engine 仍然成立。

---

# 26. 第一阶段具体交付物

第一阶段不要追求“完整 AI Mapper”。建议只交付以下东西：

1. `Mapping IR v0.1`
2. `MusicEvent schema v0.1`
3. `DifficultyProfile v0.1`
4. Mania 4K Pattern Grammar v0.1
5. 10–20 个确定性 Pattern Generator
6. Pattern Validator
7. osu! difficulty integration
8. 一个简单 Candidate Optimizer
9. 一个最小 LLM Planner
10. 生成 `.osu` 的 CLI
11. 10 首测试歌曲 benchmark
12. 自动化 evaluation report

第一阶段完成后，即使完全关闭 LLM，系统也应该仍然能够：

```text
Music Analysis
→ MappingPlan
→ Pattern Generation
→ Validation
→ Difficulty Calculation
→ .osu
```

这会成为整个项目的“地基”。

---

# 27. 最终形态

长期目标可以演化成：

```text
                        AI MAPPING SYSTEM
                                │
        ┌───────────────────────┼───────────────────────┐
        │                       │                       │
     Auto Mapper            AI Copilot           Map Analyzer
        │                       │                       │
        └───────────────────────┼───────────────────────┘
                                │
                        Base Mapping Model
                                │
                ┌───────────────┼───────────────┐
                │               │               │
             Music           Mapping         Difficulty
          Understanding     Grammar          Intelligence
                │               │               │
                └───────────────┼───────────────┘
                                │
                         Style Layer
                                │
             ┌──────────────────┼──────────────────┐
             │                  │                  │
          Mapper Style       User Style        Community Style
```

这里最关键的战略不是“训练一个很大的模型”，而是建立一个可计算的 Mapping Language：

```text
Music
→ Intent
→ Mapping Grammar
→ Pattern
→ Objects
→ Playability
→ Difficulty
→ Human Preference
```

一旦这套语言建立起来，LLM、传统 ML、small model、LoRA、RL/preference learning 都只是替换其中某些 decision maker，而不会推倒系统。

---

# 28. 当前建议的第一开发任务

正式开始编码之前，第一件事不是做 Agent，而是把以下三个 schema 定下来：

```text
1. MusicEvent
2. MappingIntent
3. PatternIntent
```

然后用 **Mania 4K** 做一个端到端最小闭环：

```text
一首歌
→ 识别 beat/onset/section
→ 产生 MappingIntent
→ 选择 1 个 Pattern
→ 生成 1–2 小节
→ difficulty/validity 检查
→ 导出 `.osu`
```

只要这个闭环跑通，后面的“完整自动作图”和“实时 Copilot”实际上是在扩大 context、增加 Pattern Grammar、加入 candidate search 和 Lazer integration，而不是重新发明系统。

---

## 29. 外部技术基础依据

- osu!lazer 的 ruleset editor 架构以 `HitObjectComposer` 为中心，并允许每个 ruleset 实现自己的 editor 组件，因此 AI 编辑器集成可以围绕现有 editor abstractions 构建。citeturn823616search1
- osu! 的通用 `DifficultyCalculator` 支持完整 difficulty calculation 和 timed/progressive calculation，可以直接作为生成系统的 evaluator。citeturn823616search0
- 当前 Mania ruleset 有独立的 `ManiaDifficultyCalculator`，其计算包括 Mania-specific strain 和 StarRating，并有 key-related ruleset configuration，因此 Mania 很适合作为第一阶段深度实现对象。citeturn823616search2turn823616search3
- osu-tools 的 difficulty/performance tooling 目前按各 ruleset 提供对应 difficulty calculators，可用于离线 benchmark 和自动化评估。citeturn823616search5
