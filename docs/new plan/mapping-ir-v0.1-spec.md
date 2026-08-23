# osu! AI Mapper — Mapping IR v0.1 Specification

## 1. Status

Version: 0.1.0
Status: Proposed implementation baseline
Primary purpose: shared contract between AI planning, deterministic pattern generation, ruleset validation, difficulty evaluation, and future style conditioning.

## 2. Design Goals

- Represent music structure independently of osu! rulesets.
- Represent mapping decisions without forcing an AI to emit raw HitObjects.
- Keep concrete object generation deterministic and testable.
- Support Standard, Taiko, Catch, and Mania through a common envelope.
- Support Auto Mapper and Copilot with the same semantic representation.
- Keep Base Mapping Intelligence independent from future Style Layer.
- Preserve provenance so human edits can become future preference/style data.
- Permit schema evolution without invalidating old datasets.

## 3. Architectural Principle

The canonical data flow is:

`Audio → MusicTimeline → MappingIntent → PatternIntent → Transition → ConcreteObject → Ruleset Validator/Difficulty`

The LLM/AI may propose `MusicTimeline`, `MappingIntent`, and `PatternIntent`, but the renderer owns coordinates/columns and object construction. Validators and difficulty calculators remain deterministic.

## 4. Semantic vs Rendered IR

### Semantic IR

Contains:

- MusicEvent
- Section
- Phrase
- MappingIntent
- PatternIntent
- PatternTransition
- DifficultyProfile
- StyleProfile
- Constraints

### Rendered IR

Contains:

- ConcreteObject

Rendered IR is derived from Semantic IR. This allows re-rendering a mapping plan with a different style or generator while retaining the original musical/mapping intent.

## 5. Top-Level Document

```json
{
  "schema": "osu-ai-mapping-ir",
  "version": "0.1.0",
  "document_id": "mapir_demo_001",
  "map": {},
  "ruleset": {},
  "difficulty_profile": {},
  "music_timeline": {},
  "mapping_plan": {},
  "concrete_objects": [],
  "constraints": {},
  "style": null,
  "provenance": {},
  "evaluation": {}
}
```

The exact JSON Schema is provided in `mapping-ir-v0.1.schema.json`.

## 6. Map Identity

`audio_hash` is mandatory. Beatmap/difficulty identifiers are optional so the same IR can represent a newly generated map before it exists in an osu! database.

## 7. Ruleset

Supported enumerations:

- `osu`
- `taiko`
- `catch`
- `mania`

Ruleset variant parameters are extensible. Mania should use `keys`, while Standard may use `circle_size`.

## 8. Difficulty Profile

The user-facing target is `target_star_rating`, but the planner should operate on orthogonal design dimensions:

- density
- rhythm_complexity
- reading
- stamina
- technicality
- movement
- ln_complexity

All normalized dimensions are `[0,1]`. They are design preferences, not direct substitutes for the ruleset's official difficulty attributes.

## 9. MusicTimeline

The timeline is the canonical temporal representation. All primary time fields are integer milliseconds. Tempo contains a base BPM and explicit changes.

Hierarchy:

`Timeline → Section → Phrase → MusicEvent`

The hierarchy is not mandatory for every event but should be populated whenever the detector has confidence.

## 10. Section Vocabulary

Baseline section labels:

`intro, verse, pre_chorus, chorus, drop, bridge, break, outro, instrumental, transition, unknown`

Additional semantic labels belong in `labels`, not in uncontrolled proliferation of the core enum.

## 11. MusicEvent Vocabulary

Baseline event types:

`beat, onset, kick, snare, hihat, percussion, bass, chord, vocal, vocal_phrase, melody, accent, silence, transition`

Features remain extensible because audio analysis models may differ.

## 12. MappingIntent

A MappingIntent answers: “What should this passage communicate as a mapping?”

Primary intents:

`establish, repeat, variation, escalation, release, climax, de_escalation, contrast, transition, accent, silence, anticipation, resolution`

The intent contains:

- time range
- primary/secondary intention
- musical targets
- continuity relation
- emphasis vector
- complexity
- confidence
- optional rationale

## 13. Emphasis Vector

The baseline six dimensions are:

- rhythm
- density
- movement
- pattern_complexity
- accent
- contrast

This allows `climax` to mean different things in different songs and rulesets.

## 14. PatternIntent

PatternIntent is ruleset-specific but wrapped in a common envelope:

- ruleset
- family
- time range
- parameters
- constraints
- transition references
- confidence
- rationale

`family` is intentionally a string rather than a global enum because Standard, Mania, Taiko, and Catch need different vocabularies.

## 15. Pattern Transition

Transitions are first-class because quality often depends more on A→B continuity than on the isolated quality of A or B.

Baseline transition concepts include:

- same_family
- rhythm_increase/decrease
- density_increase/decrease
- hand_rebalance
- column_rotation
- chord_introduction/removal
- ln_introduction/release
- shape_change
- spacing_increase/decrease
- reversal
- movement_reset

Ruleset providers may add specialized labels.

## 16. ConcreteObject

ConcreteObject is renderer output. It may represent a generic hit, hold, or future ruleset-specific object. Standard positions use x/y; Mania uses column/end_time for LN. Ruleset-specific renderers may extend the representation later.

## 17. Constraints

Constraints are separated from AI preferences. Timing, playability and music alignment limits must be machine-checkable where possible.

## 18. Style Layer

Style is optional and nullable in v0.1. Base mapping must remain valid with `style = null`.

A future style system may provide a parameter vector, learned embedding, adapter identifier, or named style. It must influence candidate selection, not bypass hard legality constraints.

## 19. Provenance

Allowed origins:

- human
- rule_based
- ai_generated
- hybrid
- imported

The document may also contain agent/model metadata and human edit events. This is essential for future preference learning.

## 20. Evaluation

Baseline metrics:

- validity
- difficulty attributes
- music alignment score
- transition score
- human acceptance
- issue list

The evaluation section is observational and must not become a hidden source of truth for the generator.

## 21. Determinism Requirements

Pattern generation must be deterministic for a fixed seed and fixed input. Randomness may be introduced by explicit seed, but it must be recorded in provenance or generator context.

## 22. Auto Mapper Flow

```text
Song Analyst
→ Timeline
→ Mapping Planner
→ Pattern Planner
→ Pattern Provider
→ Renderer
→ Validator
→ Difficulty Calculator
→ Critic
→ Revision Loop
```

## 23. Copilot Flow

```text
Editor Context
→ Music Context Retrieval
→ Mapping Intent
→ 1–3 Pattern Candidates
→ Validator / Preview
→ User Apply / Reject / Modify
```

The same Semantic IR should be used in both flows.

## 24. Future C / Style Extension

The long-term architecture is:

`Base Mapping Intelligence + Difficulty Profile + Style Profile → Pattern Candidate Ranking`

A style profile may eventually be learned from accepted edits, existing maps, pairwise preferences, or adapters. It is intentionally not required for v0.1.

## 25. MVP Scope

### Mania MVP

Required pattern families:

`single, stream, burst, jack, jump, jumpstream, single_ln, ln_rice, ln_release`

### Standard MVP

Required pattern families:

`single, jump, stream, burst, jumpstream, slider_chain`

Required geometry primitives:

`line, diagonal, arc, zigzag`

## 26. Non-Goals for v0.1

- End-to-end audio-to-.osu neural generation
- Learned mapper style
- Automatic Taiko/Catch pattern grammar completeness
- Perfect human-level difficulty estimation
- Replacing osu!lazer's ruleset implementation
- Letting an LLM directly author raw `.osu` text

## 27. Acceptance Criteria

A v0.1 implementation is considered coherent when:

1. A valid Mania 4K semantic plan can be serialized and schema-validated.
2. The same plan can be deterministically rendered into concrete objects.
3. A renderer can validate the output without requiring an LLM.
4. A pattern can be replaced without rewriting MusicTimeline.
5. A future StyleProfile can be inserted without changing MappingIntent schema.
6. Provenance can distinguish AI-generated and human-edited regions.
7. Standard and Mania providers can implement the same provider interface.
