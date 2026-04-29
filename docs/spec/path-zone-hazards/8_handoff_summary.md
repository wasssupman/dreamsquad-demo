# Path Zone Hazards Handoff Summary

**완료일**: 2026-04-29
**상태**: 구현 완료 + PlayMode 로그 검증 완료.

## 구현 요약

- `HazardSO` + `HazardEffect` + `HazardShape` 데이터 모델 추가.
- Hazard ECS entity layout 추가:
  - `Hazard`
  - `HazardEffectsBuffer`
  - `HazardCellsBuffer`
- `CcKind.DoT` 추가.
- `DotApplySystem` 추가:
  - `CcEffect(kind=DoT)`를 매 프레임 `IncomingDamage`로 변환.
  - 기존 `DamageApplicationSystem` 경로 재사용.
- `HazardSingleton` + `HazardLifetimeSystem` 추가:
  - `NativeParallelMultiHashMap<int2, HazardEffect>` 기반 cell-to-effects map 재구축.
  - BattleBridge lifecycle에서 Persistent allocation 생성/해제.
- `ZoneApplySystem` 추가:
  - 적 현재 cell이 hazard cell이면 `EnemyCcEventsSingleton.queue`로 CC enqueue.
- `EffectSpawner.SpawnHazard(EntityManager, HazardSO, int2)` 단일 spawn 진입점 추가.
- `BattleBridge.SpawnHazardWithVisual(HazardSO, int2)` / `DebugSpawnHazardAt` 추가.
- `HazardVisualLifetime` 추가:
  - visual prefab self-managed lifetime.
- Editor debug menu 추가:
  - Poison / Ice / Fire hazard spawn.
  - Input System `Mouse.current.position` 사용.
  - 클릭 셀이 비이동 타일이면 가장 가까운 `MapTileType.Walk` 셀로 스냅.
- 샘플 asset/prefab 추가:
  - `Hazard_Poison_3x3`
  - `Hazard_Ice_3x3`
  - `Hazard_Fire_3x3`
  - placeholder cube visual 3종.
- Battle JSON hazard logging 추가:
  - `spawn`
  - `zone_apply`
  - `dot_damage`

## 검증 결과

Unity compile:

- 에러 0.

EditMode tests:

- `DotApplySystemTests`
- `HazardShapeSamplerTests`
- `CcApplySystemTests`
- `ObstacleLifetimeTests`
- 총 14/14 통과.

PlayMode 로그 확인:

- 최신 확인 로그: `GameLogs/session-20260429-032504-0b4e5ac4.json`
- hazard 로그 총 1175건.
- `spawn`: 3건.
- `zone_apply`: 1014건.
- `dot_damage`: 158건.
- DoT 총합 데미지 약 26.94.
- Fire DoT와 Ice Slow가 같은 Walk cell 주변에서 실제 target에 적용된 기록 확인.

## 확인된 동작

- hazard origin은 이동 타일로 강제된다.
- 비이동 타일 클릭 시 가장 가까운 이동 타일로 스냅된다.
- Ice Slow는 `zone_apply` 로그와 target별 Slow 이벤트로 확인된다.
- Fire DoT는 `dot_damage` 로그와 target별 누적 damage로 확인된다.
- Visual과 ECS effect는 `BattleBridge.SpawnHazardWithVisual`에서만 연결되며, ECS와 Presenter는 직접 의존하지 않는다.

## 남은 주의점

- `dot_damage` 로그의 `tile`은 현재 `(0,0)`으로 남을 수 있다.
  - DoT가 `CcEffect`로 변환된 뒤 원본 hazard cell 정보를 잃기 때문이다.
  - 효과 적용 자체에는 문제가 없다.
  - 정확한 damage source cell이 필요하면 `CcEffect`에 source cell을 추가하거나 `dot_damage` 기록 시 target current cell을 계산하는 후속 작업이 필요하다.
- `zone_apply`는 매 프레임 로그가 많아질 수 있어 BattleLogger에서 hazard 로그를 세션당 2000건으로 cap 처리했다.
- Native allocation leak warning은 이전 세션에서 1회 관측됐지만, 이번 최종 compile/test 확인에서는 hazard 관련 exception 없이 진행됐다. 정확한 출처 추적은 Jobs Leak Detection stack trace 모드에서 별도 재현이 필요하다.
- 본 spec은 debug producer 1개로 단일 spawn API를 검증했다. 실제 producer 확장은 후속 spec에서 재확인해야 한다.

## 후속 후보

- `dot_damage` source cell 로깅 개선.
- hazard 로그 summary/counter 방식 전환.
- 정식 VFX prefab 교체.
- defender on-place / skill card / equipment producer가 `SpawnHazardWithVisual`을 호출하도록 통합.
- 차단형 hazard spec과의 lifecycle/visual 처리 정책 정렬.
