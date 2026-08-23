# osu! AI Mapper — Mania Pattern Grammar v0.1

## 1. Purpose

This document defines the first ruleset-specific pattern vocabulary for osu!mania. It is designed to sit below `MappingIntent` and above concrete HitObjects. The grammar is intended for 4K first, but every parameter must remain extensible to 1–18 keys.

## 2. Design Principles

1. Patterns describe intent, not raw notes.
2. Rhythm, hand policy, note-family and LN policy remain separable dimensions.
3. Concrete columns are generated deterministically from policy + context.
4. Pattern transitions are first-class objects.
5. Every pattern must expose constraints that a validator can check without an LLM.
6. Style can bias pattern selection but cannot bypass legality constraints.

## 3. Pattern Family Vocabulary

### 3.1 Basic rhythm patterns

| Family | Meaning |
|---|---|
| `single` | Isolated single notes following a specified rhythm. |
| `stream` | Continuous single-note alternation over a rhythmic subdivision. |
| `burst` | Short high-density sequence, usually 3–8 notes. |
| `jack` | Repeated notes in one column. |
| `jump` | Simultaneous notes in two or more columns. |
| `jumpstream` | Continuous stream containing controlled two-note chords. |
| `handstream` | Stream with repeated chord/hand shapes. |
| `chordstream` | Dense stream using regular chord structures. |
| `anchor` | One column acts as a repeated structural anchor while other columns move. |

### 3.2 LN families

| Family | Meaning |
|---|---|
| `single_ln` | Isolated long note. |
| `ln_rice` | Long note(s) combined with short rice notes. |
| `ln_stream` | Sustained notes embedded in a streaming structure. |
| `ln_chord` | Simultaneous long-note structure. |
| `ln_release` | Release timing is an explicit musical/rhythmic event. |
| `ln_transition` | Pattern whose main purpose is changing hand/column structure across LN states. |

## 4. Shared Pattern Parameters

All Mania patterns may use the following standard fields where applicable:

```yaml
rhythm:
  subdivision: 1/4 | 1/8 | 1/12 | 1/16 | 1/24 | custom
  swing: 0..1
  accent_strength: 0..1

density: 0..1

column_policy:
  type: alternate | adjacent | outer_to_inner | inner_to_outer | staircase | mirror | fixed | custom
  columns: [int]

hand_policy:
  type: alternate | split | dominant_left | dominant_right | balanced | custom

ln_policy:
  usage: none | sparse | moderate | dense
  ratio: 0..1

constraints:
  max_same_column_run: int
  allow_chords: bool
  max_chord_size: int
  allow_jacks: bool
```

## 5. Stream

### Intent

Use when continuous subdivision is the primary expression and the map should read as a connected stream rather than independent accents.

### Parameters

```yaml
family: stream
rhythm:
  subdivision: 1/8 | 1/12 | 1/16 | 1/24
column_policy:
  type: alternate | adjacent | staircase | mirror | custom
  columns: [0,2,1,3]
density: 0..1
```

### 4K default constraints

- `max_same_column_run = 1` unless intentional jack variation is requested.
- No chords by default.
- Transition should preserve the previous hand balance unless the MappingIntent explicitly requests contrast.

## 6. Burst

### Intent

Short density escalation or accent. A burst is a local event, not a full-section rhythm policy.

### Parameters

```yaml
family: burst
count: 3..12
subdivision: 1/8 | 1/12 | 1/16 | 1/24
shape: alternate | staircase | custom
```

### Design rule

Bursts should normally terminate into a readable landing pattern rather than stacking multiple unrelated transitions.

## 7. Jack

### Intent

Repeated same-column notes used when repeated rhythmic attack is musically justified.

### Parameters

```yaml
family: jack
column: int
count: 2..12
subdivision: 1/8 | 1/16 | custom
```

### Validator rules

- Check excessive duration and repetition.
- Check compatibility with preceding/following hand assignment.
- Do not infer acceptability solely from star rating.

## 8. Jump / Chord

### Intent

Simultaneous notes representing stronger musical events or increasing hand load.

```yaml
family: jump
chord_size: 2 | 3 | 4
chord_shape: adjacent | split | outer | custom
rhythm: 1/4 | 1/8 | 1/12 | 1/16
```

For 4K, `chord_size=2` is the conservative default. Larger chords should require explicit difficulty or style support.

## 9. Jumpstream

Combines continuous rhythm and controlled two-note chords.

```yaml
family: jumpstream
rhythm:
  subdivision: 1/8 | 1/16
chord_density: 0..1
chord_shape: split | adjacent | alternating
column_policy:
  type: custom | alternate | mirror
```

The generator should distinguish between chord placement and note order so that the resulting hand pattern remains deterministic and testable.

## 10. Handstream / Chordstream

### Handstream

Repeated structured hand shapes over a continuous stream.

```yaml
family: handstream
rhythm:
  subdivision: 1/8 | 1/16
hand_shape:
  size: 2 | 3
  type: split | adjacent | custom
rotation: none | clockwise | counterclockwise | custom
```

### Chordstream

A denser variant where chord recurrence is the primary pattern identity.

```yaml
family: chordstream
rhythm:
  subdivision: 1/8 | 1/16
chord_size: 2 | 3 | 4
repetition: 0..1
variation: 0..1
```

## 11. Anchor

An anchor creates a repeated column while the other notes vary.

```yaml
family: anchor
anchor_column: int
subdivision: 1/8 | 1/16
movement_columns: [int]
anchor_ratio: 0..1
```

Anchors should only be selected when the music or difficulty profile provides a reason to sustain a structural column.

## 12. LN Grammar

### Single LN

```yaml
family: single_ln
ln_ratio: 0..1
duration_beats: 1/4 | 1/2 | 1 | custom
column_policy: fixed | alternating
```

### LN Rice

```yaml
family: ln_rice
ln_ratio: 0..1
rice_subdivision: 1/8 | 1/16
hand_distribution: split | balanced | custom
```

### LN Stream

```yaml
family: ln_stream
ln_ratio: 0.1..0.8
rice_subdivision: 1/8 | 1/16
transition_policy: preserve_hand | rotate | custom
```

### LN Release

The release is treated as a meaningful timing event. The generator must not silently move an LN end time to fit a later pattern without surfacing the change in validation/provenance.

## 13. Transition Grammar

Supported v0.1 transition labels:

```text
same_family
rhythm_increase
rhythm_decrease
density_increase
density_decrease
hand_rebalance
column_rotation
chord_introduction
chord_removal
ln_introduction
ln_release
pattern_break
reset
```

Examples:

```text
stream → burst            : rhythm_increase
stream → jumpstream       : chord_introduction
LN-rice → stream          : ln_release
jack → stream             : hand_rebalance
stream → dense stream    : density_increase
```

## 14. Generator Contract

A Mania pattern provider consumes:

```text
PatternIntent
+ MusicTimeline
+ DifficultyProfile
+ Previous concrete objects
+ Ruleset constraints
```

and returns:

```text
ConcreteObject[]
+ PatternIssue[]
```

The generator must be deterministic for a fixed seed and fixed inputs.

## 15. Validator Contract

Every generated pattern must be checked for:

- illegal overlapping objects
- invalid column indices
- unsupported chord size
- configured jack limits
- LN duration/release constraints
- timing quantization violations
- abrupt hand-policy changes
- local density spikes outside requested bounds
- incompatible transitions

## 16. 4K MVP Pattern Set

The first implementation should only require:

```text
single
stream
burst
jack
jump
jumpstream
single_ln
ln_rice
ln_release
```

Everything else can initially be represented as an extension of these primitives.
