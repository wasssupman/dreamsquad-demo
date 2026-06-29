# Project Context - Defense Tournament

Codex should follow the project workflow and hard constraints in `CLAUDE.md`.
Use `CLAUDE.md` as the detailed policy source of truth.

## Current Spec Status

- Last finalized spec: `docs/spec/attack-hit-delay/`
- Status: completed 2026-06-29 (units 0~2 — hitDelaySec 필드 + AttackSystem fire START/RESOLVE 분리 + 배치지연 deployDelaySec. Play PASS). 직전: aggro-standoff, enemy-tile-movement-integrity.
- Commits: `a20c277`(0 필드)·`9d124f6`(2 deploy)·`3a1260c`(1 fire 분리)·`87be777`(handoff) · 양트랙 리뷰 APPROVE(`e3d5d79`) → M1 `37138e2`·M2 `eccfd9f`
- Active next: 미정. 후속: TauntGrant hitDelaySec authoring[S]·aggro 사거리통일(=1 데이터)[S]·QuadUnit 누수[S]·(II)레인[L]. (리뷰 M1 standoff metric 통일 = aggro-standoff unit 1, M2 PlayMode smoke = `Tests/PlayMode/MovementIntegritySmokeTest.cs` — 둘 다 완료.)
- 참고: 라이브 검증 시 에디터 **포커스** 필요(비포커스면 Play 시뮬 tick 안 함). WaveA defeatGoalReachedCount 는 10 으로 복원됨(clean).
- 📋 다음 세션 결정(보류): **멀티-LLM 문서 통합**. Codex=AGENTS.md / Claude=CLAUDE.md 자동주입 비대칭 + Codex 는 `@import` 미지원 → CLAUDE.md 정책이 Codex 에 자동으로 안 넘어감(현재 AGENTS.md 가 "CLAUDE.md 필독" soft 포인터일 뿐). 옵션 **A**=symlink(AGENTS↔CLAUDE 동일·단일소스·drift0, **권장**) · **B**=공유코어+`@import`(Claude만 hard, Codex soft) · **C**=핵심 절대제약 블록을 AGENTS.md 에 복제(drift 위험). 사용자와 모델 택1 후 세팅.
- Next source of truth: `docs/spec/README.md` Follow-up Backlog

## Required Reading

- `CLAUDE.md`
- `docs/spec/README.md`
- The `Last finalized spec` README and handoff above
