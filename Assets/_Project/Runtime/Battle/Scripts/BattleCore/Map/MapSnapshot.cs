using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Map
{
    // battle-core-rebuild unit 2 — 한 판의 «칸 격자 + 자리들». plain 스냅샷이다.
    //
    // 옛 `GeneratedMap`(Unity.Collections)의 후계지만 **소유권 규약이 다르다**: 여기엔
    // 해제할 네이티브 배열이 없고, 읽기 전용이며, 판이 도는 동안 바뀌지 않는다.
    // 판 중에 바뀌는 것은 둘뿐이고 둘 다 여기가 아니라 `MapRuntime` 이 든다 —
    // 장애물(`ObstacleSet`)과 점유표(`PlacementOccupancy`).
    //
    // ⚠ **배치 층과 통행 층은 다른 축이다**(M1). `PlaceMask` 는 「누가 여기 설 수 있나」,
    // `CellLayers` 는 「누가 여기를 지나갈 수 있나」다. 하나로 접으면 배치판이 통행판을
    // 따라 움직인다 — 옛 전투에서 실제로 났다(저작자가 「배치 금지」로 칠한 통로 23칸이
    // 라우팅에서 통째로 사라졌다).
    public sealed class MapSnapshot
    {
        public int Width;
        public int Height;

        /// <summary>칸 한 변의 월드 길이. 코어 좌표는 격자 원점 0 의 평면이라 원점은 없다.</summary>
        public float TileSize = 1f;

        /// <summary>칸 종류. 길이 = Width * Height, row-major(`y * Width + x`).</summary>
        public MapTile[] Tiles = System.Array.Empty<MapTile>();

        /// <summary>배치 층 비트(`LayerBits`). 저작이 정본이다 — 통행을 여기서 읽지 않는다.</summary>
        public byte[] PlaceMask = System.Array.Empty<byte>();

        /// <summary>통행 층 비트. **`Tiles` 에서만 파생한다**(M1 · 실측 사고 기록은 파일 헤더).</summary>
        public byte[] CellLayers = System.Array.Empty<byte>();

        /// <summary>레인(입구). 순번이 곧 `laneIndex` 이고 웨이브 결정론 키다.</summary>
        public int2[] Spawns = System.Array.Empty<int2>();

        /// <summary>레인별 기본 경로 번호. -1 = 골 직행. 길이는 `Spawns` 와 같거나 0.</summary>
        public int[] SpawnRoutes = System.Array.Empty<int>();

        /// <summary>마음(골). 1~N.</summary>
        public int2[] Goals = System.Array.Empty<int2>();

        /// <summary>경로 셀 평탄 배열. `WaypointRanges[path] = (start, count)`.</summary>
        public int2[] WaypointCells = System.Array.Empty<int2>();
        public int2[] WaypointRanges = System.Array.Empty<int2>();

        /// <summary>거점. 셀 + 진영 비트만 — 스탯은 정의표가 든다.</summary>
        public StructureSpot[] Structures = System.Array.Empty<StructureSpot>();

        /// <summary>보너스 포탈 칸. **`Spawns` 와 절대 안 섞는다** — `Spawns.Length` 는 레인 수다.</summary>
        public int2[] BonusSpawns = System.Array.Empty<int2>();

        public int CellCount => Width * Height;

        public static MapSnapshot Empty() => new MapSnapshot();

        /// <summary>
        /// 안 채운 배열을 칸 수에 맞게 세운다. **파생만 한다** — 저작을 덮어쓰지 않는다.
        ///
        /// 픽스처 보호 장치다: 크기·스폰·골만 세운 테스트 맵이 흐름장 조립에서 길이 불일치로
        /// 죽지 않게 한다. 저작 파이프라인(`MatchDefinitionBuilder`)은 셋 다 채워서 오므로
        /// 여기서 아무 일도 일어나지 않는다.
        ///
        /// ⚠ **통행 층은 `Tiles` 에서만 파생한다**(M1). `PlaceMask` 를 통행 정본으로 삼았다가
        /// 저작자가 「배치 금지」로 칠한 통로 23칸이 라우팅에서 통째로 사라진 실측 사고가 있다 —
        /// 저작 의미가 «칸의 종류»가 아니라 «어느 유닛이 여기 설 수 있나» 이기 때문이다.
        /// </summary>
        public void Normalize()
        {
            int n = CellCount;
            if (n <= 0) return;

            if (Tiles == null || Tiles.Length != n)
            {
                var tiles = new MapTile[n];
                for (int i = 0; i < n; i++)
                    tiles[i] = Tiles != null && i < Tiles.Length ? Tiles[i] : MapTile.Walk;
                Tiles = tiles;
            }
            if (PlaceMask == null || PlaceMask.Length != n)
            {
                var mask = new byte[n];
                // ⚠ **배치 폴백은 통행 파생과 «다르다».** 저작 파이프라인에서 안 막힌 칸은
                // Ground·Path·Air 셋을 다 연다(`LayerBits.OpenPlacement`) — 배치 마스크의 뜻이
                // 「누가 여기 설 수 있나」라서 걷는 칸도 배치를 받는다. 여기에 통행 파생을
                // 쓰면 Ground 유닛이 **어느 칸에도 못 서는** 판이 만들어진다.
                for (int i = 0; i < n; i++) mask[i] = LayerBits.OpenPlacement(Tiles[i]);
                PlaceMask = mask;
            }
            if (CellLayers == null || CellLayers.Length != n)
            {
                var layers = new byte[n];
                for (int i = 0; i < n; i++) layers[i] = LayerBits.Derive(Tiles[i]);
                CellLayers = layers;
            }

            // 마지막은 **예약 칸 폐쇄**다. 파생이 아니라 규칙이지만 여기 붙여 둔 이유는
            // 고정구가 이 한 줄을 빠뜨리면 테스트가 라이브와 **다른 판**을 보기 때문이다
            // (라이브 경로는 빌더가 같은 함수를 부른다).
            CloseReservedPlacement();
        }

        public bool InBounds(int2 cell)
            => cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;

        public int Index(int2 cell) => cell.y * Width + cell.x;

        public MapTile TileAt(int2 cell) => Tiles[Index(cell)];

        /// <summary>그 칸이 여는 배치 층. 마스크 미저작이면 칸 종류에서 파생(픽스처 보호).</summary>
        public byte PlaceLayersAt(int2 cell)
            => PlaceMask.Length == Tiles.Length
                ? LayerBits.Sanitize(PlaceMask[Index(cell)])
                : LayerBits.OpenPlacement(Tiles[Index(cell)]);

        /// <summary>배치 판정 = 칸이 연 층 ∩ 유닛이 선 층. **유닛 클래스를 보지 않는다.**</summary>
        public bool PlaceableAt(int2 cell, byte unitLayers)
            => InBounds(cell) && (PlaceLayersAt(cell) & unitLayers) != 0;

        /// <summary>그 칸이 여는 통행 층. 미저작이면 칸 종류에서 파생.</summary>
        public byte TravelLayersAt(int2 cell)
            => CellLayers.Length == Tiles.Length
                ? CellLayers[Index(cell)]
                : LayerBits.Derive(Tiles[Index(cell)]);

        public bool IsGoalCell(int2 cell)
        {
            for (int i = 0; i < Goals.Length; i++)
                if (Goals[i].Equals(cell)) return true;
            return false;
        }

        public int WaypointCountAt(int pathIndex)
            => pathIndex >= 0 && pathIndex < WaypointRanges.Length ? WaypointRanges[pathIndex].y : 0;

        public int2 WaypointAt(int pathIndex, int waypointIndex)
        {
            if (pathIndex < 0 || pathIndex >= WaypointRanges.Length) return GoalDestination;
            int2 range = WaypointRanges[pathIndex];
            if (waypointIndex < 0 || waypointIndex >= range.y) return GoalDestination;
            return WaypointCells[range.x + waypointIndex];
        }

        /// <summary>레인의 기본 경로. 미저작·범위 밖은 -1(골 직행) — 예외를 던지지 않는다.</summary>
        public int RouteForSpawn(int lane)
            => lane >= 0 && lane < SpawnRoutes.Length ? SpawnRoutes[lane] : -1;

        /// <summary>칸 → 판 좌표. 격자 원점 0 의 평면이라 y = 0 이다(제약: `BoardSpace` 는 뷰 소관).</summary>
        public float3 CellCenter(int2 cell) => GridMath.CellToWorldCenter(cell, TileSize);

        /// <summary>
        /// **예약 칸을 배치에서 닫는다.** 스폰 · 골 · 거점 footprint 세 종류이고, 옛
        /// `BattleBridge.CloseCellLayers` 의 후계다.
        ///
        /// 왜 파생이 아니라 규칙인가: 칸 종류는 셋 다 `Walk` 다(스폰·골은 정의상 걷는 칸이고,
        /// 거점은 그 위에 선다). 통행 파생이 `Walk → Path` 를 열기 때문에 Path 층 유닛에게는
        /// **적이 튀어나오는 칸과 유출 지점 위**가 배치 가능으로 보인다 — 어느 층 저작에도
        /// 없던 의미다. 그래서 저작을 읽은 **뒤** 마지막에 덮는다.
        ///
        /// ⚠ **되돌리지 않는다.** 본능이 무너져도 그 자리는 닫힌 채다(옛 전투와 같다) —
        /// 「건물이 서 있으니까」가 아니라 「그 자리는 이 판에서 배치판이 아니다」가 규칙이고,
        /// 되열면 잔해 위에 세우는 그림이 판 중간에 생긴다.
        /// 멱등이라 여러 번 불러도 같다.
        /// </summary>
        public void CloseReservedPlacement()
        {
            if (PlaceMask == null || PlaceMask.Length != CellCount) return;
            for (int i = 0; i < Spawns.Length; i++) Close(Spawns[i]);
            for (int i = 0; i < Goals.Length; i++) Close(Goals[i]);
            for (int i = 0; i < Structures.Length; i++)
            {
                int half = Structures[i].Footprint / 2;
                var c = Structures[i].Cell;
                for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                    Close(new int2(c.x + dx, c.y + dy));
            }
        }

        private void Close(int2 cell)
        {
            if (!InBounds(cell)) return;
            PlaceMask[Index(cell)] = LayerBits.None;
        }

        /// <summary>「목적지가 골 전체」를 뜻하는 센티널. 실제 칸이 아니다.</summary>
        public static int2 GoalDestination => new int2(-1, -1);
    }

    // 칸 종류. 옛 `Somnia.Battle.Data.MapTileType` 과 **값이 같다**(Walk 0 · Place 1 · Env 2 · Deco 3) —
    // 빌더가 캐스트 하나로 접을 수 있게 맞춘 것이고, 그 대응은 `MatchDefinitionBuilder` 가 진다.
    public enum MapTile : byte
    {
        Walk = 0,
        Place = 1,
        Env = 2,
        Deco = 3,
    }

    // 층 비트. 옛 `Somnia.Battle.Data.PlacementLayer` 와 값이 같다.
    //
    // 이름은 **공간** 기준이다(어떤 종류의 칸인가). 직업 기준이 아니다 — 코어는
    // 유닛 클래스를 한 번도 보지 않고 비트만 본다.
    public static class LayerBits
    {
        public const byte None = 0;
        public const byte Ground = 1 << 0;   // 배치지면
        public const byte Path = 1 << 1;   // 경로
        public const byte Air = 1 << 2;   // 비행
        public const byte All = 0xFF;      // 유닛 전용 표현 — 칸 마스크에는 쓰지 않는다

        /// <summary>칸이 가질 수 있는 정의된 비트. `All` 은 유닛 쪽 표현이라 칸에서 떨어진다.</summary>
        public const byte CellBits = Ground | Path | Air;

        /// <summary>안 막힌 칸이 «배치» 에 여는 층. 걷는 칸도 배치를 받는다 — 통행 파생과 다르다.</summary>
        public static byte OpenPlacement(MapTile tile)
            => tile == MapTile.Walk || tile == MapTile.Place ? CellBits : Air;

        /// <summary>칸 종류 → **통행** 층 비트. 파생의 단일 정의다.</summary>
        public static byte Derive(MapTile tile)
        {
            switch (tile)
            {
                case MapTile.Place: return Ground | Air;
                case MapTile.Walk: return Path | Air;
                default: return Air;   // Env·Deco 도 비행에는 열린 공간
            }
        }

        public static byte Sanitize(byte raw) => (byte)(raw & CellBits);

        /// <summary>공격이 이동체의 통행층을 타겟층으로 재사용하는 교집합 규칙. 0 = 무필터.</summary>
        public static bool CanTarget(byte attackTargetLayers, byte targetTraversalLayers)
            => attackTargetLayers == 0
               || targetTraversalLayers == 0
               || (attackTargetLayers & targetTraversalLayers) != 0;
    }

    // 거점의 «크기와 몸». 옛 `StructurePlacements.FootprintOf`/`BodyRadiusOf` 의 후계이고,
    // **한 함수가 둘을 다 정한다** — 종전엔 판정 bake 와 그림자가 같은 수를 다른 모양으로 적어
    // (한쪽 `Footprint × 0.5`, 다른 쪽 `Footprint` 를 지름으로) 형제로 보이지 않았다.
    //
    // ⚠ 크기는 **진영이 정한다**(E28 보류) — 마음 1×1 · 본능 3×3. SO 가 알 수 없는 구조라
    // 저작으로 못 바꾼다. 임의 footprint 일반화는 `battle-structures` backlog 소관이다.
    public static class StructureSize
    {
        public const int Core = 1;
        public const int Instinct = 3;

        /// <summary>몸 반경 = 점유의 **내접원**. 제약 13 의 «대상의 몸» 항이 이 값을 받는다.</summary>
        public static float BodyRadius(int footprint) => footprint * 0.5f;
    }

    // 거점의 런타임 투영. 셀 + 진영 비트 + 정의표 줄 — 마스크 파생·연결성·모드 판정은 앞의 둘만 본다.
    public struct StructureSpot
    {
        public int2 Cell;

        /// <summary>`Somnia.Battle.Battle.Units.Faction` 의 int 값. 거점 아닌 비트는 빌더에서 나올 수 없다.</summary>
        public int Faction;

        /// <summary>점유 한 변(마음 1 · 본능 3). 상수를 박으면 1×1 마음이 3×3 이라고 거짓말한다.</summary>
        public int Footprint;

        /// <summary>
        /// 정의표(`MatchDefinition.Structures`)의 인덱스. -1 = 스탯 미저작.
        ///
        /// **스탯을 여기 싣지 않는 이유**: 같은 `StructureData` 를 여러 자리에 찍는 것이
        /// 저작의 기본형이고(본능 3기 = 같은 SO 세 자리), 값을 자리마다 복제하면 「같은
        /// 건물인데 체력이 다른」 상태가 표현 가능해진다. 자리는 «어디에 무엇이 서 있나»
        /// 만 말하고 «그것이 얼마인가» 는 정의표가 든다 — 유닛·적과 같은 규율이다.
        ///
        /// ⚠ **방어 마음(골 타워)은 여기 없다.** 그쪽 정본은 `Goals` 이고 체력은
        /// `HeartDef.MaxHealth`(덱 저작) 하나다 — 두 벌이 되는 것을 저작 검증이 막고 있다.
        /// </summary>
        public int DefIndex;
    }
}
