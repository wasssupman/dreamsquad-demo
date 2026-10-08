namespace Somnia.Battle.BattleCore.Trigger
{
    // battle-core-rebuild unit 7d — **코어가 직접 실행하는 효과** 하나.
    //
    // 규칙(`Binding`)의 레일 — 감지 · 카운터 · 줄 세우기 · 수명 · 떨어짐 — 은 스킬과 똑같이 탄다. 다른 것은
    // 실행자뿐이다: `Somnia.Battle.Skills` 의 의도 어휘(`SimIntent`)에 없는 일(열기 한 걸음 · 피로 요청 · 픽업 놓기 ·
    // 사직서 떨어뜨리기)은 `ISkill` 로 못 싣고, 그 어셈블리는 **무변이 계약**이다(7a — git diff 0).
    //
    // 닫힌 축이다(제약 8 — 시즌 기믹 4종, `GimmickKind`). 구현체는 전부 `GimmickBindings` 안에 있고, 새 구현체를
    // 여기 붙이려면 그것이 **코어의 규칙**이어야 한다 — 카드·유닛 저작 스킬은 `ISkill` 이 정본이다.
    //
    // ⚠ 실행 문맥은 **틱 문맥 그대로**다. 발동 사건(`TriggerFired`)은 디스패처가 내지 않는다 — 효과가 실제로
    // 일을 했을 때만 자기 사건(`GimmickTriggered`)을 낸다(상한에 막힌 픽업 주기는 사건이 없다).
    public interface ICoreEffect
    {
        void Fire(Binding b, in TriggerEvent e, TickContext ctx);
    }
}
