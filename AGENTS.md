# Project Context - Defense Tournament

Codex should follow the project workflow and hard constraints in `CLAUDE.md`.
Use `CLAUDE.md` as the detailed policy source of truth.

## Current Spec Status

- Last finalized spec: `docs/spec/aggro-standoff/`
- Status: completed 2026-06-29 (unit 0 — aggro 도달조건=공격범위(`dist ≤ AttackState.range`), Play PASS). 직전 `enemy-tile-movement-integrity`(units 0~3) 완료.
- Commits: `738f2c1`(standoff) · tile-movement: `6f17120`/`be1d950`/`cfe04ec`/`61cb98c`(N-레인 rev)
- Active next: `docs/spec/attack-hit-delay/` — 진행 중(2026-06-29). 공격 시작 후 `hitDelaySec` 초 뒤 타격 판정. unit 0(필드 plumbing, 기본 0 무동작) 완료·compile·EditMode 26/26. **unit 1(AttackSystem fire 분리=start/resolve+지연 tick) 대기.** 후속: aggro 사거리통일[S]·QuadUnit 누수[S]·(II)레인[L].
- ⚠️ 테스트 값: `WaveA.asset` `defeatGoalReachedCount` 100 (원래 10) — 검증용, 미커밋. 되돌릴 것.
- Next source of truth: `docs/spec/README.md` Follow-up Backlog

## Required Reading

- `CLAUDE.md`
- `docs/spec/README.md`
- The `Last finalized spec` README and handoff above
