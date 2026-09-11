namespace Wassup.Battle.Combat
{
    // directional-attack-shape unit 0 — 공격 판정 도형의 **bake 된** 형태. `AttackState` 가 싣고
    // `AttackReach` 가 읽는다. 저작 struct(`Data/AttackShape`, unit 1)와 다른 타입인 이유:
    //   · sim 은 각도를 모른다 — 저작 각도는 bake 1회에 `(sin, cos)` 가 되고 폭은 반폭이 된다.
    //   · **`kind 0 = Omni`** 여야 한다. `default(AttackState)` 가 안전해야 하기 때문 — 도발 공격
    //     (`TauntAttackGrantSystem`)·v1 투사체처럼 코드가 만드는 `AttackState` 는 도형을 모르고 0 으로
    //     남는데, 그게 곧 오늘 동작(360°)이어야 한다. 저작 enum 에는 `None` 이 없다(`Circle/360` 이
    //     항등원이라 중복) — **둘의 번호를 맞추려 들지 말 것.**
    //
    // 도형은 항상 **+X 방향**이다. 유닛이 타겟 쪽으로 좌/우 반전하므로 회전이 없다 — 호출부가
    // `side` 로 dx 부호를 접어 넘긴다(`AttackReach` 헤더).
    public struct AttackShapeBaked
    {
        public const byte OmniKind = 0;     // 360° — 게이트 없음. 오늘 동작
        public const byte SectorKind = 1;   // 보는 쪽 부채꼴 — `sinHalf/cosHalf`
        public const byte BandKind = 2;     // 보는 쪽 가로 띠 — `halfWidth`

        public byte kind;
        public float sinHalf;    // Sector 전용 — 반각의 sin
        public float cosHalf;    // Sector 전용 — 반각의 cos
        public float halfWidth;  // Band 전용 — 띠의 세로 반폭(타일)

        public static AttackShapeBaked Omni => default;
        public bool IsOmni => kind == OmniKind;
    }
}
