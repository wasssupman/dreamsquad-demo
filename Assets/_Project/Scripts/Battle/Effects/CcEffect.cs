using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public enum CcKind : byte
    {
        Slow = 0,
        Impulse = 1,
        DoT = 2,
        Stun = 3,
        // combat-action-lock — Sleep: 공격+이동 정지(Stun 과 함께 action-lock). 최대 N초
        // (무한 = remainingTime +∞), 피격 시 해제(wake-on-hit). append-only.
        Sleep = 4,
    }

    public struct CcEffect : IBufferElementData
    {
        public CcKind kind;
        public float3 vector;
        public float scalar;
        public float remainingTime;
        // dot-tick-cadence unit 0 — DoT 이산 tick. tickInterval>0 이면 tickTimer 누적,
        // 주기 도달 시 scalar(=tick당 데미지) 청크 지급. 0 이면 연속(scalar=DPS).
        // tickTimer 는 슬롯 지속 상태 — CcApply 병합(매 프레임 존 refresh)에도 보존.
        public float tickInterval;
        public float tickTimer;
    }
}
