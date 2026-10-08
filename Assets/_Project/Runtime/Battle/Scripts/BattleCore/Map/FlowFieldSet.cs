using System.Collections.Generic;
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Map
{
    // 슬롯 하나의 라우팅. **직접 인덱싱 금지의 «타입» 표현**이다.
    //
    // 옛 전투는 `NativeArray<NativeArray<T>>` 가 불법이라 `[slot * CellCount + cell]` flat
    // stride 로 접었고, 「직접 인덱싱 말고 뷰로만」이 **주석으로만** 막혀 있었다. 그래서
    // 슬롯이 늘어나는 순간 조용히 다른 슬롯을 읽는 사고가 가능했다.
    //
    // 순수 C# 이면 배열의 배열로 끝난다. 각 슬롯이 **자기 길이 CellCount 배열을 통째로**
    // 들므로 stride 라는 개념 자체가 없고, `Flow`/`Dist` 를 salvage 한 순수 함수에 그대로
    // 넘겨도 어긋날 자리가 없다.
    public readonly struct FlowSlot
    {
        private readonly float2[] _flow;
        private readonly int[] _dist;
        private readonly int2 _gridSize;

        internal FlowSlot(float2[] flow, int[] dist, int2 gridSize)
        {
            _flow = flow;
            _dist = dist;
            _gridSize = gridSize;
        }

        public bool Exists => _flow != null;

        /// <summary>슬롯 하나의 방향 배열(길이 = 칸 수). stride 없음 — 평활화가 이걸 그대로 받는다.</summary>
        public float2[] Flow => _flow;

        /// <summary>슬롯 하나의 거리 배열(길이 = 칸 수). 도달 불가 = `int.MaxValue`.</summary>
        public int[] Dist => _dist;

        public float2 DirAt(int2 cell) => _flow[GridMath.CellIndex(cell, _gridSize)];
        public int DistAt(int2 cell) => _dist[GridMath.CellIndex(cell, _gridSize)];
        public bool Reaches(int2 cell) => _dist[GridMath.CellIndex(cell, _gridSize)] != int.MaxValue;
    }

    // (목적지 × 통행 마스크) 슬롯의 모음.
    //
    // ⚠ **슬롯이 없으면 시끄럽게 실패한다**(M2 이식 제외). 옛 `SlotFor` 는 완전일치가 없으면
    // primary 슬롯을 조용히 돌려줬다("현행 안전망") — 미저작 목적지가 엉뚱한 길로 가는 것을
    // 덮는다. 새 코어는 던진다: 슬롯이 없다는 것은 **빌더가 목적지를 안 만들었다**는 뜻이고,
    // 그건 맵 조립의 결함이지 런타임이 감쌀 일이 아니다.
    public sealed class FlowFieldSet
    {
        // 슬롯 키. 사전 순회를 하지 않으므로(조회만) 결정론에 영향이 없다.
        private readonly struct Key : System.IEquatable<Key>
        {
            public readonly int2 Dest;
            public readonly byte Mask;

            public Key(int2 dest, byte mask) { Dest = dest; Mask = mask; }

            public bool Equals(Key o) => Dest.Equals(o.Dest) && Mask == o.Mask;
            public override bool Equals(object o) => o is Key k && Equals(k);
            public override int GetHashCode() => (Dest.x * 397 ^ Dest.y) * 397 ^ Mask;
            public override string ToString() => $"dest({Dest.x},{Dest.y}) mask{Mask}";
        }

        private sealed class SlotData
        {
            public int2 Dest;
            public byte Mask;
            public float2[] Flow;
            public int[] Dist;
            public int2[] Sources;
            public int SourceCount;
            /// <summary>Air 를 여는 마스크는 지상 장애물을 벽으로 보지 않는다 → 재빌드 대상이 아니다.</summary>
            public bool IgnoresObstacles;
        }

        private readonly MapSnapshot _map;
        private readonly int2 _gridSize;
        private readonly List<SlotData> _slots = new List<SlotData>(8);
        private readonly Dictionary<Key, int> _index = new Dictionary<Key, int>(8);

        private readonly byte[] _walkScratch;
        private readonly CellQueue _queue;

        private uint _builtSignature = uint.MaxValue;   // 아직 한 번도 안 구웠다

        public FlowFieldSet(MapSnapshot map, byte[] masks)
        {
            _map = map;
            _gridSize = new int2(map.Width, map.Height);
            int n = math.max(1, map.CellCount);
            _walkScratch = new byte[n];
            _queue = new CellQueue(n * 2);

            // 목적지 목록: 골 센티널 → 저작 경로 칸(순서 보존, 중복 제거) → 거점.
            // 마음은 이미 골 센티널이라 안 넣는다.
            //
            // 거점은 **종류를 묻지 않고** 전부 목적지가 된다. 「어느 진영의 무엇만 목적지다」를
            // 여기서 정하면 필드가 진영 규칙을 알게 되고, 그건 선택의 몫이다 — 누가 어디로 갈
            // 자격이 있는지는 `StructureChoice` 가 저작 마스크로 묻는다.
            var dests = new List<int2>(8) { MapSnapshot.GoalDestination };
            var extents = new List<int>(8) { 0 };
            for (int i = 0; i < map.WaypointCells.Length; i++)
                AddDest(dests, extents, map.WaypointCells[i], 1);
            for (int i = 0; i < map.Structures.Length; i++)
                AddDest(dests, extents, map.Structures[i].Cell, math.max(1, map.Structures[i].Footprint));

            for (int d = 0; d < dests.Count; d++)
            for (int m = 0; m < masks.Length; m++)
            {
                byte mask = masks[m] != 0 ? masks[m] : TraversalSlots.DefaultMask;
                var key = new Key(dests[d], mask);
                if (_index.ContainsKey(key)) continue;

                int extent = extents[d];
                int sourceCount = extent == 0
                    ? math.max(1, map.Goals.Length)
                    : extent * extent;
                var slot = new SlotData
                {
                    Dest = dests[d],
                    Mask = mask,
                    Flow = new float2[n],
                    Dist = new int[n],
                    Sources = new int2[sourceCount],
                    IgnoresObstacles = (mask & LayerBits.Air) != 0,
                };
                FillSources(map, dests[d], extent, slot);
                _index[key] = _slots.Count;
                _slots.Add(slot);
            }
        }

        public int SlotCount => _slots.Count;

        /// <summary>마지막으로 반영한 장애물 시그니처.</summary>
        public uint BuiltSignature => _builtSignature;

        /// <summary>
        /// (목적지 × 통행 마스크) → 슬롯. **없으면 던진다** — fail-open 을 옮기지 않았다(M2).
        /// </summary>
        public FlowSlot Slot(int2 dest, byte mask)
        {
            if (mask == 0) mask = TraversalSlots.DefaultMask;
            if (!_index.TryGetValue(new Key(dest, mask), out int i))
                throw new System.InvalidOperationException(
                    $"흐름장 슬롯 없음: {new Key(dest, mask)} — 맵 조립이 그 목적지를 안 만들었다. "
                    + "폴백하지 않는다(미저작 목적지가 엉뚱한 길로 가는 것을 덮기 때문).");
            var s = _slots[i];
            return new FlowSlot(s.Flow, s.Dist, _gridSize);
        }

        /// <summary>그 (목적지, 마스크) 슬롯이 있나. 「갈 수 있는 거점인가」를 묻는 쪽이 쓴다.</summary>
        public bool HasSlot(int2 dest, byte mask)
            => _index.ContainsKey(new Key(dest, mask == 0 ? TraversalSlots.DefaultMask : mask));

        /// <summary>골 슬롯 — 모든 이동의 기본 목적지.</summary>
        public FlowSlot GoalSlot(byte mask) => Slot(MapSnapshot.GoalDestination, mask);

        /// <summary>
        /// 장애물 시그니처가 바뀐 틱에만 다시 굽는다. 반환 = 실제로 구웠나.
        ///
        /// **부분 재빌드**: Air 를 여는 슬롯은 지상 장애물을 벽으로 보지 않으므로 건너뛴다.
        /// 최초 1회(`force`)는 전부 굽는다.
        /// </summary>
        public bool Rebuild(ObstacleSet obstacles, bool force = false)
        {
            uint sig = obstacles.Signature;
            if (!force && sig == _builtSignature) return false;
            bool first = _builtSignature == uint.MaxValue || force;

            for (int i = 0; i < _slots.Count; i++)
            {
                var s = _slots[i];
                if (!first && s.IgnoresObstacles) continue;   // 지상 장애물과 무관한 슬롯
                MovementCellTrim.FillWalkMask(_map.CellLayers, _gridSize, s.Mask,
                                              obstacles.HasObstacles, obstacles.Blocked, _walkScratch);
                FlowFieldBuilder.BuildFromSources(_walkScratch, _gridSize, s.Sources, s.SourceCount,
                                                  s.Flow, s.Dist, _queue);
            }
            _builtSignature = sig;
            return true;
        }

        private static void AddDest(List<int2> dests, List<int> extents, int2 cell, int extent)
        {
            for (int i = 0; i < dests.Count; i++)
                if (dests[i].Equals(cell)) return;
            dests.Add(cell);
            extents.Add(extent);
        }

        // 거점 목적지의 소스가 **점유 전체**인 이유: 중심 한 칸으로 쓰면 그 칸이 배치지면일 때
        // 빌더가 소스를 버리고 슬롯이 통째로 빈 필드가 된다. 다중 소스라 적은 중심이 아니라
        // **가장 가까운 벽면**에 도착한다 — 건물을 둘러싸고 팬다.
        private static void FillSources(MapSnapshot map, int2 dest, int extent, SlotData slot)
        {
            if (extent == 0)
            {
                int count = math.max(1, map.Goals.Length);
                for (int i = 0; i < count; i++)
                    slot.Sources[i] = map.Goals.Length > 0 ? map.Goals[i] : int2.zero;
                slot.SourceCount = count;
                return;
            }

            int half = (extent - 1) / 2;
            int n = 0;
            for (int dy = 0; dy < extent; dy++)
            for (int dx = 0; dx < extent; dx++)
                slot.Sources[n++] = new int2(dest.x - half + dx, dest.y - half + dy);
            slot.SourceCount = n;
        }
    }
}
