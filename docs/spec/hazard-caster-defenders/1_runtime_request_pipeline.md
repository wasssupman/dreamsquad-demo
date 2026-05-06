# 1. Runtime Request Pipeline

## 목적

ECS 에서는 hazard spawn 요청만 만들고, `BattleBridge` 가 기존 visual 포함 spawn API 를 호출하도록 경계를 고정한다.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/HazardCastState.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/HazardSpawnRequest.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- Modify: `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs` only if small helpers are needed

## 구현

`HazardCastState` 는 unmanaged runtime state 만 가진다.

```csharp
public struct HazardCastState : IComponentData
{
    public float range;
    public float cooldownDuration;
    public float cooldownRemaining;
    public int targetMask;
    public int dataIndex;
    public HazardCastKind kind;
    public int footprintWidth;
    public int footprintHeight;
}
```

`BattleBridge.CreateDefenderEntity` 는 `DefenderUnitData.hazardCastEnabled` 를 읽고 `HazardCastState` 를 부착한다. `HazardSO` / `BlockingHazardSO` 는 bridge registry 에 등록하고 ECS 에는 `dataIndex` 만 전달한다.

요청 채널은 `HazardSpawnRequestsSingleton` 으로 둔다.

```csharp
// Assets/_Project/Scripts/Battle/Effects/HazardSpawnRequest.cs
public struct HazardSpawnRequestsSingleton : IComponentData
{
    public NativeQueue<HazardSpawnRequest> queue;
}

public struct HazardSpawnRequest
{
    public HazardCastKind kind;
    public int dataIndex;
    public int2 centerCell;
    public int width;
    public int height;
    public Entity caster;
    public Entity target;
}
```

**Singleton 생성/해제는 BattleBridge 가 담당한다.** 실제 hook point 3곳:

**필드 추가** (`BattleBridge` 필드 선언 블록):
```csharp
private NativeQueue<HazardSpawnRequest> _hazardSpawnRequestQueue;
```

**`EnsureQueriesAndQueues()`** (line ~641, 기존 queue 생성 블록 말미):
```csharp
if (_hazardSpawnRequestQueue.IsCreated) _hazardSpawnRequestQueue.Dispose();
_hazardSpawnRequestQueue = new NativeQueue<HazardSpawnRequest>(Allocator.Persistent);
var hazardSpawnSingleton = _em.CreateEntity();
_em.AddComponentData(hazardSpawnSingleton,
    new HazardSpawnRequestsSingleton { queue = _hazardSpawnRequestQueue });
```
idempotent 패턴(IsCreated 가드 후 재생성)은 기존 코드와 동일하다.

**`TeardownCurrentBattle()`**:

1. 기존 singleton destroy 블록에 `HazardSpawnRequestsSingleton` query destroy 를 추가한다.
2. 기존 dispose 블록 말미에 queue dispose 를 추가한다.

```csharp
var hazardSpawnRequestSingletons =
    _em.CreateEntityQuery(ComponentType.ReadOnly<HazardSpawnRequestsSingleton>());
_em.DestroyEntity(hazardSpawnRequestSingletons);
hazardSpawnRequestSingletons.Dispose();

if (_hazardSpawnRequestQueue.IsCreated) _hazardSpawnRequestQueue.Dispose();
```

**`OnDestroy()`** (line ~2793 이후 기존 dispose 블록 말미):
```csharp
if (_hazardSpawnRequestQueue.IsCreated) _hazardSpawnRequestQueue.Dispose();
```

**drain 위치** — `Update()` 내 `DrainHazardRuntimeEvents()` 직전에 `DrainHazardSpawnRequests()` 를 추가한다.

drain 구현 계약:

```csharp
private void DrainHazardSpawnRequests()
{
    if (!_hazardSpawnRequestQueue.IsCreated) return;
    while (_hazardSpawnRequestQueue.TryDequeue(out var req))
    {
        if (!_em.Exists(req.caster)) continue;

        if (req.kind == HazardCastKind.Zone)
        {
            if (req.dataIndex < 0 || req.dataIndex >= _zoneHazardRegistry.Count)
            {
                Debug.LogWarning($"[HazardCast] Invalid zone hazard index {req.dataIndex}; dropping.");
                continue;
            }
            var so = _zoneHazardRegistry[req.dataIndex];
            if (so == null) continue;
            SpawnHazardWithVisual(so, req.centerCell);
        }
        else if (req.kind == HazardCastKind.Blocking)
        {
            if (req.dataIndex < 0 || req.dataIndex >= _blockingHazardSoRegistry.Count)
            {
                Debug.LogWarning($"[HazardCast] Invalid blocking hazard index {req.dataIndex}; dropping.");
                continue;
            }
            var so = _blockingHazardSoRegistry[req.dataIndex];
            if (so == null) continue;
            SpawnBlockingHazardWithVisual(so, req.centerCell);
        }
    }
}
```

`target` 은 request 생성 시 target cell snapshot 을 남기기 위한 디버그/로그 용도다. Drain 시점에 target 이 죽어도 spawn 은 진행한다. caster 가 죽었으면 cast 자체를 취소한다.

registry 접근은 기존 `_blockingHazardSoRegistry` (List) 패턴을 따른다. zone SO 는 동일 방식으로 `_zoneHazardRegistry` (List) 를 추가한다.

효과 적용은 다음 Simulation tick 부터 허용한다. 이 계약은 `HazardLifetimeSystem` 이 cell map 을 rebuild 하는 현재 구조와 맞춘다.

## 완료 기준

- `HazardSpawnRequestsSingleton` 이 named struct 로 존재한다.
- `EnsureQueriesAndQueues` 에서 idempotent 생성, `TeardownCurrentBattle` 에서 singleton entity destroy + queue dispose, `OnDestroy` 에서 queue dispose 를 수행한다.
- restart/teardown 후 queue singleton 이 중복되지 않는다.
- ECS component 에 SO/prefab/GameObject 참조가 없다.
- Zone/Blocking request drain 이 명확히 분기된다.
- invalid registry index / null SO / dead caster request 는 예외 없이 drop 된다.
- dead target request 는 target cell snapshot 기준으로 spawn 진행한다.
