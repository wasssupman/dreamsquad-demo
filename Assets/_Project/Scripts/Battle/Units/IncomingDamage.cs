using Unity.Entities;

namespace Wassup.Battle.Units
{
    // Buffer element attached to units that can receive damage. Combat systems
    // append entries (via ECB); DamageApplicationSystem drains them and mutates Health.
    // This is the canonical cross-context event channel per TRD 2.5.2.
    public struct IncomingDamage : IBufferElementData
    {
        public float amount;
        // dreamcatcher-kill-and-threshold unit 2 — 이 데미지를 낸 공격자(킬 귀속용).
        // 공격/투사체 생산자는 공격자 entity 를 채우고, DoT·on-place·환경 데미지는
        // 미설정(=Entity.Null)으로 두어 '미귀속'을 표현한다. DamageApplicationSystem
        // 이 킬 프레임에 source 非Null 최대 amount entry 를 killer 로 뽑아 OnKill 발동.
        // 기존 생산자는 이 필드를 안 채워도 컴파일된다(default = Entity.Null).
        public Entity source;
    }
}
