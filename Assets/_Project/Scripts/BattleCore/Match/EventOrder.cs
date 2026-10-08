namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 4 — **담당자 사이의 순서 계약을 한 화면에 모은 곳.**
    //
    // 계약 12 의 요지는 「한 함수가 담당자 둘을 차례로 부르지 않는다」이고, 그 대신 순서를
    // **구독에 숫자로 적는다**(`EventBus`: 낮은 `order` 가 먼저). 숫자를 호출부마다 리터럴로
    // 흩어 두면 그 계약이 코드 전체에 흩어져 아무도 전순서를 못 본다 — 그래서 여기 모은다.
    //
    // ⚠ 이 파일이 곧 **X2**(「한 틱 안 처리 순서 3건이 계약이다」)의 이행이다:
    //   ① 처치 → 웨이브 예약   — 분열 자식이 전멸 판정 **앞**에 태어나야 한다.
    //      (틱 구조가 이미 절반을 보장한다: 소멸은 `CombatPhase`, 웨이브 예약은 그 뒤
    //       담당자 단계다. 여기 숫자는 **같은 사건을 받는 구독자들** 사이의 차례다.)
    //   ② 골 도달 → 안정도 → 보너스 제안 — 묵은 스트레스로 판정하면 문턱에서 떨린다.
    //      `HeartMeter` 가 `GoalReached` 를 받아 `HeartChanged` 를 내고, 보너스 제안은
    //      **그 사건**을 구독한다. 그래서 같은 플러시 안에서 순서가 성립한다.
    //   ③ 안정도 동기 → 보너스 제안 — 위와 같은 연쇄의 뒤쪽 절반.
    //
    // 값 사이를 10 씩 띄운 것은 나중에 사이에 끼울 자리를 남기기 위해서다.
    public static class EventOrder
    {
        /// <summary>관측. **누구보다 먼저** — 담당자가 상태를 바꾸기 전의 값을 남긴다.</summary>
        public const int Trace = 0;

        /// <summary>
        /// 웨이브 예약이 보는 처치·소멸. 「살아 있는 적이 0인가」의 근거가 되는 목록 변화를
        /// 점수·손패보다 **먼저** 받는다(①).
        /// </summary>
        public const int WaveBookkeeping = 10;

        /// <summary>골 도달 → 마음. 스트레스를 **먼저** 움직인다(②).</summary>
        public const int Heart = 10;

        /// <summary>배치 창이 닫히면 코스트 재생이 켜진다(X24).</summary>
        public const int CostRegen = 10;

        /// <summary>처치 → 점수. 마음 회복보다 뒤라도 무방하다(둘이 서로를 안 읽는다).</summary>
        public const int Score = 20;

        /// <summary>소멸 → 재배치 대기. 그 유닛이 아직 목록에 보이는 동안 받아야 한다.</summary>
        public const int PlacementCooldown = 20;

        /// <summary>처치·사망 → 각성 게이지와 카드 회수.</summary>
        public const int Hand = 30;

        /// <summary>안정도가 움직인 **뒤** 보너스를 다시 판정한다(③).</summary>
        public const int BonusOffer = 30;

        /// <summary>
        /// unit 7d — 스폰·활성화 → 시즌 기믹 규칙 부착(유닛 호스트 per-unit 타이머). 다른 담당자의 상태를
        /// 읽지도 바꾸지도 않으므로 차례가 규칙을 안 바꾼다 — 관측·담당자 뒤에 둔다.
        /// </summary>
        public const int GimmickAttach = 40;
        /// <summary>목표는 담당자들이 상태를 갱신한 뒤에 읽는다.</summary>
        public const int Goal = 90;
    }
}
