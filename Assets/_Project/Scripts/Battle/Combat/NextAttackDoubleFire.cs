using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-content-1 unit 3 (① 가시 갑옷) — Units→Combat handoff channel.
    // When a defender's DamagedCounter fires (Units), it adds this component; the
    // NEXT AttackSystem RESOLVE (Combat) reads it, fires the attack output twice,
    // and removes it. This is the reverse direction of IncomingDamage (a Units-owned
    // channel that Combat appends to) — the consumer's context owns the type, the
    // producer writes it. So this is Combat-owned; no new NativeQueue channel.
    public struct NextAttackDoubleFire : IComponentData
    {
        public int charges; // v1: always 1 (다음 1회 공격 한정)
    }
}
