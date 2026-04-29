# RebuildDraftMap Cleanup Responsibilities

**작업 구분**: 1

## 목적

`RebuildDraftMap()` 호출 시 시각/ECS 양쪽 모두 누적 없이 깨끗하게 재빌드되도록 cleanup 책임을 정리한다. 옵션 토글 50회 반복해도 GameObject / Entity / NativeArray 누수 없음.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `CleanupDraftMapBeforeRebuild()` 신설
- Modify: `Assets/_Project/Scripts/Core/MapView.cs` — `ResetVisualRoots()` 신설 (obstacles/background props root 재생성)

## 구현

### MapView.ResetVisualRoots()

현재 `MapView.OnDestroy` (line 78~80) 에서만 `_obstaclesRoot` / `_backgroundPropsRoot` / `_goalMarkerRoot` 를 SafeDestroy. `Initialize()` 는 이들을 건드리지 않으므로 재호출 시 GameObject 가 누적된다.

```csharp
public void ResetVisualRoots()
{
    if (_obstaclesRoot != null) { SafeDestroy(_obstaclesRoot.gameObject); _obstaclesRoot = null; }
    if (_backgroundPropsRoot != null) { SafeDestroy(_backgroundPropsRoot.gameObject); _backgroundPropsRoot = null; }
    if (_goalMarkerRoot != null) { SafeDestroy(_goalMarkerRoot.gameObject); _goalMarkerRoot = null; }
}
```

(이미 BuildSharedMaterials / BuildTiles 가 자체적으로 dispose+재생성하므로 본 메서드는 그 외 root 만 처리.)

### BattleBridge.CleanupDraftMapBeforeRebuild()

```csharp
private void CleanupDraftMapBeforeRebuild()
{
    // 1. ECS entity teardown — 옵션 변경마다 hazard / blockingHazard / placed obstacle 누적 방지
    if (_em != null)
    {
        DestroyEntitiesByType<Wassup.Battle.Effects.Hazard>();
        DestroyEntitiesByType<Wassup.Battle.Effects.BlockingHazard>();
        DestroyEntitiesByType<Wassup.Battle.Effects.Obstacle>();
    }

    // 2. Visual root teardown — obstacles + background props GameObjects
    if (mapView != null) mapView.ResetVisualRoots();

    // 3. BlockingHazard visual + SO registry (TeardownCurrentBattle 패턴 — line 236, 315~316)
    ClearBlockingHazardVisuals();
    _blockingHazardSoRegistry.Clear();
    _blockingHazardSoIndex.Clear();

    // 4. Map data + flow field
    TeardownGeneratedMap();
    TeardownFlowField();
}

private void DestroyEntitiesByType<T>() where T : unmanaged, IComponentData
{
    using var q = _em.CreateEntityQuery(ComponentType.ReadOnly<T>());
    if (!q.IsEmpty) _em.DestroyEntity(q);
}
```

컴포넌트 타입 (검증 완료):
- `Wassup.Battle.Effects.Hazard` — `Assets/_Project/Scripts/Battle/Effects/Hazard.cs:6`
- `Wassup.Battle.Effects.BlockingHazard` — `Assets/_Project/Scripts/Battle/Effects/BlockingHazard.cs:6`
- `Wassup.Battle.Effects.Obstacle` — `Assets/_Project/Scripts/Battle/Effects/Obstacle.cs:6`

**참고 — `ResetVisualRoots` 의 위치**: `MapView.Initialize` 가 호출하는 `BuildTiles` (line 140) / `BuildGoalMarker` 는 자체적으로 root 를 SafeDestroy 후 재생성한다. `InstantiateObstacles` (line 728) / `InstantiateBackgroundProps` 도 동일. 따라서 `ResetVisualRoots` 는 **방어용** — `CleanupDraftMapBeforeRebuild` 와 `BuildMapForBattle` 사이에 다른 코드가 끼어들거나, 미래에 root 자체 cleanup 이 빠진 ondemand 시각이 추가될 때 누수 0 을 보장한다. 본 spec 의 현재 코드 경로에서는 redundant 하지만 명시적으로 둔다.

### 누락 검증 체크리스트

Rebuild 시 다음 항목이 누적/leak 되지 않는지 점검:

- [ ] `_generatedMap` (NativeArray) — `TeardownGeneratedMap` 처리.
- [ ] `_flowField` (NativeArray) — `TeardownFlowField` 처리.
- [ ] `mapView._obstaclesRoot` (GameObject) — `ResetVisualRoots`.
- [ ] `mapView._backgroundPropsRoot` (GameObject) — `ResetVisualRoots`.
- [ ] `mapView._goalMarkerRoot` (GameObject) — `ResetVisualRoots`.
- [ ] `mapView._tilesRoot` (GameObject) — `BuildTiles` 자체 dispose.
- [ ] `mapView._tileFallbackMaterials` / `_tileTextureMaterials` — `BuildSharedMaterials` 자체 dispose.
- [ ] `Hazard` / `BlockingHazard` / `Obstacle` ECS entity — `DestroyEntitiesByType<>`.
- [ ] `_blockingHazardSoRegistry` (List) + `_blockingHazardSoIndex` (Dictionary) — `Clear()`.
- [ ] BlockingHazard visual GameObject — `ClearBlockingHazardVisuals()` 재사용.
- [ ] NativeQueue 8개 (GoalReached / DefenderDeath / MeteorBurst / DefenderAttack / ProjectileHit / EnemyCc / HazardRuntime / HazardDestroyed) + Singleton entity — **disposed 안함** (queues 는 draft 동안 비어 있음. `_ecsInfrastructureReady` 가드로 EnsureQueriesAndQueues 가 1회만 실행).

### 호출자 변경

Unit 0 의 `RebuildDraftMap` 이 `CleanupDraftMapBeforeRebuild` 호출 → `BuildMapForBattle` 호출 순서 보장.

## 단위 테스트 (EditMode)

Unit 4 에서 통합:
- `RebuildDraftMap` 50회 호출 후 `_em.UniversalQuery.CalculateEntityCount` 변동 < threshold.
- `mapView.transform.childCount` 가 한정 범위 (Tiles + Obstacles + BackgroundProps + Goal = 4 root).

## 완료 기준

- 컴파일 성공.
- `MapView.ResetVisualRoots()` 메서드 존재.
- `BattleBridge.CleanupDraftMapBeforeRebuild()` 메서드 존재 + RebuildDraftMap 에서 호출.
- destructible-blocking-hazards / cc-pipeline-and-obstacle 컴포넌트 타입 이름 검증 완료 (코드 grep 으로 정확한 이름 사용).
- 콘솔 에러/경고 0.

검증: 2026-04-30 — 컴파일 + EditMode 회귀 0 (RebuildDraftMap 미연결 상태로 contract 만 채움). PlayMode 검증은 Unit 5 V1~V10 에서 통합. 커밋 `3833c8a`.
