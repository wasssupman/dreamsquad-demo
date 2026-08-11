using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // spawn-point-alert unit 3 — 예고 소스가 "다음 웨이브 예측"에서 "큐잉된 웨이브의 실제 스폰
    // base"로 바뀐 뒤의 계약:
    //   • 모든 웨이브가 같은 창을 얻는다 — Wave 1(트리거 0초)과 당긴 웨이브도 포함.
    //   • lane 별 시각은 실스폰과 동일(같은 base·같은 lane 산식).
    //   • 마지막 lane 스폰이 지나면 예고가 사라진다.
    //
    // Fixture 는 WaveForceRescheduleTests 와 같은 리플렉션 격리 + laneCount 를 위한 최소 맵.
    public class SpawnAlertForecastTests
    {
        private const float Interval = 10f;
        private const float LeadIn = 2f;
        private const float Spacing = 1f;
        private const int Lanes = 3;

        private GameObject _go;
        private BattleBridge _bridge;
        private AttackUnitData _a;
        private AttackUnitData _b;
        private GeneratedMap _map;

        [SetUp]
        public void SetUp()
        {
            _a = CreateUnit("A");
            _b = CreateUnit("B");

            _go = new GameObject("BattleBridge_SpawnAlertTest");
            _bridge = _go.AddComponent<BattleBridge>();

            // laneCount 는 _generatedMap.spawns.Length 에서 온다. tiles+spawns 만 있으면 IsCreated.
            _map = new GeneratedMap
            {
                gridSize = new int2(4, 4),
                tiles = new NativeArray<MapTileType>(16, Allocator.Persistent),
                spawns = new NativeArray<int2>(Lanes, Allocator.Persistent),
            };
            for (int i = 0; i < Lanes; i++) _map.spawns[i] = new int2(i, 0);

            var waves = new List<GeneratedWave>();
            for (int i = 0; i < 4; i++)
                waves.Add(new GeneratedWave(i, i * Interval, _a, 2, _b, 2));

            SetField(_bridge, "_wavePlan", new GeneratedWavePlan(
                seed: 1, generatorVersion: 2, timerDurationSec: 40f,
                waveIntervalSec: Interval, intraWaveSpacingSec: Spacing, waves: waves,
                spawnLeadInSec: LeadIn));
            SetField(_bridge, "_generatedMap", _map);
            SetField(_bridge, "_usingGeneratedWaves", true);
            SetField(_bridge, "_running", true);
        }

        [TearDown]
        public void TearDown()
        {
            _map.Dispose();
            if (_go != null) Object.DestroyImmediate(_go);
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b);
        }

        // 큐잉 전에는 예고가 없다(배틀 시작 직후 프레임).
        [Test]
        public void NoQueuedWave_HasNoForecast()
        {
            Assert.IsFalse(_bridge.TryGetSpawnGuideForecast(out _, out var guides), "큐잉 전 예고");
            Assert.IsNull(guides);
        }

        // unit 1 에서는 Wave 1(트리거 0초)이 창을 못 만들어 자연 스킵됐다. 리드인 도입 후에는
        // 큐잉 시점부터 첫 적까지가 창이라 Wave 1 도 예고를 받는다 — 이 unit 의 핵심 반전.
        [Test]
        public void WaveOne_GetsForecast_FromQueueTime()
        {
            QueueDueWaves(0f);

            Assert.IsTrue(_bridge.TryGetSpawnGuideForecast(out float clock, out var guides),
                "Wave 1 도 예고를 받아야 한다");
            Assert.AreEqual(0f, clock, 0.0001f);
            // 웨이브 0 엔트리 4개: base 2 에서 spacing 1 → 2,3,4,5. lane = deckIndex % 3 = 0,1,2,0.
            Assert.AreEqual(2f, FirstGuideTime(guides, 0), 0.0001f);
            Assert.AreEqual(3f, FirstGuideTime(guides, 1), 0.0001f);
            Assert.AreEqual(4f, FirstGuideTime(guides, 2), 0.0001f);
        }

        [Test]
        public void WaveOne_GuideForecastPreservesSwarmAndActualLane()
        {
            _a.waypointPathIndex = 0;
            _b.waypointPathIndex = 1;
            QueueDueWaves(0f);

            Assert.IsTrue(_bridge.TryGetSpawnGuideForecast(out float clock, out var guides));
            Assert.AreEqual(0f, clock, 0.0001f);
            Assert.AreEqual(4, guides.Length,
                "A lane0/lane2 + B lane1/lane0 = 스웜×실제 lane 4개");

            int laneZeroCount = 0;
            bool hasSwarmA = false;
            bool hasSwarmB = false;
            for (int i = 0; i < guides.Length; i++)
            {
                if (guides[i].laneIndex != 0) continue;
                laneZeroCount++;
                hasSwarmA |= guides[i].swarmIndex == 0 && guides[i].waypointPathIndex == 0;
                hasSwarmB |= guides[i].swarmIndex == 1 && guides[i].waypointPathIndex == 1;
            }
            Assert.AreEqual(2, laneZeroCount, "같은 lane 의 서로 다른 스웜을 병합하지 않는다");
            Assert.IsTrue(hasSwarmA);
            Assert.IsTrue(hasSwarmB);
        }

        // 당긴 웨이브도 같은 경로(QueueWave)를 지나므로 예고를 받는다. unit 1 의 "강제 호출은
        // 예고 없이 즉시 스폰" 계약은 폐기됐다.
        [Test]
        public void ForcedWave_GetsForecast()
        {
            QueueDueWaves(0f);
            SetBattleClock(3f);
            _bridge.ForceNextWave();

            Assert.IsTrue(_bridge.TryGetSpawnGuideForecast(out _, out var guides),
                "당긴 웨이브도 예고를 받아야 한다");
            float earliest = float.MaxValue;
            for (int i = 0; i < guides.Length; i++)
                if (guides[i].firstSpawnSec < earliest) earliest = guides[i].firstSpawnSec;
            Assert.AreEqual(3f + LeadIn, earliest, 0.0001f, "당긴 시점 + 리드인");
        }

        // 마지막 lane 스폰까지는 유지된다(뒷 lane 이 자기 유닛보다 먼저 사라지면 안 된다).
        [Test]
        public void ForecastSurvivesUntilTheLastGuideSpawn()
        {
            QueueDueWaves(0f);   // 스웜×lane 시각 2 / 3 / 4 / 5

            SetBattleClock(4.5f);
            Assert.IsTrue(_bridge.TryGetSpawnGuideForecast(out _, out _),
                "아직 두 번째 스웜 lane 0(5초)가 남았다");

            SetBattleClock(5.1f);
            Assert.IsFalse(_bridge.TryGetSpawnGuideForecast(out _, out _),
                "마지막 guide 스폰이 지나면 예고가 사라진다");
        }

        // 전투가 끝나면 즉시 끊긴다(프레젠터가 잔상 없이 정리하는 근거).
        [Test]
        public void NotRunning_HasNoForecast()
        {
            QueueDueWaves(0f);
            SetField(_bridge, "_running", false);

            Assert.IsFalse(_bridge.TryGetSpawnGuideForecast(out _, out _));
        }

        // ---- helpers ----

        private void QueueDueWaves(float elapsedSec)
        {
            SetBattleClock(elapsedSec);
            var mi = typeof(BattleBridge).GetMethod("QueueDueWaves",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "QueueDueWaves 를 찾지 못했다");
            mi.Invoke(_bridge, new object[] { elapsedSec });
        }

        private void SetBattleClock(float sec) => SetField(_bridge, "_battleClock", (double)sec);

        private static float FirstGuideTime(SpawnGuideForecast[] guides, int laneIndex)
        {
            float first = float.MaxValue;
            for (int i = 0; i < guides.Length; i++)
                if (guides[i].laneIndex == laneIndex && guides[i].firstSpawnSec < first)
                    first = guides[i].firstSpawnSec;
            return first;
        }

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }

        private static void SetField(object target, string name, object value)
        {
            var type = target.GetType();
            FieldInfo fi = null;
            while (fi == null && type != null)
            {
                fi = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance
                                       | BindingFlags.Public);
                type = type.BaseType;
            }
            Assert.IsNotNull(fi, $"Field '{name}' not found on {target.GetType().Name}");
            fi.SetValue(target, value);
        }
    }
}
