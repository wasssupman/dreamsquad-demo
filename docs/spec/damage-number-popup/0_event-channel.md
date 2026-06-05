# 0 — 이벤트 채널 (DamageNumberEvent)

## 목적

Units→Presentation 단발 신호 채널을 만든다. `DamageApplicationSystem` 이 적 피격 시 enqueue 하고, `BattleBridge` 가 드레인해 데미지 숫자 팝업을 띄운다. `HealAppliedEvent` 채널을 그대로 미러링한다. 채널 수 **14 → 15**.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Battle/Units/DamageNumberEvent.cs`
- (신규) `Assets/_Project/Scripts/Battle/Units/DamageNumberEventsSingleton.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 필드/생성/파괴/해제 + 드레인 시퀀스에 스텁 호출
- `CLAUDE.md` — NativeQueue 채널 목록 (14 → 15, `DamageNumberEventsSingleton` 추가)

## 구현

### DamageNumberEvent.cs

```csharp
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // Units->Presentation one-shot signal: an enemy (AttackUnitTag) took damage
    // this frame. position = enemy LocalTransform.Position (feet) at enqueue;
    // the spawner adds a head Y-offset. amount = total damage applied this frame
    // (post-mitigation, always > 0 at enqueue site). Magnitude drives popup
    // size/color in presentation.
    public struct DamageNumberEvent
    {
        public float3 position;
        public float amount;
    }
}
```

### DamageNumberEventsSingleton.cs

```csharp
using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Queue owned by BattleBridge. DamageApplicationSystem enqueues one event per
    // enemy whose IncomingDamage was applied this frame; BattleBridge drains and
    // spawns floating damage numbers.
    public struct DamageNumberEventsSingleton : IComponentData
    {
        public NativeQueue<DamageNumberEvent> queue;
    }
}
```

### BattleBridge.cs

`HealAppliedEvent` 미러링 4지점 + 누수 방지용 스텁 드레인:

1. **필드** (line ~135, `_healAppliedEventQueue` 인근):
   ```csharp
   private NativeQueue<Wassup.Battle.Units.DamageNumberEvent> _damageNumberEventQueue;
   ```
2. **파괴** (`DestroyEcsInfrastructureEntities`, line ~340 뒤):
   ```csharp
   DestroyEntitiesByType<Wassup.Battle.Units.DamageNumberEventsSingleton>();
   ```
3. **해제** (`DisposeEcsInfrastructureNativeContainers`, line ~360 뒤):
   ```csharp
   if (_damageNumberEventQueue.IsCreated) _damageNumberEventQueue.Dispose();
   ```
4. **생성** (line ~789, healApplied 생성 뒤):
   ```csharp
   // Units->Presentation damage-number channel. DamageApplicationSystem enqueues
   // one event per enemy (AttackUnitTag) whose IncomingDamage was applied.
   if (_damageNumberEventQueue.IsCreated) _damageNumberEventQueue.Dispose();
   _damageNumberEventQueue = new NativeQueue<Wassup.Battle.Units.DamageNumberEvent>(Allocator.Persistent);
   var damageNumberSingleton = _em.CreateEntity();
   _em.AddComponentData(damageNumberSingleton, new Wassup.Battle.Units.DamageNumberEventsSingleton { queue = _damageNumberEventQueue });
   ```
5. **드레인 스텁** — `Update()` 드레인 시퀀스(`DrainHealAppliedEvents();` 뒤, line ~1527)에 호출 추가:
   ```csharp
   DrainDamageNumberEvents();
   ```
   그리고 스텁 메서드(`DrainHealAppliedEvents` 인근):
   ```csharp
   // Stub until unit 4 wires the spawner — drain + drop so the queue can't grow
   // unbounded if the game is played between commits.
   private void DrainDamageNumberEvents()
   {
       if (!_damageNumberEventQueue.IsCreated) return;
       _damageNumberEventQueue.Clear();
   }
   ```

## 완료 기준

- compile: CS 에러 0 (UnityMCP refresh + read_console).
- `DamageNumberEventsSingleton` 엔티티가 다른 14개 채널과 동일하게 생성/파괴/해제된다(코드 검토).
- `CLAUDE.md` 채널 목록이 15개로 갱신됨.
- 런타임 효과는 아직 없음(unit 1 enqueue + unit 4 드레인 후 표시).

✅ 2026-06-05 compile 클린 (UnityMCP force refresh + read_console 에러 0). 커밋 대기.
