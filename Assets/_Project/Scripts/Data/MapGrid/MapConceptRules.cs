using System.Collections.Generic;

namespace Wassup.Data.MapGrid
{
    // map-rework unit 0 → **unit 7 에서 폭 규칙이 뒤집혔다**. 저작 가드, 순수 static.
    //
    // 옛 계약(unit 0): 복도 최소 폭 2 — 폭1 Walk 금지.
    // 새 계약(unit 7): **직선 구간은 폭 1**, 폭 2 는 합류부·광장 진입부처럼 넓어질 이유가
    // 있는 곳만. 근거는 실측이다 — 개편 5맵이 전부 폭1 0칸이 되자 **근접 유닛이 판에서
    // 사라졌다**(사거리 1은 직교 인접까지만 닿아 폭2 복도의 먼 차선이 항상 자유다).
    // 은퇴시킨 것은 「폭1」이 아니라 「**단방향**」이었다 — 갈래·광장은 계속 요구한다.
    //
    // **경고 전용이다** — 에러로 만들면 비개편 맵(Hook·Test·MovementLab)의 bake 가 막힌다.
    // 배선은 페인터 경고 박스만: OnValidate 에 넣으면 임포트마다 콘솔이 울린다.
    public static class MapConceptRules
    {
        // 목표치(unit 7). Walk 셀 기준 비율.
        public const float MinChokeRatio = 0.40f;   // 근접이 **완전히** 막을 수 있는 칸
        public const float MaxWidth2Ratio = 0.25f;  // 폭 2 는 제한적으로

        // 국소 통로 폭 = 이 칸을 지나는 **가로 런과 세로 런의 min**. Walk 가 아니면 0.
        //
        // 왜 「양옆이 모두 막혔나」(옛 판정)가 아니라 런 길이인가: 옛 판정은 **불리언**이라
        // 「폭1 인가 아닌가」만 답한다. 새 계약은 폭2 비율에도 상한을 두므로 **수치**가 필요하다.
        // (둘은 폭1 판정 자체로는 대체로 일치한다 — 교차 칸을 옛 판정이 놓친다고 처음 적었는데
        //  실측하니 사실이 아니었다. 십자 교차 중심은 런 기준으로도 폭 3 이다.)
        //
        // 이 함수가 오늘 개편 5맵의 «폭1 0칸»을 드러낸 그 계산이고, 가드·테스트·저작 검산이
        // **이것 하나**를 본다 — 계산이 두 벌이면 수치가 갈린다.
        public static int LocalWidth(
            IReadOnlyList<MapTileType> tiles, int width, int height, int x, int y)
        {
            if (!IsWalk(tiles, width, height, x, y)) return 0;

            int hx = 1;
            for (int i = x - 1; IsWalk(tiles, width, height, i, y); i--) hx++;
            for (int i = x + 1; IsWalk(tiles, width, height, i, y); i++) hx++;

            int vy = 1;
            for (int j = y - 1; IsWalk(tiles, width, height, x, j); j--) vy++;
            for (int j = y + 1; IsWalk(tiles, width, height, x, j); j++) vy++;

            return hx < vy ? hx : vy;
        }

        // 근접이 **완전히** 막는 칸 = 폭 1 이면서 지상 배치칸에 **직교** 인접.
        // 대각은 세지 않는다 — 사거리 1의 월드 거리는 직교 1.0, 대각 1.41 이라 안 닿는다.
        //
        // placeMask 가 null 이면 타일에서 파생한다(`PlacementLayers.Derive` 와 같은 규칙:
        // Place 타일이 Ground 를 연다). 저작 마스크가 있으면 그쪽이 정본이다.
        public static void MeasureMeleeLanes(
            IReadOnlyList<MapTileType> tiles, IReadOnlyList<byte> placeMask,
            int width, int height,
            out int walkCells, out int chokeCells, out int width2Cells)
        {
            walkCells = 0; chokeCells = 0; width2Cells = 0;
            if (tiles == null || tiles.Count != width * height) return;

            bool Ground(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height) return false;
                int i = y * width + x;
                byte m = placeMask != null && placeMask.Count == width * height
                    ? placeMask[i]
                    : PlacementLayers.Derive(tiles[i]);
                return (m & (byte)PlacementLayer.Ground) != 0;
            }

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int w = LocalWidth(tiles, width, height, x, y);
                    if (w == 0) continue;
                    walkCells++;
                    if (w == 2) width2Cells++;
                    if (w != 1) continue;
                    if (Ground(x - 1, y) || Ground(x + 1, y) || Ground(x, y - 1) || Ground(x, y + 1))
                        chokeCells++;
                }
        }

        // unit 7 — 「근접이 설 자리가 있는가」. 옛 `ValidateCorridorWidth`(폭1 경고)를 대체한다.
        public static void ValidateMeleeLanes(
            IReadOnlyList<MapTileType> tiles, IReadOnlyList<byte> placeMask,
            int width, int height, List<string> warnings)
        {
            MeasureMeleeLanes(tiles, placeMask, width, height,
                out int walk, out int choke, out int width2);
            if (walk == 0) return;

            float chokeRatio = (float)choke / walk;
            float width2Ratio = (float)width2 / walk;

            if (chokeRatio < MinChokeRatio)
                warnings.Add(
                    $"근접 완전차단칸 {choke}/{walk} ({chokeRatio:P0}) — 목표 {MinChokeRatio:P0} 미달. "
                    + "직선 구간을 폭1 로 좁혀야 근접이 판에 선다(폭2 복도는 먼 차선이 항상 자유다)");

            if (width2Ratio > MaxWidth2Ratio)
                warnings.Add(
                    $"폭2 Walk {width2}/{walk} ({width2Ratio:P0}) — 상한 {MaxWidth2Ratio:P0} 초과. "
                    + "폭2 는 합류부·광장 진입부처럼 넓어질 이유가 있는 곳만");
        }

        // 광장 판정: 4×4 전부 Walk 블록이 1개 이상 존재하는가.
        // unit 7 이후에도 유지된다 — 광장은 «직선 구간»이 아니고, 포위·공성·사거리 차이가
        // 사는 자리다. 좁은 목(근접)과 광장(원거리·기동)이 한 맵에 같이 있어야 한다.
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

        private static bool IsWalk(
            IReadOnlyList<MapTileType> tiles, int width, int height, int x, int y)
            => x >= 0 && x < width && y >= 0 && y < height
               && tiles[y * width + x] == MapTileType.Walk;
    }
}
