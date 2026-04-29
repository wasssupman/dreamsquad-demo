# HazardDestroyedEvents Channel + UnitLifecycleSystem Branch

**작업 구분**: 5

## 목적

차단형 hazard 의 destruction 알림 채널 신설. ECS 가 entity destroy 직전 이벤트 enqueue → BattleBridge drain → visual destroy + destruction VFX 트리거.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/HazardDestroyedEvent.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/HazardDestroyedEventsSingleton.cs`
- Modify: `Assets/_Project/Scripts/Battle/Units/UnitLifecycleSystem.cs` (hazard 분기 추가)
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (singleton lifecycle 등록 — Unit 7 의 spawn API 기반 작업과 묶어 처리 가능)

## 구현

### HazardDestroyedEvent.cs

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct HazardDestroyedEvent
    {
        public Entity hazardEntity;   // already-destroyed (drain 시점 엔티티 invalid) — 매핑 lookup 용
        public int    hazardSoIndex;  // visual prefab / VFX 매핑
        public float3 worldPosition;  // destruction VFX 트리거 좌표
        public int2   centerCell;     // 진단 / 로깅
    }
}
```

### HazardDestroyedEventsSingleton.cs

기존 `DefenderDeathEventsSingleton.cs` 를 정확히 복제 — `NativeQueue<HazardDestroyedEvent> queue`, `Dispose` 패턴.

```csharp
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    public struct HazardDestroyedEventsSingleton : IComponentData
    {
        public NativeQueue<HazardDestroyedEvent> queue;
    }
}
```

### UnitLifecycleSystem 분기 추가

현재 `UnitLifecycleSystem.cs` line 79~85 (catch-all dead loop):
```csharp
foreach (var (_, entity) in
         SystemAPI.Query<RefRO<DeadTag>>()
                  .WithNone<DefenderTile>()
                  .WithEntityAccess())
{
    ecb.DestroyEntity(entity);
}
```

**변경 — hazard 분기를 먼저, defender-dead 와 동일한 enqueue-before-destroy 패턴**:
```csharp
// Hazard destruction 이벤트 — entity destroy 보다 먼저 enqueue
bool hasHazardSink = _hazardDestroyedSingletonQuery.CalculateEntityCount() == 1;
foreach (var (hazard, transform, entity) in
         SystemAPI.Query<RefRO<BlockingHazard>, RefRO<LocalTransform>>()
                  .WithAll<DeadTag>()
                  .WithEntityAccess())
{
    if (hasHazardSink)
    {
        var sink = _hazardDestroyedSingletonQuery.GetSingletonRW<HazardDestroyedEventsSingleton>();
        var obstacle = SystemAPI.GetComponent<Obstacle>(entity);
        sink.ValueRW.queue.Enqueue(new HazardDestroyedEvent
        {
            hazardEntity   = entity,
            hazardSoIndex  = hazard.ValueRO.hazardSoIndex,
            worldPosition  = transform.ValueRO.Position,
            centerCell     = obstacle.cell,
        });
    }
    ecb.DestroyEntity(entity);
}

// 기존 catch-all — hazard 는 위에서 처리됐지만 WithNone<BlockingHazard> 필터로 중복 destroy 방지
foreach (var (_, entity) in
         SystemAPI.Query<RefRO<DeadTag>>()
                  .WithNone<DefenderTile>()
                  .WithNone<BlockingHazard>()       // ← 추가
                  .WithEntityAccess())
{
    ecb.DestroyEntity(entity);
}
```

`OnCreate` 에 query 추가:
```csharp
_hazardDestroyedSingletonQuery = state.GetEntityQuery(
    ComponentType.ReadWrite<Wassup.Battle.Effects.HazardDestroyedEventsSingleton>());
```

### BattleBridge singleton lifecycle

기존 `DefenderDeathEventsSingleton` lifecycle 패턴 그대로 — World init 시 `new NativeQueue<HazardDestroyedEvent>(Allocator.Persistent)` 생성, OnDestroy 에서 dispose. (Unit 7 의 spawn API 통합 시 함께 추가.)

### 핵심 결정

- **enqueue 가 ECB destroy 보다 먼저** — DefenderDeath 패턴 (line 67~72) 동일. drain 시점에 entity 는 invalid 이지만 worldPosition / SO index 메타로 visual 매핑 충분.
- **hazardEntity 는 매핑 lookup 용** — BattleBridge 의 `Dictionary<Entity, GameObject>` 매핑 검색 키. drain 시 entity invalid 라도 dictionary key 비교는 가능.
- **catch-all loop 의 `WithNone<BlockingHazard>` 필터** — hazard 가 위 분기에서 이미 destroy → 두 번 destroy 방지.

## 단위 테스트 (EditMode)

`HazardDestroyedEventTests`:
- DeadTag 부착된 hazard → queue 에 1 event enqueue + entity destroy.
- 이벤트의 `worldPosition` / `centerCell` / `hazardSoIndex` 가 hazard 의 메타와 일치.
- queue sink 미존재 시 → entity destroy 만 수행 (fail-open, DefenderDeath 패턴 동일).
- 중복 destroy 0 (catch-all 분기 회귀 검증).

## 완료 기준

- 컴파일 + Burst 활성.
- EditMode 신규 테스트 통과 + 기존 회귀 0.
- BattleBridge 의 singleton lifecycle 정상 (Editor 종료 시 NativeQueue 누수 경고 0). Unit 7 과 묶어 검증.
- 동작 변화: 본 unit 단독으론 hazard entity 가 없어 효과 0. Unit 7 spawn 후 검증 가능.
- 콘솔 에러/경고 0.

검증: 2026-04-29 — `HazardDestroyedEventTests` 추가, 관련 3/3 통과, 전체 EditMode 149/149 통과, 콘솔 에러/경고 0. BattleBridge drain 은 Unit 8 visual 매핑 전까지 queue drain stub. 커밋 `3f5ab31`.
