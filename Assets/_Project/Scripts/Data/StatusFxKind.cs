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
        // bleed-fighter-defender 후속 — 전투 스택 4종의 온-바디 오라.
        // **점등 = DoT CcEffect 진행 중 / 종류 = 스택 슬롯**으로 나눠 쓴다. CcEffect 는 kind
        // 하나로 병합돼 **어느 스택이 만든 DoT 인지 모르므로**(종류 식별 불가) 종류는 슬롯에서만
        // 알 수 있는데, 슬롯은 파생 DoT 보다 먼저 사라진다(Consume 이 스택을 0으로 되돌리고
        // 슬롯도 perAppDuration 이 지나면 만료 — 출혈은 슬롯 2s vs 도트 4.85s). 그래서 bridge 가
        // 살아 있는 슬롯을 볼 때 종류를 래치하고(`_stackAuraLatch`) 점등은 DoT 로만 판단한다.
        // 슬롯 보유를 점등 조건에 함께 걸면 **도트 후반부에 오라가 꺼진다**(2026-07-29 회귀).
        Bleed = 7,
        FireStack = 8,
        IceStack = 9,
        PoisonStack = 10,
        // 다음 항목은 반드시 **끝에** 추가할 것 — registry 가 int 로 직렬화한다.
    }
}
