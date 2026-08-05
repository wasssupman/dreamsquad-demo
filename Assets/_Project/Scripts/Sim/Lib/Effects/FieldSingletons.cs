using System.Collections.Generic;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — Effects 가 소유하는 정적 flow field.
    /// 구 `FlowFieldSingleton` 이식(`NativeArray` → 관리 배열). Movement 는 **읽기만** 한다.
    ///
    /// ⚠ `IsCreated` 를 남긴다 — 소비자(`MovementSystem`·`DefenderFieldSystem`)가 그 술어로
    /// 게이트한다. 네이티브 컬렉션의 할당 여부였던 것이 신 sim 에선 **null 여부**다.
    /// `Dispose` 는 없다(관리 GC).
    /// </summary>
    public struct FlowFieldSingleton
    {
        /// 셀당 단위 방향. goal 은 zero.
        public SimVec2[] flow;
        /// 최근접 goal 로부터의 BFS cost. 도달 불가 = `int.MaxValue`.
        public int[] dist;
        public SimInt2 gridSize;
        /// <summary>
        /// 픽스처 폴백 전용 기준 — `goals` 미설정 시 <see cref="IsGoalCell"/> 이 이걸 쓴다.
        /// 프로덕션 빌더는 `goals` 를 항상 채우므로 읽히지 않는다.
        /// </summary>
        public SimInt2 goalCell;
        /// 골 집합. 미설정 시 `goalCell` 폴백.
        public SimInt2[] goals;
        public float tileSize;
        /// 보드 월드 원점(Tilemap 모드 = 0 고정).
        public SimVec3 origin;
        /// 디버그·재빌드 마커.
        public int version;

        public bool IsCreated => flow != null && dist != null;

        /// <summary>
        /// 골 멤버십. `goals` 가 있으면 그것으로(1~4 소량 루프), 없으면 `goalCell` 폴백.
        /// 폴백이 있어야 goals 를 안 채우는 픽스처가 기존 단일-goal 동작을 그대로 유지한다 —
        /// 도달·wall 예외·해저드 검증이 골 개수에 무관해진다.
        /// </summary>
        public bool IsGoalCell(SimInt2 cell)
        {
            if (goals != null && goals.Length > 0)
            {
                for (int i = 0; i < goals.Length; i++)
                    if (goals[i].Equals(cell)) return true;
                return false;
            }
            return cell.Equals(goalCell);
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — 막힌 셀 집합. 구 `ObstacleSingleton` 이식
    /// (`NativeParallelHashSet` → <see cref="HashSet{T}"/>).
    ///
    /// ⚠ **집합 순회 순서에 의존하는 코드를 만들지 말 것.** 소비자는 `Contains` 만 쓴다
    /// (`MovementCellTrim`). `HashSet` 순회는 삽입 순서를 보장하지 않으므로, 순회가 필요한
    /// 규칙이 생기면 그때 순서 있는 표현으로 바꿔야 한다.
    /// </summary>
    public struct ObstacleSingleton
    {
        public HashSet<SimInt2> blockedCells;
        public bool IsCreated => blockedCells != null;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/2 — 장애물. 구 `Obstacle` 이식.
    /// `worldPosition` 은 프레젠테이션 전용이지만 **상태 라인에 나가므로** 함께 옮긴다
    /// (트림은 `cell` 만 소비한다).
    /// </summary>
    public struct Obstacle
    {
        public SimInt2 cell;
        public SimVec3 worldPosition;
        public float remainingLife;
    }
}
