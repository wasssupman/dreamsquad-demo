namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 4 — **누가 효과를 받을 수 있나.** 순수 술어 셋이다.
    //
    // 여기 모은 이유는 F3(「거점은 상태이상·모디파이어에 **전면 면역**이다」)이 옛 전투에서
    // **어느 spec 에도 계약으로 안 적혀 있던** 규칙이기 때문이다 — 진입 가드 세 곳
    // (스탯·스택·행동불능)에 흩어져 있었고, 그래서 새 효과가 생길 때마다 넷째 구멍이 열렸다.
    //
    // ⚠ **회복만 열려 있다.** 원 금지의 명분은 「최대 체력 재계산이 표시·정규화를 어긋나게
    // 한다」였고 그것은 스탯 모디파이어 전용이다 — 회복은 최대치를 안 건드린다. 「악몽을
    // 잡을수록 마음이 회복된다」가 그 예외를 요구한 저울이다.
    public static class EffectEligibility
    {
        /// <summary>
        /// 이 종류의 군중 제어를 받나. 거절 둘: **거점**(F3 — 종류 불문) · **보스 면역**
        /// (기절·수면·넉백 출처 불문 — **감속은 받는다**). null 은 거절이다 — 이미 사라진
        /// 대상에 거는 것은 요청이 아니라 사고다.
        ///
        /// ⚠ **종류를 반드시 묻는다.** 옛 `CcActionLock.IsBossImmune(kind) = IsLock(kind) ||
        /// kind == Impulse` 였다. 종류 축을 잃은 술어가 감속까지 막아 보스에 둔화 카드가 안
        /// 걸렸다(2026-09-24 드리프트 감사 M6) — 인자 없는 오버로드를 두지 않는 이유다.
        /// </summary>
        public static bool AcceptsCc(Unit victim, CcRequestKind kind)
            => victim != null
               && !victim.IsStructure
               && !(victim.Attack != null && victim.Attack.BossImmune && IsBossImmuneKind(kind));

        /// <summary>보스가 막는 종류 = 행동 잠금(기절·수면) + 넉백. 감속은 스탯 축이라 통과한다.</summary>
        public static bool IsBossImmuneKind(CcRequestKind kind)
            => kind == CcRequestKind.Stun || kind == CcRequestKind.Sleep || kind == CcRequestKind.Impulse;

        /// <summary>
        /// 스탯·스택 모디파이어를 받나. 거점은 안 받는다(F3) — 보스 면역은 **여기 없다**
        /// (그쪽 면역은 행동불능 축 전용이고, 버프·디버프까지 막으면 가호가 보스에 안 걸린다).
        /// </summary>
        public static bool AcceptsModifier(Unit target)
            => target != null && !target.IsStructure;

        /// <summary>
        /// 회복을 받나. **거점도 받는다** — 금지 계약에서 의도적으로 빠진 하나다.
        /// 마음은 체력이 담당자에게 있어 그 회복도 담당자가 적용한다(`HeartMeter`).
        /// </summary>
        public static bool AcceptsHeal(Unit target) => target != null && !target.Dead;
    }
}
