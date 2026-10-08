using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.BattleCoreUnity.Cards;
using Somnia.Battle.Data;

namespace Somnia.Battle.Tests.EditMode
{
    // battle-core-rebuild unit 8b — 진입 입력의 **순수 함수 몫**(맵 풀 4갈래 · 웨이브 원천 서열 · 저작 플랜 시계 · 첫 손패 고정).
    // 판을 짓지 않는다 — 라이브 풀 에셋에 대고 「같은 시드 = 같은 엔트리」를 묻는다(옛 tournament-seed-map-select 결정론).
    public sealed class MatchEntryBuildTests
    {
        private const string PoolPath = "Assets/_Project/Data/Maps/MapStagePool.asset";

        private static MapStagePool Pool()
        {
            var pool = AssetDatabase.LoadAssetAtPath<MapStagePool>(PoolPath);
            Assert.IsNotNull(pool, "라이브 맵 풀");
            Assert.Greater(pool.Count, 1, "선택을 물을 만큼 엔트리가 있다");
            return pool;
        }

        [Test]
        public void 같은_토너먼트_시드는_같은_맵과_덱이다()
        {
            var pool = Pool();
            for (ulong seed = 1; seed < 40; seed += 7)
            {
                Assert.IsTrue(MatchDefinitionBuilder.TrySelectEncounter(pool, -1, 0, true, seed, out var a, out int ia, out var sa));
                Assert.IsTrue(MatchDefinitionBuilder.TrySelectEncounter(pool, -1, 0, true, seed, out var b, out int ib, out _));
                Assert.AreEqual("tournament", sa);
                Assert.AreEqual(ia, ib);
                Assert.AreEqual((int)(seed % (ulong)pool.Count), ia, "seed % Count 그대로(`MapPoolSelect`)");
                Assert.AreSame(a.stage, b.stage);
                Assert.AreSame(a.deck, b.deck, "맵과 덱은 같은 인덱스로 잠긴다");
            }
        }

        [Test]
        public void 네_갈래_서열_dev_강제_디버그_토너먼트_0번()
        {
            var pool = Pool();
            MatchDefinitionBuilder.TrySelectEncounter(pool, 1, 3, true, 2, out _, out int i, out var src);
            Assert.AreEqual(1, i); Assert.AreEqual("dev", src);
            MatchDefinitionBuilder.TrySelectEncounter(pool, -1, 3, true, 2, out _, out i, out src);
            Assert.AreEqual(3 % pool.Count, i); Assert.AreEqual("debug", src);
            MatchDefinitionBuilder.TrySelectEncounter(pool, -1, 0, false, 0, out _, out i, out src);
            Assert.AreEqual(0, i); Assert.AreEqual("fallback0", src);
            if (pool.DevCount > 0)
            {
                MatchDefinitionBuilder.TrySelectEncounter(pool, pool.Count, 0, false, 0, out var dev, out i, out src);
                Assert.AreEqual("dev(devEntry)", src);
                Assert.AreSame(pool.GetDev(0).stage, dev.stage, "dev 슬롯은 dev 강제만 닿는다");
            }
        }

        [Test]
        public void 강제_플랜은_모드_플랜을_이기고_모드_플랜은_엔트리_플랜을_이긴다()
        {
            var forced = ScriptableObject.CreateInstance<WavePlanAsset>();
            var modePlan = ScriptableObject.CreateInstance<WavePlanAsset>();
            var encounter = ScriptableObject.CreateInstance<WavePlanAsset>();
            var mode = ScriptableObject.CreateInstance<MatchModeData>();
            mode.waveSourceKind = WaveSourceKind.AuthoredPlan;
            mode.plan = modePlan;
            try
            {
                Assert.AreSame(forced, MatchDefinitionBuilder.ResolveEntryPlan(forced, mode, encounter), "①② > ③");
                Assert.IsNull(MatchDefinitionBuilder.ResolveEntryPlan(null, mode, encounter), "③ > ④ (모드 제 플랜은 모드가 싣는다)");
                var e = new EntryAuthoring { EncounterPlan = encounter };
                Assert.AreSame(modePlan, MatchDefinitionBuilder.ResolveWavePlan(mode, null, in e));
                mode.waveSourceKind = WaveSourceKind.GeneratedFromDeck;
                Assert.AreSame(encounter, MatchDefinitionBuilder.ResolveWavePlan(mode, null, in e), "④ — 모드가 플랜을 안 고르면 엔트리 플랜");
            }
            finally
            {
                Object.DestroyImmediate(forced); Object.DestroyImmediate(modePlan);
                Object.DestroyImmediate(encounter); Object.DestroyImmediate(mode);
            }
        }

        [Test]
        public void 저작_플랜은_제_시계로_돈다_0_은_끝없음()
        {
            var plan = ScriptableObject.CreateInstance<WavePlanAsset>();
            try
            {
                var mode = ModeDef.Default();
                plan.timerDurationSec = 60f;
                MatchDefinitionBuilder.ApplyEntryPlanClock(ref mode, plan);
                Assert.AreEqual(WaveSourceKind.AuthoredPlan, mode.WaveSource);
                Assert.AreEqual(ClockKind.FixedLimit, mode.Clock);
                Assert.AreEqual(60f, mode.MatchSeconds);
                plan.timerDurationSec = 0f;
                MatchDefinitionBuilder.ApplyEntryPlanClock(ref mode, plan);
                Assert.AreEqual(ClockKind.CountUp, mode.Clock, "옛 「0 = endless」");
            }
            finally { Object.DestroyImmediate(plan); }
        }
    }
}
