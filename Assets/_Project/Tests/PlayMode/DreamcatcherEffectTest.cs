using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // ingame-dreamcatcher Unit 2 — card buffs reach current AND future matching
    // defenders, stack, and respect the target axis. Drives BattleBridge directly
    // (SetDefenderPool + BeginPlacement) for a deterministic single session.
    public class DreamcatcherEffectTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // Stop any in-flight tweens (e.g. BattleScene's DraftView) so they do not
        // fire OnComplete after the next test unloads their target.
        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator CardBuffs_ApplyToCurrentAndFutureMatchingUnits()
        {
            // BattleScene/DraftView pre-existing missing-script + draft warnings.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            // let GameManager.Start + PrepareDraftMap build the playfield.
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var cat = FindCatalog();
            Assert.IsNotNull(cat, "DefenderCatalog loaded");

            var ranger = cat.ById("ranger");
            var scout = cat.ById("scout");        // future ranger
            var guardian = cat.ById("guardian");
            var caster = cat.ById("fire_caster");

            bridge.SetDefenderPool(new[] { ranger, scout, guardian, caster });
            bridge.BeginPlacement();
            // BeginPlacement alone skips the cost reset that PlacementPhaseView does;
            // fund placement directly for the test.
            var gm = Object.FindObjectOfType<GameManager>();
            Assert.IsNotNull(gm, "GameManager present");
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            // diagnostic: if nothing is placeable, surface the rejection reasons.
            var reasons = new System.Collections.Generic.Dictionary<string, int>();
            int validTiles = 0;
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                {
                    if (bridge.CanPlaceDefenderAt(x, y, ranger, out var rr)) validTiles++;
                    else { var k = rr.ToString(); reasons[k] = reasons.ContainsKey(k) ? reasons[k] + 1 : 1; }
                }
            if (validTiles == 0)
            {
                var sb = new System.Text.StringBuilder("no placeable tile for ranger. reasons:");
                foreach (var kv in reasons) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
                Assert.Fail(sb.ToString());
            }

            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place ranger");
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            Assert.IsTrue(PlaceFirstValid(bridge, caster), "place caster");

            // AttackSpeed axis avoids synergy contamination (synergy buffs DamageMul).
            bridge.ApplyDreamcatcherCardHosted(MakeCard(CardTargetAxis.ClassRanger, CardBuffKind.AttackSpeed, 10f));
            bridge.ApplyDreamcatcherCardHosted(MakeCard(CardTargetAxis.ClassRanger, CardBuffKind.AttackSpeed, 10f)); // stack
            bridge.ApplyDreamcatcherCardHosted(MakeCard(CardTargetAxis.ClassGuardian, CardBuffKind.EffectiveHealth, 15f));
            yield return null;
            yield return null;
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.AreEqual(1.21f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f, "ranger AS stacked +10% x2");
            Assert.AreEqual(0.87f, GetStat(bridge, em, "guardian").dmgTakenMul, 0.01f, "guardian eHP +15% -> dmgTaken 0.87");
            Assert.AreEqual(1.0f, GetStat(bridge, em, "fire_caster").attackSpeedMul, 0.001f, "caster unaffected (axis)");

            // future placement inherits active effects
            Assert.IsTrue(PlaceFirstValid(bridge, scout), "place future ranger");
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(1.21f, GetStat(bridge, em, "scout").attackSpeedMul, 0.01f, "future ranger inherits stacked AS");
        }

        // dreamcatcher-empower-aura — revoke 가 감소형(Multiplicative, mult<1) 버프를 실제로 중립화하는지.
        // 회귀 가드: 예전엔 revoke 가 1f→FromMultiplier→Additive+0 을 보내 원본 Multiplicative 슬롯과
        // op 불일치 → 미머지 → 버프 잔존(+강화 오라 잔존). 이제 원본 op 로 identity 를 emit해 중립화.
        [UnityTest]
        public IEnumerator RevokeNeutralizesReductionShapedBuff()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var cat = FindCatalog();
            var guardian = cat.ById("guardian");
            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");

            // EffectiveHealth → DmgTakenMul ≈ 0.87 (Multiplicative). hosted = revocable handle>0.
            int handle = bridge.ApplyDreamcatcherCardHosted(MakeCard(CardTargetAxis.ClassGuardian, CardBuffKind.EffectiveHealth, 15f));
            Assert.Greater(handle, 0, "hosted card returns revocable handle");
            yield return null; yield return null; yield return null;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.AreEqual(0.87f, GetStat(bridge, em, "guardian").dmgTakenMul, 0.01f, "eHP applied (0.87)");

            bridge.RevokeDreamcatcherEffects(handle);
            yield return null; yield return null; yield return null;
            Assert.AreEqual(1.0f, GetStat(bridge, em, "guardian").dmgTakenMul, 0.01f,
                "revoke restores dmgTakenMul to 1.0 (Multiplicative slot neutralized → net identity → no aura)");
        }

        [UnityTest]
        public IEnumerator CrackedGrail_RevokeNeutralizesBothAdditiveEffects()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var guardian = cat.ById("guardian");
            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");

            yield return null; yield return null; yield return null;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var baseline = GetStat(bridge, em, "guardian");

            int handle = bridge.ApplyDreamcatcherCardHosted(MakeCard(CardTargetAxis.All,
                new CardEffect { kind = CardBuffKind.AttackDamage, percent = 70f },
                new CardEffect { kind = CardBuffKind.EffectiveHealth, percent = -40f }));
            Assert.Greater(handle, 0, "cracked grail returns revocable handle");
            yield return null; yield return null; yield return null;

            var applied = GetStat(bridge, em, "guardian");
            Assert.AreEqual(baseline.damageMul + 0.7f, applied.damageMul, 0.01f, "AttackDamage +70%");
            Assert.AreEqual(baseline.dmgTakenMul + 0.667f, applied.dmgTakenMul, 0.01f, "EffectiveHealth -40%");

            bridge.RevokeDreamcatcherEffects(handle);
            yield return null; yield return null; yield return null;

            var revoked = GetStat(bridge, em, "guardian");
            Assert.AreEqual(baseline.damageMul, revoked.damageMul, 0.01f, "damage reward revoked");
            Assert.AreEqual(baseline.dmgTakenMul, revoked.dmgTakenMul, 0.01f, "health curse revoked");
        }

        private static DreamcatcherCard MakeCard(CardTargetAxis axis, CardBuffKind kind, float pct)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.axis = axis;
            c.effects = new[] { new CardEffect { kind = kind, percent = pct } };
            return c;
        }

        private static DreamcatcherCard MakeCard(CardTargetAxis axis, params CardEffect[] effects)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.axis = axis;
            c.effects = effects;
            return c;
        }

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static ModifierStats GetStat(BattleBridge bridge, EntityManager em, string id)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var t = val.GetType();
                var entity = (Entity)t.GetField("Item1").GetValue(val);
                var data = (DefenderUnitData)t.GetField("Item2").GetValue(val);
                if (data.id == id && em.Exists(entity) && em.HasComponent<ModifierStats>(entity))
                    return em.GetComponentData<ModifierStats>(entity);
            }
            return default;
        }
    }
}
