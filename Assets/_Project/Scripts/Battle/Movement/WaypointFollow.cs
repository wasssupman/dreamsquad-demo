using Unity.Entities;

namespace Wassup.Battle.Movement
{
    // waypoint-routing unit 3 — Movement 소유 경로 진행 상태.
    // 경로 셀은 FlowFieldSingleton(Effects 소유)을 RO 로 읽고, 여기에는 인덱스만 둔다.
    public struct WaypointFollow : IComponentData
    {
        public int pathIndex;
        public int index;
    }
}
