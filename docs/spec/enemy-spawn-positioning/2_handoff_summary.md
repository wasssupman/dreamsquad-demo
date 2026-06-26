# 2 — Handoff Summary

## Commit
- `2487bb0` `feat(view): 적 비주얼 피봇 오프셋 — 유닛 타입별 visualOffset (enemy-spawn-positioning 0)`
- `010a32e` `feat(spawn): 적 스폰 측면 분산 — 셀 내 sub-cell 오프셋 (enemy-spawn-positioning 1)`
- main 직접 커밋(이 프로젝트 spec 워크플로우 관행). 무관한 dirty 에셋 118개 미스테이징 보존.

## Implemented
- **목표 1 — 비주얼 피봇 오프셋**: `AttackUnitData.visualOffset`(유닛 타입별, view-space). 공유 계약
  `ISpineUnitVisualData.SpineVisualOffset` 로 확장(타입 분기 없이). `SpineUnitView.ApplyRenderPosition`
  단일 지점에서 `(Vector3)ToView(world)+offset`. 기본 `(0,0,0)` 무회귀. `_simWorld` 불오염 → 정렬/셀 무영향.
- **목표 2 — 스폰 측면 분산**: 같은 lane 적을 스폰 셀 내 상/중/하 sub-cell 로 분산. cardinal flow 가
  오프셋을 전방 보존 → 직진 구간 간격 유지, 한 점 겹침 해소.
- `SpawnSpread` 순수 헬퍼(`SlotFraction`/`Perpendicular`/`LateralOffset`, `|오프셋|<0.5타일` 강제) + EditMode 8/8.
- `BattleBridge`: `spawnWorldPos += ComputeSpawnLateralOffset(...)` **한 줄**. lateral 축 = `flow[spawnCell]` 수직.
  슬롯 배정 Sequential(lane별 round-robin)/Random(seed 결정론). config 4노브. 슬롯 커서·RNG 는 맵 빌드마다 리셋.

## Key Files
- 목표1: `Data/AttackUnitData.cs`, `Data/ISpineUnitVisualData.cs`, `Data/DefenderUnitData.cs`, `Presentation/SpineUnitView.cs`
- 목표2: `Battle/Movement/SpawnSpread.cs`, `Tests/EditMode/SpawnSpreadTests.cs`, `Bridge/BattleBridge.cs`

## Verified
- compile 0 에러(CS). EditMode `SpawnSpreadTests` 8/8 pass. Play: 상/중/하 분산 유지·겹침 해소 사용자 확인.
- console: 기존 missing-script 경고(무관) 외 신규 에러 없음.

## Notes (되돌리면 안 되는 의도)
- 측면 분산은 **sim 스폰 위치**(셀 내 sub-cell) — 비주얼 전용 아님. 타겟팅도 실제로 ≤0.33타일 분산된다.
  단 **`|오프셋|<0.5타일` 불변식**이 핵심: 유닛이 스폰 셀에 머물러 `WorldToCell`/goal/cell-trim/blockedCells
  등 셀 단위 시스템이 그대로 동작. `SpawnSpread.MaxHalfFraction(0.49)` 가 이를 강제 — 풀거나 키우면 셀 단위 로직 오작동.
- 목표1 `visualOffset`(view-space 피봇)과 목표2 스폰 오프셋(sim)은 **직교**. 혼동 금지.
- `FlowFieldBuilder` 가 cardinal 단위벡터라 직진 보존이 성립. flow 가 diagonal/centering 으로 바뀌면 분산 거동 재검토.
- `spawnSpreadFraction` 는 BattleScene serialized(현재 C# default 0.33 사용, 씬 미저장). 튜닝은 사용자 영역.

## Follow-up
- **비주얼 수직추적**: 코너에서도 항상 경로 수직 유지(현재는 코너에서 측면→앞뒤 회전). sim 은 중심선, View 가 렌더 오프셋.
- Quad 폴백 경로(`QuadUnitView`) `visualOffset` 미배선 — 적=Spine 라 무영향. 필요 시 사소.
- 유닛 간 separation/boid 회피.
- 블록 시 우회 재라우팅(`BuildFlowField` rebuild 트리거) — 이동 아키텍처 별도 스펙(대화 중 논의됨, flow field 유지 결론).
