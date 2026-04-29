# Bridge: PrepareDraftMap / RebuildDraftMap + BeginPlacement Skip

**작업 구분**: 0

## 목적

`BattleBridge` 에 draft 진입 시점의 맵 빌드 진입점을 추가한다. 기존 `EnsureQueriesAndQueues()` 마지막에 inline 호출되는 `BuildMapForBattle()` 를 분리해, draft / placement 어느 단계에서든 명시적으로 호출 가능하게 만든다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

### 1. EnsureQueriesAndQueues 에서 BuildMapForBattle 분리

현재 `EnsureQueriesAndQueues()` 의 마지막 줄 (line ~683):

```csharp
BuildMapForBattle();
```

이 줄을 **삭제**한다. 호출 책임을 PrepareDraftMap / RebuildDraftMap / BeginPlacement(폴백) 으로 이전.

### 2. EnsureQueriesAndQueues 멱등화

연속 호출이 안전하도록 보장. 이미 대부분의 NativeQueue 가 `IsCreated → Dispose` 패턴이지만, 호출 빈도를 줄이기 위해 가드 플래그를 둔다:

```csharp
private bool _ecsInfrastructureReady;

private void EnsureQueriesAndQueues()
{
    if (_ecsInfrastructureReady) return;  // ★ 추가
    // ... 기존 로직 (queries + queues + singletons) ...
    _ecsInfrastructureReady = true;       // ★ 추가
}
```

**Reset 책임 (정확히 두 곳)**:

- `TeardownCurrentBattle()` — 이 메서드가 NativeQueue dispose + singleton entity destroy 까지 처리하므로 (line ~226~322), 끝부분에 `_ecsInfrastructureReady = false` 추가.
- `OnDestroy()` — 라이프사이클 종료. line ~2421.

**금지** — `StopBattle()` 은 `_running = false` / `_placementAllowed = false` 만 토글하고 ECS state 를 dispose 하지 않으므로 (line 686~692), 여기서 reset 하면 다음 EnsureQueriesAndQueues 호출 시 기존 singleton entity 가 살아있는 채로 새로 만들어 **duplicate singleton** 이 생긴다. Reset 금지. `OnDisable` 메서드는 BattleBridge 에 존재하지 않음 — 추가 금지.

### 3. PrepareDraftMap 신설

```csharp
public void PrepareDraftMap()
{
    if (deck == null || map == null)
    {
        Debug.LogError("[BattleBridge] deck or map reference missing.", this);
        return;
    }
    _world = World.DefaultGameObjectInjectionWorld;
    if (_world == null)
    {
        Debug.LogWarning("[BattleBridge] Default World not ready at PrepareDraftMap; deferring 1 frame.");
        StartCoroutine(DeferredPrepareDraftMap());
        return;
    }
    _em = _world.EntityManager;

    EnsureQueriesAndQueues();   // ECS infrastructure (queues, singletons, queries)
    BuildMapForBattle();         // Map + flow field + obstacles + props + camera
}

private System.Collections.IEnumerator DeferredPrepareDraftMap()
{
    yield return null;
    PrepareDraftMap();
}
```

### 4. RebuildDraftMap 신설

```csharp
public void RebuildDraftMap()
{
    if (_world == null) { PrepareDraftMap(); return; }

    // Unit 1 책임 — visual + ECS cleanup
    CleanupDraftMapBeforeRebuild();
    BuildMapForBattle();
}
```

(`CleanupDraftMapBeforeRebuild` 구현은 Unit 1.)

### 5. BeginPlacement skip 가드

현재 `BeginPlacement()` 는 `EnsureQueriesAndQueues()` 호출 시 자동으로 BuildMapForBattle 까지 끌고 갔다. 분리 후 BeginPlacement 는:

```csharp
public void BeginPlacement()
{
    // ... 기존 state init (placementAllowed, _pending.Clear, etc.) ...
    EnsureQueriesAndQueues();  // 멱등 — PrepareDraftMap 이 이미 했으면 no-op

    // ★ 폴백: PrepareDraftMap 이 호출되지 않은 edge (테스트, 직접 진입) 에서만 빌드
    if (!_generatedMap.IsCreated)
    {
        Debug.LogWarning("[BattleBridge] BeginPlacement: map not prepared, building now.");
        BuildMapForBattle();
    }
}
```

### 6. Public API 표면

본 unit 후 `BattleBridge` 의 외부 호출 가능 메서드:

| 메서드 | 호출자 | 역할 |
|---|---|---|
| `PrepareDraftMap()` | `GameManager.Start` | 1회. ECS infra + 맵 빌드 |
| `RebuildDraftMap()` | `DraftController` (옵션 변경 / Redraft) | 정리 후 재빌드 |
| `BeginPlacement()` | `PlacementPhaseView` (DraftConfirmed) | placement 상태 진입. 맵 이미 있으면 skip |
| `StartBattle()` | `PlacementPhaseView` (placement 카운트다운 후) | 변경 없음 |

## 단위 테스트 (EditMode)

Unit 4 에서 통합 작성 (PrepareDraftMap → IsCreated, BeginPlacement → BuildMapForBattle 미호출 등).

## 완료 기준

- 컴파일 성공.
- `EnsureQueriesAndQueues` 마지막 줄의 `BuildMapForBattle()` 호출 제거 확인.
- `PrepareDraftMap` / `RebuildDraftMap` 메서드 존재.
- `BeginPlacement` 가 `_generatedMap.IsCreated` 면 BuildMapForBattle 호출 안 함.
- 본 unit 만으로는 GameManager 호출 변경 없으므로 기존 동작 회귀 0 (BeginPlacement 가 폴백으로 빌드).
- 콘솔 에러/경고 0.
