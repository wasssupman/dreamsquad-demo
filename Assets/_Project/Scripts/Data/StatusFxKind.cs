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
        // gimmick-match-integration — 번아웃("불금은 없습니다!" 기믹). 소스 = StatModifierSlot 중
        // header.origin==ModifierOrigin.Burnout (Fatigue 임계 파생 전용 origin, review #3).
        Burnout = 3,
        // season-gimmick-overwork — 라스트런(레드불 기믹). 소스 = LastRun 컴포넌트 보유
        // (레드불 소비~crash 창을 권위적으로 정의, review #3).
        LastRun = 4,
        // subconscious-curse-expansion unit 3 — 살찌운 제물 표식(적 전용). 소스 =
        // BattleBridge 표식 등록부(_bountyMarked) — 처치/유출 드레인이 제거하므로
        // 잔존 키 = 활성 표식(ECS 쿼리 불요).
        Marked = 5,
        // unit-status-fx 6 — Stun(CcKind.Stun, 적·아군 공통 action-lock). 소스 = CcEffect 버퍼.
        // 수면(Zz)과 시각 구분용(스턴탄 등). Sleep 아이콘과 동일 경로.
        Stun = 6,
        // Freeze, Poison … 나중에 끝에 추가 + registry 항목 + reconcile 소스 훅.
    }
}
