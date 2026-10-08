using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Map
{
    // 맵의 **런타임 상태**. 스냅샷은 읽기만 한다.
    //
    // 옛 전투는 이 자리에 싱글턴 컴포넌트 셋(`FlowFieldSingleton`·`DefenderFieldSingleton`·
    // `ObstacleSingleton`)이 있었고, 「기하는 1벌 · 라우팅만 N벌」을 한 컴포넌트에 욱여넣어야
    // 했다(`GetSingleton` 이 매치 2개에서 던지기 때문 — Entities 산물). 여기서는 그냥 필드다.
    //
    // 기하(`TileSize`·격자 크기·원점)의 주인은 여기 하나다(M3). 좌표계는 **격자 원점 0 의
    // 평면**이고 y = 0 이다 — 화면 좌표 변환은 뷰 소관이다.
    public sealed class MapRuntime
    {
        public readonly MapSnapshot Snapshot;
        public readonly FlowFieldSet Flow;
        public readonly NavGridSet Nav;
        public readonly ObstacleSet Obstacles;
        public readonly DefenderHuntField Hunt;
        public readonly PlacementOccupancy Occupancy;

        private readonly int2[] _effectScratch;

        public MapRuntime(MapSnapshot map, byte[] traversalMasks)
        {
            Snapshot = map ?? MapSnapshot.Empty();
            // 픽스처가 크기만 세우고 격자를 안 채웠을 수 있다 — 파생으로 메운다(저작은 무변).
            Snapshot.Normalize();
            GridSize = new int2(Snapshot.Width, Snapshot.Height);

            // 통행 마스크 목록은 정의표에서 온다(그 판에 나오는 유닛들이 여는 층). 기본 마스크를
            // 첫 슬롯에 고정하는 이유: 층이 미주입(0)인 유닛이 반드시 갈 곳이 있어야 한다.
            byte[] masks = NormalizeMasks(traversalMasks);
            HuntLayers = masks.Length > 0 ? masks[0] : TraversalSlots.DefaultMask;

            Obstacles = new ObstacleSet(GridSize);
            Flow = new FlowFieldSet(Snapshot, masks);
            Nav = new NavGridSet(Snapshot);
            Hunt = new DefenderHuntField(Snapshot);
            Occupancy = new PlacementOccupancy();
            _effectScratch = new int2[math.max(1, Snapshot.CellCount)];

            // 최초 1회는 장애물이 없어도 전 슬롯을 굽는다 — 그래야 첫 틱부터 방향이 있다.
            Obstacles.BeginRebuild();
            Obstacles.EndRebuild();
            Flow.Rebuild(Obstacles, force: true);
            Nav.Invalidate(Obstacles.Signature);
        }

        public int2 GridSize { get; }

        public float TileSize => Snapshot.TileSize;

        /// <summary>공용 사냥판을 굽는 층. 오늘 무제한 감지 저작은 전부 지상이다(M7 메모).</summary>
        public byte HuntLayers { get; }

        public bool InBounds(int2 cell) => Snapshot.InBounds(cell);

        public int2 CellOf(float3 pos) => GridMath.WorldToCell(pos, TileSize, GridSize);

        public float3 CenterOf(int2 cell) => GridMath.CellToWorldCenter(cell, TileSize);

        /// <summary>효과 타일 자리 뽑기(M16). 판 시작에 한 번 — 회수는 없다.</summary>
        public int SelectEffectTiles(int mapSeed, int count, int2[] outCells)
            => EffectTileSelect.SelectCells(Snapshot, mapSeed, count, _effectScratch, outCells);

        // 기본 마스크가 항상 첫 슬롯이고, 나머지는 호출자 순서를 보존해 중복 제거한다.
        private static byte[] NormalizeMasks(byte[] raw)
        {
            var list = new System.Collections.Generic.List<byte>(4) { TraversalSlots.DefaultMask };
            if (raw != null)
                for (int i = 0; i < raw.Length; i++)
                {
                    byte m = raw[i] != 0 ? raw[i] : TraversalSlots.DefaultMask;
                    if (!list.Contains(m)) list.Add(m);
                }
            return list.ToArray();
        }
    }
}
