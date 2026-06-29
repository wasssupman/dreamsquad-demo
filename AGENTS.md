# Project Context - Defense Tournament

Codex should follow the project workflow and hard constraints in `CLAUDE.md`.
Use `CLAUDE.md` as the detailed policy source of truth.

## Current Spec Status

- Last finalized spec: `docs/spec/attack-hit-delay/`
- Status: completed 2026-06-29 (units 0~2 — hitDelaySec 필드 + AttackSystem fire START/RESOLVE 분리 + 배치지연 deployDelaySec. Play PASS). 직전: aggro-standoff, enemy-tile-movement-integrity.
- Commits: `a20c277`(0 필드)·`9d124f6`(2 deploy)·`3a1260c`(1 fire 분리)·`87be777`(handoff) · 양트랙 리뷰 APPROVE(`e3d5d79`) → M1 `37138e2`·M2 `eccfd9f`
- Active next: 미정. 후속: TauntGrant hitDelaySec authoring[S]·aggro 사거리통일(=1 데이터)[S]·QuadUnit 누수[S]·(II)레인[L]. (리뷰 M1 standoff metric 통일 = aggro-standoff unit 1, M2 PlayMode smoke = `Tests/PlayMode/MovementIntegritySmokeTest.cs` — 둘 다 완료.)
- 참고: 라이브 검증 시 에디터 **포커스** 필요(비포커스면 Play 시뮬 tick 안 함). WaveA defeatGoalReachedCount 는 10 으로 복원됨(clean).
- Next source of truth: `docs/spec/README.md` Follow-up Backlog

## Required Reading

- `CLAUDE.md`
- `docs/spec/README.md`
- The `Last finalized spec` README and handoff above
