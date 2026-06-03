using System.Collections;
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
            Time.timeScale = 1f; // a scene DreamcatcherController may have paused.
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        // The scene's DreamcatcherController would otherwise fire on our placements
        // (pausing via timeScale / drawing cards) and pollute these focused tests.
        private static void NeutralizeSceneController()
        {
            var dc = Object.FindObjectOfType<DreamcatcherController>();
            if (dc != null) Object.Destroy(dc.gameObject);
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
            NeutralizeSceneController();
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
            bridge.ApplyDreamcatcherCard(MakeCard(CardTargetAxis.ClassRanger, CardBuffKind.AttackSpeed, 10f));
            bridge.ApplyDreamcatcherCard(MakeCard(CardTargetAxis.ClassRanger, CardBuffKind.AttackSpeed, 10f)); // stack
            bridge.ApplyDreamcatcherCard(MakeCard(CardTargetAxis.ClassGuardian, CardBuffKind.EffectiveHealth, 15f));
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

        [UnityTest]
        public IEnumerator EnteringPlacement_TriggersController_AutoPicksAndApplies()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            NeutralizeSceneController();
            var cat = FindCatalog();
            var ranger = cat.ById("ranger");
            var gm = Object.FindObjectOfType<GameManager>();

            bridge.SetDefenderPool(new[] { ranger });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            // deck of 3 ranger-AS cards so any auto-pick buffs the ranger's attack speed.
            var deck = ScriptableObject.CreateInstance<DreamcatcherDeck>();
            deck.cards = new[]
            {
                MakeCard(CardTargetAxis.ClassRanger, CardBuffKind.AttackSpeed, 10f),
                MakeCard(CardTargetAxis.ClassRanger, CardBuffKind.AttackSpeed, 10f),
                MakeCard(CardTargetAxis.ClassRanger, CardBuffKind.AttackSpeed, 10f),
            };

            // build controller inactive so fields are set before OnEnable subscribes.
            var go = new GameObject("DreamcatcherController_Test");
            go.SetActive(false);
            var ctrl = go.AddComponent<DreamcatcherController>();
            SetField(ctrl, "bridge", bridge);
            SetField(ctrl, "deck", deck);
            go.SetActive(true);

            // Place a ranger BEFORE the pick (pick now happens on entering Placement).
            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place ranger");
            yield return null;

            // Entering the Placement phase is the first-pick trigger.
            gm.SetPhase(GamePhase.Placement);
            yield return null;
            yield return null;
            yield return null;

            var em = Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.AreEqual(1.1f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f,
                "entering placement auto-picked a ranger card and applied it");

            Object.Destroy(go);
        }

        private static void SetField(object obj, string name, object value)
        {
            obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
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
