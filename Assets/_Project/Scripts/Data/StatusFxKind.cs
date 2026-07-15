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
        // dreamcatcher-empower-aura — 드림캐쳐가 스탯 모디파이어를 적용한 유닛의 강화 오라.
        // 소스 = StatModifierSlot 중 header.origin==ModifierOrigin.Dreamcatcher. 온-바디 지속 VFX.
        Empowered = 2,
        // gimmick-match-integration — 번아웃(워라벨 기믹). 소스 = StatModifierSlot 중
        // header.origin==ModifierOrigin.Stack (Fatigue 임계가 유일한 Stack 출처 → 번아웃 창과 일치).
        Burnout = 3,
        // Stun, Freeze, Poison … 나중에 끝에 추가 + registry 항목 + reconcile 소스 훅.
    }
}
