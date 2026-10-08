using System.Collections.Generic;
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Map
{
    // 「어느 칸을 누가 쓰고 있나」. **점유와 주인은 항상 쌍으로 바뀐다.**
    //
    // 옛 전투는 칸 집합(`_occupiedTiles`)과 칸→주인 사전(`_defenderCellOwner`)을 따로 들고
    // 두 곳에서 갱신했다. 쌍이 깨지면 죽은 유닛이 칸을 영영 물고, 증상은 「가끔 못 놓는 칸」으로
    // 나온다. 여기서는 한 사전이 둘을 겸하고, 해제가 O(점유 칸 수)가 되도록 역방향
    // (주인 → 자기 칸들)을 함께 든다.
    //
    // ⚠ **다칸 footprint 가 라이브다**(M29 — 방어유닛 전원 2×2, 캐논 2×3). 1×1 을 전제하면
    // 첫 배치에서 깨진다. 앵커는 **min 코너**이고 대표 칸은 없다(짝수 변엔 중심 칸이 없어서
    // 대표 칸은 정수 나눗셈 동전 던지기였고 사거리를 반 칸 옮겼다).
    //
    // 사전을 **순회하지 않는다** — 조회·추가·제거만이라 결정론에 영향이 없다.
    public sealed class PlacementOccupancy
    {
        private readonly Dictionary<long, int> _ownerOfCell = new Dictionary<long, int>(64);
        private readonly Dictionary<int, CellSpan> _cellsOfOwner = new Dictionary<int, CellSpan>(64);

        private readonly struct CellSpan
        {
            public readonly int2 Anchor;
            public readonly int Width;
            public readonly int Height;

            public CellSpan(int2 anchor, int width, int height)
            {
                Anchor = anchor; Width = width; Height = height;
            }
        }

        public int OwnerCount => _cellsOfOwner.Count;

        public bool IsOccupied(int2 cell) => _ownerOfCell.ContainsKey(Key(cell));

        /// <summary>그 칸의 주인. 없으면 `SimEntityId.NoneValue`.</summary>
        public int OwnerAt(int2 cell)
            => _ownerOfCell.TryGetValue(Key(cell), out int id) ? id : SimEntityId.NoneValue;

        /// <summary>그 사각이 전부 비어 있나(다칸 배치 판정 — 한 칸이라도 물려 있으면 거절).</summary>
        public bool IsFree(int2 anchor, int width, int height)
        {
            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
                if (IsOccupied(new int2(anchor.x + dx, anchor.y + dy))) return false;
            return true;
        }

        /// <summary>점유. 이미 그 주인이 다른 칸을 물고 있으면 먼저 놓는다(쌍 유지).</summary>
        public void Occupy(SimEntityId owner, int2 anchor, int width, int height)
        {
            Release(owner);
            _cellsOfOwner[owner.Value] = new CellSpan(anchor, width, height);
            for (int dy = 0; dy < height; dy++)
            for (int dx = 0; dx < width; dx++)
                _ownerOfCell[Key(new int2(anchor.x + dx, anchor.y + dy))] = owner.Value;
        }

        /// <summary>해제. 소멸과 **항상 짝**이다 — 쌍이 깨지면 죽은 유닛이 칸을 영영 문다.</summary>
        public bool Release(SimEntityId owner)
        {
            if (!_cellsOfOwner.TryGetValue(owner.Value, out var span)) return false;
            _cellsOfOwner.Remove(owner.Value);
            for (int dy = 0; dy < span.Height; dy++)
            for (int dx = 0; dx < span.Width; dx++)
                _ownerOfCell.Remove(Key(new int2(span.Anchor.x + dx, span.Anchor.y + dy)));
            return true;
        }

        public void Clear()
        {
            _ownerOfCell.Clear();
            _cellsOfOwner.Clear();
        }

        private static long Key(int2 cell) => ((long)cell.x << 32) ^ (uint)cell.y;
    }
}
