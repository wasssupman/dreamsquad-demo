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
        // dot-effect-extraction unit 1 — **지속 피해 오라**. 소스 = `DotEffect.element`
        // (`origin` 은 보지 않는다 — 장판 화염이든 스택 폭발이든 그림은 같은 화염이다).
        // 점등 의미는 "이 종류의 지속 피해가 돌고 있다" 이지 "스택이 쌓여 있다" 가 아니다.
        // 초판은 스택 슬롯을 소스로 삼아 이름이 `FireStack` 계열이었는데, 실제로 화염·독 도트를
        // 만드는 것은 **해저드 장판**(FireCaster·PoisonCaster)이고 이들은 스택을 부여하지 않는다.
        // 이름이 없는 개념을 가리키고 있었으므로 정정했다 — enum 값은 그대로라 에셋 무영향.
        //
        // ⚠ `Ice` 는 현재 producer 가 없다. `StackModifier_Ice` 의 임계가 감속·스턴뿐이라
        // 얼음 도트가 생기지 않는다(`StackModifier_Fire` 는 에셋 자체가 없다).
        Bleed = 7,
        Fire = 8,
        Ice = 9,
        Poison = 10,
        // 다음 항목은 반드시 **끝에** 추가할 것 — registry 가 int 로 직렬화한다.
    }
}
