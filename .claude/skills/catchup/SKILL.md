---
name: catchup
description: Use at the start of a wassup project session, after context loss, or before choosing next work to reconstruct current status and next steps from CLAUDE.md, git status/log, docs/spec README files, handoff summaries, and project README/spec documents. Especially useful when the user asks what is currently done, what remains, where to continue, or asks to check git log/spec/readme/CLAUDE.md before working.
---

# Project Status Recovery

## Purpose

Reconstruct the current project state before acting. The goal is to answer:

- What changed recently?
- Which spec is active or most likely next?
- What is already completed?
- What should be done next?
- What local dirty state must not be overwritten?

Use local files as source of truth. Do not infer status from memory when the repo can answer it.

## Mandatory Read Order

1. `CLAUDE.md`
   - Project rules, workflow, hard constraints, and current documentation structure.
2. Git status and recent history
   - `git status --short`
   - `git log --oneline -12`
   - If a commit looks relevant, inspect it with `git show --stat --oneline <hash>` before summarizing it.
3. Spec index and likely active specs
   - `docs/spec/README.md`
   - Relevant `docs/spec/{feature-slug}/README.md`
   - Latest `{N}_handoff_summary.md` or `_session_handoff.md` in that spec, if present.
4. Project/product references only as needed
   - `CLAUDE.md` for technical constraints (절대 제약 + 추가 제약); `docs/reference/battle-core-architecture.md` for the battle structure map.
   - `docs/reference/ingame-flow.md` for product intent (설계 지향 7축). `docs/TRD.md`/`docs/PRD.md` were retired 2026-09-03.
   - `docs/prototype/**` only when historical Phase context is explicitly needed.

## Finding the Likely Active Spec

Use evidence, in this order:

1. User named a spec or feature in the current request.
2. Dirty worktree paths point into a specific `docs/spec/{feature-slug}/` or feature code area.
3. Most recent commits mention a spec slug or feature name.
4. `docs/spec/README.md` Follow-up Backlog has an item that matches the request.
5. If none of the above is clear, report "active spec unclear" and list the 2-3 most plausible candidates with evidence.

Useful commands:

```sh
rg --files docs/spec | sort
find docs/spec -name '*handoff*' -o -name '_session_handoff.md'
git status --short
git log --oneline -12
```

Prefer `rg` over slower recursive searches when possible.

## What To Extract

From `CLAUDE.md`:

- Current workflow phase: prototype archive vs spec-driven work.
- Hard constraints that affect the next task.
- Required verification/commit/handoff expectations.

From git:

- Dirty files, grouped as tracked changes and untracked files.
- Recent commit chain, with the newest 3-5 commits summarized by feature area.
- Any mismatch between docs status and recent commits.

From spec README:

- Status line or completion state.
- Work unit table and the next unfinished numbered file.
- Feature-wide contracts.
- Follow-up candidates.

From handoff summaries:

- Commit hash/title.
- Implemented behavior.
- Key files to inspect next.
- Verification done or missing.
- Explicit follow-up items and cautions.

## Output Shape

Keep the report short and decision-oriented. Use Korean if the user used Korean.

Use this fixed structure by default:

```text
## Catchup

### 현재 상태
- 한 줄 결론: 현재 작업이 어디까지 왔는지.
- 활성/추정 spec: docs/spec/{slug}/ 또는 "불명확".
- 최근 완료: 최근 커밋/문서 기준 완료된 일 2-4개.
- 로컬 변경: dirty worktree 요약. 내 변경인지 불명확하면 그렇게 표시.

### 근거
- CLAUDE.md: 현재 워크플로우/제약 중 이번 판단에 영향을 주는 내용.
- git: 최근 커밋 3-5개와 의미, dirty 상태.
- spec: 읽은 README/handoff/번호 문서와 상태 판단.

### 다음 작업
1. ...
2. ...
3. ...

### 주의점
- ...
```

Rules:

- Keep `현재 상태` to 4 bullets or fewer.
- Put the single most likely next action first under `다음 작업`.
- Include exact paths for docs/code and exact commit hashes when relevant.
- If the active spec is unclear, do not guess silently. Say `활성/추정 spec: 불명확` and list candidates with evidence.
- If there are console errors, failed tests, or unverified PlayMode work, include them in `주의점`.

## Guardrails

- Do not mark dirty files as yours unless you created or edited them in this session.
- Do not clean or revert local changes while recovering status.
- Do not start implementation until the active spec and next work unit are clear enough.
- If the next task is outside the active spec scope, recommend creating or extending a spec first.
- If `CLAUDE.md` and a spec conflict, treat `CLAUDE.md` as project workflow policy and the spec README as feature contract; call out the conflict.
- Handoff summaries are maps, not source of truth. Prefer README/numbered spec files for contract, and code/git history for implementation details.
