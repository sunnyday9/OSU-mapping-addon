# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

- **`CONTEXT.md`** at the repo root if it exists (or **`CONTEXT-MAP.md`** at the repo root if it exists: it points at one `CONTEXT.md` per context; read each one relevant to the topic).
- **`docs/decisions/`**: this repo's decision records, numbered `NNN-*.md` (this is the repo's ADR home). Read the ones that touch the area you're about to work in.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and `/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

Single-context repo (most repos, including this one):

```
/
├── CONTEXT.md                 ← created lazily by /domain-modeling
├── docs/decisions/            ← decision records (ADR home)
│   ├── 001-ruleset-inheritance.md
│   └── 010-sr-calibration.md
└── src/
```

Multi-context repo (presence of `CONTEXT-MAP.md` at the root):

```
/
├── CONTEXT-MAP.md
├── docs/decisions/                       ← system-wide decisions
└── src/
    ├── ordering/
    │   ├── CONTEXT.md
    │   └── docs/adr/                     ← context-specific decisions
    └── billing/
        ├── CONTEXT.md
        └── docs/adr/
```

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in `CONTEXT.md`. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal: either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing decision record (in `docs/decisions/`), surface it explicitly rather than silently overriding:

> _Contradicts 004-verifier-aggregation.md, but worth reopening because…_
