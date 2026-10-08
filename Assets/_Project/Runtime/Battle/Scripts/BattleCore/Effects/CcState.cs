using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Effects
{
    // battle-core-rebuild unit 6a — 한 개체에 걸린 **군중 제어**.
    //
    // ⚠ **런타임 슬롯은 셋뿐이다**: 넉백 · 기절 · 수면. 저작 어휘(`CcRequestKind`)에는 감속과
    // 지속 피해도 있지만 그 둘은 **다른 파이프라인으로 간다** —
    //   · 감속 → **이동속도 모디파이어**(`StatKind.MoveSpeedMul`)
    //   · 지속 피해 → `DotSet`(자기 버퍼 · 2축 키)
    // 옛 전투는 셋을 한 버퍼에 담았고, 그래서 화염 장판이 출혈의 값을 덮어쓰는 과피해가 났다.
    //
    // ⚠ **Root(전면 정지)는 없다.** 필요해지면 이동 배율 0 이 아니라 전용 플래그로 만든다 —
    // 배율 0 은 바닥 클램프(0.15)에 먼저 걸린다.

    public enum CcSlotKind : byte
    {
        /// <summary>넉백. **외력이라 행동 잠금 축이 아니다**(밀리는 중에도 때린다).</summary>
        Impulse = 0,
        Stun = 1,
        Sleep = 2,
    }

    /// <summary>슬롯이 왜 사라졌나. 사건이 값으로 나른다.</summary>
    public enum CcClearReason : byte
    {
        Expired = 0,
        /// <summary>피격 기상. **수면만** 이렇게 풀린다 — 기절은 안 깬다.</summary>
        WokeUp = 1,
    }

    public struct CcSlot
    {
        public bool Active;

        /// <summary>남은 시간(초). `float.PositiveInfinity` = 무한(자연 통과).</summary>
        public float Remaining;

        /// <summary>넉백 전용 — 초당 속도(거리 ÷ 지속). 방향은 적이 가던 방향의 반대다.</summary>
        public float3 Vector;

        /// <summary>건 쪽. 트레이스·귀속용이고 **병합 키가 아니다**.</summary>
        public SimEntityId Source;
    }

    public sealed class CcState
    {
        private const int SlotCount = 3;

        // 종류당 하나라 **배열이 곧 슬롯 표**다. 리스트가 아닌 이유: 개수가 상수이고,
        // 그래서 「어느 슬롯이 이겼나」가 순회 순서에 매이지 않는다.
        private readonly CcSlot[] _slots = new CcSlot[SlotCount];

        public CcSlot Slot(CcSlotKind kind) => _slots[(int)kind];

        public bool IsActive(CcSlotKind kind) => _slots[(int)kind].Active;

        /// <summary>
        /// 행동 잠금인가 — **기절·수면**만. 넉백은 잠금이 아니다.
        /// 이 술어가 `Unit.ActionLocked` 에 OR 로 합류하고, 그 잠금은 **START 만** 막는다.
        /// </summary>
        public bool IsLocked
            => _slots[(int)CcSlotKind.Stun].Active || _slots[(int)CcSlotKind.Sleep].Active;

        /// <summary>
        /// 슬롯이 하나라도 있나. **「군중 제어에 걸린 적」 배율의 술어 절반**이다 —
        /// 나머지 절반은 지속 피해(`DotSet.Any`)이고, 그 합이 옛 `CcEffect` 버퍼 전체와 같다.
        /// ⚠ **감속은 여기 안 센다.** 옛 전투에서도 감속은 이 버퍼 밖이었다(의도된 제외).
        /// </summary>
        public bool Any
        {
            get
            {
                for (int i = 0; i < SlotCount; i++) if (_slots[i].Active) return true;
                return false;
            }
        }

        /// <summary>이번 틱 넉백 변위(초당 속도 × dt). 이동이 소비한다.</summary>
        public float3 ImpulseStep(float dt)
        {
            var slot = _slots[(int)CcSlotKind.Impulse];
            return slot.Active ? slot.Vector * dt : float3.zero;
        }

        /// <summary>한 슬롯을 걸거나 갱신한다. 반환 = **새로 걸렸나**(갱신이면 false).</summary>
        public bool Apply(CcSlotKind kind, float seconds, float3 vector, SimEntityId source)
        {
            if (seconds <= 0f) return false;
            int i = (int)kind;
            bool fresh = !_slots[i].Active;
            _slots[i] = CcMerge.Merge(in _slots[i], seconds, vector, source);
            return fresh;
        }

        /// <summary>그 슬롯을 지운다. 반환 = 지웠나.</summary>
        public bool Clear(CcSlotKind kind)
        {
            int i = (int)kind;
            if (!_slots[i].Active) return false;
            _slots[i] = default;
            return true;
        }

        /// <summary>
        /// 남은 시간을 깎고 만료된 슬롯을 지운다. 반환 = 만료된 종류의 비트마스크
        /// (`1 &lt;&lt; (int)CcSlotKind`). 순회는 **종류 번호 오름차순**이라 결정론이다.
        /// </summary>
        public int Decay(float dt)
        {
            int cleared = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                if (!_slots[i].Active) continue;
                _slots[i].Remaining -= dt;   // +∞ 는 그대로 +∞ 다
                if (_slots[i].Remaining > 0f) continue;
                _slots[i] = default;
                cleared |= 1 << i;
            }
            return cleared;
        }

        public void Reset()
        {
            for (int i = 0; i < SlotCount; i++) _slots[i] = default;
        }
    }
}
