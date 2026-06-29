# Project Context - Defense Tournament

Codex should follow the project workflow and hard constraints in `CLAUDE.md`.
Use `CLAUDE.md` as the detailed policy source of truth.

## Current Spec Status

- Last finalized spec: `docs/spec/aggro-standoff/`
- Status: completed 2026-06-29 (unit 0 — aggro 도달조건=공격범위(`dist ≤ AttackState.range`), Play PASS). 직전 `enemy-tile-movement-integrity`(units 0~3) 완료.
- Commits: `738f2c1`(standoff) · tile-movement: `6f17120`/`be1d950`/`cfe04ec`/`61cb98c`(N-레인 rev)
- Active next: 미정. 후속: aggro 공격사거리 통일[S], QuadUnit 뷰 누수[S](presentation), (II) 레인 대형[L].
- ⚠️ 테스트 값: `WaveA.asset` `defeatGoalReachedCount` 100 (원래 10) — 검증용, 미커밋. 되돌릴 것.
- Next source of truth: `docs/spec/README.md` Follow-up Backlog

## Required Reading

- `CLAUDE.md`
- `docs/spec/README.md`
- The `Last finalized spec` README and handoff above
