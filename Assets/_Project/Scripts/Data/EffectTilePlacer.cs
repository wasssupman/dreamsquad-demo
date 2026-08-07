using System.Collections.Generic;
using Unity.Mathematics;

namespace Wassup.Data
{
    // effect-tiles unit 0 — 맵 seed 결정론으로 효과 타일을 놓을 배치 가능 셀을 선정하는 순수 함수.
    // BackgroundPropPlacer 미러(static, Wassup.Data). 같은 맵/seed = 양측 동일 (비동기 토너먼트).
    // placement-mask unit 1 — 효과 타일은 "그 칸에 유닛을 놓으면" 발동하는 배치 결합 시스템이라
    // 배치 정본(placeMask)을 따른다. 파생 마스크(≡ tiles==Place)에서는 결과 불변.
    public static class EffectTilePlacer
    {
        // prop 배치(BackgroundPropPlacer 도 map seed 사용)와 decorrelate 하는 XOR 상수.
        private const int SeedSalt = 0x51F15EED;

        // 배치 가능 셀만, 중복 없음, count 상한(가용 셀보다 크면 전부). 수집은 row-major 라 결정론.
        public static List<int2> SelectCells(in GeneratedMap map, int seed, int count)
        {
            var result = new List<int2>();
            if (!map.IsCreated || count <= 0) return result;

            var placeCells = new List<int2>();
            for (int y = 0; y < map.gridSize.y; y++)
            for (int x = 0; x < map.gridSize.x; x++)
            {
                var cell = new int2(x, y);
                // unit 4 — 효과 타일은 Ground(배치지면) 층 고정. 경로 위로 번지지 않는다.
                if (map.PlaceableAt(cell, PlacementLayer.Ground))
                    placeCells.Add(cell);
            }
            if (placeCells.Count == 0) return result;

            // 0-seed panic 가드 = |1u (TilemapMapView ring props 와 동일 관용).
            var rng = Random.CreateFromIndex((uint)(seed ^ SeedSalt) | 1u);
            int take = math.min(count, placeCells.Count);
            // partial Fisher-Yates — 앞 take 개만 확정.
            for (int i = 0; i < take; i++)
            {
                int j = rng.NextInt(i, placeCells.Count);
                (placeCells[i], placeCells[j]) = (placeCells[j], placeCells[i]);
                result.Add(placeCells[i]);
            }
            return result;
        }
    }
}
