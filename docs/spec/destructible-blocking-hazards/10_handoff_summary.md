# Destructible Blocking Hazards — Handoff Summary

**완료일**: 2026-04-29
**상태**: 구현 완료 — Unit 0~9 구현 + 사용자 PlayMode 확인 완료.

## Commit

| Unit | 해시 | 설명 |
|---|---|---|
| 0~9 | 커밋 미작성 | Faction 인프라 + BlockingHazard entity + spawn/destroy 채널 + visual presenter + 검증 |

## Implemented

- Faction enum + FactionTag + AttackState.targetMask
- 디펜더/적 spawn 코드 mask 부여
- AttackSystem 두 loop 의 target snapshot query 가 mask 기반
- Blocking hazard range 판정은 `BlockingHazardCellsBuffer` 의 가장 가까운 점유 cell 기준
- BlockingHazard 컴포넌트 + 멀티셀 buffer
- ObstacleLifetimeSystem 멀티셀 확장
- HazardDestroyedEventsSingleton + UnitLifecycleSystem 분기
- BlockingHazardSO + Rock_3x3 샘플 + placeholder visual
- EffectSpawner.SpawnBlockingHazard + 충돌 거부 + BattleBridge 매개
- BlockingHazardPresenter + HealthBar 부착 + destruction VFX
- 디버그 spawn 메뉴 + PlayMode 검증 V1~V6
- 디버그 메뉴 보강: goal 근처 클릭 시 가장 가까운 유효 3x3 spawn cell 로 스냅

## Key Files

Effects/: Faction.cs (Units), FactionTag.cs (Units), BlockingHazard.cs, BlockingHazardCellsBuffer.cs, BlockingHazardSO.cs, BlockingHazardPresenter.cs, HazardDestroyedEvent.cs, HazardDestroyedEventsSingleton.cs, ObstacleLifetimeSystem.cs (수정), EffectSpawner.cs (수정), BlockingHazardDebugMenu.cs

Combat/: AttackState.cs (targetMask 추가), AttackSystem.cs (mask query 전환)

Units/: UnitLifecycleSystem.cs (hazard destroyed 분기)

Bridge/: BattleBridge.cs (FactionTag/targetMask 부여, SpawnBlockingHazardWithVisual, HazardDestroyedEvents drain)

Data/: Hazard_Rock_3x3.asset

Prefabs/: BlockingHazard_Placeholder.prefab

Tests/: AttackSystemMaskTests, ObstacleLifetimeTests (멀티셀), HazardDestroyedEventTests, SpawnBlockingHazardTests

## Verified

- 컴파일 성공
- Unit 5 시점 전체 EditMode 149/149 통과
- `SpawnBlockingHazardTests` 5/5 통과(TestResults.xml 기준)
- PlayMode 사용자 확인 통과: spawn 동작, 유효 cell 스냅, 콘솔 에러 없음
- LocalTransform writer 단독 = MovementSystem
- NativeQueue lifecycle 은 BattleBridge teardown / OnDestroy dispose 경로에 연결
- 콘솔 에러 0

## Notes

- Unit 2 전환 후 기존 테스트 fixture 는 `Health + FactionTag + IncomingDamage` target contract 를 명시하도록 갱신.
- `HealthBarSystem` 은 별도 bar entity 모델이므로 blocking hazard entity 에 `HealthBarTag` 를 직접 붙이지 않고 `BattleBridge.CreateHealthBar(owner, ...)` 를 재사용.
- 최초 debug spawn 은 골 cell 인접 클릭에서 거부됐다. `TryFindValidBlockingHazardCell` 로 유효 3x3 위치를 탐색하도록 보강.
- `BattleBridge.SyncMonoUnitViews` 에서 stale `_aliveAttackersQuery` 로 NRE가 반복되어 query 재생성 방어를 추가.
- TextMeshPro 한글 glyph fallback 경고는 별도 UI/font 문제이며 본 feature 에러는 아님.

## Follow-up

- AttackSystem 의 두 loop 통합 (코드 중복 분석 후 별도 spec)
- AttackUnitTag/DefenderUnitTag 의 진영 식별 역할 폐기 (FactionTag 일원화)
- Taunt 컴포넌트 (radius 기반 어그로) — 스킬/효과 spec
- Faction 추가 진영 (Goal / FieldProp / Totem) — 후속 feature 진입 시
- BlockingHazardSO 추가 필드 (반격 능력 / on-destroy hazard 생성 / counterMask 등)
- HP-비례 visual 변형 (균열 / 색조)
- 정식 VFX prefab (unity-vfx-authoring)
- 멀티 hazard 동시 spawn 부하 측정 + incremental blockedCells 갱신
- Hazard 종 다양화 (Wall_1x3 / Cross 등)
