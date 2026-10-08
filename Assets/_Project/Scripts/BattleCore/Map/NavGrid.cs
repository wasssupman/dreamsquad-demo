// salvaged from Assets/_Project/Scripts/Battle/Movement/NavGrid.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `NativeArray<byte>` → `byte[]`, `NativeHashSet<int2>` → `bool[]`(칸 인덱스).
//   해시셋을 배열로 바꾼 이유는 둘이다 — ⑴ 장애물은 격자 전체를 덮는 조밀한 정보라 해시가
//   손해고 ⑵ 해시셋은 **순회 순서가 계약이 아니라서** 시그니처를 XOR 로 접어야 했는데,
//   배열이면 row-major 라 순서가 구조적으로 고정된다. 술어 자체는 한 줄도 안 바꿨다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Map
{
    // 벽 질의의 단일 진입점.
    //
    // 벽은 두 층이다: 맵에서 오는 정적 벽(칸 종류 ∩ 통행 층)과 매 틱 재수집되는 동적
    // 장애물(배치 유닛·길막 장판). 갱신 주기가 달라 하나로 구울 수 없다. 그래서 `NavGrid` 는
    // 저장 상태가 아니라 **틱 뷰**다 — 두 출처를 합쳐 읽기만 하고, 조립은 호출자가 한다.
    //
    // 생성자가 맵 런타임 타입을 받지 않는 것은 의도다. plain 값만 받아야 같은 함수를
    // 테스트 픽스처가 직접 세울 수 있다.
    public readonly struct NavGrid
    {
        public readonly byte[] StaticWalk;     // 1 = 걸을 수 있음. null = 평지(픽스처 보호)
        public readonly bool[] BlockedCells;   // 칸 인덱스 → 장애물. null = 없음
        public readonly bool HasObstacles;
        public readonly int2 GridSize;
        public readonly float TileSize;
        public readonly float3 Origin;

        public NavGrid(byte[] staticWalk, bool[] blockedCells, bool hasObstacles,
                       int2 gridSize, float tileSize, float3 origin = default)
        {
            StaticWalk = staticWalk;
            BlockedCells = blockedCells;
            HasObstacles = hasObstacles;
            GridSize = gridSize;
            TileSize = tileSize;
            Origin = origin;
        }

        public bool InBounds(int2 cell)
            => cell.x >= 0 && cell.x < GridSize.x && cell.y >= 0 && cell.y < GridSize.y;

        // 「이 칸을 걸을 수 없는가」 를 묻는 유일한 지점. 경계 밖은 항상 막힘.
        //
        // 골 예외가 없는 것에 유의 — 골은 걷는 칸이라 마스크에서 이미 통행 가능이다.
        // (골이 걷는 칸이 아닌 맵이 생기면 그건 맵 저작 결함이지 술어가 감쌀 일이 아니다.)
        public bool IsBlocked(int2 cell)
        {
            if (!InBounds(cell)) return true;
            int idx = GridMath.CellIndex(cell, GridSize);
            // 마스크 미생성 = 평지로 본다. 프로덕션은 항상 채우므로 해당 없고, 이 규약은
            // 마스크를 안 쓰는 단위 테스트 픽스처를 보호한다.
            if (StaticWalk != null && StaticWalk[idx] == 0) return true;
            return HasObstacles && BlockedCells != null && BlockedCells[idx];
        }

        // BFS 소비자는 배열을 요구한다. 술어는 여기 하나뿐이므로 각 호출부가 벽 합성을
        // 복제하지 않는다. `outMask` 는 길이 = 칸 수여야 한다(호출자 책임).
        public void MaterializeWalkMask(byte[] outMask)
        {
            for (int y = 0; y < GridSize.y; y++)
            for (int x = 0; x < GridSize.x; x++)
            {
                var cell = new int2(x, y);
                outMask[GridMath.CellIndex(cell, GridSize)] = IsBlocked(cell) ? (byte)0 : (byte)1;
            }
        }
    }
}
