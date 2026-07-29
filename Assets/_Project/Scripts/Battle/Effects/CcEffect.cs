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

    // boss-jjangssen unit 3 — CC 가 "직접 걸린 것" 인지 "스택 임계가 유발한 것" 인지.
    // 보스 면역이 이 축을 필요로 한다: StackModifierTickSystem 이 ApplyDot/ApplyStun 을
    // 직접 CC 와 **같은 EnemyCcEventsSingleton 큐**로 넣으므로 kind 만으로는 구별되지 않는다.
    // 기본값 Direct(0) → 기존 생산자 전부 무변화. append-only.
    public enum CcSource : byte
    {
        Direct = 0,
        StackThreshold = 1,
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
