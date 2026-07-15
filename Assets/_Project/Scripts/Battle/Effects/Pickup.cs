using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // season-gimmick-overwork unit 4 — 픽업 종류. 소비 효과가 종류별로 다르다.
    // append-only (StackKind/StatusFxKind 전례). 각 값은 실존 생산자에 매핑.
    public enum PickupKind : byte
    {
        Redbull, // 야근 기믹: 소비 시 라스트런(공속 버프 → 최대체력 컷)
    }

    // season-gimmick-overwork unit 4 — 맵 위 소비형 픽업 (Effects 소유).
    // 이동/배치 타일영역에 스폰. 같은 셀의 유닛(적 통과 / defender 배치)이 소비(unit 5).
    // 미소비 시 remainingLife 만료로 despawn. one-shot 소비형 — 해저드(지속 영역)와 별개 아키타입.
    public struct Pickup : IComponentData
    {
        public int2 cell;
        public PickupKind kind;
        public float remainingLife;
    }
}
