using Unity.Mathematics;

namespace Wassup.BattleCore.Map
{
    // **공용 사냥판** — 무제한 감지(보스·보너스)의 이동 필드.
    //
    // 무제한 감지의 진짜 질문은 「**아무** 방어유닛이나」라서 공용 필드가 **정확한 답**이다.
    // 유한 반경 감지는 여기 오지 않는다 — 그쪽은 「그 적에게」가 질문이라 대상 지향 추격판
    // (`ChaseFieldCache`)을 굽는다.
    //
    // ⚠ **반경은 동시에 살아 있는 헌터 중 가장 짧은 사거리로 내려간다**(M7 · min fold).
    // 소스는 「모든 헌터가 발사 가능한 칸」이어야 사거리 짧은 헌터가 dist 0 칸에서 발사 불가로
    // 서버리는 교착이 구조적으로 불가능하다. 사거리 긴 헌터는 소스 도달 전에 교전으로 먼저 멈춘다.
    // 이질 사거리(근접 잡몹 + 원거리 보스)가 겹치는 구간이 그 조건이다.
    //
    // ⚠ 소스 수집은 **진영 필터 하나뿐**인데 감지는 타겟 마스크·통행층·클래스 필터를 지난다.
    // **같지 않은 것이 정상**이고 그 차이가 「발견한 대상 ≠ 걸어가는 목적지」다(M8).
    public sealed class DefenderHuntField
    {
        private readonly int2 _gridSize;
        private readonly float2[] _flow;
        private readonly int[] _dist;
        private readonly byte[] _walkScratch;
        private readonly CellQueue _queue;

        private int2[] _defenderCells = new int2[16];
        private int2[] _sources = new int2[64];

        public DefenderHuntField(MapSnapshot map)
        {
            _gridSize = new int2(map.Width, map.Height);
            int n = math.max(1, map.CellCount);
            _flow = new float2[n];
            _dist = new int[n];
            _walkScratch = new byte[n];
            _queue = new CellQueue(n * 2);
            Clear();
        }

        /// <summary>이 틱에 소스가 하나라도 섰나. false = 전 칸 도달 불가(골 마칭으로 되돌아간다).</summary>
        public bool HasSources { get; private set; }

        public float2[] Flow => _flow;
        public int[] Dist => _dist;

        public bool Reaches(int2 cell) => _dist[GridMath.CellIndex(cell, _gridSize)] != int.MaxValue;
        public int DistAt(int2 cell) => _dist[GridMath.CellIndex(cell, _gridSize)];

        public void Clear()
        {
            for (int i = 0; i < _dist.Length; i++) _dist[i] = int.MaxValue;
            for (int i = 0; i < _flow.Length; i++) _flow[i] = float2.zero;
            HasSources = false;
        }

        /// <summary>
        /// 살아 있는 방어유닛 칸을 소스 디스크로 펴 필드를 다시 굽는다.
        /// `rangeTiles` = 헌터 사거리의 min fold(호출자가 접어서 준다 — M7).
        /// 방어유닛 0 이면 전 칸 도달 불가로 리셋된다.
        /// </summary>
        public void Rebuild(byte[] cellLayers, byte huntLayers, ObstacleSet obstacles,
                            int2[] defenderCells, int defenderCount, int rangeTiles)
        {
            if (defenderCount <= 0) { Clear(); return; }
            rangeTiles = math.max(1, rangeTiles);

            EnsureDefenderCapacity(defenderCount);
            for (int i = 0; i < defenderCount; i++) _defenderCells[i] = defenderCells[i];

            int needed = defenderCount * FlowFieldBuilder.DiscArea(rangeTiles);
            if (_sources.Length < needed) _sources = new int2[needed];

            MovementCellTrim.FillWalkMask(cellLayers, _gridSize, huntLayers,
                                          obstacles.HasObstacles, obstacles.Blocked, _walkScratch);
            int count = FlowFieldBuilder.CollectDefenderSources(
                _walkScratch, _gridSize, _defenderCells, defenderCount, rangeTiles, _sources);
            if (count == 0) { Clear(); return; }

            FlowFieldBuilder.BuildFromSources(_walkScratch, _gridSize, _sources, count,
                                              _flow, _dist, _queue);
            HasSources = true;
        }

        private void EnsureDefenderCapacity(int n)
        {
            if (_defenderCells.Length >= n) return;
            int cap = _defenderCells.Length;
            while (cap < n) cap *= 2;
            _defenderCells = new int2[cap];
        }
    }
}
