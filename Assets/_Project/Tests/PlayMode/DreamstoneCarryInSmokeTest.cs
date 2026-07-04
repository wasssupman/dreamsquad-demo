using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // dreamstone-loadout Unit 3 — equipped stones become a match-long, axis=All
    // buff on current + future defenders via BattleBridge.SetDreamstones (pending)
    // + BeginPlacement (apply, set-then-apply). Drives BattleBridge directly,
    // mirroring DreamcatcherEffectTest's infra (SetDefenderPool + BeginPlacement,
    // no scene DreamcatcherController interference).
    public class DreamstoneCarryInSmokeTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // Stop any in-flight tweens so they do not fire OnComplete after the next
        // test unloads their target (DreamcatcherEffectTest pattern).
        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            Time.timeScale = 1f;
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        // The scene's DreamcatcherController would otherwise auto-pick on entering
        // Placement and pollute the DamageMul channel this test measures.
        private static void NeutralizeSceneController()
        {
            var dc = Object.FindObjectOfType<DreamcatcherController>();
            if (dc != null) Object.Destroy(dc.gameObject);
        }

        [UnityTest]
        public IEnumerator EquippedStones_ApplyStackAndSurviveRestart()
        {
            // BattleScene/DraftView pre-existing missing-script + draft warnings.
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            NeutralizeSceneController();
            var cat = FindCatalog();
            Assert.IsNotNull(cat, "DefenderCatalog loaded");
            var ranger = cat.ById("ranger");
            Assert.IsNotNull(ranger, "ranger unit exists");

            var gm = Object.FindObjectOfType<GameManager>();
            Assert.IsNotNull(gm, "GameManager present");

            // Unique ATK stone, +7.5% each. Four slots of the SAME stone id — the
            // "4x Unique attack stone" scenario the loadout cap math is built around.
            var atkUnique = ScriptableObject.CreateInstance<DreamstoneData>();
            atkUnique.id = "stone_atk_unique_test";
            atkUnique.grade = DreamstoneGrade.Unique;
            atkUnique.effect = new CardEffect { kind = CardBuffKind.AttackDamage, percent = 7.5f };
            var fourStones = new List<DreamstoneData> { atkUnique, atkUnique, atkUnique, atkUnique };

            // set-then-apply: SetDreamstones only stages a pending list; BeginPlacement
            // clears _activeDcEffects and immediately reapplies pending stones right
            // after (BattleBridge.BeginPlacement / ApplyPendingDreamstones), so Set
            // must come before BeginPlacement — this is the order under test.
            bridge.SetDreamstones(fourStones);
            bridge.SetDefenderPool(new[] { ranger });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place ranger");
            yield return null;
            yield return null;
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.AreEqual(1.30f, GetStat(bridge, em, "ranger").damageMul, 0.01f,
                "4x Unique ATK stone (+7.5% each, additive) = +30% damageMul");

            // 복합 스택 — a same-stat dreamcatcher card pick coexists with the stones
            // (distinct stackId -> distinct additive slot, per ModifierStatsAggregateSystem).
            bridge.ApplyDreamcatcherCard(MakeCard(CardTargetAxis.ClassRanger, CardBuffKind.AttackDamage, 10f));
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(1.40f, GetStat(bridge, em, "ranger").damageMul, 0.01f,
                "stone (+30%) and card (+10%) coexist additively");

            // 재시작 회귀 — BeginPlacement clears _activeDcEffects (dropping the
            // ephemeral card pick above) but reapplies the still-pending stones exactly
            // once via ApplyPendingDreamstones: no leak (not 1.60), no loss (not 1.0).
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place ranger after restart");
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(1.30f, GetStat(bridge, em, "ranger").damageMul, 0.01f,
                "restart re-applies the stone loadout exactly once -- no accumulation, no loss");
        }

        private static DreamcatcherCard MakeCard(CardTargetAxis axis, CardBuffKind kind, float pct)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.axis = axis;
            c.effects = new[] { new CardEffect { kind = kind, percent = pct } };
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
