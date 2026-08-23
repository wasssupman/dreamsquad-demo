using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using Wassup.Battle.Movement;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode
{
    // duel-route-tours unit 2 — 「5웨이브에만 뱀 투어」의 회귀선.
    //
    // ⚠ 이 배선은 **시드에 매인 결과지 코드가 보장하는 값이 아니다.** 컨셉은 블록(3웨이브)
    // 단위 룰렛이고 `variantSlots` 는 블록 가운데 웨이브에만 끼므로, 「웨이브 4~6 블록의
    // 주인이 벌떼」라는 시드 결과가 5웨이브를 만든다. 생성기에 웨이브 번호 특례를 넣는 것은
    // 땜빵이라 하지 않기로 했다(사용자 지시 2026-08-22).
    //
    // 그래서 그 취약성을 **여기서 빨갛게 만든다**: 시드·가중치·컨셉 풀이 바뀌어 뱀이 다른
    // 웨이브로 옮겨가면 이 단언이 알려준다. 옮겨간 것이 의도라면 상수를 고치고, 아니라면
    // 저작을 되돌린다.
    public class DuelSnakeTourWaveTests
    {
        private const string DeckPath = "Assets/_Project/Scripts/Data/Decks/Deck_Duel.asset";
        private const string MapPath = "Assets/_Project/Data/Maps/MapDocument_Duel.asset";

        // 투어는 lane1(상단 스폰) 위상이라 path 2 다. path 0 은 공중 예약(Skimmer).
        private const int SnakePathIndex = 2;
        private const int SnakeWaveNumber = 5;
        private const int InspectWaves = 15;
        // rev 3 — 세로 왕복 반복을 버리고 **가로대 + 세로획 하나**(ㅗ/ㅜ)로 갔다.
        // 왕복 8회는 길이는 나왔지만 화면에서 같은 진폭의 반복으로 읽혔다(사용자 지적).
        private const int SnakeWaypointCount = 5;
        // 가로대가 놓이는 행. lane0 = ㅗ(y5 대 · 위로 획) / lane1 = ㅜ(y4 대 · 아래로 획).
        // y4·y5 만 본능 3×3(x3~5 · x17~19 의 y1~3 · y6~8)을 안 건드리는 행이라 선택지가 없다.
        private const int BarRowUp = 5;
        private const int BarRowDown = 4;

        [Test]
        public void SnakeTour_AppearsOnlyOnWaveFive()
        {
            var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackDeck>(DeckPath);
            var doc = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDocument>(MapPath);
            Assert.IsNotNull(deck); Assert.IsNotNull(doc);

            var plan = WavePatternGenerator.Generate(deck, deck.ResolveWaveSeed(), 2);
            var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.Temp);
            try
            {
                var laneRoutes = new int[map.spawns.Length];
                for (int i = 0; i < laneRoutes.Length; i++) laneRoutes[i] = map.RouteForSpawn(i);

                int waves = Mathf.Min(InspectWaves, plan.waves.Count);
                for (int i = 0; i < waves; i++)
                {
                    int snakeCount = CountOnPath(
                        plan, i, laneRoutes, plan.intraWaveSpacingSec, SnakePathIndex);
                    if (i + 1 == SnakeWaveNumber)
                        Assert.Greater(snakeCount, 0,
                            $"{SnakeWaveNumber}웨이브에 뱀 편성이 없다 — 시드나 컨셉 가중치가 바뀌어 "
                            + "블록 배정이 옮겨갔을 수 있다");
                    else
                        Assert.AreEqual(0, snakeCount,
                            $"{i + 1}웨이브에도 뱀이 붙었다 — 「가끔만 다르게 온다」가 깨진다");
                }
            }
            finally { map.Dispose(); }
        }

        // unit 0 철회 계약: spawnRoutes 는 **레인의 성질**이라 켜는 순간 전 웨이브에 걸린다.
        // 뱀을 그 축에 다시 얹으면 판 내내 같은 길이 되어 이 spec 의 목표가 무너진다.
        [Test]
        public void DuelLaneRoutes_StayEmpty_SoTheTourIsWaveScoped()
        {
            var doc = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDocument>(MapPath);
            Assert.IsNotNull(doc);
            Assert.IsTrue(doc.SpawnRoutes == null || doc.SpawnRoutes.Count == 0,
                "Duel 의 spawnRoutes 가 채워졌다 — 경로가 전 웨이브에 걸린다(unit 0 철회 사유)");
        }

        [Test]
        public void SnakePathsAuthored_AndAirReservationIntact()
        {
            var doc = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDocument>(MapPath);
            Assert.IsNotNull(doc);
            var paths = doc.WaypointPaths;
            Assert.AreEqual(3, paths.Count, "path 0 공중 예약 + 투어 2개");

            Assert.AreEqual(1, paths[0].Cells.Count, "path 0 = 공중 예약 1점");
            Assert.AreEqual(new Vector2Int(11, 4), paths[0].Cells[0],
                "Enemy_Skimmer.waypointPathIndex = 0 이 이 강 셀을 탄다 — 되돌리지 말 것");

            for (int p = 1; p <= 2; p++)
            {
                Assert.AreEqual(SnakeWaypointCount, paths[p].Cells.Count,
                    $"path {p} 경유점 수 — 가로대 + 세로획 하나");
                // 도달 판정이 체비셰프 1 이라 인접한 두 지점은 뒤엣것이 자동 통과된다.
                for (int c = 1; c < paths[p].Cells.Count; c++)
                {
                    Vector2Int a = paths[p].Cells[c - 1], b = paths[p].Cells[c];
                    int cheb = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
                    Assert.GreaterOrEqual(cheb, 2,
                        $"path {p} 지점 {c} {b} 가 직전과 인접 — 자동 통과되어 저작이 무시된다");
                }
            }

            // rev 3 — 두 레인은 **서로 반대쪽으로 획을 뻗어야** 엇갈린다. 같은 쪽이면
            // 두 레인이 겹쳐 흘러 「두 갈래로 온다」가 화면에서 사라진다.
            // 획의 끝점(가장 멀리 간 y)으로 방향을 잰다 — 가로대 행 자체는 y4·y5 로 붙어 있어
            // 구분에 기여하지 않는다.
            Assert.Less(ExtremeY(paths[1].Cells, BarRowUp), 0,
                "path 1(ㅗ)의 세로획이 위로 뻗지 않는다");
            Assert.Greater(ExtremeY(paths[2].Cells, BarRowDown), 0,
                "path 2(ㅜ)의 세로획이 아래로 뻗지 않는다");
        }

        // 가로대에서 가장 멀리 벗어난 편차. 음수 = 위로(ㅗ), 양수 = 아래로(ㅜ).
        // 화면 좌표가 아니라 격자 y 라 «위»가 +y 지만, 여기서는 «가로대에서 어느 쪽으로
        // 뻗었나」만 물으므로 부호 규약을 뒤집어 ㅗ = 음수로 읽는다(글자 모양과 같은 방향).
        private static int ExtremeY(
            System.Collections.Generic.IReadOnlyList<Vector2Int> cells, int barRow)
        {
            int best = 0;
            foreach (var c in cells)
            {
                int d = barRow - c.y;   // 위로 갈수록 음수
                if (Mathf.Abs(d) > Mathf.Abs(best)) best = d;
            }
            return best;
        }

        // rev 2 (사용자 지적 2026-08-23) — **경유점이 아니라 실제로 밟는 셀**을 본다.
        //
        // 처음 저작한 투어는 경유점이 전부 거점 밖이었는데도 건물 위를 레인당 15칸 지났다.
        // 세로 왕복 열(x=4·18)이 하필 본능 3×3 의 중심 열이었기 때문이다. 거점은 통행을
        // **막지 않고 점유만 선언**하므로(instinct-content unit 1 — 막는 것은 BlockingHazard
        // 뿐) 흐름장이 태연히 건물을 관통한다.
        //
        // 두 번째로 놓친 것은 **적 마음(20,4)** 이다. 오프라인 검산에 본능 4기만 넣고 1×1
        // 마음을 빼먹었고 이 테스트가 잡았다 — 거점 종류를 열거하지 않고 `doc.Structures`
        // 전부를 `FootprintOf` 로 도는 형태라서 잡혔다. 열거식으로 되돌리지 말 것.
        //
        // 그래서 「경유점이 footprint 밖인가」로는 이 결함을 못 잡는다. 프로덕션과 같은
        // FlowFieldBuilder 로 구간마다 실제 셀을 재현해 관통 0 을 요구한다.
        [Test]
        public void SnakeTour_NeverWalksThroughStructures()
        {
            var doc = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDocument>(MapPath);
            Assert.IsNotNull(doc);

            var occupied = new System.Collections.Generic.HashSet<Vector2Int>();
            foreach (var s in doc.Structures)
            {
                if (s.data == null) continue;
                var faction = StructurePlacements.DeriveFaction(s.side, s.data.kind);
                int half = StructurePlacements.FootprintOf(faction) / 2;
                for (int dy = -half; dy <= half; dy++)
                    for (int dx = -half; dx <= half; dx++)
                        occupied.Add(new Vector2Int(s.cell.x + dx, s.cell.y + dy));
            }
            Assert.Greater(occupied.Count, 0, "거점이 하나도 없다 — 저작이 비었을 수 있다");

            var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.Temp);
            try
            {
                for (int lane = 0; lane < map.spawns.Length; lane++)
                {
                    int path = lane + 1;   // lane0 → path1, lane1 → path2
                    var cells = TraceTour(in map, map.spawns[lane], doc.WaypointPaths[path].Cells);
                    var pierced = new System.Collections.Generic.List<Vector2Int>();
                    foreach (var c in cells)
                        if (occupied.Contains(c) && !pierced.Contains(c)) pierced.Add(c);

                    Assert.IsEmpty(pierced,
                        $"path {path}: 적이 거점 위를 지난다 {string.Join(" ", pierced)} — "
                        + "거점은 통행을 막지 않으므로 **저작이 피해야 한다**. "
                        + "세로 왕복 열이 3×3 footprint 열(x3~5 · x17~19)과 겹치지 않는지 볼 것");
                }
            }
            finally { map.Dispose(); }
        }

        // 스폰 → 경유점들 → 골 을 구간마다 흐름장으로 이어 실제 밟는 셀을 만든다.
        // 프로덕션과 같은 빌더라 「저작이 무엇을 만드나」의 정본 판정이다.
        private static System.Collections.Generic.List<Vector2Int> TraceTour(
            in GeneratedMap map, Unity.Mathematics.int2 spawn,
            System.Collections.Generic.IReadOnlyList<Vector2Int> waypoints)
        {
            Assert.Greater(waypoints.Count, 0, "경로가 비었다");
            var stops = new System.Collections.Generic.List<Vector2Int>(waypoints);
            stops.Add(new Vector2Int(map.goal.x, map.goal.y));

            var cells = new System.Collections.Generic.List<Vector2Int>
                { new Vector2Int(spawn.x, spawn.y) };
            var cur = cells[0];
            foreach (var stop in stops) cur = Descend(in map, cur, stop, cells);
            return cells;
        }

        private static Vector2Int Descend(
            in GeneratedMap map, Vector2Int from, Vector2Int to,
            System.Collections.Generic.List<Vector2Int> sink)
        {
            int w = map.gridSize.x, h = map.gridSize.y, n = w * h;
            var walk = new NativeArray<byte>(n, Allocator.Temp);
            var flow = new NativeArray<Unity.Mathematics.float2>(n, Allocator.Temp);
            var dist = new NativeArray<int>(n, Allocator.Temp);
            try
            {
                for (int i = 0; i < n; i++)
                    walk[i] = map.tiles[i] == MapTileType.Walk ? (byte)1 : (byte)0;
                Wassup.Battle.Effects.FlowFieldBuilder.Build(
                    walk, map.gridSize, new Unity.Mathematics.int2(to.x, to.y), flow, dist);

                var c = from;
                for (int step = 0; step < n && c != to; step++)
                {
                    var f = flow[c.y * w + c.x];
                    int dx = f.x > 0.01f ? 1 : (f.x < -0.01f ? -1 : 0);
                    int dy = f.y > 0.01f ? 1 : (f.y < -0.01f ? -1 : 0);
                    if (dx == 0 && dy == 0) break;
                    c = new Vector2Int(c.x + dx, c.y + dy);
                    sink.Add(c);
                }
                return c;
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        // 실제 스폰이 붙일 경로를 스폰과 **같은 함수**로 센다(예고도 이 함수를 쓴다).
        private static int CountOnPath(
            GeneratedWavePlan plan, int waveIndex, int[] laneRoutes, float spacing, int pathIndex)
        {
            var wave = plan.waves[waveIndex];
            var expanded = WavePatternGenerator.ExpandWave(
                wave, wave.triggerTimeSec, laneRoutes.Length, spacing);
            int count = 0;
            for (int k = 0; k < expanded.Count; k++)
            {
                var unit = expanded[k].entry.unitType;
                if (unit == null) continue;
                int lane = expanded[k].laneIndex;
                int laneDefault = lane >= 0 && lane < laneRoutes.Length ? laneRoutes[lane] : -1;
                if (WaypointRouting.ResolvePathIndex(
                        unit.waypointPathIndex, expanded[k].pathIndex, laneDefault) == pathIndex)
                    count++;
            }
            return count;
        }
    }
}
