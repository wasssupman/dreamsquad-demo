using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // dreamcatcher-shield-break unit 0 — 부여된 실드가 피격으로 완전 소진(Sum>0→0)될 때
    // DamageApplicationSystem 이 host 의 OnShieldBreak DcTriggerSlot 를 읽어 emit.
    // 시간만료로 사라지는 실드는 이 경로(ShieldMath.Absorb)를 안 타므로 배제된다.
    // 소비: BattleBridge.DrainShieldBreakEvents (payload 분기 실행 — unit 2).
    public struct ShieldBreakEvent
    {
        public Entity host;
        public float3 position;                   // host world position (AoE 중심 / 적 쿼리 중심)
        public Wassup.Data.DcPayloadKind payload;
        public float magnitude;                   // SelfTileAoe: AoE 데미지 / AreaSleep: 적 수 cap(M)
        public int tileRange;                     // AoE 반경 / 수면 반경 (Chebyshev, N)
        public float duration;                    // AreaSleep: 수면 초(L)
        public int aoeDataIndex;                  // SelfTileAoe: AoE view ProjectileData index (-1 = none)
    }

    // BattleBridge 가 큐 수명 소유 (StartBattle create, teardown + OnDestroy dispose) —
    // DefenderDeathEventsSingleton 패턴 미러.
    public struct ShieldBreakEventsSingleton : IComponentData
    {
        public NativeQueue<ShieldBreakEvent> queue;
    }
}
