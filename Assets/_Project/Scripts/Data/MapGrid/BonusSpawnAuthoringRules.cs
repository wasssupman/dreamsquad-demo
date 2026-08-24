using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data.MapGrid
{
    // bonus-wave-pull unit 1 — 보너스 포탈 칸 저작 규칙의 **단일 소유자**.
    // `MapDocument.OnValidate` 와 `MapPainterWindow` 가 둘 다 이 함수를 부른다. 규칙을
    // 복제하면 「툴은 통과했는데 런타임이 폴백」 이 생긴다(waypoint-routing unit 5 선례).
    //
    // 규칙은 **금지 목록이 아니라 양성 조건**이다. 포탈은 보드 한가운데 열리므로 「벽이
    // 아니다」만으로는 부족하다 — 격리된 칸에 저작하면 그 적들이 골에 영영 못 가고
    // 필드에 남는다(방어유닛 0기일 때의 폴백이 goal flow 다 — boss-defender-field 계약 5).
    public static class BonusSpawnAuthoringRules
    {
        // 포탈 개수 계약(README 계약 2). 0 = 미저작(보너스 당기기 없는 맵)도 유효하다.
        public const int RequiredPortalCount = 2;

        public static void Validate(
            IReadOnlyList<Vector2Int> cells,
            int width, int height,
            IReadOnlyList<MapTileType> tiles,
            IReadOnlyList<Vector2Int> goals,
            List<string> errors)
        {
            if (errors == null) return;
            int count = cells?.Count ?? 0;
            if (count == 0) return;   // 미저작 = 보너스 당기기 없는 맵. 정상.

            if (count != RequiredPortalCount)
                errors.Add($"bonusSpawns 는 0개(미저작) 또는 {RequiredPortalCount}개여야 한다 — 현재 {count}개.");

            // ⓒ 서로 다른 칸. 같은 칸이면 10기가 한 점에 태어난다.
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    if (cells[i] == cells[j])
                        errors.Add($"bonusSpawns 에 같은 칸 {cells[i]} 이 두 번 있다.");

            for (int i = 0; i < count; i++)
            {
                var c = cells[i];
                if (c.x < 0 || c.x >= width || c.y < 0 || c.y >= height)
                {
                    errors.Add($"bonusSpawn {c} 이 격자 밖 ({width}×{height}).");
                    continue;
                }

                // ⓐ 걸을 수 있는 칸(Walk). Place·Env·Deco 는 전부 벽이라 스폰 즉시
                // FlowRecovery 로 한 프레임을 버리고, 최악엔 그 칸에서 못 빠져나온다.
                if (!IsWalkable(tiles, width, height, c))
                {
                    errors.Add($"bonusSpawn {c} 이 걸을 수 없는 칸이다 — 포탈은 통행 가능한 칸에만.");
                    continue;
                }

                // ⓑ 골까지 도달 가능. 이걸 빼면 격리 칸의 보너스 적이 영영 안 죽는다.
                if (!CanReachAnyGoal(tiles, width, height, c, goals))
                    errors.Add($"bonusSpawn {c} 에서 골에 도달할 수 없다 — 격리된 칸.");
            }
        }

        // ★**Walk 만 통행 가능하다.** `!= Place` 로 쓰면 안 된다 — Env·Deco 도 벽이다
        // (`SimFieldInstaller`: `walk[i] = tiles[i] == Walk ? 1 : 0`). Duel 의 중앙 열에는
        // Env 기둥이 늘어서 있어서, 느슨한 규칙이면 정확히 그 칸들이 검증을 통과한다.
        private static bool IsWalkable(
            IReadOnlyList<MapTileType> tiles, int width, int height, Vector2Int c)
        {
            if (tiles == null || tiles.Count != width * height) return true; // 타일 미저작 문서는 판정 보류
            return tiles[c.y * width + c.x] == MapTileType.Walk;
        }

        // 4-이웃 BFS. 격자가 작아(23×10 급) 저작 시점 1회 비용은 무시 가능하다.
        private static bool CanReachAnyGoal(
            IReadOnlyList<MapTileType> tiles, int width, int height,
            Vector2Int start, IReadOnlyList<Vector2Int> goals)
        {
            if (goals == null || goals.Count == 0) return true;   // goals 에러는 다른 곳이 낸다
            if (tiles == null || tiles.Count != width * height) return true;

            var goalSet = new HashSet<Vector2Int>(goals);
            var seen = new bool[width * height];
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            seen[start.y * width + start.x] = true;

            // 골 칸 자체는 통행이 열려야 하므로 벽 판정에서 예외로 두지 않는다 — 맵 저작이
            // 골을 Place 로 찍었다면 그건 다른 검증이 잡을 문제다.
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (goalSet.Contains(cur)) return true;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cur.x + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int ny = cur.y + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int idx = ny * width + nx;
                    if (seen[idx]) continue;
                    var n = new Vector2Int(nx, ny);
                    if (!goalSet.Contains(n) && tiles[idx] != MapTileType.Walk) continue;
                    seen[idx] = true;
                    queue.Enqueue(n);
                }
            }
            return false;
        }
    }
}
