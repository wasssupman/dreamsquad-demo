# Project Context - Defense Tournament

Codex should follow the project workflow and hard constraints in `CLAUDE.md`.
Use `CLAUDE.md` as the detailed policy source of truth.

## Current Spec Status

- Last finalized spec: `docs/spec/enemy-tile-movement-integrity/`
- Status: completed 2026-06-29 (units 0~3 — 결정론 스폰 + 코너 target=0+deadband + aggro 타일 제약. `movement-lane-centering` 리프레임)
- Handoff: `docs/spec/enemy-tile-movement-integrity/4_handoff_summary.md`
- Commits: `6f17120`(0) · `be1d950`(1) · `cfe04ec`(2) · 검증/docs(3)
- Active next: `docs/spec/aggro-standoff/` — 진행 중(2026-06-29). aggro 도달조건 = 공격범위(`dist ≤ AttackState.range`)에서 이동 종료. unit 0 구현·compile 0, **Play 거동 검증 대기**(taunt defender 배치 필요). 후속: QuadUnit 뷰 누수[S], (II) 레인 대형[L].
- Next source of truth: `docs/spec/README.md` Follow-up Backlog

## Required Reading

- `CLAUDE.md`
- `docs/spec/README.md`
- The `Last finalized spec` README and handoff above
