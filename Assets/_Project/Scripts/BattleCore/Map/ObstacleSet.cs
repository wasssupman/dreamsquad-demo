// 시그니처 규칙은 Assets/_Project/Scripts/Battle/Effects/ObstacleSignature.cs 에서 옮겼다
// (battle-core-rebuild unit 2). 해시 자체는 그대로고, 순회가 `NativeHashSet` 에서 row-major
// 배열로 바뀌어 **순서가 구조적으로 고정**됐다 — XOR 의 교환법칙에 기대지 않아도 된다.
using Unity.Mathematics;

namespace Wassup.BattleCore.Map
{
    // 「지금 어느 칸이 막혀 있나」 + 「그게 바뀌었나」.
    //
    // 매 틱 재수집한다(배치 유닛 footprint · 길막 장판 · 디버그 저작). 집합 자체로는 변화를
    // 알 수 없어서 시그니처를 함께 굽고, **바뀐 틱에만** 흐름장을 다시 굽는다.
    //
    // ⚠ 해시 충돌 시 변경을 놓친다(한 틱 지연이 아니라 영영 놓침). 칸 좌표는 작은 정수쌍이고
    // 실사용 격자는 수백 칸이라 확률이 무시 가능하지만, **개수를 함께 섞어** 「몇 개인가」가
    // 다르면 반드시 다른 시그니처가 나오도록 완화한다.
    public sealed class ObstacleSet
    {
        private readonly bool[] _blocked;
        private readonly bool[] _manual;
        private readonly int2 _gridSize;

        private int _count;
        private uint _signature;

        public ObstacleSet(int2 gridSize)
        {
            _gridSize = gridSize;
            int n = math.max(1, gridSize.x * gridSize.y);
            _blocked = new bool[n];
            _manual = new bool[n];
        }

        /// <summary>칸 인덱스 → 막힘. `NavGrid` 가 이 배열을 그대로 읽는다.</summary>
        public bool[] Blocked => _blocked;

        public bool HasObstacles => _count > 0;
        public int Count => _count;
        public uint Signature => _signature;

        /// <summary>디버그·저작이 고정으로 막는 칸. 매 틱 재수집을 살아남는다(`DebugSetObstacle`).</summary>
        public void SetManual(int2 cell, bool on)
        {
            if (!InBounds(cell)) return;
            _manual[GridMath.CellIndex(cell, _gridSize)] = on;
        }

        /// <summary>틱 재수집 시작. 고정 칸은 남고 나머지는 지워진다.</summary>
        public void BeginRebuild()
        {
            for (int i = 0; i < _blocked.Length; i++) _blocked[i] = _manual[i];
        }

        public void Block(int2 cell)
        {
            if (!InBounds(cell)) return;
            _blocked[GridMath.CellIndex(cell, _gridSize)] = true;
        }

        /// <summary>사각 점유를 통째로 막는다(다칸 footprint — M29).</summary>
        public void BlockRect(int2 anchor, int width, int height)
        {
            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
                Block(new int2(anchor.x + dx, anchor.y + dy));
        }

        /// <summary>재수집 종료 → 시그니처 갱신. 반환 = 바뀌었나(= 흐름장을 다시 구워야 하나).</summary>
        public bool EndRebuild()
        {
            uint acc = 0u;
            int count = 0;
            for (int i = 0; i < _blocked.Length; i++)
            {
                if (!_blocked[i]) continue;
                acc ^= CellHash(new int2(i % _gridSize.x, i / _gridSize.x));
                count++;
            }
            uint next = count == 0 ? 0u : acc ^ ((uint)count * 2654435761u);   // Knuth multiplicative
            _count = count;
            bool changed = next != _signature;
            _signature = next;
            return changed;
        }

        // 좌표를 섞어 (x,y) 와 (y,x) 가 같은 값이 되지 않게 한다 — 대칭 좌표쌍이 서로를
        // XOR 로 상쇄해 빈 집합과 구분이 안 되는 사고를 막는다.
        public static uint CellHash(int2 cell)
        {
            uint x = (uint)(cell.x + 32768);
            uint y = (uint)(cell.y + 32768);
            uint h = x * 73856093u ^ y * 19349663u;
            h ^= h >> 13;
            h *= 1274126177u;
            return h ^ (h >> 16);
        }

        private bool InBounds(int2 cell)
            => cell.x >= 0 && cell.x < _gridSize.x && cell.y >= 0 && cell.y < _gridSize.y;
    }
}
