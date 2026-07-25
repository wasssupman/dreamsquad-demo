using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    // dreamcatcher-shield-break unit 0 — 부여된 실드가 피격으로 완전 소진(Sum>0→0)될 때
    // DamageApplicationSystem 이 host 의 OnShieldBreak DcTriggerSlot 를 읽어 emit.
    // 시간만료로 사라지는 실드는 이 경로(ShieldMath.Absorb)를 안 타므로 배제된다.
    // 소비: BattleBridge.DrainShieldBreakEvents (payload 분기 실행 — unit 2).
    // dreamcatcher-trigger-gates unit 0 — OnDamagedN×SelfTileAoe(피격 폭발)도 이 채널을
    // 공유한다 (같은 Units 발동 시점·같은 드레인 실행기, 신규 채널 금지). 구분은
    // fromDamagedTrigger. v1 은 디펜더 카드 전용 — host 가 defender 라는 드레인 가정은
    // 적측 OnDamagedN 이 열리는 날 재검토.
    public struct ShieldBreakEvent
    {
        public Entity host;
        public float3 position;                   // host world position (AoE 중심 / 적 쿼리 중심)
        public Wassup.Data.DcPayloadKind payload;
        public float magnitude;                   // SelfTileAoe: AoE 데미지 / AreaSleep: 적 수 cap(M)
        public int tileRange;                     // AoE 반경 / 수면 반경 (Chebyshev, N)
        public float duration;                    // AreaSleep: 수면 초(L)
        public int aoeDataIndex;                  // SelfTileAoe: AoE view ProjectileData index (-1 = none)
        public bool fromDamagedTrigger;           // true = OnDamagedN 발(피격 폭발), false = 실드 파열
    }

    // BattleBridge 가 큐 수명 소유 (StartBattle create, teardown + OnDestroy dispose) —
    // DefenderDeathEventsSingleton 패턴 미러.
    public struct ShieldBreakEventsSingleton : IComponentData
    {
        public NativeQueue<ShieldBreakEvent> queue;
    }
}
