# 4 — Handoff Summary

> rev 2026-07-11: unit 5 버그픽스 반영 — 소스 규칙 4-이웃 → 공격가능 반경(Chebyshev ≤ 헌터 사거리 min). 실플레이에서 레인 비인접 배치가 무시되던 결함 해소, 사용자 확인 완료.

## Commit

- `dc298ceb` feat(boss-defender-field): 보스 방어유닛-지향 flow field 사냥 이동 (units 0~2)
- unit 5: `5_attackable_sources_fix.md` 참조 (소스=공격가능 셀 + 보스 부재 재빌드 skip + R min-fold)

## Implemented

- `FlowFieldBuilder.BuildFromSources` — multi-source BFS. 기존 단일-goal `Build` 는 1-소스 위임(기존 테스트 무수정 통과 = 무회귀 증명).
- `FlowFieldBuilder.CollectDefenderSources` — 방어유닛(벽 셀)의 walkable 4-이웃을 소스로 수집.
- `DefenderFieldSingleton`(Effects) — walkMask/flow/dist Persistent. BattleBridge 가 goal field 와 같은 지점에서 생성/teardown(멱등, `TeardownFlowField` 내 통합).
- `DefenderFieldSystem`(Effects, Burst) — 매 프레임 필드 재빌드. 이벤트 훅/dirty 추적 0.
- `MovementSystem` 보스 분기 — `BossTag` + hunt-dist 유한 → defender field flow-follow. 사냥 중 goal-leak 스킵. 방어유닛 0 → dist 전부 MaxValue → 자동 goal 마칭(무상태 fallback, softlock 구조적 불가).
- `FlowRecovery.RecoveryDir` — zero-flow 복구 순수함수 추출(ecs-review M3).

## Key Files

- `Assets/_Project/Scripts/Battle/Effects/FlowFieldBuilder.cs` · `DefenderFieldSingleton.cs` · `DefenderFieldSystem.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` · `FlowRecovery.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`BuildFlowField`/`TeardownFlowField`)
- `Assets/_Project/Tests/EditMode/FlowFieldBuilderTests.cs` · `FlowRecoveryTests.cs`

## Verified

- EditMode 653 — 651 pass / 0 fail / 2 기존 skip (신규 10종 포함).
- Play e2e: 보스 뒤 배치 → 역주행 재교전 → 교전 → 사망 후 재개 → 0마리 goal leak. 비-보스는 동일 조건 직진(무회귀). 상세 트레이스는 `3_play_validation.md`.
- ecs-reviewer: APPROVE-WITH-CHANGES → M3(순수함수+테스트) 반영 완료. CRITICAL/HIGH 0.
- console 에러/신규 leak 경고 0.

## Notes (되돌리면 안 되는 것)

- **소스 = "공격 가능한 walkable 셀"(Chebyshev ≤ 헌터 사거리 min), 4-이웃으로 되돌리기 금지** — 4-이웃은 레인 비인접 배치를 전부 놓치는 검증된 결함(unit 5). min fold 를 max 로 바꾸면 이질 사거리 헌터 스톨 가능.
- **직선추격/wall-slide 재도입 금지** — `enemy-hunter-targeting`(폐기) 사슬 참조. 이동은 오직 필드.
- **벽 판정은 goal field 유지** — defender field 의 zero-flow 는 소스 셀(dist 0) 포함이라 벽 프록시 오판.
- **fallback 은 per-frame 무상태** (`dist==MaxValue → goal flow`) — "방어유닛 0"과 "도달불가"를 한 규칙으로 처리하는 지점. 상태 추가 금지.
- FSM 변경 0 — 사냥은 `Marching` 그대로. Engaging/Halt 가 정지·공격 담당.

## Follow-up

- 필드 dirty-skip 최적화(대형 그리드 대비, ecs-review M2) — README 후속 후보에 있음.
- 보스 어그로 면역 / 일반 헌터 아키타입 / 타겟 정책 — README 후속 후보.
- ecs-reviewer 에이전트 정의의 채널 목록이 CLAUDE.md(17개)와 불일치(stale, ecs-review M1) — 워크플로우 메타 항목.
- 가디언 aggro 우선 라이브 확인 미실시(코드 경로 불변으로 판정) — 보스+가디언 동시 등장 콘텐츠 나오면 확인.
