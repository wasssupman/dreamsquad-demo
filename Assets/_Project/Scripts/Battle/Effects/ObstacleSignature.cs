using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // continuous-agent-movement unit 5 — 차단 셀 집합의 "바뀌었나" 판정용 시그니처.
    //
    // ObstacleLifetimeSystem 이 매 프레임 blockedCells 를 Clear 후 재수집하므로 집합 자체로는
    // 변화를 알 수 없다. 그렇다고 매 프레임 필드를 다시 굽는 것은 낭비다.
    //
    // ⚠ 순서 무관이어야 한다. 집합 순회 순서는 청크 구성에 따라 달라질 수 있고, 순서에
    // 의존하면 내용이 같은데도 시그니처가 흔들려 헛 재빌드가 나며 결정론이 깨진다.
    // XOR 은 교환·결합법칙을 만족해 순회 순서와 무관하다.
    //
    // 순수 함수. NativeHashSet 하나만 받는다.
    public static class ObstacleSignature
    {
        // 해시 충돌 시 변경을 놓친다(한 프레임 지연이 아니라 영영 놓침). 셀 좌표는 작은
        // 정수쌍이고 실사용 격자는 ≤180셀이라 확률이 무시 가능하지만, 개수를 함께 섞어
        // "몇 개인가"가 다르면 반드시 다른 시그니처가 나오도록 완화한다.
        public static uint Compute(in NativeHashSet<int2> blockedCells, bool hasObstacles)
        {
            if (!hasObstacles || !blockedCells.IsCreated) return 0u;

            uint acc = 0u;
            int count = 0;
            foreach (var cell in blockedCells)
            {
                acc ^= CellHash(cell);
                count++;
            }
            return acc ^ ((uint)count * 2654435761u);   // Knuth multiplicative
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
    }
}
