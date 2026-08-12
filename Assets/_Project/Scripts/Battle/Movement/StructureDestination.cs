using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // instinct-content unit 3 — 「이 적이 지금 어느 거점으로 걸어가는가」.
    //
    // Movement 맥락 소유(라우팅 상태다). Units/Combat 은 읽기만 한다.
    //
    // 왜 필요한가: 흐름장은 늘 골로 흐른다. 스폰이 하나면 흐름장도 하나라 **전원 같은
    // 길**로 가고, 그 길 밖의 본능은 아무도 안 친다(unit 2 실측: 방문 0 · 75초 뒤 HP 95%).
    // 목적지를 유닛이 들고 있어야 「가까운 본능부터 부순다」가 성립한다.
    //
    // 왜 저작 웨이포인트를 재활용하지 않았나: 웨이포인트는 맵이 소유한 **정적 경로**
    // (`waypointCells` 는 필드 수명)라 유닛마다 다른 목적지를 실을 수 없다. 이쪽은
    // 유닛별 · 전투 중 변하는 선택이다.
    public struct StructureDestination : IComponentData
    {
        public int2   cell;        // 거점 중심 셀 = 흐름장 슬롯 키(`SlotFor`)
        public Entity structure;   // 이 거점이 죽으면 재선정한다
    }
}
