using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.BattleCore.Map
{
    // 통행 층마다 다른 벽(M1).
    //
    // 예전엔 틱당 하나(Path 전용)였는데, 그러면 Ground 를 여는 유닛이 배치지에 서는 순간
    // 자기 칸이 벽으로 읽혀 영원히 clamp 됐다 — 경로는 찾는데 발을 못 뗐다.
    //
    // 층 종류 수만큼만 조립한다(한-칸 메모가 아니라 **층별 캐시**다 — 순회 순서가 층별로
    // 뭉치지 않아도 재조립이 안 일어난다). 장애물이 바뀐 틱에 통째로 무효화한다.
    //
    // ⚠ **`_masks` 사전은 순회하지 않는다 — 순회 순서가 결정론에 영향을 준다.**
    // 사전은 「이 층의 벽 배열이 어디 있나」를 답하는 **조회용**이고, 층 목록이 필요한 곳은
    // 삽입 순서를 보존하는 `_layers` 를 본다. `Dictionary` 순회 순서는 런타임·해시·삽입
    // 이력에 따라 달라지므로 그 위에서 판을 굴리면 같은 시드가 다른 판이 된다.
    public sealed class NavGridSet
    {
        private readonly MapSnapshot _map;
        private readonly int2 _gridSize;
        private readonly Dictionary<byte, byte[]> _masks = new Dictionary<byte, byte[]>(4);
        private readonly List<byte> _layers = new List<byte>(4);

        private uint _builtSignature = uint.MaxValue;

        public NavGridSet(MapSnapshot map)
        {
            _map = map;
            _gridSize = new int2(map.Width, map.Height);
        }

        /// <summary>장애물이 바뀌면 다음 `For` 가 다시 조립하도록 표시한다.</summary>
        public void Invalidate(uint signature)
        {
            if (signature == _builtSignature) return;
            _builtSignature = signature;
            _layers.Clear();   // 키 목록만 비운다 — 배열은 재사용한다(틱 중 할당 0)
        }

        /// <summary>그 통행 층이 보는 벽. 0 = 미주입 → 기본 마스크로 읽어 현행을 재현한다.</summary>
        public NavGrid For(byte traversalLayers, ObstacleSet obstacles)
        {
            if (traversalLayers == 0) traversalLayers = TraversalSlots.DefaultMask;

            if (!_masks.TryGetValue(traversalLayers, out var mask))
            {
                mask = new byte[math.max(1, _map.CellCount)];
                _masks[traversalLayers] = mask;
            }

            bool fresh = false;
            for (int i = 0; i < _layers.Count; i++)
                if (_layers[i] == traversalLayers) { fresh = true; break; }

            if (!fresh)
            {
                MovementCellTrim.FillWalkMask(_map.CellLayers, _gridSize, traversalLayers,
                                              obstacles.HasObstacles, obstacles.Blocked, mask);
                _layers.Add(traversalLayers);
            }
            return new NavGrid(mask, null, false, _gridSize, _map.TileSize);
        }
    }
}
