using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 9 — Effects 맥락이 소유하는 정적 flow field.
    // Allocator.Persistent 로 할당, 판 종료 / OnDestroy 에서 dispose.
    // Movement 맥락이 읽기 전용으로 consume.
    public struct FlowFieldSingleton : IComponentData
    {
        public NativeArray<float2> flow;        // [width * height], 각 cell 의 단위 방향. goal = zero.
        public NativeArray<int>    dist;        // BFS cost from nearest goal. Unreachable = int.MaxValue.
        public int2                gridSize;    // (Width, Height)
        public int2                goalCell;    // 픽스처 폴백 전용 기준(goals 미설정 시 IsGoalCell 이 이걸 씀).
                                                // 프로덕션 BuildFlowField 는 goals 를 항상 채우므로 여기선 읽히지 않음.
        public NativeArray<int2>   goals;       // multi-goal-map — 골 집합. 미설정 시 goalCell 폴백.
        public float               tileSize;
        public float3              origin;      // board 월드 원점 (Tilemap 모드 = zero 고정). map-origin-placement.
        public int                 version;     // 디버그 / Phase 10 event-driven rebuild 마커

        public bool IsCreated => flow.IsCreated && dist.IsCreated;

        // multi-goal-map 유닛 2 — 셀이 골인가. goals 설정 시 멤버십(1~4 소량 루프),
        // 아니면 primary goalCell 폴백. goals 를 안 채우는 EditMode 픽스처는 기존 단일-goalCell
        // 동작 그대로 유지 → 도달/wall예외/해저드검증이 골 개수에 무관해진다.
        public bool IsGoalCell(int2 cell)
        {
            if (goals.IsCreated && goals.Length > 0)
            {
                for (int i = 0; i < goals.Length; i++)
                    if (goals[i].Equals(cell)) return true;
                return false;
            }
            return cell.Equals(goalCell);
        }

        public void Dispose()
        {
            if (flow.IsCreated) flow.Dispose();
            if (dist.IsCreated) dist.Dispose();
            if (goals.IsCreated) goals.Dispose();
        }
    }
}
