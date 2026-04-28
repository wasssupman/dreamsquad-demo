# ZoneApplySystem

**작업 구분**: 3

## 목적

매 프레임 적의 cell → `cellToEffects` lookup → 각 effect 를 `EnemyCcEventsSingleton.queue` 로 enqueue. 짧은 `restDuration` + CC merge refresh 로 적이 zone 안 → 효과 지속, 빠져나감 → 자연 감쇠 (enter/exit 추적 없음).

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/ZoneApplySystem.cs`

## ZoneApplySystem

```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HazardLifetimeSystem))]
[UpdateBefore(typeof(CcApplySystem))]
public partial struct ZoneApplySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var hazardSingleton = SystemAPI.GetSingleton<HazardSingleton>();
        if (hazardSingleton.cellToEffects.Count() == 0) return;

        var ccQueue = SystemAPI.GetSingleton<EnemyCcEventsSingleton>().queue;
        if (!SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var ff)) return;

        foreach (var (transform, entity) in
                 SystemAPI.Query<RefRO<LocalTransform>>()
                          .WithAll<PathFollowState>()
                          .WithEntityAccess())
        {
            int2 cell = GridMath.WorldToCell(transform.ValueRO.Position, ff.tileSize, ff.gridSize);
            if (!hazardSingleton.cellToEffects.TryGetFirstValue(cell, out var effect, out var it)) continue;
            do
            {
                ccQueue.Enqueue(new EnemyCcEvent
                {
                    target = entity,
                    effect = HazardEffectToCcEffect(effect),
                });
            } while (hazardSingleton.cellToEffects.TryGetNextValue(out effect, ref it));
        }
    }

    private static CcEffect HazardEffectToCcEffect(in HazardEffect h)
    {
        return new CcEffect
        {
            kind = h.kind,
            scalar = h.param1,        // Slow=mul, DoT=dmg/sec
            vector = float3.zero,     // (Impulse 미래 확장 시 별도 변환 정책)
            remainingTime = h.restDuration,
        };
    }
}
```

## Burst / 동시성

- `cellToEffects` 는 read-only 사용 (HazardLifetimeSystem 이 이전 stage 에서 갱신 완료).
- `ccQueue.Enqueue` 는 main-thread 단일 enqueue (현재 시스템 구조 부합).
- 추후 IJobEntity 로 전환 시 `NativeQueue.AsParallelWriter`.

## 단위 테스트 (EditMode)

`ZoneApplySystemTests`:
- 적 1마리 + 한 cell hazard (Slow effect 1개) → 매 프레임 ccQueue 에 1 event enqueue
- 같은 cell 에 hazard 2개 (Slow + DoT 합쳐 2 effect) → 매 프레임 ccQueue 에 2 event enqueue
- 적이 hazard 외 cell 위치 → ccQueue enqueue 0
- 빈 cellToEffects → enqueue 0
- 변환 정확성: HazardEffect{kind=Slow, param1=0.4, restDuration=0.2} → CcEffect{kind=Slow, scalar=0.4, remainingTime=0.2}

## 완료 기준

- 컴파일 + Burst 활성.
- 단위 테스트 통과.
- spawn 진입점 미존재 → 런타임 동작 변화 0.
- 콘솔 에러/경고 0.
