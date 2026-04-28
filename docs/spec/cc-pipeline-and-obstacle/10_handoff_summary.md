# CC Pipeline & Obstacle — Handoff Summary

**완료일**: 2026-04-29

## Commits

| 커밋 | 설명 |
|---|---|
| c788a6c | Unit 0 — CcEffect/EnemyCcEvents 데이터 모델 |
| 53c0bd9 | Unit 1 — CcApplySystem + CcDecaySystem + NativeQueue lifecycle |
| 1638c77 | Unit 2 — SlowEffect → CcEffect 마이그레이션 |
| dba2bad | Unit 3 — MovementSystem Impulse 합성 |
| 61a3965 | Unit 4 — DefenderSO 5필드 + DefenderCcData ECS 미러 |
| 1de57c9 | Unit 5 — AttackSystem 넉백 enqueue |
| 6ad4354 | fix — 넉백 방향 D-E (물리 충돌) + zero-flow 복구 |
| 66e2654 | Unit 6 — on-place 방사형 push |
| f733898 | Unit 7 — Obstacle/ObstacleSingleton/ObstacleLifetimeSystem |
| fd99f92 | Unit 8 — MovementCellTrim + obstacle cell-trim |
| 2ee808d | Unit 9 — SpawnObstacle API + debug menu/visual |
| 431726c | fix — ClampToBoundary epsilon-inset (통과 버그) |

## Implemented

- `CcKind` enum (Slow/Impulse) + `CcEffect` IBufferElementData + `EnemyCcEventsSingleton` NativeQueue
- `CcApplySystem` (큐 → buffer merge, UpdateBefore MovementSystem)
- `CcDecaySystem` (IJobEntity, remainingTime tick + remove, UpdateAfter MovementSystem)
- `SlowEffect` 완전 제거 → `EffectSpawner.ApplySlow` 내부가 buffer 경로로 전환 (시그니처 유지)
- MovementSystem switch (Slow × speedMul, Impulse × displacement 독립 합성)
- MovementSystem zero-flow 복구 (4-neighbor dist 최솟값 방향)
- `DefenderCcData` IComponentData + DefenderUnitData 5필드 (모두 default 0)
- AttackSystem 넉백 enqueue (knockbackDistance > 0, 방향 = normalize(D-E))
- BattleBridge `ApplyOnPlacePush` (drop 즉시 발동, `onPlacePushRadius` 내 방사형)
- `Obstacle`/`ObstacleSingleton`/`ObstacleLifetimeSystem` (매 프레임 blockedCells 재구축)
- `MovementCellTrim.IsWallCell` + `ClampToBoundary` (epsilon-inset)
- MovementSystem option-B cell-trim (obstacle + zero-flow 양쪽 차단)
- `EffectSpawner.SpawnObstacle` + `BattleBridge.DebugSpawnObstacleAt` + `ObstacleDebugMenu`

## Key Files

```
Assets/_Project/Scripts/Battle/Effects/
  CcEffect.cs                 — CC 데이터 모델
  EnemyCcEvents.cs            — 이벤트 큐 싱글턴
  CcApplySystem.cs            — 큐 → buffer
  CcDecaySystem.cs            — tick + remove
  EffectSpawner.cs            — ApplyCc, ApplySlow (thin wrapper), SpawnObstacle
  Obstacle.cs / ObstacleSingleton.cs / ObstacleLifetimeSystem.cs
  ObstacleDebugMenu.cs        — #if UNITY_EDITOR

Assets/_Project/Scripts/Battle/Movement/
  MovementSystem.cs           — Slow/Impulse 합성, cell-trim, zero-flow 복구
  MovementCellTrim.cs         — IsWallCell, ClampToBoundary (epsilon-inset)

Assets/_Project/Scripts/Battle/Combat/
  AttackSystem.cs             — 넉백 enqueue
  DefenderCcData.cs           — SO CC 필드 ECS 미러

Assets/_Project/Scripts/Data/
  DefenderUnitData.cs         — knockback/onPlacePush 5필드

Assets/_Project/Scripts/Bridge/BattleBridge.cs
  — ApplyOnPlacePush, DebugSpawnObstacleAt, 모든 singleton lifecycle
```

## Verified

- 컴파일 에러/경고 0
- 133/133 EditMode 테스트 통과
- PlayMode Unit 2 ★: Slow 회귀 없음 확인
- PlayMode Unit 5: 넉백 경로 유지 + 넉백 후 재이동 확인
- PlayMode Unit 6: drop 즉시 방사형 push 확인
- PlayMode Unit 9 ★: 시나리오 1~4 (기본 차단/다중 적/knockback×cube/push×cube) 확인

## Notes

- **넉백 방향**: `normalize(D - E)` (D=투사체 방향, E=적 flow 방향) 물리 충돌 모델. FlowField 없을 때 D fallback.
- **ClampToBoundary epsilon**: `half = tileSize*0.5 - 1e-3`. WorldToCell이 정확히 0.5*tileSize를 인접 셀로 반올림하므로 epsilon 없으면 clamped 위치가 즉시 차단 셀로 매핑됨.
- **CcDecaySystem .Run()**: `.Schedule()` 대신 동기 실행. 다음 프레임 MovementSystem의 CcEffect 읽기와 cross-frame dependency conflict 방지.
- **zero-flow 복구**: 임펄스로 flow=0 셀 진입 시 4-neighbor dist 최솟값 방향으로 탈출. 5초 등 큰 lifetime으로 적을 가두면 큐브 소멸 후 자동 복귀.
- **on-place push 타이밍**: `TryBeginDefenderDeployment`(drop 즉시) — `TriggerDeploymentOnPlaceSkill`(애니메이션 후)가 아님.
- **FlowFieldBuilder**: ObstacleSingleton 참조 안 함 (큐브 때문에 재경로 없음).

## Follow-up

- 큐브 시각 Presenter (ObstaclePresenter MonoBehaviour, optional mesh/particle)
- 큐브 spawn 게임 통합 (디펜더 능력 / 스킬 카드)
- 적-적 분산 처리 (여러 적이 같은 큐브 앞에 겹침)
- Stun/Root 등 추가 CcKind — enum + switch case 추가만으로 확장 가능
