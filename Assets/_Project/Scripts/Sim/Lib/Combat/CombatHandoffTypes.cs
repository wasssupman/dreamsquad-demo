namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — Units→Combat 인계 채널. 구 `NextAttackDoubleFire` 이식.
    ///
    /// 방어유닛의 `DamagedCounter` 가 발동하면(Units) 이 컴포넌트가 붙고, **다음** 공격 RESOLVE
    /// 가(Combat) 그것을 읽어 출력을 두 번 내고 지운다.
    ///
    /// ⚠ 소유는 **Combat** 이다 — `IncomingDamage` 의 정확한 반대 방향이다(그건 Units 소유 채널에
    /// Combat 이 append 한다). 규칙은 "**소비자의 맥락이 타입을 소유하고 생산자가 쓴다**" 이고,
    /// 그 덕에 새 채널 싱글턴이 필요 없다.
    /// </summary>
    public struct NextAttackDoubleFire
    {
        /// v1 은 항상 1(다음 1회 한정).
        public int charges;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 궁극기 도약의 이탈 상태. 구 `UltimateLeapState` 이식.
    /// **존재 자체가 "판 밖"** 이다 — 타겟 후보에서 빠지고 들어온 피해는 버려진다.
    ///
    /// **18-J(기믹)의 타입이지만 여기서 연다** — 소비자(<see cref="Wassup.Sim.Units.DamageApplicationSystem"/>)가
    /// 먼저 필요로 한다(`CcEffect` 를 18-C 가 먼저 연 것과 같은 배치). 발동·해제·착지는 18-J 소유다.
    ///
    /// ⚠ **소비 사이트는 닫힌 집합이 아니다.** 소스는 이 컴포넌트 하나지만 소비처는 손으로
    /// 열거해야 하고 컴파일러가 돕지 않는다 — 구 sim 에서 실제로 하나를 놓쳐 발사 패턴 유닛이
    /// 화면 밖 보스를 쐈다. 적을 후보로 담는 쿼리를 새로 만들면 여기 목록에 추가할 것:
    /// <list type="number">
    /// <item>공격 타겟 후보 쿼리</item>
    /// <item>투사체 재조준 풀</item>
    /// <item>투사체 히트 AoE 쿼리(splash·TileAoe·bounce 후보)</item>
    /// <item>발사 패턴의 적 풀</item>
    /// <item>피해 정산 — <b>쿼리 제외가 아니라 버퍼 Clear</b></item>
    /// </list>
    /// 5번이 choke point 다: 피해 생산자는 여럿인데 소비 1곳을 막아 전부 덮는다. 1~4 는
    /// "겨누지 않기"(그림)이고 5 는 "이미 날아온 것 버리기"(규칙)라 역할이 다르다.
    ///
    /// ⚠ `remaining` 은 **sim 시계**로 감소한다. 예고 창은 회피 창이자 피해 게이트 = 게임 규칙이라
    /// 연출 시계가 소유할 수 없다(일반 도약의 창이 뷰 시계 소유인 것과 비대칭인 게 맞다).
    ///
    /// ⚠ `landingCell` 은 **발동 프레임에 고정**된다 — 예고는 약속이다. 착지 직전 재계산하면
    /// 빨간 타일을 보고 유닛을 빼는 회피 플레이가 거짓말이 된다.
    /// </summary>
    public struct UltimateLeapState
    {
        public float remaining;
        public SimInt2 landingCell;
        /// 착지 셀 중심 월드 좌표(셀→월드 재변환 회피).
        public SimVec3 landingWorld;
        public float slamDamage;
        /// 예고 타일 범위 = 슬램 피해 범위(같은 값이 계약).
        public int slamTileRange;
        /// 착지 슬램 VFX (&lt;0 = 무연출).
        public int projectileDataIndex;
    }
}
