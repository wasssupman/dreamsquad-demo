using Unity.Mathematics;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 2 — 판 위에 깔린 «장(場)». 유닛이 아니라서 `BattleWorld.Units`
    // 밖에 산다(UML §2 의 `FieldCarrier`).
    //
    // 이 unit 이 여는 것은 **이동이 소비하는 두 종류**뿐이다: 포탈과 당김. 수명·생성은 효과
    // 레이어(unit 6)가 갖고, 여기서는 이동이 매 틱 읽기만 한다. 아군 버프 장은 종류만 예약한다.
    //
    // ⚠ **당김은 이동을 «대체»하지 않는다.** 이동 뒤에 더해지는 가산 변위이고, 그래서 벽과
    // 장애물에 막힌다. 대체하게 만들면 회오리가 유닛을 벽 안으로 끌고 들어간다.
    public enum FieldKind : byte
    {
        None = 0,
        AllyBuff = 1,
        Pull = 2,
        Portal = 3,
    }

    public sealed class FieldCarrier
    {
        public FieldKind Kind;

        /// <summary>Portal = 입구 · Pull = 중심 · AllyBuff = 중심.</summary>
        public float3 Center;

        /// <summary>Portal = 출구. 다른 종류에서는 쓰지 않는다.</summary>
        public float3 Exit;

        /// <summary>Portal = 입구 반경(월드) · Pull = 반경(칸) · AllyBuff = 반경(칸).</summary>
        public float Range;

        /// <summary>Pull = 초당 당기는 거리.</summary>
        public float Speed;

        /// <summary>남은 시간. 0 이하 = 무기한(효과 레이어가 정한다).</summary>
        public float Duration;

        /// <summary>진영(발동자). 당김·포탈은 진영을 보지 않는다 — 순찰 아군도 밀린다.</summary>
        public int Faction;
    }
}
