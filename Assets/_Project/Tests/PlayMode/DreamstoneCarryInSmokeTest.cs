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
using Wassup.UI;

namespace Wassup.Tests.PlayMode
{
    // dreamstone-loadout Unit 3 — equipped stones become a match-long, axis=All
    // buff on current + future defenders via BattleBridge.SetDreamstones (pending)
    // + BeginPlacement (apply, set-then-apply). Drives BattleBridge directly,
    // mirroring DreamcatcherEffectTest's infra (SetDefenderPool + BeginPlacement,
    // no scene DreamcatcherController interference).
    public class DreamstoneCarryInSmokeTest
    {
        private PlayerProfileSO _profSO;

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            // Clear the in-memory squad so other tests / a draft path are not polluted
            // by this run's setup (SquadCarryInSmokeTest pattern, extended to stoneIds).
            // No-op for the bridge-direct test above, which never sets _profSO.
            var squad = _profSO != null && _profSO.profile != null ? _profSO.profile.SelectedSquad() : null;
            if (squad != null)
            {
                for (int i = 0; i < squad.unitIds.Count; i++) squad.unitIds[i] = "";
                if (squad.stoneIds != null)
                    for (int i = 0; i < squad.stoneIds.Count; i++) squad.stoneIds[i] = "";
            }
        }

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

        // dreamstone-loadout Unit 3 (rev, review follow-up) — end-to-end through the
        // real production seam this class's first test bypasses: GameManager.Start ->
        // StartSquadMatch -> ResolveEquippedStones -> GameManager.stoneCatalog ->
        // BattleBridge.SetDreamstones. Mirrors SquadCarryInSmokeTest's profile-priming
        // technique (OutgameMenuController reflection to reach the live PlayerProfileSO)
        // and forces a deterministic squad exactly like it does for unitIds.
        //
        // NOTE: requires BattleScene's GameManager.stoneCatalog scene reference (wired
        // 2026-07-04, same commit as this test). Unwired, ResolveEquippedStones resolves
        // to an empty list and damageMul stays at 1.0 — exactly the silent-no-op wiring
        // seam this test guards against regressing.
        [UnityTest]
        public IEnumerator EquippedSquad_StartSquadMatch_EndToEnd()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Outgame, LoadSceneMode.Single);
            yield return null;

            var menu = Object.FindObjectOfType<OutgameMenuController>();
            Assert.IsNotNull(menu, "outgame menu present");
            _profSO = (PlayerProfileSO)menu.GetType()
                .GetField("profileSO", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(menu);
            Assert.IsNotNull(_profSO, "profileSO wired");

            var profile = _profSO.profile;
            var squad = profile.SelectedSquad();
            Assert.IsNotNull(squad, "default squad exists");
            Assert.GreaterOrEqual(profile.ownedUnitIds.Count, 2, "owned pool seeded");

            // Force a deterministic filled squad + 4x Unique ATK stone regardless of
            // disk state ("stone_atk_unique" — the real unit 0 catalog asset id).
            squad.unitIds[0] = profile.ownedUnitIds[0];
            squad.unitIds[1] = profile.ownedUnitIds[1];
            for (int i = 0; i < SquadSave.StoneSlotCount; i++) squad.stoneIds[i] = "stone_atk_unique";
            string placedUnitId = squad.unitIds[0];

            // BattleScene/DraftView pre-existing missing-script noise on load.
            LogAssert.ignoreFailingMessages = true;

            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var gm = Object.FindObjectOfType<GameManager>();
            Assert.IsNotNull(gm, "battle GameManager present");
            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            NeutralizeSceneController();

            // squad map-setup — squad mode opens a map-setup step first; the player
            // presses START to advance. Simulate that here (SquadCarryInSmokeTest).
            Assert.AreNotEqual(GamePhase.Draft, gm.CurrentPhase, "squad mode skips draft");
            gm.RequestPlacement();
            yield return null;
            yield return null;
            Assert.AreEqual(GamePhase.Placement, gm.CurrentPhase,
                "after map-setup START, squad mode enters Placement");

            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            var cat = FindCatalog();
            var unit = cat.ById(placedUnitId);
            Assert.IsNotNull(unit, "squad unit 0 resolves from catalog");
            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place squad unit 0");
            yield return null;
            yield return null;
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.AreEqual(1.30f, GetStat(bridge, em, placedUnitId).damageMul, 0.01f,
                "StartSquadMatch carry-in (wired stoneCatalog): 4x Unique ATK stone = +30% damageMul");
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
