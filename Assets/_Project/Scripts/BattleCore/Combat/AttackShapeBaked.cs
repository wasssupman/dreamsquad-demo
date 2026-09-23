// salvaged from Assets/_Project/Scripts/Data/AttackShapeBaked.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: 없음. 이 unit 이 이 타입을 가져오는 이유는 하나다 — `AttackReach` 의
// 부가 타격 진입점이 도형 항을 받고, 그 진입점을 「나중에 쓸 거니까」로 빼 두면 그때
// **원 항만 쓰는 복사본**이 생긴다(제약 13 이 막는 바로 그 형태). 도형의 **저작·bake** 는 unit 3 이다.
namespace Wassup.BattleCore.Combat
{
    // 공격 판정 도형의 **bake 된** 형태. 코어는 각도를 모른다 — 저작 각도는 bake 1회에
    // `(sin, cos)` 가 되고 폭은 반폭이 된다.
    //
    // ⚠ **`kind 0 = Omni`** 여야 한다. 코드가 만드는 공격(도발 공격·투사체)은 도형을 모르고
    // 0 으로 남는데, 그게 곧 오늘 동작(360°)이어야 한다.
    public struct AttackShapeBaked
    {
        public const byte OmniKind = 0;     // 360° — 게이트 없음
        public const byte SectorKind = 1;   // 주 대상 방향 부채꼴 — `sinHalf`/`cosHalf`
        public const byte BandKind = 2;     // 주 대상 방향 띠 — `halfWidth`

        public byte kind;
        public float sinHalf;
        public float cosHalf;
        public float halfWidth;

        public static AttackShapeBaked Omni => default;
        public bool IsOmni => kind == OmniKind;
    }
}
