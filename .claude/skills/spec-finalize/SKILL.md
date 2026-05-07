---
name: spec-finalize
description: Use when the user declares a spec complete, e.g. "이 스펙 완료", "spec 완료", "finalize this spec", or asks to synchronize completed spec status for Claude/Codex. Updates only a thin Current Spec Status pointer in CLAUDE.md and AGENTS.md so both LLMs share the same latest spec context.
---

# Spec Finalize

## Purpose

Synchronize a minimal completed-spec pointer across Claude and Codex entrypoint docs
when the user explicitly says a spec is complete.

This skill does **not** replace spec README, numbered work-unit docs, handoff summaries,
or git history. It only keeps both LLMs aligned on where to resume reading.

## Trigger Phrases

Use this skill when the user says or clearly implies:

- `이 스펙 완료`
- `spec 완료`
- `스펙 마감`
- `finalize this spec`
- `mark this spec complete`
- `CLAUDE.md / AGENTS.md 진행상황 맞춰줘`

Do not use this skill for ordinary catchup/status questions. Use `catchup` for read-only
status recovery.

## Scope

Allowed writes:

- `CLAUDE.md`
- `AGENTS.md`
- The active spec docs only when required by the existing project workflow:
  - `docs/spec/{slug}/README.md`
  - `docs/spec/{slug}/{N}_handoff_summary.md`

Default write behavior:

- Update only a single `Current Spec Status` section in `CLAUDE.md` and `AGENTS.md`.
- If the section already exists, replace it in place.
- If the section does not exist, insert it near the top, after the project one-line identity or equivalent intro.
- If `AGENTS.md` does not exist, create a concise Codex-facing file with project rules pointer plus `Current Spec Status`.

Do not add a running history log to `CLAUDE.md` or `AGENTS.md`.

## Required Inputs

Determine these from local files before editing:

- Spec slug/path, e.g. `docs/spec/unit-rarity-and-draft-rules/`
- Completion date
- Handoff file path, if present
- Final commit hash/title, if already committed
- Next source of truth, usually `docs/spec/README.md` Follow-up Backlog

If the completed spec is ambiguous, stop and ask one concise question. Do not guess.

## Current Spec Status Format

Use this exact shape in both `CLAUDE.md` and `AGENTS.md`:

```md
## Current Spec Status

- Last finalized spec: `docs/spec/{slug}/`
- Status: completed YYYY-MM-DD
- Handoff: `docs/spec/{slug}/{N}_handoff_summary.md` or `none`
- Commit: `{short_hash}` `{title}` or `pending`
- Next source of truth: `docs/spec/README.md` Follow-up Backlog
```

Keep it to these five bullets. Do not include implementation details.

## Workflow

1. Confirm the active completed spec.
   - Prefer user-provided slug/name.
   - Otherwise use recent commits and `docs/spec/**` status lines.
   - If unclear, ask.
2. Inspect the spec README and latest handoff summary.
3. Inspect recent git history for the final implementation/docs commit.
4. Update `CLAUDE.md` and `AGENTS.md` with the same `Current Spec Status` block.
5. Preserve the existing role of each file:
   - `CLAUDE.md`: Claude/project workflow policy.
   - `AGENTS.md`: Codex-facing project entrypoint.
6. Show the diff summary and commit only if the user asks to commit.

## AGENTS.md Creation Template

If `AGENTS.md` is missing, create a concise file:

```md
# Project Context - Defense Tournament

Codex should follow the project workflow and hard constraints in `CLAUDE.md`.
Use `CLAUDE.md` as the detailed policy source of truth.

## Current Spec Status

- Last finalized spec: `docs/spec/{slug}/`
- Status: completed YYYY-MM-DD
- Handoff: `docs/spec/{slug}/{N}_handoff_summary.md` or `none`
- Commit: `{short_hash}` `{title}` or `pending`
- Next source of truth: `docs/spec/README.md` Follow-up Backlog

## Required Reading

- `CLAUDE.md`
- `docs/spec/README.md`
- The `Last finalized spec` README and handoff above
```

## Guardrails

- Do not treat `CLAUDE.md` or `AGENTS.md` as a changelog.
- Do not duplicate handoff details into `CLAUDE.md` or `AGENTS.md`.
- Do not mark a spec complete unless the user explicitly says it is complete or the spec docs already say completed.
- Do not update unrelated spec status.
- Do not remove existing project hard constraints.
