using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // instinct-content unit 3 rev 3 — **예고선이 실제 이동선과 같은가.**
    //
    // 사용자 실측 증상(2026-08-12): 「웨이브 안내 가이드와 몬스터 이동 경로가 다르게 노출된다」.
    // 원인은 예고선이 스폰 → 웨이포인트 → **마음** 만 그리고, unit 3 이 넣은 「스폰 시 거점을
    // 목적지로 고른다」를 몰랐던 것.
    //
    // 이 테스트가 재는 것은 **증상 그 자체**다: 적이 실제로 밟은 셀들이 예고선 근처에 있는가.
    // 「내가 고친 함수가 옳은 값을 낸다」가 아니다 — 그건 EditMode 가 이미 말하고, 그것만으로는
    // 이 증상이 사라졌다고 말할 수 없다(CLAUDE.md 버그 수정 절차).
    public class SpawnGuideMatchesWalkTest
    {
        private const int DuelMapIndex = 9;
        private const float MeasureSec = 25f;
        private const float NearGuideTiles = 1.6f;   // 분리·평활화로 선에서 이만큼은 벌어진다

        private int _savedIndex = -1;

        [SetUp]
        public void SetUp() => _savedIndex = DevMapOverride.Index;

        [TearDown]
        public void TearDown()
        {
            DevMapOverride.Index = _savedIndex;
            LogAssert.ignoreFailingMessages = false;
        }

        private static object GetField(object target, string name)
        {
            var fi = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"Field '{name}' not found");
            return fi.GetValue(target);
        }

        private static float DistanceToPolyline(float2 p, List<Vector3> path)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                float2 a = new float2(path[i].x, path[i].z);
                float2 b = new float2(path[i + 1].x, path[i + 1].z);
                float2 ab = b - a;
                float len2 = math.lengthsq(ab);
                float2 q = len2 < 1e-8f ? a : a + ab * math.saturate(math.dot(p - a, ab) / len2);
                best = math.min(best, math.distance(p, q));
            }
            return best;
        }

        [UnityTest]
        public IEnumerator Duel_EnemiesWalkAlongTheAdvertisedGuideLine()
        {
            LogAssert.ignoreFailingMessages = true;
            DevMapOverride.Index = DuelMapIndex;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            bridge.BeginPlacement();
            yield return null;
            bridge.StartBattle();
            yield return null;

            var em = (EntityManager)GetField(bridge, "_em");
            var field = em.CreateEntityQuery(
                    ComponentType.ReadOnly<Wassup.Battle.Effects.FlowFieldSingleton>())
                .GetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>();

            // ── 화면에 광고되는 그 선 ──
            var guide = new List<Vector3>();
            Assert.IsTrue(bridge.TryGetSpawnPathSim(0, 0, Wassup.Battle.Effects.TraversalSlots.DefaultMask, guide),
                "예고선을 못 만든다");
            Assert.GreaterOrEqual(guide.Count, 2, "예고선이 2점 미만이다");

            // 선이 거점을 실제로 경유하는지 — 이게 rev 3 의 핵심. 마음으로 직행하면 실패한다.
            var instinctWorld = Wassup.Battle.Movement.GridMath.CellToWorldCenter(
                new int2(4, 3), field.tileSize, 0f, origin: field.origin);
            float guideToInstinct = DistanceToPolyline(
                new float2(instinctWorld.x, instinctWorld.z), guide);
            Debug.Log($"[rev3] 예고선 점 {guide.Count} · 선↔북본능(4,3) 최단 {guideToInstinct:F2} 타일");

            // ── 적이 실제로 밟은 자리 ──
            int sampled = 0, offGuide = 0;
            float worstOff = 0f;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < MeasureSec)
            {
                using (var q = em.CreateEntityQuery(
                    ComponentType.ReadOnly<FactionTag>(),
                    ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()))
                {
                    var tags = q.ToComponentDataArray<FactionTag>(Allocator.Temp);
                    var xf = q.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.Temp);
                    for (int k = 0; k < tags.Length; k++)
                    {
                        if (tags[k].value != Faction.EnemyUnit) continue;
                        float d = DistanceToPolyline(
                            new float2(xf[k].Position.x, xf[k].Position.z), guide) / field.tileSize;
                        sampled++;
                        // 최대 이탈은 **항상** 갱신한다. 위반일 때만 재면 통과 시 0 이 찍혀
                        // 「완벽히 선 위를 걷는다」로 오독된다(실제로는 「위반 없음」일 뿐).
                        worstOff = math.max(worstOff, d);
                        if (d > NearGuideTiles) offGuide++;
                    }
                    tags.Dispose(); xf.Dispose();
                }
                yield return null;
            }

            float offRatio = sampled > 0 ? (float)offGuide / sampled : 1f;
            Debug.Log($"[rev3] 표본 {sampled} · 선에서 {NearGuideTiles} 타일 초과 이탈 {offGuide} "
                      + $"({offRatio:P1}) · 최대 이탈 {worstOff:F2} 타일");

            Assert.Greater(sampled, 100, "적 표본이 없다 — 측정이 공허하다");
            Assert.Less(offRatio, 0.35f,
                "적 다수가 예고선을 벗어나 걷는다 — 가이드와 이동선이 갈렸다(rev 3 회귀)");
        }

        // waypoint-flight-enemy unit 11 — **레인 기본 경로 맵에서도** 예고선이 실제 이동선인가.
        //
        // 사용자 실측 증상(2026-08-15): 「웨이포인트 레인인데 경로 가이드가 최단거리로 그려진다」.
        // 원인은 BuildSpawnGuideForecasts 가 unit.waypointPathIndex(SO 저작, 지상 전원 -1)만 싣고
        // 맵의 레인 기본(GeneratedMap.RouteForSpawn)을 해석하지 않은 것 — 스폰(SpawnUnit)은 두 축을
        // WaypointRouting.ResolvePathIndex 로 합치는데 예보만 한 축이었다.
        //
        // Coil(풀 1)은 spawnRoutes = [1, -1] — 레인 0 지상이 경로 1(웨이포인트 (8,9))을 기본으로 탄다.
        [UnityTest]
        public IEnumerator Coil_RoutedLaneGuide_AdvertisesTheLaneDefaultRoute()
        {
            const int CoilMapIndex = 1;
            LogAssert.ignoreFailingMessages = true;
            DevMapOverride.Index = CoilMapIndex;
            RenderTexture.active = null;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            bridge.BeginPlacement();
            yield return null;
            bridge.StartBattle();
            yield return null;

            // ── seam: 예보가 레인 기본 경로를 싣는가 ──
            Assert.IsTrue(bridge.TryGetSpawnGuideForecast(out _, out var forecasts),
                "웨이브 1 예보가 없다");
            int lane0Ground = -1;
            for (int i = 0; i < forecasts.Length; i++)
                if (forecasts[i].laneIndex == 0
                    && (forecasts[i].traversalLayers & (byte)Wassup.Data.PlacementLayer.Air) == 0)
                { lane0Ground = i; break; }
            Assert.GreaterOrEqual(lane0Ground, 0, "레인 0 지상 예보가 없다 — 계측 무효");
            Assert.AreEqual(1, forecasts[lane0Ground].waypointPathIndex,
                "레인 0 지상 예보가 맵 레인 기본 경로(1)를 싣지 않았다 — 가이드가 최단거리를 그린다");

            // ── 그 예보로 그린 선이 웨이포인트를 실제로 경유하는가 ──
            var em = (EntityManager)GetField(bridge, "_em");
            var field = em.CreateEntityQuery(
                    ComponentType.ReadOnly<Wassup.Battle.Effects.FlowFieldSingleton>())
                .GetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>();
            var guide = new List<Vector3>();
            Assert.IsTrue(bridge.TryGetSpawnPathSim(
                    forecasts[lane0Ground].laneIndex,
                    forecasts[lane0Ground].waypointPathIndex,
                    forecasts[lane0Ground].traversalLayers, guide),
                "레인 0 예고선을 못 만든다");
            var wpWorld = Wassup.Battle.Movement.GridMath.CellToWorldCenter(
                new int2(8, 9), field.tileSize, 0f, origin: field.origin);
            float wpDist = DistanceToPolyline(new float2(wpWorld.x, wpWorld.z), guide) / field.tileSize;
            Debug.Log($"[unit11] 레인0 가이드 점 {guide.Count} · 선↔웨이포인트(8,9) 최단 {wpDist:F2} 타일");
            Assert.Less(wpDist, NearGuideTiles, "레인 0 가이드가 웨이포인트 (8,9) 를 경유하지 않는다");

            // ── 증상 본체: 경로 1 을 실제로 걷는 적들이 그 가이드 근처를 지나가는가 ──
            int sampled = 0, offGuide = 0;
            float worstOff = 0f;
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 12f)
            {
                using (var q = em.CreateEntityQuery(
                    ComponentType.ReadOnly<Wassup.Battle.Movement.WaypointFollow>(),
                    ComponentType.ReadOnly<Unity.Transforms.LocalTransform>()))
                {
                    var follows = q.ToComponentDataArray<Wassup.Battle.Movement.WaypointFollow>(Allocator.Temp);
                    var xf = q.ToComponentDataArray<Unity.Transforms.LocalTransform>(Allocator.Temp);
                    for (int k = 0; k < follows.Length; k++)
                    {
                        if (follows[k].pathIndex != 1) continue;   // 레인 0 지상(경로 1)만
                        float d = DistanceToPolyline(
                            new float2(xf[k].Position.x, xf[k].Position.z), guide) / field.tileSize;
                        sampled++;
                        worstOff = math.max(worstOff, d);
                        if (d > NearGuideTiles) offGuide++;
                    }
                    follows.Dispose(); xf.Dispose();
                }
                yield return null;
            }

            float offRatio = sampled > 0 ? (float)offGuide / sampled : 1f;
            Debug.Log($"[unit11] 경로1 적 표본 {sampled} · {NearGuideTiles} 타일 초과 이탈 {offGuide} "
                      + $"({offRatio:P1}) · 최대 이탈 {worstOff:F2} 타일");
            Assert.Greater(sampled, 30, "경로 1 을 걷는 적 표본이 없다 — 계측이 공허하다");
            Assert.Less(offRatio, 0.35f,
                "경로 1 적 다수가 광고된 가이드를 벗어나 걷는다 — 예보와 스폰의 경로 해석이 갈렸다");
        }
    }
}
