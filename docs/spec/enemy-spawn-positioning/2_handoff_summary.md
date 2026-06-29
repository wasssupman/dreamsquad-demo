# 2 — Handoff Summary (최종, units 0~4)

## Commit
- `2487bb0` `feat(view): 적 비주얼 피봇 오프셋 — 유닛 타입별 visualOffset (0)`
- `010a32e` `feat(spawn): 적 스폰 측면 분산 — 셀 내 sub-cell 오프셋 (1)`
- `06cc883` `feat(spawn): 상/중/하 슬롯 → 중앙 ± 연속 랜덤 분산 (4)`
- `9aacea5` `docs(spec): 완료 표기/handoff/backlog`
- main 직접 커밋(이 프로젝트 관행). 무관 dirty 에셋 미스테이징 보존.

## Implemented (units 0~4)
- **목표1 — 비주얼 피봇 오프셋**: `AttackUnitData.visualOffset`(유닛 타입별, view-space). 공유 계약
  `ISpineUnitVisualData.SpineVisualOffset` 로 확장(타입 분기 없이). `SpineUnitView.ApplyRenderPosition`
  단일 지점. 기본 `(0,0,0)` 무회귀. `_simWorld` 불오염 → 정렬/셀 무영향.
- **목표2 — 스폰 측면 분산**: 스폰 시 **중앙 기준 ± 연속 랜덤** perpendicular 오프셋
  (`[−fraction, +fraction·topScale]×tile`). cardinal flow 가 전방 보존 → 직진 간격 유지, 한 점 겹침 해소.
- **핵심 불변식 `|오프셋|<0.5·tile`**(`SpawnSpread.MaxHalfFraction` + `LateralOffset` clamp) → 셀 단위 시스템 불변.
- **`topScale`**: 키 큰 캐릭터가 화면 위로 솟지 않게 상단(+) 범위만 압축(기본 0.5).
- `SpawnSpread` 순수 헬퍼(`FractionRange`/`Perpendicular`/`LateralOffset`) + EditMode 10/10.
  `BattleBridge` 스폰 한 줄 `+= 오프셋`, `_spawnSpreadRng`(map seed 빌드 리셋).
- (이력: unit 1 sub-cell 토대 → unit 3 비대칭 슬롯 → **unit 4 연속 랜덤**으로 수렴. 슬롯/Sequential·Random 모드는 unit 4 에서 제거.)

## Key Files
- 목표1: `Data/AttackUnitData.cs`, `Data/ISpineUnitVisualData.cs`, `Data/DefenderUnitData.cs`, `Presentation/SpineUnitView.cs`
- 목표2: `Battle/Movement/SpawnSpread.cs`, `Tests/EditMode/SpawnSpreadTests.cs`, `Bridge/BattleBridge.cs`(`Spawn Spread` 노브 + `ComputeSpawnLateralOffset`)

## Verified
- compile 0 에러. EditMode `SpawnSpreadTests` 10/10. Play 측정(execute_code): 적 13마리 중 10마리가
  spread 범위(avg perp 0.175) 내 — 분산 정상.

## Notes (되돌리면 안 되는 의도)
- 측면 분산은 **sim 스폰 위치**(셀 내 sub-cell). `|오프셋|<0.5·tile` 불변식이 핵심.
- 목표1 `visualOffset`(view-space)과 목표2 스폰 오프셋(sim)은 **직교**.
- `FlowFieldBuilder` 가 cardinal 이라 오프셋 전방 보존 + **중심 복원 없음**. 이 "복원 없음"이 코너 엣지-허깅의 원인(아래 follow-up).
- `spawnSpreadFraction`/`topScale` 은 BattleScene serialized(C# default 0.2/0.5, 씬 미저장). 튜닝은 사용자 영역.

## Follow-up
- **코너 lane-centering** → `docs/spec/enemy-tile-movement-integrity/` (2026-06-29 분리). 코너에서 유닛이 셀
  엣지(±0.49)에 얼어붙어 경로 밖처럼 보임 — flow 에 중심 복원력 없음. ecs-reviewer + 설계 비평 결과
  **본 spec 스코프 밖 + 메커니즘 재설계 필요**로 판정 → 별도 스펙. 진단/측정은 `4_continuous_spread.md` 완료 라인.
- Quad 폴백 `visualOffset` 미배선(적=Spine 라 무영향).
- 유닛 간 separation/boid. 블록 시 우회 재라우팅(`BuildFlowField` rebuild) — 이동 아키텍처 별도.
