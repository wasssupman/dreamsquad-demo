# Hazard Lifetime + Singleton

**작업 구분**: 2

## 목적

`HazardSingleton.cellToEffects` (NativeMultiHashMap) + `HazardLifetimeSystem` 신설. 매 프레임 hazard entity 의 lifetime tick + 만료 destroy + singleton map 재구축. 본 단위는 spawn API 미존재라 동작 변화 0.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/HazardSingleton.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/HazardLifetimeSystem.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — singleton 생성/해제 lifecycle 추가 (StartBattle, Cleanup, OnDestroy 모두)

## HazardSingleton

```csharp
public struct HazardSingleton : IComponentData
{
    public NativeMultiHashMap<int2, HazardEffect> cellToEffects;  // Allocator.Persistent
}
```

= 한 cell 에 여러 hazard overlap 가능 → MultiHashMap. ZoneApplySystem (Unit 3) 이 cell 별 모든 effect 를 lookup.

## HazardLifetimeSystem

```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(CcApplySystem))]   // ZoneApplySystem 가 직후 read 위해 먼저 갱신
public partial struct HazardLifetimeSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<HazardSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        var singletonRW = SystemAPI.GetSingletonRW<HazardSingleton>();
        singletonRW.ValueRW.cellToEffects.Clear();

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (hazard, cells, effects, entity) in
                 SystemAPI.Query<RefRW<Hazard>,
                                 DynamicBuffer<HazardCellsBuffer>,
                                 DynamicBuffer<HazardEffectsBuffer>>()
                          .WithEntityAccess())
        {
            hazard.ValueRW.remainingLife -= dt;
            if (hazard.ValueRO.remainingLife <= 0f)
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            for (int c = 0; c < cells.Length; c++)
                for (int e = 0; e < effects.Length; e++)
                    singletonRW.ValueRW.cellToEffects.Add(cells[c].cell, effects[e].effect);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
```

매 프레임 Clear + 재구축. Hazard 수 작음 가정 (≤ 16 정도). incremental 갱신은 후속 후보.

## Singleton lifecycle (BattleBridge)

기존 `EnemyCcEvents` / `ObstacleSingleton` 패턴 그대로:
- **StartBattle**: `cellToEffects = new NativeMultiHashMap<int2, HazardEffect>(64, Allocator.Persistent)` + singleton entity 부착
- **Cleanup**: dispose + singleton entity destroy
- **OnDestroy**: `if (_hazardCellToEffects.IsCreated) _hazardCellToEffects.Dispose();` (cc-pipeline-and-obstacle 의 C1 fix 패턴 따름)

## 단위 테스트 (EditMode)

`HazardLifetimeTests`:
- dt 후 `remainingLife` 감소 확인
- `remainingLife <= 0` entity destroy 확인
- 살아있는 hazard 1개 + cells 9개 × effects 1개 → cellToEffects 9 entry
- 살아있는 hazard 2개 (overlap cell) → 같은 cell 에 여러 effect entry
- 만료된 hazard 의 effect 는 cellToEffects 에서 제외

## 완료 기준

- 컴파일 + Burst 활성.
- 단위 테스트 통과.
- 런타임 동작 변화 0 (spawn 진입점 미존재).
- NativeMultiHashMap 정상 lifecycle (Editor 종료 시 leak 0).
- 콘솔 에러/경고 0.
