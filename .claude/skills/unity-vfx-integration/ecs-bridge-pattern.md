# ECS Bridge Pattern

이 문서는 현재 코드베이스의 `MeteorBurstEvent` 패턴을 기준으로 VFX 통합 템플릿을 요약한다.

참조 파일:
- `Assets/_Project/Scripts/Battle/Combat/MeteorBurstEvent.cs`
- `Assets/_Project/Scripts/Battle/Combat/MeteorBurstEventsSingleton.cs`
- `Assets/_Project/Scripts/Battle/Combat/MeteorResolutionSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 목적
- ECS simulation 완료 시점에만 발동해야 하는 VFX 를 MonoBehaviour 계층으로 넘긴다.
- ECS 내부 판정과 렌더 연출의 시간차를 줄이되, ownership 은 `BattleBridge` 하나로 고정한다.

## 템플릿 구조
1. 값 타입 payload struct 작성
2. `NativeQueue<T>` 를 가진 singleton component 작성
3. 관련 ECS system 에서 `AsParallelWriter()` 로 enqueue
4. `BattleBridge` 가 queue 를 생성, singleton 을 주입, `Update()` 에서 drain
5. drain 시 `VfxSpawner` 또는 presenter 의 public method 호출

## 예시: MeteorBurstEvent
현재 구현은 아래 의도를 가진다.
- `MeteorResolutionSystem` 이 `MeteorPending.warningRemaining` 이 0 이하가 되는 프레임에 피해를 적용한다.
- 같은 프레임에 `MeteorBurstEvent { center, radius }` 를 queue 에 넣는다.
- `BattleBridge.Update()` 가 `DrainMeteorBurstEvents()` 에서 dequeue 한다.
- dequeue 된 payload 는 `vfxSpawner.SpawnMeteorBurst(new Vector3(evt.center.x, 0f, evt.center.z), evt.radius)` 로 전달된다.

## 구현 템플릿
```csharp
public struct ExampleImpactEvent
{
    public float3 center;
    public float radius;
}

public struct ExampleImpactEventsSingleton : IComponentData
{
    public NativeQueue<ExampleImpactEvent> queue;
}
```

```csharp
private NativeQueue<ExampleImpactEvent> _exampleImpactQueue;

private void EnsureQueriesAndQueues()
{
    if (_exampleImpactQueue.IsCreated) _exampleImpactQueue.Dispose();
    _exampleImpactQueue = new NativeQueue<ExampleImpactEvent>(Allocator.Persistent);
    var singleton = _em.CreateEntity();
    _em.AddComponentData(singleton, new ExampleImpactEventsSingleton { queue = _exampleImpactQueue });
}
```

```csharp
NativeQueue<ExampleImpactEvent>.ParallelWriter? writer = null;
if (!_exampleImpactQuery.IsEmpty)
{
    var singleton = _exampleImpactQuery.GetSingletonRW<ExampleImpactEventsSingleton>();
    writer = singleton.ValueRW.queue.AsParallelWriter();
}

if (writer.HasValue)
{
    writer.Value.Enqueue(new ExampleImpactEvent
    {
        center = center,
        radius = radius,
    });
}
```

```csharp
private void DrainExampleImpactEvents()
{
    if (!_exampleImpactQueue.IsCreated) return;
    while (_exampleImpactQueue.TryDequeue(out var evt))
    {
        if (vfxSpawner == null) continue;
        vfxSpawner.SpawnExampleImpact(new Vector3(evt.center.x, 0f, evt.center.z), evt.radius);
    }
}
```

## 규칙
- MonoBehaviour 는 queue singleton 을 직접 찾지 않는다. `BattleBridge` 필드만 사용한다.
- payload 에 `Entity`, `GameObject`, `Material` 같은 소유권 복잡한 참조를 넣지 않는다.
- queue 생성과 dispose 책임은 `BattleBridge` 에 둔다.
- direct call 과 queue path 가 둘 다 필요하면 분리 유지한다.

## 언제 direct call 을 쓰나
- placement pulse
- warning ring
- 클릭 즉시 피드백

## 언제 NativeQueue 를 쓰나
- 실제 피해 적용 프레임의 burst
- 사망 확정 이후의 이펙트
- ECS 상태 전이와 1프레임 정확도로 묶여야 하는 경우
