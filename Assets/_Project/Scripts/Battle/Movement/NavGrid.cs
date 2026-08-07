using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // continuous-agent-movement unit 1 — 벽 질의의 단일 진입점.
    //
    // 벽은 이 게임에서 두 층이다: 맵 빌드 시 1회 굽는 정적 벽(tiles == Walk)과,
    // ObstacleLifetimeSystem 이 매 프레임 Clear 후 재수집하는 동적 장애물. 갱신 주기가
    // 달라 하나로 구울 수 없다. 그래서 NavGrid 는 저장 상태가 아니라 **프레임 뷰**다 —
    // 두 출처를 합쳐 읽기만 하고, 조립은 호출자가 프레임마다 한다.
    //
    // 생성자가 FlowFieldSingleton/ObstacleSingleton 을 받지 않는 것은 의도다. 그 타입을
    // 알면 ECS 에 묶여 다른 아키텍처가 같은 함수를 재사용할 수 없다. plain 값만 받는다.
    public readonly struct NavGrid
    {
        public readonly NativeArray<byte>   staticWalk;   // 1 = walkable. 미생성이면 flow 폴백(아래).
        public readonly NativeHashSet<int2> blockedCells;
        public readonly bool                hasObstacles;
        public readonly int2                gridSize;
        public readonly float               tileSize;
        public readonly float3              origin;

        // ── unit 1 한정: 정적 마스크 부재 시 기존 zero-flow 술어를 그대로 재현하기 위한 입력.
        //    unit 2 가 술어를 마스크 단독으로 바꾸면서 이 셋을 함께 제거한다.
        public readonly NativeArray<float2> flow;
        public readonly NativeArray<int2>   goals;
        public readonly int2                goalCell;

        public NavGrid(
            NativeArray<byte>   staticWalk,
            NativeHashSet<int2> blockedCells,
            bool                hasObstacles,
            int2                gridSize,
            float               tileSize,
            float3              origin,
            NativeArray<float2> flow     = default,
            NativeArray<int2>   goals    = default,
            int2                goalCell = default)
        {
            this.staticWalk   = staticWalk;
            this.blockedCells = blockedCells;
            this.hasObstacles = hasObstacles;
            this.gridSize     = gridSize;
            this.tileSize     = tileSize;
            this.origin       = origin;
            this.flow         = flow;
            this.goals        = goals;
            this.goalCell     = goalCell;
        }

        public bool InBounds(int2 cell)
            => cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y;

        // "이 칸을 걸을 수 없는가" 를 묻는 유일한 지점. 경계 밖은 항상 막힘.
        public bool IsBlocked(int2 cell)
        {
            if (!InBounds(cell)) return true;
            if (IsStaticWall(cell)) return true;
            return hasObstacles && blockedCells.IsCreated && blockedCells.Contains(cell);
        }

        // ⚠ unit 1 은 술어를 바꾸지 않는다 — **flow 가 있으면 무조건 기존 zero-flow 규칙**이다.
        // 정적 마스크가 우선하면 고립된 Walk 셀(도달 불가 → flow=0)의 판정이 벽에서 통행가능으로
        // 뒤집힌다. 그건 의미 변경이고 unit 2 의 몫이다. unit 1 에서 그게 새면 "이관 탓인지 술어
        // 탓인지" 를 가리려고 unit 을 나눈 의미가 사라진다.
        // unit 2 가 이 우선순위를 뒤집고 flow/goals 폴백을 통째로 제거한다.
        private bool IsStaticWall(int2 cell)
        {
            if (flow.IsCreated)
            {
                // multi-goal-map — 골 셀은 flow=0 이라 zero-flow=wall 규칙에 걸린다.
                // 모든 골을 wall 예외로 빼 적이 골 밖으로 clamp 되지 않게 한다.
                if (IsGoalCell(cell)) return false;
                return math.lengthsq(flow[GridMath.CellIndex(cell, gridSize)]) < 1e-6f;
            }
            if (staticWalk.IsCreated)
                return staticWalk[GridMath.CellIndex(cell, gridSize)] == 0;
            return false;
        }

        // FlowFieldSingleton.IsGoalCell 과 같은 규칙(goals 멤버십 / goalCell 폴백).
        // 폴백 술어 전용이라 unit 2 에서 함께 사라진다.
        private bool IsGoalCell(int2 cell)
        {
            if (goals.IsCreated && goals.Length > 0)
            {
                for (int i = 0; i < goals.Length; i++)
                    if (goals[i].Equals(cell)) return true;
                return false;
            }
            return cell.Equals(goalCell);
        }

        // BFS 소비자(AggroChaseMath·PatrolAreaMath)는 배열을 요구한다. 술어는 여기 하나뿐이므로
        // 각 호출부가 벽 합성을 복제하지 않는다.
        // outMask 는 gridSize.x * gridSize.y 길이여야 한다(호출자 책임).
        public void MaterializeWalkMask(NativeArray<byte> outMask)
        {
            for (int y = 0; y < gridSize.y; y++)
            for (int x = 0; x < gridSize.x; x++)
            {
                var cell = new int2(x, y);
                outMask[GridMath.CellIndex(cell, gridSize)] = IsBlocked(cell) ? (byte)0 : (byte)1;
            }
        }
    }
}
