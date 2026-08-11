using System.Collections.Generic;

namespace Wassup.Data.MapGrid
{
    // map-rework unit 0 — 개편 컨셉(복도 폭≥2 · 광장 1+)의 저작 가드. 순수 static.
    //
    // **경고 전용이다** — 에러로 만들면 비개편 맵(Hook·Test·MovementLab)의 bake 가 막힌다
    // (계약 1). 배선은 페인터 경고 박스만: OnValidate 에 넣으면 임포트마다 콘솔이 울려
    // 레거시 맵 5장이 상시 소음이 된다.
    public static class MapConceptRules
    {
        // 폭1 판정: Walk 셀에서 **한 축의 양옆이 모두 비-Walk** 이면 그 축으로 폭1이다.
        //   가로 폭1 복도 셀 → 위·아래 모두 막힘 → 걸린다
        //   폭2 복도 셀     → 복도 방향 + 폭 방향 이웃이 있어 안 걸린다
        //   L자 코너 셀     → 두 축 모두 한쪽은 Walk 라 안 걸린다
        // 경고는 요약 1줄(개수 + 좌표 최대 5개) — 셀당 1줄이면 레거시 맵에서 박스가 무너진다.
        public static void ValidateCorridorWidth(
            IReadOnlyList<MapTileType> tiles, int width, int height, List<string> warnings)
        {
            if (tiles == null || tiles.Count != width * height) return;

            bool Walk(int x, int y)
                => x >= 0 && x < width && y >= 0 && y < height
                   && tiles[y * width + x] == MapTileType.Walk;

            int count = 0;
            var samples = new List<string>(5);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (!Walk(x, y)) continue;
                    bool narrowVertical = !Walk(x, y - 1) && !Walk(x, y + 1);   // 가로 복도의 폭1
                    bool narrowHorizontal = !Walk(x - 1, y) && !Walk(x + 1, y); // 세로 복도의 폭1
                    if (!narrowVertical && !narrowHorizontal) continue;
                    count++;
                    if (samples.Count < 5) samples.Add($"({x},{y})");
                }

            if (count > 0)
                warnings.Add($"폭1 Walk {count}칸 — 개편 컨셉(폭≥2) 미달: {string.Join(" ", samples)}{(count > 5 ? " …" : "")}");
        }

        // 광장 판정: 4×4 전부 Walk 블록이 1개 이상 존재하는가.
        public static bool HasPlaza(
            IReadOnlyList<MapTileType> tiles, int width, int height, int plazaSize = 4)
        {
            if (tiles == null || tiles.Count != width * height) return false;
            for (int y = 0; y + plazaSize <= height; y++)
                for (int x = 0; x + plazaSize <= width; x++)
                {
                    bool all = true;
                    for (int dy = 0; dy < plazaSize && all; dy++)
                        for (int dx = 0; dx < plazaSize && all; dx++)
                            if (tiles[(y + dy) * width + (x + dx)] != MapTileType.Walk) all = false;
                    if (all) return true;
                }
            return false;
        }

        public static void ValidatePlaza(
            IReadOnlyList<MapTileType> tiles, int width, int height, List<string> warnings)
        {
            if (tiles == null || tiles.Count != width * height) return;
            if (!HasPlaza(tiles, width, height))
                warnings.Add("4×4 광장 없음 — 개편 컨셉은 마음이 설 광장 1개 이상을 요구한다");
        }
    }
}
