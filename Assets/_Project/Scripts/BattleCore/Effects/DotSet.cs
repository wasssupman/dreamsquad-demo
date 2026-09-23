using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.BattleCore.Effects
{
    // battle-core-rebuild unit 6a — 한 개체가 받고 있는 **지속 피해**.
    //
    // ⚠ **군중 제어와 별개 버퍼다.** 지속 피해는 crowd control 이 아니고 중첩 정책도 정반대다 —
    // 기절·수면은 「가장 긴 것 하나」가 정답이지만 지속 피해는 **출처별로 공존**해야 한다.
    // 한 버퍼를 쓰던 시절엔 화염 장판이 출혈의 값을 덮어써, 출혈 중인 적이 장판을 밟았다
    // 나와도 **장판 요율로 계속 타는** 과피해(실측 총 ~194, 의도 50)가 났다.
    //
    // ⚠ **병합 키는 2축이다**(설계 불변식 13): `DotOrigin`(어느 파이프라인이 만들었나) ×
    // `DotElement`(화면에 보이는 그림). **둘을 한 필드로 겸직시키지 말 것** — 지금은 원소
    // 하나가 파이프라인 하나에서만 나와(출혈 = 스택, 화염 = 장판) 우연히 1:1 이지만, 화염을
    // 스택으로도 만드는 순간 장판 화염과 중첩 폭발 화염이 한 슬롯에서 서로를 덮는다.
    // 출처(`SimEntityId`)를 축으로 쓰는 것도 안 된다 — 존은 해저드 개체를 만들 수 없고,
    // 난도질꾼 2기는 출처가 둘인데 둘 다 출혈이라 식별에 기여가 없다.
    //
    // ⚠ **다중 공격자 도트는 합산하지 않는다** — 난도질꾼 2기가 물어도 출혈은 한 슬롯이고
    // 남은 시간만 긴 쪽이다(2026-07-30 사용자 결정 「그대로 두기」). 2축 키의 **귀결이지
    // 버그가 아니다.** 뒤집으려면 `enemy-fire-stack-shooter` README 계약 2·6-1 을 인용하고
    // 재승인을 받는다.

    /// <summary>어느 파이프라인이 만들었나. **슬롯을 가르는 축**이다. append-only.</summary>
    public enum DotOrigin : byte
    {
        Unspecified = 0,
        Stack = 1,
        Zone = 2,
        OnPlace = 3,
    }

    /// <summary>화면에 보이는 그림. **오라가 읽는 축**이고 슬롯을 가르지 않는다. append-only.</summary>
    public enum DotElement : byte
    {
        None = 0,
        Bleed = 1,
        Fire = 2,
        Ice = 3,
        Poison = 4,
    }

    public struct DotSlot
    {
        public DotOrigin Origin;
        public DotElement Element;

        /// <summary>주기 &gt;0 이면 **틱당 피해**, 0 이면 DPS(연속).</summary>
        public float Scalar;

        public float TickInterval;

        /// <summary>슬롯 지속 상태 — 병합(매 틱 존 갱신 포함)에도 **리셋 금지**.</summary>
        public float TickTimer;

        public float Remaining;
    }

    public sealed class DotSet
    {
        private readonly List<DotSlot> _slots = new List<DotSlot>(2);

        public IReadOnlyList<DotSlot> Slots => _slots;

        public int Count => _slots.Count;

        public bool Any => _slots.Count > 0;

        /// <summary>
        /// 슬롯을 걸거나 갱신한다. 반환 = **새로 걸렸나**.
        ///
        /// 갱신: 값·주기는 들어온 것, 남은 시간은 긴 쪽, **타이머는 보존하되 주기가 바뀌면
        /// 비례 환산**(F6). 신규: **진입 즉시 1회** 주기 위해 타이머를 주기로 채운다(F7) —
        /// 생산자 셋이 전부 이 규약에 기대고 있다.
        /// </summary>
        public bool Apply(DotOrigin origin, DotElement element, float scalar,
                          float tickInterval, float seconds)
        {
            if (seconds <= 0f) return false;

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Origin != origin || _slots[i].Element != element) continue;

                var slot = _slots[i];
                slot.TickTimer = CcMerge.CarryTimer(slot.TickTimer, slot.TickInterval, tickInterval);
                slot.Scalar = scalar;
                slot.TickInterval = tickInterval;
                slot.Remaining = math.max(slot.Remaining, seconds);
                _slots[i] = slot;
                return false;
            }

            _slots.Add(new DotSlot
            {
                Origin = origin,
                Element = element,
                Scalar = scalar,
                TickInterval = tickInterval,
                TickTimer = tickInterval,   // 첫 틱 즉발(연속이면 무영향)
                Remaining = seconds,
            });
            return true;
        }

        /// <summary>
        /// 이번 틱 지급량을 **앞에서부터** 걷는다(F8). 역순으로 돌면 여러 도트가 걸린 대상의
        /// 피해 숫자 표시 순서가 조용히 뒤집힌다. 만료 제거는 `RemoveExpired` 가 뒤에서부터.
        ///
        /// 지급 한 번 = 피해 한 건 = 화면의 숫자 하나라 **호출부가 건마다 인박스에 넣는다.**
        /// </summary>
        public void Step(int index, float dt, out int ticks, out float perTick, out float continuous)
        {
            var slot = _slots[index];
            ticks = 0;
            perTick = 0f;
            continuous = 0f;

            if (slot.TickInterval <= 0f)
            {
                continuous = slot.Scalar * dt;   // 연속: 값이 DPS 다
            }
            else
            {
                ticks = DotTick.Advance(ref slot.TickTimer, slot.TickInterval, dt);
                perTick = slot.Scalar;
            }

            slot.Remaining -= dt;
            _slots[index] = slot;
        }

        /// <summary>만료를 **뒤에서부터** 지운다(F8). 반환 = 지운 수.</summary>
        public int RemoveExpired(List<DotSlot> removed = null)
        {
            int n = 0;
            for (int i = _slots.Count - 1; i >= 0; i--)
            {
                if (_slots[i].Remaining > 0f) continue;
                removed?.Add(_slots[i]);
                _slots.RemoveAt(i);
                n++;
            }
            return n;
        }

        public void Reset() => _slots.Clear();
    }

    // 전투 스택 → 원소. 순수 매핑이라 아키텍처 종속 메서드 밖에 둔다(제약 10).
    // 기믹 스택(피로도)은 `None` — 전투 도트를 만들지 않으므로 오라 대상이 아니다.
    public static class DotElementMap
    {
        public static DotElement FromStack(StackKind kind)
        {
            switch (kind)
            {
                case StackKind.Bleed: return DotElement.Bleed;
                case StackKind.Fire: return DotElement.Fire;
                case StackKind.Ice: return DotElement.Ice;
                case StackKind.Poison: return DotElement.Poison;
                default: return DotElement.None;
            }
        }

        /// <summary>
        /// (출처, 원소)를 **사람이 읽을 수 있는 한 정수**로 접는다. 골든 한 줄의 `i` 칸이
        /// 하나뿐이라 둘을 실어야 하고, `102` 를 「장판·화염」으로 눈이 바로 읽는다.
        /// </summary>
        public static int PackArg(DotOrigin origin, DotElement element)
            => (int)origin * 100 + (int)element;

        public static DotOrigin OriginOfArg(int arg) => (DotOrigin)(arg / 100);

        public static DotElement ElementOfArg(int arg) => (DotElement)(arg % 100);
    }
}
