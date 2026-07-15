namespace Wassup.Data
{
    // unit-status-fx Unit 0 — 상태 연출 종류. append-only(직렬화 안전: 새 상태는 끝에).
    // 각 kind 는 StatusFxRegistry 에서 프리팹으로 매핑되고, BattleBridge reconcile 이
    // 대응 ECS 소스(Aggro=Aggroed)로 활성 유닛을 찾는다.
    public enum StatusFxKind : byte
    {
        Aggro = 0,
        // unit-status-fx 5 — Sleep(CcKind.Sleep, 적·아군 공통). 소스 = CcEffect 버퍼.
        Sleep = 1,
        // unit-buff-debuff-aura 0 — 순 스탯 상태 온-바디 오라. 소스 = ModifierStats(전 유닛).
        // 스탯 모디파이어 한정: CcEffect 기반(Stun/DoT/Impulse)은 여기 아님(각각 별 kind).
        Buffed = 2,
        Debuffed = 3,
        // Stun, Freeze, Poison … 나중에 끝에 추가 + registry 항목 + reconcile 소스 훅.
    }
}
