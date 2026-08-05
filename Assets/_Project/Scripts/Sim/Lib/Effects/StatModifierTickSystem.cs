using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-C/4 — 캡처 #29 · <see cref="SimPhase.ModifierTick"/>(P7).
    /// 구 `Wassup.Battle.Effects.StatModifierTickSystem` 이식.
    ///
    /// 슬롯의 `remaining` 을 깎고 만료분을 제거한 뒤, 만료가 있었으면 집계를 깨운다.
    ///
    /// ⚠ **dirty 로 쿼리를 좁히지 말 것.** 한때 `EnabledRefRW&lt;ModifierStatsDirty&gt;` 로 쿼리했다가
    /// 집계가 dirty 를 끈 엔티티가 통째로 스킵돼 `remaining` 이 영영 안 줄었고, 모디파이어가
    /// **영구 지속**되는 버그가 났다. 그 사고의 주석이 구 코드에 아직 남아 있다.
    /// 모든 슬롯 보유자를 훑는 것이 계약이다(`ModifierFrameworkTests` Test 5 가 박제).
    ///
    /// ⚠ **역순 순회 + swap-back 제거**다. 안정 제거(`RemoveAt`)로 바꾸면 슬롯 순서가 달라지고,
    /// 집계의 곱셈 누적 순서가 바뀌어 부동소수 마지막 비트가 갈린다(= parity 파손).
    /// </summary>
    public sealed class StatModifierTickSystem
    {
        public void Run(SimWorld world)
        {
            float dt = world.DeltaTime;

            // 구 쿼리 = `RefRO<ModifierStats>` + `WithAll<StatModifierSlot>`.
            // ModifierStats 보유가 조건이라는 점이 중요하다 — 없으면 틱조차 하지 않는다.
            foreach (SimEntityId e in world.With<ModifierStats>())
            {
                List<StatModifierSlot> slots = world.GetBuffer<StatModifierSlot>(e);
                if (slots == null) continue;   // 버퍼 부재 = 쿼리 불일치(빈 버퍼는 통과한다)

                bool anyExpired = false;
                for (int i = slots.Count - 1; i >= 0; i--)
                {
                    StatModifierSlot s = slots[i];
                    s.header.remaining -= dt;
                    if (s.header.remaining <= 0f)
                    {
                        RemoveAtSwapBack(slots, i);
                        anyExpired = true;
                    }
                    else
                    {
                        slots[i] = s;
                    }
                }

                // 만료가 생기면 집계를 깨워 스탯 캐시를 다시 계산하게 한다.
                //
                // 구 코드는 여기 `HasComponent<ModifierStatsDirty>` 가드가 있었다. 그 가드는
                // **3상태 표현의 산물**이다 — 구 sim 은 dirty 를 끄기만 하고 제거하지 않으므로,
                // ApplySystem 을 한 번이라도 거친 엔티티(=슬롯을 가진 모든 엔티티)에게 그 검사는
                // **항상 참**이었다. 신 sim 은 "존재 = dirty" 2상태라 집계가 마커를 제거하고,
                // 같은 가드를 그대로 옮기면 두 번째 만료부터 영영 거짓이 되어 **집계가 안 깨어난다**.
                // ⇒ 가드를 떼는 것이 구 sim 의 실제 거동을 보존하는 이식이다.
                //
                // 접힘으로 도달 불가능해진 구 상태가 하나 있다: "슬롯은 있는데 dirty 컴포넌트가
                // 아예 없는" 엔티티. 구 sim 에선 만료가 집계를 못 깨웠다. 그러나 그 상태는
                // ApplyStat 이 항상 MarkDirty 를 부르므로 프로덕션에서 만들어지지 않는다.
                if (anyExpired) world.Set(e, default(ModifierStatsDirty));
            }
        }

        /// 구 `DynamicBuffer.RemoveAtSwapBack` 대응 — 마지막 원소를 끌어와 덮고 꼬리를 자른다.
        private static void RemoveAtSwapBack<T>(List<T> list, int index)
        {
            int last = list.Count - 1;
            list[index] = list[last];
            list.RemoveAt(last);
        }
    }
}
