using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    // waypoint-routing unit 10 — 라이브 맵의 레인 경로 저작 pin.
    // (test-suite-fast-lane unit 0 에서 WaypointPathAuthoringTests.cs 로부터 파일 분리 —
    // 검증 규칙 로직 테스트(합성 경로)는 코어 lane 에 남는다.)
    //
    // 이 pin 이 지키는 것은 «저작이 존재한다» 가 아니라 **저작이 실제로 작동하는 모양**이다:
    // 레인 하나는 대조군으로 남아야 하고(둘 다 우회하면 «다른 길»이 안 읽힌다), 저작 경로는
    // 검증을 통과해야 하며, Skimmer 가 쓰는 경로 0 을 지상 레인이 빼앗으면 안 된다.
    public class LiveMapSpawnRouteAuthoringTests
    {
        // 레인 경로를 실제로 저작한 맵. 저작 기준은 unit 10 rev 3 의 셋이다 —
        // 유턴 0(골에서 멀어졌다 되돌아오지 않는다) · 새 경로 40% 이상 · 진입면 상이.
        // Serpent·Spiral·Twin 은 후보 최선이 10~26% 라 «보이지 않으면 저작하지 않는다» 로 뺐다.
        private static readonly string[] RoutedMaps =
        {
            "Assets/_Project/Data/Maps/MapDocument_Coil.asset",
            "Assets/_Project/Data/Maps/MapDocument_Zig.asset",
        };

        private static MapDocument Load(string path)
        {
            var doc = UnityEditor.AssetDatabase.LoadAssetAtPath<MapDocument>(path);
            Assert.IsNotNull(doc, $"맵 문서를 찾지 못했다: {path}");
            return doc;
        }

        // 라이브 6맵 전부 — 저작했든 안 했든 검증을 통과해야 한다.
        [TestCase("Assets/_Project/Data/Maps/MapDocument_Serpent.asset")]
        [TestCase("Assets/_Project/Data/Maps/MapDocument_Coil.asset")]
        [TestCase("Assets/_Project/Data/Maps/MapDocument_Twin.asset")]
        [TestCase("Assets/_Project/Data/Maps/MapDocument_Spiral.asset")]
        [TestCase("Assets/_Project/Data/Maps/MapDocument_Zig.asset")]
        [TestCase("Assets/_Project/Data/Maps/MapDocument_Hook.asset")]
        public void LiveMap_SpawnRoutes_PassAuthoringValidation(string path)
        {
            var doc = Load(path);
            var errors = new List<string>();
            var warnings = new List<string>();
            WaypointAuthoringRules.ValidateSpawnRoutes(
                doc.SpawnRoutes, doc.WaypointPaths, doc.Spawns, errors, warnings);

            Assert.IsEmpty(errors, $"{path}: 레인 경로 저작 에러 — {string.Join(" / ", errors)}");
            // 가로지르기는 «레인 1 적이 맵을 건너 레인 0 복도로 간다» 는 저작 사고다.
            foreach (string w in warnings)
                Assert.IsFalse(w.Contains("가로지르기"), $"{path}: {w}");
        }

        // 대조군이 없으면 「이 맵은 원래 이렇게 온다」가 화면에서 안 읽힌다.
        [Test]
        public void RoutedMaps_RouteOneLane_AndKeepTheOtherAsShortestPath(
            [ValueSource(nameof(RoutedMaps))] string path)
        {
            var routes = Load(path).SpawnRoutes;
            Assert.IsNotNull(routes, $"{path}: 저작 대상인데 spawnRoutes 가 없다");

            int routed = 0, shortest = 0;
            foreach (int r in routes) { if (r >= 0) routed++; else shortest++; }
            Assert.GreaterOrEqual(routed, 1, $"{path}: 한 레인도 우회하지 않으면 이 spec 이 하는 일이 없다");
            Assert.GreaterOrEqual(shortest, 1,
                $"{path}: 전 레인이 우회하면 대조가 사라져 «다른 길»이 아니라 «그냥 먼 길»이 된다");
        }

        // 저작이 검증을 통과한다는 것과 그 값이 **런타임에 도달한다**는 것은 다른 주장이다.
        // 실제 에셋으로 투영까지 돌려 문서→GeneratedMap 경로를 끝까지 확인한다(계약 6 —
        // 순수 함수 그린은 증거가 아니다).
        [Test]
        public void AuthoredLaneRoute_SurvivesProjectionToRuntime(
            [ValueSource(nameof(RoutedMaps))] string path)
        {
            var doc = Load(path);
            var map = MapDocumentBuilder.ToGeneratedMap(doc, Allocator.Temp);
            try
            {
                Assert.AreEqual(doc.Spawns.Count, map.spawns.Length);
                Assert.IsTrue(map.spawnRoutes.IsCreated,
                    "저작했는데 투영이 비었다 — 레인 기본 경로가 런타임에 도달하지 못한다");
                Assert.AreEqual(map.spawns.Length, map.spawnRoutes.Length,
                    "spawnRoutes 는 spawns 와 같은 길이여야 한다(RouteForSpawn 의 전제)");

                for (int lane = 0; lane < map.spawns.Length; lane++)
                {
                    int route = map.RouteForSpawn(lane);
                    Assert.AreEqual(doc.SpawnRoutes[lane], route, $"레인 {lane} 투영 불일치");
                    if (route >= 0)
                    {
                        Assert.Less(route, map.WaypointPathCount,
                            $"레인 {lane} 이 없는 경로를 가리킨다");
                        Assert.Greater(map.waypointRanges[route].y, 0,
                            $"레인 {lane} 이 가리키는 경로 {route} 에 지점이 하나도 없다");
                    }
                }
            }
            finally { map.Dispose(); }
        }

        // 경로 0 은 Skimmer 의 SO 지정(Air)이 쓴다. 지상 레인 기본이 그걸 가리키면 지상 적이
        // 공중 전용으로 저작된 경로를 타게 되어 두 콘텐츠가 한 저작을 공유한다.
        [Test]
        public void GroundLaneRoute_DoesNotStealTheAirPath(
            [ValueSource(nameof(RoutedMaps))] string path)
        {
            var doc = Load(path);
            var skimmer = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackUnitData>(
                "Assets/_Project/Data/Enemies/Enemy_Skimmer.asset");
            Assert.IsNotNull(skimmer);
            Assert.GreaterOrEqual(skimmer.waypointPathIndex, 0, "Skimmer 는 경로를 저작으로 지정한다");

            foreach (int r in doc.SpawnRoutes)
                Assert.AreNotEqual(skimmer.waypointPathIndex, r,
                    $"{path}: 지상 레인 기본이 Skimmer 의 Air 경로를 가리킨다 — 경로를 하나 더 만들 것");
        }
    }
}
