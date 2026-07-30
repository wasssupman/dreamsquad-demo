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

    // boss-jjangssen unit 8 — `CcSource`(직접/스택임계) 축은 은퇴했다. 도입 근거였던
    // "스택 DoT 가 CC 큐를 공유한다" 가 dot-effect-extraction 으로 소멸했고, 남은 유일한
    // 통과자는 Ice 5중첩 스턴이라 축이 보스 면역에 구멍만 유지하고 있었다. 면역은 이제
    // kind 만으로 판정한다(CcActionLock.IsBossImmune).

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
