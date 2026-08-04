using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Sim.Match;

namespace Wassup.Tests.EditMode
{
    // spawn-point-alert unit 3 — 예고 소스가 "다음 웨이브 예측"에서 "큐잉된 웨이브의 실제 스폰
    // base"로 바뀐 뒤의 계약:
    //   • 모든 웨이브가 같은 창을 얻는다 — Wave 1(트리거 0초)과 당긴 웨이브도 포함.
    //   • lane 별 시각은 실스폰과 동일(같은 base·같은 lane 산식).
    //   • 마지막 lane 스폰이 지나면 예고가 사라진다.
    //
    // battle-sim-extraction unit 14 — 예고는 `MatchWaveSchedule` 소유가 됐다. laneCount 가 plain
    // 인자라 **레인 수만을 위해 NativeArray 맵을 만들던 픽스처가 사라졌다**(Allocator.Persistent
    // 두 개 + Dispose 포함). 전투 중 여부(`_running`) 게이트만 Bridge 쪽에 남아 마지막 테스트가
    // 그것을 따로 덮는다.
    public class SpawnAlertForecastTests
    {
        private const float Interval = 10f;
        private const float LeadIn = 2f;
        private const float Spacing = 1f;
        private const int Lanes = 3;

        private MatchWaveSchedule _schedule;
        private AttackUnitData _a;
        private AttackUnitData _b;

        [SetUp]
        public void SetUp()
        {
            _a = CreateUnit("A");
            _b = CreateUnit("B");
            _schedule = new MatchWaveSchedule();
            _schedule.Initialize(BuildPlan(), authored: false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b);
        }

        private GeneratedWavePlan BuildPlan()
        {
            var waves = new List<GeneratedWave>();
            for (int i = 0; i < 4; i++)
                waves.Add(new GeneratedWave(i, i * Interval, _a, 2, _b, 2));
            return new GeneratedWavePlan(
                seed: 1, generatorVersion: 2, timerDurationSec: 40f,
                waveIntervalSec: Interval, intraWaveSpacingSec: Spacing, waves: waves,
                spawnLeadInSec: LeadIn);
        }

        // 큐잉 전에는 예고가 없다(배틀 시작 직후 프레임).
        [Test]
        public void NoQueuedWave_HasNoForecast()
        {
            Assert.IsFalse(_schedule.TryGetSpawnAlertForecast(0f, out var first), "큐잉 전 예고");
            Assert.IsNull(first);
        }

        // unit 1 에서는 Wave 1(트리거 0초)이 창을 못 만들어 자연 스킵됐다. 리드인 도입 후에는
        // 큐잉 시점부터 첫 적까지가 창이라 Wave 1 도 예고를 받는다 — 이 unit 의 핵심 반전.
        [Test]
        public void WaveOne_GetsForecast_FromQueueTime()
        {
            _schedule.QueueDueWaves(0f, Lanes, null);

            Assert.IsTrue(_schedule.TryGetSpawnAlertForecast(0f, out var first),
                "Wave 1 도 예고를 받아야 한다");
            Assert.AreEqual(Lanes, first.Length, "lane 수");
            // 웨이브 0 엔트리 4개: base 2 에서 spacing 1 → 2,3,4,5. lane = deckIndex % 3 = 0,1,2,0.
            Assert.AreEqual(2f, first[0], 0.0001f);
            Assert.AreEqual(3f, first[1], 0.0001f);
            Assert.AreEqual(4f, first[2], 0.0001f);
        }

        // 당긴 웨이브도 같은 큐잉 경로를 지나므로 예고를 받는다. unit 1 의 "강제 호출은 예고 없이
        // 즉시 스폰" 계약은 폐기됐다.
        [Test]
        public void ForcedWave_GetsForecast()
        {
            _schedule.QueueDueWaves(0f, Lanes, null);
            Assert.IsTrue(_schedule.TryForceNextWave(3f, Lanes, null));

            Assert.IsTrue(_schedule.TryGetSpawnAlertForecast(3f, out var first),
                "당긴 웨이브도 예고를 받아야 한다");
            float earliest = float.MaxValue;
            for (int i = 0; i < first.Length; i++)
                if (first[i] >= 0f && first[i] < earliest) earliest = first[i];
            Assert.AreEqual(3f + LeadIn, earliest, 0.0001f, "당긴 시점 + 리드인");
        }

        // 마지막 lane 스폰까지는 유지된다(뒷 lane 이 자기 유닛보다 먼저 사라지면 안 된다).
        [Test]
        public void ForecastSurvivesUntilTheLastLaneSpawn()
        {
            _schedule.QueueDueWaves(0f, Lanes, null);   // lane 시각 2 / 3 / 4

            Assert.IsTrue(_schedule.TryGetSpawnAlertForecast(3.5f, out _),
                "아직 lane 2(4초)가 남았다");
            Assert.IsFalse(_schedule.TryGetSpawnAlertForecast(4.1f, out _),
                "마지막 lane 스폰이 지나면 예고가 사라진다");
        }

        // 전투가 끝나면 즉시 끊긴다(프레젠터가 잔상 없이 정리하는 근거). 이 게이트만 Bridge 소유라
        // 스케줄에 플랜을 주입한 실제 Bridge 로 확인한다.
        [Test]
        public void NotRunning_HasNoForecast()
        {
            var go = new GameObject("BattleBridge_SpawnAlertRunningGate");
            try
            {
                var bridge = go.AddComponent<BattleBridge>();
                var schedule = (MatchWaveSchedule)FindField(bridge, "_waveSchedule").GetValue(bridge);
                schedule.Initialize(BuildPlan(), authored: false);
                schedule.QueueDueWaves(0f, Lanes, null);

                FindField(bridge, "_running").SetValue(bridge, true);
                Assert.IsTrue(bridge.TryGetSpawnAlertForecast(out _, out _), "전투 중에는 서빙한다");

                FindField(bridge, "_running").SetValue(bridge, false);
                Assert.IsFalse(bridge.TryGetSpawnAlertForecast(out _, out _), "전투가 끝나면 끊긴다");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---- helpers ----

        private static AttackUnitData CreateUnit(string name)
        {
            var unit = ScriptableObject.CreateInstance<AttackUnitData>();
            unit.displayName = name;
            return unit;
        }

        private static FieldInfo FindField(object target, string name)
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
            return fi;
        }
    }
}
