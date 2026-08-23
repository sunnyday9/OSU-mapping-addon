我已经审计了你给出的 branch：



`sunnyday9/OSU-mapping-addon` → `feat/mvp-a-mapping-ir`



结论先说：



> \*\*这条 branch 已经完成了“Mapping IR v0.1 的大部分数据模型和最小可运行闭环”，但还不能认为已经完整实现了我们前面定义的 Mapping IR v0.1 + Mapping Intelligence 架构。\*\*

>

> 如果按我们之前的设计来验收，我会给目前的状态大约 \*\*65%～75% 完成度\*\*。

>

> 更准确地说：

>

> \*\*IR 数据结构：✅ 基本完成\*\*

>

> \*\*IR 序列化/测试：✅ 基本完成\*\*

>

> \*\*Semantic → Pattern → Concrete 的管线：✅ 已有 MVP\*\*

>

> \*\*Mania 4K Pattern Engine：✅ 有明显实现\*\*

>

> \*\*Music → Mapping Intelligence：⚠️ 还是规则型 placeholder / MVP\*\*

>

> \*\*LLM/Candidate Ranking/Evidence：❌ 尚未实现\*\*

>

> \*\*Standard Pattern Grammar/Provider：❌ 目前 Mapping IR 管线仍硬编码 Mania\*\*

>

> \*\*严格的 canonical JSON Schema：⚠️ 我没有在这个 branch 中找到对应的 schema 文件\*\*

>

> \*\*完整四 ruleset 可扩展架构：⚠️ 接口层已经有，实际 pipeline 尚未真正解耦\*\*



\---



\# 一、先看最重要的：Mapping IR v0.1 本体



这一部分其实做得相当不错。



Branch 中已经建立了专门的：



```text

src/AiStudio.Core/MappingIr/

```



下面进一步分成：



```text

Analysis

Model

Patterns

Planning

Rendering

Serialization

Timeline

Validation

```



这个模块划分和我们前面讨论的架构基本一致。



而且顶层 `MappingDocument` 已经包含：



```text

schema

version

document\_id

map

ruleset

difficulty\_profile

music\_timeline

mapping\_plan

concrete\_objects

constraints

style

provenance

evaluation

```



这和我们定义的 v0.1 顶层结构高度一致。



\### 这一项：✅ 基本通过



\---



\# 二、Semantic IR / Rendered IR 分离



这个部分也已经基本落实。



现在有：



```text

MusicTimeline

MappingIntent

PatternIntent

PatternTransition

```



以及：



```text

ConcreteObject

```



这实际上已经形成：



```text

Semantic IR

&#x20;   ↓

Rendered IR

```



这一点非常重要，而且代码结构是对的。



例如：



```csharp

MappingPlan

{

&#x20;   Intents

&#x20;   Patterns

&#x20;   Transitions

}

```



然后另外存在：



```csharp

IReadOnlyList<ConcreteObject>

```



说明作者没有直接让 planner 产生 `.osu` object，这一点符合我们之前的核心设计。



\### 这一项：✅ 通过



\---



\# 三、Difficulty Profile



这部分完成度很高。



现在已经有：



```text

TargetStarRating

DimensionProfile

DifficultyPreferences

Tolerance

```



其中 dimension 已经有：



```text

density

rhythm\_complexity

reading

stamina

technicality

movement

ln\_complexity

```



这正是我们之前建议的：



> Star Rating 是目标约束，而不是 mapping strategy。



代码也明确把 target SR 和 orthogonal dimensions 分开了。



\### 这一项：✅ 通过



\---



\# 四、MusicTimeline：结构存在，但内容远远没有达到我们设计的水平



这是目前最大的差距之一。



现在已经有：



```text

Timeline

&#x20;├─ Section

&#x20;├─ Phrase

&#x20;└─ MusicEvent

```



这一点在结构上是正确的。



但是当前 `MusicTimelineBuilder` 的实现非常 MVP。



它主要做的是：



```text

BeatGrid

\+

AudioSection

↓

Section

↓

每个 Section 一个 Phrase

↓

根据 BeatGrid 生成 MusicEvent

```



具体来说，当前 `MusicEvent` 基本只来自：



```text

Beat

Onset

```



而不是我们规划的：



```text

kick

snare

hihat

bass

vocal

melody

chord

accent

...

```



现有 builder 的 event 生成逻辑实际上是：



> 每个 beat 生成一个 event；小节第一拍标成 onset，其余标成 beat。



而且：



```text

每一个 section → 一个 phrase

```



是目前的 MVP 策略。



这意味着现在它\*\*还没有真正实现我们之前设计的 Musical Representation\*\*。



目前实际上更像：



```text

Audio

&#x20;↓

BPM

&#x20;↓

BeatGrid

&#x20;↓

Section

&#x20;↓

Beat Events

```



而我们真正想要的是：



```text

Audio

&#x20;↓

Acoustic Features

&#x20;↓

Music Representation

&#x20;↓

Section

&#x20;↓

Phrase

&#x20;↓

Music Events

&#x20;    ├─ kick

&#x20;    ├─ snare

&#x20;    ├─ vocal

&#x20;    ├─ bass

&#x20;    ├─ melody

&#x20;    └─ accent

```



\### 这一项：



\*\*结构：✅\*\*



\*\*语义内容：⚠️ 约 30% 完成\*\*



\---



\# 五、Audio Analyzer 还没有达到我们设计的 Music Intelligence



目前接口是：



```csharp

AnalyseBeatAsync()

AnalyseSectionsAsync()

```



输出：



```text

BeatGrid

AudioSection

```



并且当前描述依旧明确写着：



> BASS + spectral flux，段落使用 energy envelope / onset density。



这在“无模型 MVP”阶段完全合理。



但是还没有我们后面设计的：



```text

instrument activity

vocal activity

pitch contour

harmonic change

spectral embeddings

MERT

music embeddings

rhythmic role

```



所以：



\### Mapping Intelligence 的“输入端”还没有完成。



\---



\# 六、MappingIntent：结构正确，但是“智能”还是 rule-based



现在 `MappingIntent` 已经有：



```text

primary

secondary

musical\_targets

emphasis

complexity

confidence

continuity

rationale

```



这一点很好。



尤其：



```text

rationale

confidence

continuity

```



非常符合我们之前强调的可解释性。



所以 IR schema 这一层是做对的。



但是现在真正决定它的是：



```csharp

primaryIntentFor(...)

```



逻辑基本上是：



```text

Intro → Establish/Escalation

Chorus → Climax

Bridge → DeEscalation

Verse → Repeat/Variation

Outro → Resolution

```



然后 `emphasis` 基本由：



```text

section.energy

\+

difficulty profile

```



计算。



所以当前实际上：



```text

MappingIntent

=

Rules

\+

Section Type

\+

Energy

\+

Difficulty Profile

```



而不是我们设计的：



```text

MappingEvidence

\+

Global Mapping Plan

\+

Local Context

\+

Previous Pattern

\+

Next Phrase

\+

Difficulty Profile

\+

Candidate Ranking

```



\### 这一项：



\*\*IR 数据结构：✅\*\*



\*\*Mapping Intelligence：⚠️ 低完成度\*\*



\---



\# 七、目前还没有 MappingEvidence Layer



这是我认为现在应该重点补的一个结构。



我们之前设计了：



```text

Music Representation

&#x20;       ↓

Mapping Evidence

&#x20;       ↓

Mapping Intent

```



例如：



```text

rhythm\_evidence

accent\_evidence

energy\_evidence

vocal\_evidence

movement\_evidence

density\_evidence

climax\_evidence

```



但 branch 目前没有这个独立概念。



现在基本直接：



```text

AudioSection

↓

MappingIntent

```



这个中间层缺失。



\### 这一项：❌ 缺失



而且我认为这个不是“以后再说”的小功能。



它应该成为 Base Mapping AI 的核心可解释层。



\---



\# 八、Global Mapping Planner：目前还没有真正实现



当前 planner 是：



```csharp

for each section

&#x20;   createIntent()

&#x20;   createPattern()

&#x20;   createTransition()

```



也就是说：



```text

Section-by-section

```



进行规划。



我们之前设计的是：



```text

Pass 1: Global Planner



整个歌曲

↓

difficulty curve

mapping arc

section roles

global climax

global contrast



Pass 2: Local Planner



每个 phrase

↓

MappingIntent

↓

Pattern

```



目前没有：



```text

DifficultyCurve

MappingArc

Global mapping budget

```



也没有：



```text

Future-aware planning

```



例如：



> 当前 Chorus 已经很强，但 final chorus 更强，所以当前应该保留余量。



现在的 planner 没有这个概念。



\### 这一项：❌ 尚未实现



\---



\# 九、PatternIntent：基本完成，而且 interface 很合理



这一块做得不错。



现在：



```csharp

PatternIntent

```



确实是：



```text

Ruleset

Family

StartTime

EndTime

Parameters

Constraints

Confidence

TransitionIn

TransitionOut

Rationale

```



这与我们的设计非常接近。



而且还做了：



```csharp

IPatternProvider

```



接口：



```csharp

PatternGenerationResult Generate(

&#x20;   PatternIntent intent,

&#x20;   PatternGenerationContext context);

```



这个抽象非常适合作为：



```text

ManiaPatternProvider

OsuPatternProvider

TaikoPatternProvider

CatchPatternProvider

```



的基础。



\### 这一项：✅ 架构通过



\---



\# 十、但是 Pattern Engine 目前实际上是 Mania-only



这是目前非常明显的一个架构问题。



虽然：



```text

RulesetKind

```



支持：



```text

Osu

Taiko

Catch

Mania

```



而：



```text

IPatternProvider

```



也是 ruleset-agnostic。



但是：



\### `MappingIrPipeline` 是直接写死：



```csharp

this.provider = provider ?? new Mania4KPatternProvider();

```



并且最终 renderer 也是：



```csharp

ManiaOsuRenderer

```



所以当前真实数据流是：



```text

Mapping IR

&#x20;     ↓

Mania 4K

&#x20;     ↓

Mania renderer

```



而不是：



```text

Mapping IR

&#x20;     ↓

RulesetProvider

&#x20;     ↓

Osu / Mania / Taiko / Catch

```



\---



\# 十一、Validator 也写死 Mania 4K



这个更加明显。



`MappingValidator` 中：



```csharp

if ruleset != Mania

&#x20;   warning



if keys != 4

&#x20;   error

```



并且 concrete object validation 明确就是：



```text

column 0–3

hold

LN

```



也就是说：



> \*\*Validator 的真实能力是 Mania 4K validator，而不是通用 Mapping IR validator。\*\*



所以：



\### 四 ruleset 的 IR 接口：✅



\### 四 ruleset 的 IR 实际实现：❌



\---



\# 十二、Mania 4K Pattern Provider：已经有很不错的 MVP



这里要给一个肯定。



现在已经真正实现了：



```text

single

stream

burst

jack

jump

jumpstream

single\_ln

ln\_rice

ln\_release

```



这是一个实际可运行的 Pattern Provider，而不是空接口。



同时：



```text

beat grid

subdivision

column order

chord

LN duration

LN ratio

```



也已经有参数化处理。



这已经达到：



> \*\*“可以开始做真正的 Pattern Generator MVP”\*\*



的程度。



\### 这一项：✅ MVP 通过



\---



\# 十三、但它还不是我们定义的完整 Mania Grammar



我们之前规划过：



```text

stream

burst

jack

jumpstream

handstream

chordstream

anchor

...

```



当前 provider 暂时没有：



```text

handstream

chordstream

anchor

```



也就是说：



```text

Grammar spec

>

Provider implementation

```



这是正常的 MVP 状态，但不能称为完整 grammar implementation。



\---



\# 十四、还有一个很重要的潜在 Bug：随机数“确定性”其实不完全成立



这个我建议你马上修。



现在：



```csharp

context.CreateFamilyRandom(...)

```



使用：



```csharp

Seed ^ family.GetHashCode()

```



问题是：



> `.NET string.GetHashCode()` 不应该被当作跨进程、跨运行稳定的 hash。



所以：



```text

同 seed

\+

同 input

```



\*\*不保证跨进程一定生成完全相同结果。\*\*



这与代码注释中声称的：



> fixed seed + fixed input → deterministic



是不完全一致的。



应该改成一个明确稳定的 hash，例如：



```text

SHA-256

FNV-1a

xxHash

自定义 stable string hash

```



然后：



```text

derivedSeed = StableHash(family + seed)

```



\### 这是我认为目前一个真正的工程缺陷，而不只是 feature gap。



\---



\# 十五、还有一个 JSON IR 的潜在问题



`PatternIntent.Parameters` 现在是：



```csharp

IReadOnlyDictionary<string, object?>

```



这对于开放式 schema 很灵活。



但是在 JSON 反序列化之后，数组值通常会进入 `JsonElement` 表示。



而 `Mania4KPatternProvider` 对：



```csharp

column\_order

```



的读取方式是：



```csharp

value is object\[] arr

```



与此同时自定义 converter 目前只处理：



```text

null → empty list

null → empty dictionary

enum

```



没有处理 `object` nested JSON values 的强类型化。



所以存在这样的风险：



```text

C# 构建 PatternIntent

→ 正常



JSON serialize

→ deserialize

→ column\_order = JsonElement



PatternProvider

→ `value is object\[]` 为 false

→ silently fallback

```



换句话说：



> \*\*IR 在内存中和 IR 从 JSON 文件恢复后，可能不是完全等价的。\*\*



你们已经写了 round-trip test，但这个 test 目前主要验证：



```text

document

mapping\_plan count

objects count

```



并没有真正验证：



```text

Parameters.column\_order

Parameters.subdivision

Constraints

nested arrays

```



这个建议马上增加。



\---



\# 十六、Schema 文件本身：我没有看到 canonical schema



我在这个 branch 的目录结构和文件搜索中没有找到：



```text

mapping-ir-v0.1.schema.json

```



目前看起来主要依赖：



```text

C# types

\+

tests

\+

docs

```



来定义协议。



这意味着：



> 目前还没有一个真正的、独立于 C# 的 canonical JSON Schema 作为“协议真相源”。



而我们之前设计的目标其实是：



```text

mapping-ir-v0.1.schema.json

&#x20;         ↓

C# types

&#x20;         ↓

LLM schema

&#x20;         ↓

Python dataset

&#x20;         ↓

validators

```



所以这里我会建议：



\### ❌ 还没有完全满足 v0.1 protocol requirement



\---



\# 十七、当前测试做得怎么样？



测试组织是不错的。



已经有：



```text

DeterministicMappingPlannerTests

JsonMappingIrSerializerTests

Mania4KPatternProviderTests

MappingValidatorTests

TimelineAndRenderingTests

```



这说明作者已经意识到 IR 是需要独立测试的，不是只测 UI。



但缺的几个非常重要：



```text

JSON Schema validation

JSON → IR → JSON semantic equivalence

Pattern parameter round-trip

Transition consistency

Cross-ruleset IR validation

MappingEvidence tests

Difficulty profile influence tests

Global planner tests

```



尤其：



> \*\*没有 canonical schema validation，就很难真正说“Mapping IR v0.1 已经实现”。\*\*



\---



\# 十八、Auto Mapper pipeline：MVP 闭环已经存在



这是目前 branch 一个很不错的地方。



它已经有：



```text

Audio

&#x20;↓

Audio Analyzer

&#x20;↓

Timeline

&#x20;↓

Mapping Planner

&#x20;↓

Pattern Provider

&#x20;↓

Concrete Objects

&#x20;↓

Validator

&#x20;↓

.osu Renderer

```



这说明：



> \*\*不是只有 data model，是真的已经可以串成一个 prototype。\*\*



所以从工程角度，这是一个很好的基础。



\---



\# 十九、但是它和我们最新设计的 AI Agent Architecture 还差很远



目前：



```text

IAudioAnalyzer

DeterministicMappingPlanner

Mania4KPatternProvider

MappingValidator

ManiaOsuRenderer

```



而我们的目标是：



```text

Song Analyst

Global Planner

Local Planner

Mapping Evidence

Candidate Generator

LLM Planner

Candidate Ranker

Critic

Difficulty Optimizer

Copilot Advisor

```



这些大部分还没有。



特别是：



\### 没有 LLM



而且这是\*\*可以接受的\*\*，因为我们之前本来就决定：



> 第一版可以不训练、不接 LLM，先跑通 deterministic baseline。



所以：



> \*\*没有 LLM 本身不是问题。\*\*



真正的问题是：



> \*\*当前 deterministic baseline 应该被明确设计成“Baseline Model”，而不是最终 Mapping Intelligence。\*\*



现在的代码已经通过 `IMappingPlanner` 给未来替换留下了接口，这一点是正确的。



\---



\# 二十、我给这条 branch 的实际状态评分



| 模块                        | 状态                    | 完成度 |

| ------------------------- | --------------------- | --: |

| MappingDocument           | ✅                     | 95% |

| Ruleset abstraction       | ✅                     | 80% |

| DifficultyProfile         | ✅                     | 90% |

| MusicTimeline schema      | ✅                     | 85% |

| MusicTimeline 内容质量        | ⚠️                    | 35% |

| MappingIntent schema      | ✅                     | 90% |

| MappingEvidence           | ❌                     |  0% |

| PatternIntent             | ✅                     | 90% |

| PatternTransition         | ✅                     | 75% |

| ConcreteObject            | ✅                     | 85% |

| Provenance                | ✅                     | 80% |

| Evaluation                | ✅                     | 70% |

| Serialization             | ✅                     | 80% |

| Canonical JSON Schema     | ❌/⚠️                  | 20% |

| Mania 4K generator        | ✅                     | 75% |

| Standard generator        | ❌（就这套 IR pipeline 而言） | 10% |

| Taiko/Catch generator     | ❌                     |  0% |

| Mapping Validator         | ✅ Mania 4K MVP        | 65% |

| Music → Mapping reasoning | ⚠️                    | 25% |

| Global Mapping Planner    | ❌                     | 10% |

| Candidate Ranking         | ❌                     |  0% |

| Critic                    | ❌                     |  0% |

| Difficulty feedback       | ❌/⚠️                  | 15% |

| Copilot context           | ❌                     | 10% |

| Style Layer               | ⚠️ schema 有           | 15% |



\---



\# 二十一、所以我的总体判断



\### 如果你的目标是：



> \*\*“完成一个 Mapping IR v0.1 的第一版工程骨架 + Mania 4K prototype”\*\*



那么我会说：



> \*\*✅ 已经基本完成，而且结构相当不错。\*\*



尤其这些地方我认为是正确方向：



```text

AiStudio.Core

&#x20;   ↓

MappingIr

&#x20;   ↓

Model

Planning

Patterns

Validation

Serialization

Timeline

Rendering

```



以及：



```text

IMappingPlanner

IPatternProvider

IMappingValidator

```



这些接口都为以后替换模型留下了位置。



\---



\### 但如果你的目标是：



> \*\*“完全实现我们前面设计的 Mapping IR v0.1 + Mapping Intelligence v0.1”\*\*



那么：



> \*\*❌ 还没有完成。\*\*



最主要不是少几个字段，而是\*\*AI 决策链还没有建立\*\*。



\---



\# 二十二、我认为现在最应该修的 8 件事



我会按优先级排序：



\### P0 — Canonical Schema



把：



```text

mapping-ir-v0.1.schema.json

```



真正放进 repo。



然后：



```text

JSON

↓

Schema Validator

↓

C# serializer

```



三者 CI 强制一致。



\---



\### P0 — 修复 Deterministic Seed



不要再用：



```csharp

string.GetHashCode()

```



改为稳定 hash。



否则你以后做：



```text

reproducibility

dataset

AI debugging

```



会很痛苦。



\---



\### P0 — 修 JSON nested parameter types



把：



```text

Dictionary<string, object?>

```



逐步替换成更结构化的：



```text

PatternParameters

PatternConstraints

```



至少 Mania 先强类型化：



```text

ManiaPatternParameters

```



否则你以后从 LLM JSON → IR → Pattern Generator 时会不断踩 `JsonElement` 问题。



\---



\### P1 — 加 MappingEvidence



新增：



```text

MappingEvidence

```



并把：



```text

MusicTimeline

&#x20;→ Evidence

&#x20;→ MappingIntent

```



固定下来。



这是未来真正接 AI 的关键。



\---



\### P1 — 把 Global Planner 和 Local Planner 分开



现在：



```text

section

→ intent

→ pattern

```



太短了。



应该变成：



```text

Song

→ Global Mapping Plan

→ Section Plan

→ Phrase Plan

→ Pattern Plan

```



\---



\### P1 — 把 Difficulty Calculator 接入 IR Loop



现在 pipeline 的 evaluation 基本是：



```text

object\_count

duration

alignment

```



而我们之前设计的是：



```text

Map

&#x20;↓

osu DifficultyCalculator

&#x20;↓

actual SR

&#x20;↓

target SR

&#x20;↓

revision

```



这才是真正的 difficulty-controlled generator。



\---



\### P1 — 把 Ruleset 彻底从 Pipeline 中解耦



现在：



```csharp

Mania4KPatternProvider

ManiaOsuRenderer

ManiaValidator

```



都是硬编码。



应该变成：



```text

IR

&#x20;↓

IRulesetMappingBackend

&#x20;    ├─ Osu

&#x20;    ├─ Mania

&#x20;    ├─ Taiko

&#x20;    └─ Catch

```



这样以后才真正是四 ruleset architecture。



\---



\### P2 — Candidate Ranking



等上面完成后，再加入：



```text

Candidate A

Candidate B

Candidate C

↓

Score

```



第一版甚至不需要 ML：



```text

music alignment

\+

difficulty fit

\+

continuity

\+

readability

```



就足够。



\---



\# 二十三、我尤其建议不要马上往这个 branch 加 LLM



这是这次审查之后我最明确的建议。



现在最正确的路线不是：



```text

LLM

```



而是：



```text

完善 Deterministic Baseline

&#x20;       ↓

MappingEvidence

&#x20;       ↓

Candidate generation

&#x20;       ↓

Scoring

&#x20;       ↓

Difficulty feedback

&#x20;       ↓

再接 LLM

```



否则你会得到：



```text

LLM

&#x20;↓

一个目前还没有完全定义清楚的 IR

&#x20;↓

不稳定 Pattern Generator

```



最后会很难知道问题到底出在哪里。



\---



\# 二十四、一个特别重要的现实问题



这个 branch 的 `README/PLAN` 与我们当前讨论的项目路线已经出现明显漂移。



例如 README 仍然写着：



```text

M3 生成 v2

M4–M7 pending

osu! 标准为当前重点

```



但 branch 实际上已经出现：



```text

AiStudio.Core.MappingIr

Mania4KPatternProvider

Mapping IR pipeline

```



同时 `PLAN.md` 本身又描述了一套更早的四模式生成路线。



所以现在非常值得做一次：



> \*\*Architecture / PLAN / README / code 的统一\*\*



否则后面很容易发生：



```text

文档说 A

IR 说 B

generator 说 C

UI 说 D

```



\---



\# 结论



我现在会把这个 branch 定义为：



> \*\*Mapping IR v0.1 Core MVP：基本成型，但还不是完整的 Mapping Intelligence v0.1。\*\*



最值得肯定的是，\*\*底层骨架已经基本走在正确道路上\*\*：



```text

Audio

&#x20;↓

Timeline

&#x20;↓

MappingPlan

&#x20;↓

PatternIntent

&#x20;↓

ConcreteObject

&#x20;↓

Validator

&#x20;↓

Renderer

```



而且已经通过接口把 Planner / Pattern Provider / Validator 解耦。



但现在最大的缺口是：



```text

&#x20;               当前 branch

&#x20;                    │

Audio ──→ Section ──→ Rule-based Mapping

&#x20;                    │

&#x20;                    ↓

&#x20;                 Pattern

```



我们真正想要的是：



```text

Audio

&#x20;↓

Music Representation

&#x20;↓

Mapping Evidence

&#x20;↓

Global Mapping Plan

&#x20;↓

Local Mapping Intent

&#x20;↓

Candidate Patterns

&#x20;↓

Ranker / LLM

&#x20;↓

Pattern Generator

&#x20;↓

Difficulty Feedback

&#x20;↓

Critic

&#x20;↓

Revision

```



\*\*所以我不建议推翻这个 branch。相反，我认为它已经是一个很好的 MAPPING-IR foundation，应该在它上面继续迭代。\*\*



另外，我尝试通过本地 `git` 拉取 branch 做一次真实 `dotnet test/build` 验证，但当前执行环境无法解析 `github.com`，因此这次结论基于 branch 实际源码、测试文件和 workflow 配置审计，\*\*没有冒充说我已经本地编译通过\*\*。



如果你接下来让我继续，我最建议的下一步就是：\*\*直接针对这个 branch，给出一份“从当前代码 → 我们目标架构”的逐文件改造计划（P0/P1/P2），具体指出每个现有文件该保留、修改、拆分还是新增什么，以及优先修掉上面提到的 `GetHashCode` 和 JSON `object/JsonElement` 问题。\*\*



