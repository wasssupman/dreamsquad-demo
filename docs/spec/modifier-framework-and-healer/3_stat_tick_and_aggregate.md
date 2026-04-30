# 3. StatModifier Tick & Aggregate

## 목적

`StatModifierSlot` 의 시간 만료 처리 + `BuffStats` 캐시의 dirty-driven 재계산. 두 system 으로 분리.

scope: Stat 사이드 lifetime + cache 만. Stack 사이드 tick 은 4번. ApplySystem 은 2번에서 완료.

## 변경 대상

| 파일 | 변경 |
|---|---|
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/StatModifierTickSystem.cs` | 신규 — Burst-compatible ISystem. tick remaining + 만료 슬롯 제거 + dirty enable. |
| `Assets/_Project/Scripts/Battle/Effects/Modifiers/BuffStatsAggregateSystem.cs` | 신규 — Burst-compatible ISystem. dirty enabled entity 만 합성 → BuffStats write → dirty disable. |

## 구현

**`StatModifierTickSystem.cs`**
```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ModifierApplySystem))]
[UpdateBefore(typeof(BuffStatsAggregateSystem))]
public partial struct StatModifierTickSystem : ISystem {
    [BurstCompile] public void OnUpdate(ref SystemState state) {
        float dt = SystemAPI.Time.DeltaTime;
        foreach (var (slots, dirty, entity) in
                 SystemAPI.Query<DynamicBuffer<StatModifierSlot>, EnabledRefRW<BuffStatsDirty>>()
                          .WithEntityAccess()) {
            bool changed = false;
            for (int i = slots.Length - 1; i >= 0; i--) {
                var s = slots[i];
                s.header.remaining -= dt;
                if (s.header.remaining <= 0f) {
                    slots.RemoveAtSwapBack(i);
                    changed = true;
                } else {
                    slots[i] = s;
                }
            }
            if (changed) dirty.ValueRW = true;
        }
    }
}
```

**`BuffStatsAggregateSystem.cs`** — 합성식 `final = (base + Σadd) * Πmul * (override_max if any else 1)`:
```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(StatModifierTickSystem))]
public partial struct BuffStatsAggregateSystem : ISystem {
    [BurstCompile] public void OnUpdate(ref SystemState state) {
        foreach (var (slots, stats, dirty) in
                 SystemAPI.Query<DynamicBuffer<StatModifierSlot>, RefRW<BuffStats>, EnabledRefRW<BuffStatsDirty>>()) {
            // base: damageMul=1, attackSpeedMul=1, dmgTakenMul=1, regenPerSec=0.
            // 4개 stat × 3개 op = 12개 합성 슬롯. inline 계산 (배열 X — Burst 친화).
            float dMul=1f, dAdd=0f, dOver=0f; bool dHasOver=false;
            float aMul=1f, aAdd=0f, aOver=0f; bool aHasOver=false;
            float tMul=1f, tAdd=0f, tOver=0f; bool tHasOver=false;
            float rMul=1f, rAdd=0f, rOver=0f; bool rHasOver=false;

            for (int i = 0; i < slots.Length; i++) {
                var s = slots[i];
                ref float mul = ref dMul; ref float add = ref dAdd; ref float over = ref dOver; ref bool hasOver = ref dHasOver;
                switch (s.stat) {
                    case StatKind.DamageMul:      /* dMul/dAdd/dOver */ break;
                    case StatKind.AttackSpeedMul: mul = ref aMul; add = ref aAdd; over = ref aOver; hasOver = ref aHasOver; break;
                    case StatKind.DmgTakenMul:    mul = ref tMul; add = ref tAdd; over = ref tOver; hasOver = ref tHasOver; break;
                    case StatKind.RegenPerSec:    mul = ref rMul; add = ref rAdd; over = ref rOver; hasOver = ref rHasOver; break;
                }
                switch (s.op) {
                    case CombineOp.Multiplicative: mul *= s.magnitude; break;
                    case CombineOp.Additive:       add += s.magnitude; break;
                    case CombineOp.Override:       over = math.max(over, s.magnitude); hasOver = true; break;
                }
            }

            float Combine(float baseV, float mul, float add, float over, bool hasOver) =>
                hasOver ? over : (baseV + add) * mul;

            stats.ValueRW.damageMul      = Combine(1f, dMul, dAdd, dOver, dHasOver);
            stats.ValueRW.attackSpeedMul = Combine(1f, aMul, aAdd, aOver, aHasOver);
            stats.ValueRW.dmgTakenMul    = Combine(1f, tMul, tAdd, tOver, tHasOver);
            stats.ValueRW.regenPerSec    = Combine(0f, rMul, rAdd, rOver, rHasOver);

            dirty.ValueRW = false;
        }
    }
}
```

**주의**:
- ref 변수 switch 패턴이 Burst 호환 안 될 수 있음. 안 되면 stat 별로 4개 분기에서 직접 mul/add/over 변수 갱신 (코드 약간 더 길지만 Burst 친화). 작성 시 Burst inspector 로 확인.
- `BuffStats` 가 entity 에 없으면 어떻게? Aggregate 가 작동하려면 BuffStats 컴포넌트가 미리 add 되어야 함. **결정**: BattleBridge 가 defender / enemy spawn 시 `BuffStats` (디폴트값) + `BuffStatsDirty` (disabled) 함께 add. 또는 ApplySystem 에서 첫 부착 시 ecb.AddComponent — 이 단위 시점 spec 미결. **권장**: 5번 단위 (AttackSystem outputs 진입) 시점에 `DefenderUnitTag` 가진 entity 에 일괄 add. 1번/2번 단위 동안은 BuffStats 가 없어도 무관 (소비자 없음).

## 완료 기준

- [ ] 2개 system 신규 작성. 컴파일 통과.
- [ ] EditMode 테스트:
  - [ ] 단일 Mul 슬롯 부착 후 만료 → BuffStats 가 디폴트(1.0) 로 복귀.
  - [ ] Mul + Mul 합성: damageMul = m1 * m2.
  - [ ] Mul + Add 합성: damageMul = (1 + add) * mul.
  - [ ] Override 단독: 다른 슬롯 무시하고 over_max.
  - [ ] dirty 가 unset 후 다음 프레임 변경 없으면 Aggregate 가 BuffStats 안 건드림 (값 직접 비교).
- [ ] 본 문서 하단에 확인 일자 + 커밋 해시 기재 후 commit.

## 후속 단위 의존

- 5번이 BuffStats add 시점 결정 + AttackSystem 이 BuffStats read.
- 7번이 legacy 컴포넌트 read 도 추가.

---

확인 일자 + 커밋 해시: _(작업 완료 시 기재)_
