# osu! AI Mapper — Mapping Intelligence Specification v0.1

> Status: Draft / Architecture Baseline
> Version: 0.1.0
> Date: 2026-08-23
> Companion specification: `osu-ai-mapping-ir` Mapping IR v0.1
>
> This specification defines how the Mapping Intelligence Layer transforms music evidence, mapping context, difficulty requirements, and optional style information into a structured mapping plan. It is intentionally designed so that the first implementation can run without a trained model or LLM, while later implementations can replace individual decision components with learned models or agentic reasoning without changing the Mapping IR contract.

---

## 1. Purpose

The Mapping Intelligence Layer answers one question:

> Given a song, a target ruleset, a difficulty profile, and the current mapping context, what mapping decision should be made next, and why?

It is explicitly **not** the same thing as the Pattern Renderer.

The intelligence layer selects and explains mapping intent and pattern candidates. Deterministic downstream components are responsible for turning those decisions into concrete hit objects and validating them.

The intended architecture is:

```text
Audio
  ↓
Music Representation
  ↓
Mapping Evidence
  ↓
Global Mapping Plan
  ↓
Local Mapping Intent
  ↓
Pattern Candidate Generation
  ↓
Candidate Scoring / Ranking
  ↓
PatternIntent
  ↓
Deterministic Pattern Provider
  ↓
ConcreteObjects
  ↓
Ruleset Validation + Difficulty Evaluation
  ↓
Critic / Revision
```

The same intelligence stack must support both product modes:

1. **Auto Mapper** — generates a complete map through repeated planning, generation, evaluation, and revision.
2. **AI Copilot** — exposes the same decisions at macro, meso, and micro granularity while the mapper remains in control.

---

## 2. Design Principles

### 2.1 Semantic planning before object generation

The system must not require an LLM or neural model to directly emit `.osu` objects. Mapping decisions are represented semantically first.

### 2.2 Deterministic rendering

Given the same `PatternIntent`, mapping context, generator version, and stable seed, the renderer should produce the same concrete objects.

### 2.3 Explainability

Every non-trivial mapping decision should be traceable to evidence, constraints, or explicit user/style preferences.

### 2.4 Ruleset separation

The intelligence layer has a shared semantic vocabulary, but Pattern Families and rendering constraints remain ruleset-specific.

### 2.5 Difficulty as a constraint, not as the whole objective

Star Rating is a target outcome. `DifficultyProfile` describes how that difficulty should be achieved.

### 2.6 Music alignment is first-class

A pattern that is mechanically valid but poorly related to the music is not considered a high-quality mapping candidate.

### 2.7 Future style conditioning

The Base Intelligence Layer must be useful with no style information. A future Style Layer may bias candidate generation or ranking without replacing the Base Intelligence contract.

### 2.8 Model optionality

No v0.1 component may require a proprietary LLM or a custom-trained neural model. Learned components are optional replacements behind stable interfaces.

---

## 3. Scope

### 3.1 In scope

- Audio-derived beat and musical evidence.
- Section and phrase level structure.
- Mapping Evidence representation.
- Global mapping planning.
- Local mapping intent planning.
- Candidate pattern generation.
- Candidate ranking.
- Pattern transition planning.
- Difficulty-aware decision making.
- Deterministic baseline implementation.
- Optional LLM integration contract.
- Optional learned ranking model contract.
- Critic and revision loop.
- Auto Mapper and Copilot usage of the same intelligence layer.
- Provenance and decision logging for future preference learning.

### 3.2 Out of scope for v0.1

- End-to-end neural `Audio → .osu` generation.
- Training a foundation model from scratch.
- Full style imitation of arbitrary mappers.
- Fully autonomous reinforcement learning.
- Perfect automatic vocal transcription or source separation.
- Human-quality subjective judgement replacement.
- Guaranteed ranked-eligibility or ranked submission.

---

## 4. Relationship to Mapping IR v0.1

Mapping Intelligence consumes and produces the semantic portion of Mapping IR.

### 4.1 Input objects

```text
MappingDocument
├── RulesetInfo
├── DifficultyProfile
├── MusicTimeline
├── MappingPlan (existing context)
├── Constraints
└── StyleProfile (optional)
```

### 4.2 Intelligence-specific intermediate objects

```text
AcousticEvidence
MusicRepresentation
MappingEvidence
GlobalMappingPlan
PatternCandidate
DecisionTrace
CriticReport
```

### 4.3 Output objects

```text
MappingIntent
PatternIntent
PatternTransition
Provenance / DecisionTrace
```

The canonical relationship is:

```text
MusicEvent
   ↓
MappingEvidence
   ↓
MappingIntent
   ↓
PatternCandidate[]
   ↓
PatternIntent
   ↓
PatternTransition
```

---

## 5. High-Level Architecture

```text
┌─────────────────────────────────────────────────────┐
│                  Input / Context                    │
│ Audio + Existing Map + Ruleset + Difficulty Profile│
└──────────────────────────────┬──────────────────────┘
                               ↓
┌─────────────────────────────────────────────────────┐
│               Music Understanding                   │
│ Beat / onset / energy / instrument / embedding      │
└──────────────────────────────┬──────────────────────┘
                               ↓
┌─────────────────────────────────────────────────────┐
│               Structure Analysis                    │
│ Song → Section → Phrase → MusicEvent                │
└──────────────────────────────┬──────────────────────┘
                               ↓
┌─────────────────────────────────────────────────────┐
│               Mapping Evidence                      │
│ rhythm / accent / density / movement / climax / ... │
└──────────────────────────────┬──────────────────────┘
                               ↓
┌──────────────────────────────┴──────────────────────┐
│                                                     │
│                 Global Planner                      │
│                                                     │
└──────────────────────────────┬──────────────────────┘
                               ↓
                         Global Plan
                               ↓
┌─────────────────────────────────────────────────────┐
│                  Local Planner                       │
│ context window + previous/next patterns + evidence   │
└──────────────────────────────┬──────────────────────┘
                               ↓
                    MappingIntent
                               ↓
┌─────────────────────────────────────────────────────┐
│              Candidate Generation                   │
│ Pattern Library + Rules + optional LLM              │
└──────────────────────────────┬──────────────────────┘
                               ↓
                    PatternCandidate[]
                               ↓
┌─────────────────────────────────────────────────────┐
│                Candidate Ranking                     │
│ music + difficulty + continuity + readability       │
│ + style + validity                                  │
└──────────────────────────────┬──────────────────────┘
                               ↓
                        PatternIntent
                               ↓
                   Deterministic Provider
                               ↓
                     ConcreteObjects
                               ↓
┌─────────────────────────────────────────────────────┐
│                 Evaluation Layer                     │
│ ruleset validation + difficulty + music alignment    │
└──────────────────────────────┬──────────────────────┘
                               ↓
                         Critic / QA
                               ↓
                       Revision Loop
```

---

## 6. Music Representation Contract

Mapping Intelligence must not directly depend on raw third-party analyzer structures.

A shared internal representation is required.

### 6.1 AcousticFrame

Minimum logical fields:

```text
TimeMs
BeatStrength
OnsetStrength
Energy
SpectralFlux
LowEnergy
MidEnergy
HighEnergy
InstrumentActivity
Embedding (optional)
Confidence
```

`Embedding` is optional in v0.1 and must never be required for the baseline implementation.

### 6.2 MusicRepresentationFrame

Derived semantic representation:

```text
TimeMs
RhythmicRole
DominantActivities[]
EnergyLevel
MelodicDirection
HarmonicChange
AccentStrength
PhraseId
SectionId
EmbeddingReference (optional)
Confidence
```

### 6.3 MusicEvent

The existing Mapping IR event vocabulary remains authoritative. The intelligence implementation should progressively populate meaningful event types such as:

```text
beat
onset
kick
snare
hihat
percussion
bass
chord
vocal
vocal_phrase
melody
accent
silence
transition
```

Beat-only synthetic events are acceptable for the first deterministic baseline but are not considered sufficient for full Intelligence v0.1 quality.

---

## 7. Mapping Evidence

`MappingEvidence` is the critical bridge between music analysis and mapping decisions.

### 7.1 Purpose

Evidence answers:

> What properties of the music or current map support a mapping decision?

It must be possible for a critic or UI to inspect evidence independently of the final decision.

### 7.2 Recommended shape

```json
{
  "id": "evidence_001",
  "start_time": 32000,
  "end_time": 34000,
  "rhythm": 0.92,
  "accent": 0.84,
  "energy": 0.91,
  "vocal": 0.88,
  "movement": 0.41,
  "density": 0.77,
  "repetition": 0.61,
  "climax": 0.89,
  "novelty": 0.35,
  "beat_confidence": 0.96,
  "confidence": 0.87,
  "sources": [
    "audio.onset",
    "audio.energy",
    "structure.chorus",
    "music.vocal_phrase"
  ]
}
```

### 7.3 Evidence is not intent

Do not collapse the following:

```text
rhythm evidence = 0.92
```

into:

```text
use stream
```

Evidence constrains and informs the planner; it does not dictate a pattern by itself.

---

## 8. Mapping Decision Model

A mapping decision is a function of:

```text
Decision = f(
    MusicEvidence,
    GlobalPlan,
    LocalContext,
    Ruleset,
    DifficultyProfile,
    CurrentMapping,
    Constraints,
    StyleProfile(optional)
)
```

### 8.1 Required context dimensions

#### Music context

- Current section.
- Current phrase.
- Relevant MusicEvents.
- Local energy.
- Rhythm structure.
- Accent structure.
- Instrument / vocal evidence where available.

#### Mapping context

- Previous 1–4 patterns.
- Current pattern state.
- Next planned section/phrase.
- Existing concrete objects near the decision.
- Existing mapping intent.

#### Difficulty context

- Target SR.
- Local difficulty budget.
- Difficulty dimensions.
- Current measured difficulty where available.

#### Style context

- Optional.
- Never required for Base Intelligence.

---

## 9. Global Mapping Planner

The Global Planner operates before local pattern generation.

### 9.1 Goal

Create a high-level mapping arc for the song so local decisions are aware of the whole song.

### 9.2 Outputs

The global planner should produce:

```text
GlobalMappingPlan
├── DifficultyCurve
├── MappingComplexityCurve
├── SectionPlans[]
├── GlobalClimax
├── ContrastPoints[]
└── PatternBudget / DensityBudget
```

### 9.3 DifficultyCurve

Represents intended difficulty progression over time.

Example:

```json
[
  { "time": 0, "target": 1.5 },
  { "time": 30000, "target": 3.0 },
  { "time": 60000, "target": 4.5 },
  { "time": 90000, "target": 5.5 }
]
```

This is an internal planning target, not the official SR calculation.

### 9.4 MappingComplexityCurve

Should separately track:

```text
density
rhythm_complexity
pattern_complexity
movement
reading
LN_complexity
technicality
```

### 9.5 Global Climax

The planner should identify one or more candidate climaxes and assign relative strength.

The local planner must not independently maximize every high-energy section.

### 9.6 Baseline implementation

The first implementation may derive the plan deterministically from section energy, section type, target SR, and difficulty profile.

It must be implemented behind:

```csharp
interface IGlobalMappingPlanner
```

so that later learned or LLM planners can replace it.

---

## 10. Local Mapping Planner

The Local Planner turns one phrase or phrase group into `MappingIntent`.

### 10.1 Context window

Minimum recommended window:

```text
Previous phrase
Current phrase
Next phrase
Current section
Previous 1–4 mapping patterns
Current difficulty budget
Global mapping plan
```

### 10.2 Primary intents

The v0.1 vocabulary is:

```text
establish
repeat
variation
escalation
release
climax
de_escalation
contrast
transition
accent
silence
anticipation
resolution
```

### 10.3 Required intent fields

The Mapping IR v0.1 `MappingIntent` should include:

```text
primary
secondary[]
musical_targets[]
emphasis
complexity
confidence
continuity
rationale
```

### 10.4 Intent selection rules

The planner should prefer continuity unless there is evidence for change.

A pattern change should normally have at least one explicit reason:

```text
music change
section change
phrase change
intent escalation
intent release
difficulty correction
readability correction
style preference
```

---

## 11. Candidate Generation

The Pattern Planner should generate a small set of valid candidates rather than immediately choosing one.

### 11.1 Candidate count

Baseline recommendation:

```text
3–5 candidates per decision point
```

Auto Mapper may reduce this to 1–3 after confidence filtering.

### 11.2 Candidate source types

Candidates may come from:

1. Deterministic rules.
2. Pattern grammar templates.
3. Learned classifier / recommender.
4. Optional LLM planner.
5. Style-conditioned transforms.

### 11.3 Candidate representation

A candidate must be representable as a `PatternIntent` plus scoring metadata.

Recommended additional metadata:

```text
candidate_id
family
predicted_fit
expected_difficulty_cost
expected_stamina_cost
expected_reading_cost
music_alignment_prior
continuity_prior
style_prior
reason_codes[]
```

---

## 12. Candidate Ranking

Candidate ranking is the main decision point of Base Intelligence.

### 12.1 Baseline score

The initial deterministic ranker may use:

```text
Score =
  0.30 * MusicAlignment
+ 0.20 * DifficultyFit
+ 0.20 * Continuity
+ 0.15 * Readability
+ 0.10 * StructuralFit
+ 0.05 * Validity
```

These weights are defaults, not permanent constants. They must be configurable and logged.

### 12.2 MusicAlignment

Measures how well the candidate explains the strongest relevant musical evidence.

Examples:

```text
strong rhythmic evidence → rhythm-oriented pattern
ascending melodic phrase → ascending movement where supported by ruleset
strong accent → accent/structure change
high energy → increased complexity/density when appropriate
```

### 12.3 DifficultyFit

Measures whether the candidate moves the local/global difficulty toward the target profile.

### 12.4 Continuity

Measures:

- Transition cost.
- Hand balance or movement continuity.
- Pattern-family continuity.
- Consistency of rhythm language.

### 12.5 Readability

Measures whether the candidate introduces unnecessary cognitive complexity.

### 12.6 StructuralFit

Measures whether the candidate supports the current macro role:

```text
establish
variation
escalation
climax
release
resolution
```

### 12.7 Validity

A hard-invalid candidate must be rejected before ranking.

---

## 13. Deterministic Baseline Intelligence

The current project must provide a fully usable baseline without a trained model or LLM.

### 13.1 Required baseline pipeline

```text
BeatGrid + Sections
  ↓
MusicTimeline
  ↓
Evidence Builder
  ↓
Global Deterministic Planner
  ↓
Local Deterministic Planner
  ↓
Candidate Generator
  ↓
Rule-based Ranker
  ↓
PatternIntent
```

### 13.2 Baseline limitations

The baseline is expected to be simple and explainable.

It must be explicitly labelled in provenance as:

```text
origin = rule_based
```

and should never be described as a learned music intelligence model.

---

## 14. Optional LLM Planner

An LLM is an optional decision component, not the system of record.

### 14.1 LLM responsibilities

The LLM may:

- Interpret structured music evidence.
- Compare candidate patterns.
- Choose among candidate intents.
- Explain rationale.
- Suggest alternatives.
- Produce structured `MappingIntent` or candidate specifications within an enforced schema.

### 14.2 LLM must not

- Directly write `.osu` text as the authoritative output.
- Bypass ruleset validators.
- Bypass difficulty constraints.
- Generate arbitrary unsupported Pattern Families.
- Directly modify editor state without tool mediation.

### 14.3 Structured output contract

The LLM must emit either:

```text
MappingIntent
```

or:

```text
PatternCandidate[]
```

using the canonical Mapping IR schema.

### 14.4 Tool-mediated operation

Recommended tools:

```text
get_music_context()
get_mapping_context()
get_global_plan()
list_pattern_candidates()
score_candidate()
validate_pattern()
calculate_local_difficulty()
preview_pattern()
```

The agent itself should not have direct access to raw editor mutation APIs.

---

## 15. Critic / Evaluation Layer

The Critic identifies problems after generation.

### 15.1 Critic categories

```text
validity
music_alignment
difficulty
continuity
readability
pattern_transition
structural_fit
style_fit
```

### 15.2 Critic output

Recommended structure:

```json
{
  "valid": false,
  "issues": [
    {
      "code": "density_mismatch",
      "severity": "warning",
      "time_range": [42000, 44000],
      "message": "Chorus energy increased but mapping density did not increase.",
      "suggested_actions": [
        "increase_density",
        "introduce_pattern_variation"
      ]
    }
  ]
}
```

### 15.3 Hard vs soft issues

#### Hard

```text
invalid timing
illegal overlap
unsupported object
invalid pattern constraint
unplayable structure
```

#### Soft

```text
weak music alignment
unnecessary pattern repetition
insufficient escalation
poor transition
style mismatch
```

Hard issues block acceptance. Soft issues influence revision ranking.

---

## 16. Difficulty Feedback Loop

Difficulty evaluation must be a closed loop.

```text
Pattern / Map
   ↓
Official Ruleset Difficulty Calculator
   ↓
Observed Difficulty
   ↓
Compare to Target
   ↓
Generate Revision Candidates
   ↓
Re-rank
```

### 16.1 Target error

```text
error = observed_sr - target_sr
```

### 16.2 Directional revision

If:

```text
observed < target
```

candidate generation may increase:

```text
density
rhythm_complexity
pattern_complexity
stamina
movement
LN_complexity
```

subject to the selected Difficulty Profile.

If:

```text
observed > target
```

the inverse should be attempted.

### 16.3 Local and global difficulty

The system must distinguish:

```text
whole-map SR
local difficulty
section difficulty budget
```

The global target must not be achieved by producing one extreme section while leaving the rest under-mapped.

---

## 17. Music Alignment Evaluation

`music_alignment_score` should evolve beyond simple beat-grid alignment.

### 17.1 Minimum v0.1 evaluation

At minimum evaluate:

```text
beat alignment
onset alignment
accent correspondence
section density correspondence
energy ↔ mapping complexity correspondence
```

### 17.2 Future evaluation

May include:

```text
vocal phrase correspondence
melodic direction correspondence
instrument-specific mapping
motif consistency
```

### 17.3 Important distinction

A note being perfectly snapped to the beat does not imply good music alignment.

The metric must evaluate **semantic correspondence**, not only timing correctness.

---

## 18. Pattern Transition Intelligence

Pattern transitions are first-class decisions.

### 18.1 Transition properties

```text
from_pattern
 to_pattern
transition_type
overlap
constraints
cost
```

### 18.2 Transition types

Examples:

```text
same_family
density_increase
density_decrease
chord_introduction
chord_removal
hand_rebalance
movement_change
ln_release
rhythm_change
contrast
reset
```

### 18.3 Transition scoring

A candidate pair should be penalized if the transition requires a large semantic or mechanical jump without supporting musical evidence.

---

## 19. Auto Mapper Workflow

The Auto Mapper should operate as an iterative planner, not a single-shot generator.

```text
1. Analyze audio
2. Build MusicTimeline
3. Build MappingEvidence
4. Create GlobalMappingPlan
5. For each phrase:
   a. Build local context
   b. Produce MappingIntent
   c. Generate candidates
   d. Validate candidates
   e. Rank candidates
   f. Render best candidate
   g. Evaluate local difficulty
   h. Update mapping state
6. Evaluate whole map
7. Run Critic
8. Revise weak areas
9. Stop when acceptance criteria are met
```

### 19.1 Revision budget

The implementation should use an explicit maximum iteration count so that generation cannot loop indefinitely.

Recommended baseline:

```text
max_revisions_per_phrase = 3
max_global_revisions = 2
```

These values are configuration, not hard protocol limits.

---

## 20. Copilot Workflow

Copilot exposes the same intelligence at different granularities.

### 20.1 Macro suggestion

Examples:

```text
"This chorus should escalate into a higher-density pattern."
"The next section should provide release rather than another climax."
```

### 20.2 Meso suggestion

Examples:

```text
"Try 1/8 stream → 1/16 burst → release."
"Introduce an LN transition over the last half-bar."
```

### 20.3 Micro suggestion

Example:

```text
"Next column: 3."
```

Micro suggestions must use deterministic or learned local pattern state, not repeated LLM calls for every note.

---

## 21. Preference Logging

Every human decision should optionally become a preference event.

### 21.1 Preference event

```json
{
  "context_id": "ctx_001",
  "candidates": ["p1", "p2", "p3"],
  "selected": "p2",
  "action": "apply",
  "modified": true,
  "timestamp": "..."
}
```

### 21.2 Use

These records support future:

```text
Candidate ranking model
Preference model
Style model
Personalization
A/B testing
```

### 21.3 No accidental style training

Preference data must not automatically modify the Base Intelligence model.

Base behaviour and user-specific preference learning must remain separated.

---

## 22. Style Layer Compatibility

Style is optional and layered after Base Intelligence.

```text
Base Intelligence
       ↓
Candidate Generation
       ↓
Style-conditioned Ranking
       ↓
PatternIntent
```

### 22.1 Style inputs

Possible dimensions:

```text
density_preference
rhythm_complexity_preference
LN_preference
movement_preference
technicality_preference
pattern_variety_preference
readability_preference
```

### 22.2 Style isolation requirement

The Style Layer must never be required for a valid Base decision.

Therefore:

```text
Style = null
```

must produce a complete mapping decision.

---

## 23. Interfaces

The implementation should use replaceable interfaces.

```csharp
public interface IMusicRepresentationBuilder
{
    MusicTimeline Build(MusicInput input);
}

public interface IMappingEvidenceBuilder
{
    IReadOnlyList<MappingEvidence> Build(
        MusicTimeline music,
        MappingContext context,
        DifficultyProfile difficultyProfile);
}

public interface IGlobalMappingPlanner
{
    GlobalMappingPlan Plan(
        MusicTimeline music,
        IReadOnlyList<MappingEvidence> evidence,
        DifficultyProfile difficultyProfile,
        RulesetInfo ruleset);
}

public interface ILocalMappingPlanner
{
    MappingIntent Plan(
        MappingContext context,
        IReadOnlyList<MappingEvidence> evidence,
        GlobalMappingPlan globalPlan,
        DifficultyProfile difficultyProfile,
        StyleProfile? style);
}

public interface IPatternCandidateGenerator
{
    IReadOnlyList<PatternCandidate> Generate(
        MappingIntent intent,
        MappingContext context,
        DifficultyProfile difficultyProfile,
        StyleProfile? style);
}

public interface IPatternCandidateRanker
{
    IReadOnlyList<RankedPatternCandidate> Rank(
        IReadOnlyList<PatternCandidate> candidates,
        MappingContext context,
        DifficultyProfile difficultyProfile,
        StyleProfile? style);
}

public interface IMappingCritic
{
    CriticReport Evaluate(MappingEvaluationContext context);
}
```

Concrete names may differ in implementation, but responsibilities must remain separate.

---

## 24. Decision Trace and Provenance

Every planner decision should be traceable.

Minimum provenance:

```text
origin
agent
model
version
generated_at
input_context_hash
evidence_ids[]
candidate_ids[]
selected_candidate_id
```

A future debug UI should be able to answer:

> Why was this pattern chosen?

with:

```text
Music evidence
+ Global intent
+ Difficulty target
+ Previous pattern
+ Candidate scores
+ Style preference (if any)
```

---

## 25. Error Handling and Fallback

The intelligence layer must fail gracefully.

### 25.1 Missing audio features

Fallback:

```text
embedding unavailable → deterministic features
vocal unavailable → generic melodic activity
section confidence low → unknown / transition-safe planning
```

### 25.2 LLM unavailable

Fallback:

```text
LLM Planner → Deterministic Local Planner
```

### 25.3 Ranker unavailable

Fallback:

```text
ML Ranker → deterministic weighted scoring
```

### 25.4 Difficulty calculator unavailable

The system may still produce a draft but must mark:

```text
Evaluation.ValidityKnown = false
Evaluation.DifficultyKnown = false
```

and must not claim target SR was achieved.

---

## 26. Ruleset Extensibility

The intelligence layer uses shared semantic concepts, while candidate families are ruleset-specific.

```text
Shared
├── establish
├── escalation
├── climax
├── release
├── variation
└── resolution

Ruleset-specific
├── osu
│   ├── jump
│   ├── stream
│   ├── slider_chain
│   └── movement patterns
│
├── mania
│   ├── stream
│   ├── jack
│   ├── jumpstream
│   ├── LN-rice
│   └── chord patterns
│
├── taiko
│   ├── don/kat structures
│   ├── rolls
│   └── drum pattern grammar
│
└── catch
    ├── horizontal movement
    ├── hyperdash
    └── catcher movement patterns
```

No intelligence component may assume Mania-only concepts before the Pattern Candidate boundary.

---

## 27. Baseline Implementation Requirements for v0.1

The first implementation is considered conformant when it can:

1. Build a `MusicTimeline` from a song.
2. Produce Mapping Evidence from available deterministic features.
3. Produce a Global Mapping Plan.
4. Produce Local MappingIntent objects.
5. Generate at least three Pattern Candidates for supported pattern classes where possible.
6. Reject hard-invalid candidates.
7. Rank candidates using deterministic scoring.
8. Produce a `PatternIntent` for the winning candidate.
9. Render the pattern through an `IPatternProvider`.
10. Run ruleset validation.
11. Measure available difficulty information.
12. Produce a Critic report.
13. Execute at least one revision pass when a soft or hard issue is detected.
14. Record provenance and decision traces.
15. Operate without an LLM.

For the current MVP branch, Mania 4K is an acceptable first ruleset implementation target. The shared interfaces must remain ruleset-neutral.

---

## 28. Conformance Levels

### Level 0 — Data Conformance

- Mapping IR objects serialize and deserialize correctly.
- MusicTimeline, MappingIntent, PatternIntent and provenance are present.

### Level 1 — Deterministic Intelligence

- Evidence → intent → candidate → ranking works without LLM.
- Results are reproducible.

### Level 2 — Closed-loop Mapping

- Pattern rendering, validation and difficulty feedback are integrated.
- Critic can trigger revision.

### Level 3 — Copilot

- Macro, meso and micro suggestions use the same intelligence state.
- Human actions are logged as preference events.

### Level 4 — Learned Intelligence

- Learned candidate ranker or intent model replaces a deterministic component without IR changes.

### Level 5 — Style Conditioning

- Style Profile changes candidate ranking while preserving Base validity.

---

## 29. Evaluation Metrics

The system must be evaluated at multiple levels.

### 29.1 Validity

```text
invalid object rate
invalid pattern rate
validator error rate
```

Target for accepted generated maps:

```text
0 hard validation errors
```

### 29.2 Difficulty accuracy

```text
|observed_SR - target_SR|
```

The exact target tolerance belongs to the product-level generation policy; the intelligence layer must expose the error rather than hide it.

### 29.3 Music alignment

Track at minimum:

```text
beat alignment
onset correspondence
accent correspondence
section/density correspondence
```

### 29.4 Transition quality

Measure:

```text
transition issue rate
pattern continuity score
unnecessary pattern reset rate
```

### 29.5 Human acceptance

Track:

```text
candidate accept rate
manual edit rate
reject rate
alternative selection rate
```

### 29.6 Preference learning quality

When a ranking model is introduced:

```text
pairwise accuracy
NDCG / ranking quality
acceptance uplift vs baseline
```

---

## 30. Security and Reliability Boundaries

The intelligence layer must not be allowed to bypass:

- Ruleset validation.
- Difficulty calculation.
- Editor mutation permissions.
- File path safety.
- Resource limits.
- Revision iteration limits.

LLM-produced structured data must always pass schema validation and domain validation before being applied.

---

## 31. Implementation Order

Recommended order for the current branch:

```text
P0
1. MappingEvidence model
2. Canonical JSON Schema alignment
3. Stable deterministic seed
4. Strongly typed pattern parameters
5. GlobalMappingPlan

P1
6. Evidence builder
7. Global deterministic planner
8. Local planner refactor
9. Candidate generator
10. Candidate ranker
11. Difficulty feedback adapter
12. Critic

P2
13. Copilot context API
14. Preference event logging
15. Optional LLM planner
16. Optional learned ranker
17. Style conditioning
```

This order is intentional. LLM integration comes only after the deterministic decision system is observable and testable.

---

## 32. Recommended Current Branch Refactor

The current implementation already has a strong foundation in:

```text
AiStudio.Core/MappingIr/Model
AiStudio.Core/MappingIr/Planning
AiStudio.Core/MappingIr/Patterns
AiStudio.Core/MappingIr/Validation
AiStudio.Core/MappingIr/Rendering
AiStudio.Core/MappingIr/Serialization
AiStudio.Core/MappingIr/Timeline
```

The next architectural additions should be:

```text
MappingIr/
├── Evidence/
├── GlobalPlanning/
├── LocalPlanning/
├── Candidates/
├── Ranking/
├── Critique/
├── Difficulty/
├── Provenance/
└── Copilot/
```

The existing `IMappingPlanner` should be decomposed rather than discarded.

Recommended direction:

```text
Current
IMappingPlanner
   ↓
Refactor into
IGlobalMappingPlanner
ILocalMappingPlanner
IPatternCandidateGenerator
IPatternCandidateRanker
```

---

## 33. Non-Goals and Anti-Patterns

The following are explicitly discouraged:

### Audio-to-note black box

```text
Audio → LLM → .osu
```

### Difficulty-only generation

```text
Generate anything
→ tweak until SR matches
```

### Section-only planning

```text
Chorus → always use hardest pattern
```

### Per-note LLM calls

```text
one LLM call per note
```

### Hidden style leakage

```text
user preference silently changes Base model behaviour
```

### Unvalidated LLM output

```text
LLM JSON → editor
```

All of these make the system harder to debug, evaluate, and evolve.

---

## 34. Example Decision Trace

For a Mania 4K chorus:

```text
Music Evidence
├── rhythm = 0.92
├── accent = 0.84
├── energy = 0.91
├── vocal = 0.88
└── climax = 0.89

Global Plan
└── current role = climax

Difficulty Profile
├── target SR = 5.5
├── density = 0.72
├── rhythm complexity = 0.64
└── stamina = 0.48

Mapping Intent
└── primary = climax

Candidates
├── stream       score 0.81
├── jumpstream   score 0.84
├── LN-rice      score 0.63
└── jack         score 0.22

Selected
└── jumpstream

Why
├── strongest rhythm evidence
├── suitable density for target
├── supports climax
└── compatible with preceding stream
```

The important property is not the exact numbers; it is that the decision is inspectable.

---

## 35. Future Learned Models

The specification intentionally supports several future learned components.

### 35.1 Intent classifier

```text
MusicEvidence + Context → MappingIntent probabilities
```

### 35.2 Candidate ranker

```text
Context + Candidate → preference score
```

### 35.3 Music alignment model

```text
Music segment + Pattern → alignment score
```

### 35.4 Transition model

```text
Pattern A + Pattern B + Music → transition quality
```

### 35.5 Style model

```text
Base candidates + Style Profile → personalized ranking
```

No model may change the core Mapping IR contract without a schema version change.

---

## 36. Definition of Done for Mapping Intelligence v0.1

Mapping Intelligence v0.1 is considered implemented when all of the following are true:

```text
[ ] Music evidence is explicit and serializable.
[ ] Mapping Evidence is a first-class object.
[ ] Global planning exists independently from local planning.
[ ] Local planning produces MappingIntent.
[ ] Candidate generation produces multiple alternatives.
[ ] Candidates are validated before ranking.
[ ] Candidate ranking is deterministic in baseline mode.
[ ] Winning candidate becomes PatternIntent.
[ ] Pattern transitions are explicit.
[ ] Ruleset generation remains behind a provider interface.
[ ] Difficulty feedback is observable.
[ ] Critic can produce actionable issues.
[ ] Revision loop is bounded and testable.
[ ] Provenance records decision origin.
[ ] Copilot and Auto Mapper share the same intelligence state.
[ ] LLM is optional.
[ ] Style is optional and isolated.
[ ] JSON serialization is schema-valid.
[ ] Tests cover baseline determinism and round-trip semantics.
```

---

## 37. Final Architecture Statement

The Mapping Intelligence Layer is not a single model.

It is a **decision system** composed of:

```text
Music Understanding
        +
Evidence Construction
        +
Global Planning
        +
Local Planning
        +
Candidate Generation
        +
Candidate Ranking
        +
Pattern Transition Reasoning
        +
Difficulty Feedback
        +
Critic / Revision
        +
Optional LLM
        +
Optional Style Layer
```

The foundational invariant is:

```text
Base Intelligence
      ↓
valid, explainable mapping candidates
      ↓
Ruleset-specific rendering
      ↓
objective validation
```

Future AI models are replaceable decision modules inside this system, not the system itself.

This is the architectural property that allows the project to begin with a deterministic A-quality baseline and later evolve toward C-style personalization without rewriting the Mapping IR, Pattern Grammar, renderer, or validator stack.
