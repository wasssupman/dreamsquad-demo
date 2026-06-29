# Project Context - Defense Tournament

Codex should follow the project workflow and hard constraints in `CLAUDE.md`.
Use `CLAUDE.md` as the detailed policy source of truth.

## Current Spec Status

- Last finalized spec: `docs/spec/enemy-spawn-positioning/`
- Status: completed 2026-06-29 (units 0~4 — visualOffset + 중앙 ± 연속 랜덤 스폰 분산)
- Handoff: `docs/spec/enemy-spawn-positioning/2_handoff_summary.md`
- Commits: `2487bb0` (0) · `010a32e` (1) · `06cc883` (4) · `f68ec26` (완료/분리)
- Active next: `docs/spec/enemy-tile-movement-integrity/` — 진행 중(2026-06-29 착수, `movement-lane-centering` 에서 리프레임). 적 타일 이동 결함 3종 픽스(레인 시스템 폐기): ①aggro 타일 제약 ②코너 target=0+deadband ③결정론 스폰. unit 0(결정론 스폰 분산) 구현, 검증 대기.
- Next source of truth: `docs/spec/README.md` Follow-up Backlog

## Required Reading

- `CLAUDE.md`
- `docs/spec/README.md`
- The `Last finalized spec` README and handoff above
