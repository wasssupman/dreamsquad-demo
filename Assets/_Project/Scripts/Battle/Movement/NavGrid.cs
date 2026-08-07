using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    // continuous-agent-movement unit 1·2 — 벽 질의의 단일 진입점.
    //
    // 벽은 이 게임에서 두 층이다: 맵 빌드 시 1회 굽는 정적 벽(tiles == Walk)과,
    // ObstacleLifetimeSystem 이 매 프레임 Clear 후 재수집하는 동적 장애물. 갱신 주기가
    // 달라 하나로 구울 수 없다. 그래서 NavGrid 는 저장 상태가 아니라 **프레임 뷰**다 —
    // 두 출처를 합쳐 읽기만 하고, 조립은 호출자가 프레임마다 한다.
    //
    // 생성자가 FlowFieldSingleton/ObstacleSingleton 을 받지 않는 것은 의도다. 그 타입을
    // 알면 ECS 에 묶여 다른 아키텍처가 같은 함수를 재사용할 수 없다. plain 값만 받는다.
    //
    // unit 2 — 술어의 근거는 **지형 데이터**다. 이전엔 flow == 0 을 벽으로 읽었는데,
    // 그건 경로 계산 *결과*에 벽의 정의를 얹은 형태라 두 가지를 못 했다:
    //   (1) 봉쇄로 필드가 끊기면 차단 구역 전체가 벽이 된다(D1-b 를 켤 수 없다).
    //   (2) 평활화 레이캐스트가 쓸 수 없다 — 가시선은 "지형이 막혔나"를 묻는데
    //       flow 는 "거기서 어디로 가나"만 안다.
    public readonly struct NavGrid
    {
        public readonly NativeArray<byte>   staticWalk;   // 1 = walkable (tiles == Walk)
        public readonly NativeHashSet<int2> blockedCells;
        public readonly bool                hasObstacles;
        public readonly int2                gridSize;
        public readonly float               tileSize;
        public readonly float3              origin;

        public NavGrid(
            NativeArray<byte>   staticWalk,
            NativeHashSet<int2> blockedCells,
            bool                hasObstacles,
            int2                gridSize,
            float               tileSize,
            float3              origin)
        {
            this.staticWalk   = staticWalk;
            this.blockedCells = blockedCells;
            this.hasObstacles = hasObstacles;
            this.gridSize     = gridSize;
            this.tileSize     = tileSize;
            this.origin       = origin;
        }

        public bool InBounds(int2 cell)
            => cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y;

        // "이 칸을 걸을 수 없는가" 를 묻는 유일한 지점. 경계 밖은 항상 막힘.
        //
        // 골 예외가 없는 것에 유의 — 골은 tiles == Walk 라 마스크에서 이미 통행 가능이다.
        // (골이 Walk 가 아닌 맵이 생기면 그건 맵 저작 결함이지 술어가 감쌀 일이 아니다.)
        public bool IsBlocked(int2 cell)
        {
            if (!InBounds(cell)) return true;
            // 마스크 미생성 = 평지로 본다(정적 벽 없음). 프로덕션은 SimFieldInstaller 가 항상
            // 채우므로 해당 없고, 이 규약은 마스크를 안 쓰는 EditMode 픽스처를 보호한다
            // (goals 를 IsCreated 불변식에서 뺀 것과 같은 전략).
            if (staticWalk.IsCreated && staticWalk[GridMath.CellIndex(cell, gridSize)] == 0) return true;
            return hasObstacles && blockedCells.IsCreated && blockedCells.Contains(cell);
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
