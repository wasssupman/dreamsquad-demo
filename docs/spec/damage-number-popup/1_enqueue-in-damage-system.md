# 1 — enqueue (DamageApplicationSystem)

## 목적

`DamageApplicationSystem`(Units 맥락, 실제 HP 차감 지점)에서 **적(`AttackUnitTag`) 유닛이 데미지를 받았을 때만** `DamageNumberEvent` 를 enqueue 한다. 디펜더 피격은 제외. 완화(`dmgTakenMul`) 적용 후의 실제 데미지를 보낸다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs`

## 구현

`AttackUnitTag` 룩업과 `DamageNumberEventsSingleton` RW 를 추가하고, `totalDamage` 확정 직후 조건부 enqueue.

1. **필드** 추가:
   ```csharp
   private ComponentLookup<AttackUnitTag> _attackTagLookup;
   ```
2. **OnCreate**:
   ```csharp
   _attackTagLookup = state.GetComponentLookup<AttackUnitTag>(isReadOnly: true);
   ```
3. **OnUpdate** 상단(다른 `.Update(ref state)` 인근):
   ```csharp
   _attackTagLookup.Update(ref state);
   bool hasDamageNumberQueue = SystemAPI.TryGetSingletonRW<DamageNumberEventsSingleton>(out var damageNumberSingleton);
   ```
4. **루프 내** — `totalDamage *= dmgTakenMul;` 직후:
   ```csharp
   // Enemy-only floating damage number. Filter to AttackUnitTag so defender hits
   // produce no popup (per spec scope). amount is post-mitigation damage.
   if (hasDamageNumberQueue && totalDamage > 0f
       && _attackTagLookup.HasComponent(entity)
       && _transformLookup.HasComponent(entity))
   {
       damageNumberSingleton.ValueRW.queue.Enqueue(new DamageNumberEvent
       {
           position = _transformLookup[entity].Position,
           amount = totalDamage,
       });
   }
   ```

## 계약/주의

- enqueue 는 **이 한 곳만**. Burst 호환(룩업 + NativeQueue.Enqueue 모두 Burst-safe). `[BurstCompile]` 유지.
- `totalDamage` 는 이미 `dmgTakenMul` 곱해진 값(line 58). heal 과 무관하게 데미지가 양수면 표시.
- DoT/다단 히트는 프레임당 합산된 `totalDamage` 로 1회 표시(IncomingDamage 버퍼 합산 결과). 틱별 분리 표시는 비범위.
- 드레인은 아직 스텁(unit 0). 표시는 unit 4 에서.

## 완료 기준

- compile: CS 에러 0, Burst 에러 0 (read_console).
- 코드 검토: 적(`AttackUnitTag`)만, `totalDamage > 0` 일 때만 enqueue.
- 런타임 표시는 unit 4 후 확인.

✅ 2026-06-05 compile 클린 (force refresh + read_console 에러 0, Burst 포함). 커밋 대기.
