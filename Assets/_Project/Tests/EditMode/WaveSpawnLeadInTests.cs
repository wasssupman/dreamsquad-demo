using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-pattern unit 11 — 웨이브 트리거와 첫 적 등장 사이의 리드인.
    //
    // 계약: 트리거 그리드(i*interval)와 `_waveTimeShift` 리스케줄은 **불변**이고, `QueueWave`
    // 의 스폰 base 만 리드인만큼 밀린다. 리드인이 트리거 그리드나 shift 산식으로 새면 강제
    // 호출 연타마다 누적 왜곡되므로, 아래 테스트가 그 분리를 고정한다.
    //
    // Fixture 는 WaveForceRescheduleTests 와 동일 방식 — ECS world 없이 리플렉션으로
    // 플랜/클럭을 주입해 스케줄러만 격리 검증한다(_generatedMap 미생성 → laneCount 1).
    public class WaveSpawnLeadInTests
    {
        private const float Interval = 10f;
        private const float LeadIn = 2f;

        private GameObject _go;
        private BattleBridge _bridge;
        private AttackUnitData _a;
        private AttackUnitData _b;

        [SetUp]
        public void SetUp()
        {
            _a = CreateUnit("A");
            _b = CreateUnit("B");

            _go = new GameObject("BattleBridge_WaveLeadInTest");
            _bridge = _go.AddComponent<BattleBridge>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            if (_a != null) Object.DestroyImmediate(_a);
            if (_b != null) Object.DestroyImmediate(_b);
        }

        // ── 생성기: 덱 값 → 플랜 ────────────────────────────────────────────

        [Test]
        public void Generate_CarriesDeckLeadInIntoPlan()
        {
            var plan = WavePatternGenerator.Generate(CreateDeck(LeadIn), 4242);
            Assert.AreEqual(LeadIn, plan.spawnLeadInSec, 0.0001f, "덱 리드인이 플랜에 실려야 한다");
        }

        [Test]
        public void Generate_ClampsNegativeLeadInToZero()
        {
            var plan = WavePatternGenerator.Generate(CreateDeck(-3f), 4242);
            Assert.AreEqual(0f, plan.spawnLeadInSec, 0.0001f, "음수 리드인은 0 으로 클램프");
        }

        // 리드인은 스폰 base 전용이다. 같은 시드면 리드인이 뭐든 플랜 시각(브리핑·로그의
        // source of truth)은 동일해야 한다.
        [Test]
        public void Generate_TriggerGridIsIndependentOfLeadIn()
        {
            var none = WavePatternGenerator.Generate(CreateDeck(0f), 777);
            var with = WavePatternGenerator.Generate(CreateDeck(5f), 777);

            Assert.AreEqual(none.waves.Count, with.waves.Count, "웨이브 수");
            for (int i = 0; i < none.waves.Count; i++)
            {
                Assert.AreEqual(i * Interval, none.waves[i].triggerTimeSec, 0.0001f,
                    $"웨이브 {i} 트리거는 i×interval 이어야 한다");
                Assert.AreEqual(none.waves[i].triggerTimeSec, with.waves[i].triggerTimeSec, 0.0001f,
                    $"웨이브 {i} 트리거가 리드인에 오염됐다");
            }
        }

        // 작성 플랜은 그룹 상대 시각으로 리드인을 직접 표현한다 — 덱 값을 겹치면 이중 가산.
        [Test]
        public void FromPlanAsset_HasNoLeadIn()
        {
            var asset = ScriptableObject.CreateInstance<WavePlanAsset>();
            try
            {
                asset.timerDurationSec = 40f;
                asset.waves = new List<AuthoredWave>
                {
                    new AuthoredWave
                    {
                        durationSec = 12f,
                        intervalSec = 0.5f,
                        groups = new List<AuthoredSpawnGroup>
                        {
                            new AuthoredSpawnGroup { triggerTimeSec = 2f, unit = _a, count = 2 },
                        },
                    },
                };

                var plan = WavePatternGenerator.FromPlanAsset(asset);
                Assert.AreEqual(0f, plan.spawnLeadInSec, 0.0001f,
                    "작성 플랜에 리드인이 실리면 그룹 offset 과 이중 가산된다");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        // 사용자 결정 2026-07-26(A안) — 전 덱이 리드인을 갖는다(엔드리스 포함). 구체 값은
        // 밸런스라 못박지 않고 "0 으로 꺼져 있지 않다"만 지킨다. 0 이 되면 웨이브 전환이
        // 다시 예고 없이 터지고 예고선 창도 사라진다.
        [Test]
        public void EveryShippedDeck_CarriesALeadIn()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:AttackDeck");
            Assert.Greater(guids.Length, 0, "AttackDeck 에셋을 하나도 찾지 못했다");

            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackDeck>(path);
                if (deck == null || !deck.useGeneratedWaves) continue;
                Assert.Greater(deck.waveSpawnLeadInSec, 0f,
                    $"'{deck.deckId}'({path}) 의 waveSpawnLeadInSec 가 0 — 첫 적이 트리거와 동시에 나온다");
            }
        }

        // ── 브리지: 플랜 → 큐잉 ────────────────────────────────────────────

        [Test]
        public void QueueDueWaves_FirstSpawnLandsAfterLeadIn()
        {
            InjectPlan(LeadIn);

            QueueDueWaves(0f);
            Assert.AreEqual(1, NextWaveIndex(), "0초에 wave 1 큐잉(트리거 그리드는 불변)");
            Assert.AreEqual(LeadIn, FirstPendingSpawnSec(), 0.0001f,
                "첫 적은 트리거 + 리드인에 나와야 한다");
        }

        [Test]
        public void LeadInZero_KeepsLegacySpawnTiming()
        {
            InjectPlan(0f);

            QueueDueWaves(0f);
            Assert.AreEqual(0f, FirstPendingSpawnSec(), 0.0001f,
                "리드인 0 = 기존 동작(트리거 = 첫 스폰)");
        }

        // 강제 호출도 리드인을 따르되, `_waveTimeShift`(트리거 그리드 기준)에는 새지 않는다.
        // 새면 다음 웨이브가 13초가 아니라 11/15초로 어긋난다.
        [Test]
        public void ForceNextWave_AppliesLeadInWithoutPollutingShift()
        {
            InjectPlan(LeadIn);

            QueueDueWaves(0f);            // wave 1 자동 큐잉
            ClearPending();
            SetBattleClock(3f);
            _bridge.ForceNextWave();      // wave 2 를 3초로 당김

            Assert.AreEqual(3f + LeadIn, FirstPendingSpawnSec(), 0.0001f,
                "당긴 웨이브의 첫 적도 리드인 뒤에 나온다");

            QueueDueWaves(12.9f);
            Assert.AreEqual(2, NextWaveIndex(), "wave 3 은 3+10=13초 전에 나오면 안 된다");
            QueueDueWaves(13f);
            Assert.AreEqual(3, NextWaveIndex(), "wave 3 은 호출 시점 + 원래 간격에 큐잉된다");
        }

        // ---- helpers ----

        // 0, 10, 20, 30초 4웨이브 — seed 경로가 만드는 모양(i * interval)과 동일.
        private void InjectPlan(float leadInSec)
        {
            var waves = new List<GeneratedWave>();
            for (int i = 0; i < 4; i++)
                waves.Add(new GeneratedWave(i, i * Interval, _a, 2, _b, 2));

            var plan = new GeneratedWavePlan(
                seed: 1, generatorVersion: 2, timerDurationSec: 40f,
                waveIntervalSec: Interval, intraWaveSpacingSec: 1f, waves: waves,
                spawnLeadInSec: leadInSec);

            SetField(_bridge, "_wavePlan", plan);
            SetField(_bridge, "_usingGeneratedWaves", true);
            SetField(_bridge, "_running", true);
        }

        private AttackDeck CreateDeck(float leadInSec)
        {
            var deck = ScriptableObject.CreateInstance<AttackDeck>();
            deck.useGeneratedWaves = true;
            deck.attackUnitPool = new[] { _a, _b };
            deck.minWaveCount = 4;
            deck.maxWaveCount = 4;
            deck.minUnitsPerWave = 2;
            deck.maxUnitsPerWave = 2;
            deck.waveCountJitter = 0;
            deck.intraWaveSpacingSec = 1f;
            deck.maxWaveIntervalSec = Interval;
            deck.timerDurationSec = 40f;
            deck.bossUnit = null;
            deck.bossWaveInterval = 0;
            deck.waveSpawnLeadInSec = leadInSec;
            return deck;
        }

        // pending 큐의 가장 이른 스폰 시각. 큐가 비면 실패시킨다.
        private float FirstPendingSpawnSec()
        {
            var pending = (IList)GetField(_bridge, "_pending");
            Assert.Greater(pending.Count, 0, "pending 스폰 큐가 비었다");

            var entryField = pending[0].GetType()
                .GetField("entry", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(entryField, "PendingSpawnEntry.entry 필드를 찾지 못했다");

            float min = float.MaxValue;
            for (int i = 0; i < pending.Count; i++)
            {
                var entry = (SpawnEntry)entryField.GetValue(pending[i]);
                if (entry.triggerTimeSec < min) min = entry.triggerTimeSec;
            }
            return min;
        }

        private void ClearPending() => ((IList)GetField(_bridge, "_pending")).Clear();

        private void QueueDueWaves(float elapsedSec)
        {
            SetBattleClock(elapsedSec);
            var mi = typeof(BattleBridge).GetMethod("QueueDueWaves",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mi, "QueueDueWaves 를 찾지 못했다");
            mi.Invoke(_bridge, new object[] { elapsedSec });
        }

        private void SetBattleClock(float sec) => SetField(_bridge, "_battleClock", (double)sec);

        private int NextWaveIndex() => (int)GetField(_bridge, "_nextWaveIndex");

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

        private static void SetField(object target, string name, object value) =>
            FindField(target, name).SetValue(target, value);

        private static object GetField(object target, string name) =>
            FindField(target, name).GetValue(target);
    }
}
