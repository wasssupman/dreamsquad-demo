using Unity.Entities;

namespace Wassup.Battle.Units
{
    // shield-guardian-defender unit 0 — 실드 부여 이벤트 버퍼 (IncomingHeal 동형).
    // 생산자(Effects, ShieldCastSystem)가 append 하고 DamageApplicationSystem 이
    // 매 프레임 drain(출처별 병합 — ShieldMath.Merge) 후 Clear 한다.
    // source = 캐스터 엔티티(중첩 키). 방어유닛 스폰 시 사전 부착.
    public struct IncomingShield : IBufferElementData
    {
        public Entity source;
        public float amount;
    }
}
