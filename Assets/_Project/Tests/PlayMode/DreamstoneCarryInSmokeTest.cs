using System.Collections;
using System.Collections.Generic;
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
            TimeManager.Instance.ResetAll();
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
            bridge.ApplyDreamcatcherCardHosted(MakeCard(CardTargetAxis.ClassRanger, CardBuffKind.AttackDamage, 10f));
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
            var catalogIds = new System.Collections.Generic.List<string>(
                ((Wassup.Data.DefenderCatalog)menu.GetType()
                    .GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(menu)).AllIds());
            Assert.GreaterOrEqual(catalogIds.Count, 2, "catalog has units");

            // Force a deterministic filled squad + the Unique Attack Stone block
            // (stone_001..stone_004 = tiers 7.5/6/6/4.5, sum 24 -- unit 5 rev
            // 2026-07-06b replaced the old flat "stone_atk_unique" x4 duplicate-id
            // catalog with 64 individually-owned stones; that legacy id no longer
            // resolves, so this e2e now equips the 4 real distinct instances).
            squad.unitIds[0] = catalogIds[0];
            squad.unitIds[1] = catalogIds[1];
            squad.stoneIds[0] = "stone_001";
            squad.stoneIds[1] = "stone_002";
            squad.stoneIds[2] = "stone_003";
            squad.stoneIds[3] = "stone_004";
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
            Assert.AreEqual(1.24f, GetStat(bridge, em, placedUnitId).damageMul, 0.01f,
                "StartSquadMatch carry-in (wired stoneCatalog): Unique ATK 7.5+6+6+4.5 = +24% damageMul");
        }

        // dreamstone-loadout Unit 6 — CostRate stones route through GameManager to
        // CostRuntime instead of BattleBridge's entity registry. Mirrors
        // EquippedSquad_StartSquadMatch_EndToEnd's profile-priming technique, but
        // equips a single Unique Cost Stone (stone_049, +7.5%) and asserts the
        // CostRuntime side effect directly via the (now public) RegenRateMultiplier
        // property rather than an entity ModifierStats read.
        [UnityTest]
        public IEnumerator EquippedCostRateStone_StartSquadMatch_SetsRegenRateMultiplier()
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
            var catalogIds = new System.Collections.Generic.List<string>(
                ((Wassup.Data.DefenderCatalog)menu.GetType()
                    .GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(menu)).AllIds());
            Assert.GreaterOrEqual(catalogIds.Count, 1, "catalog has units");

            // Force a deterministic filled squad (unitIds must be non-empty for
            // GameManager.Start to take the squad branch at all) equipped with only
            // stone_049 (Unique Cost Stone, +7.5%); remaining 3 stone slots empty.
            squad.unitIds[0] = catalogIds[0];
            squad.stoneIds[0] = "stone_049";
            squad.stoneIds[1] = "";
            squad.stoneIds[2] = "";
            squad.stoneIds[3] = "";

            // BattleScene/DraftView pre-existing missing-script noise on load.
            LogAssert.ignoreFailingMessages = true;

            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            yield return null;
            yield return null;

            var gm = Object.FindObjectOfType<GameManager>();
            Assert.IsNotNull(gm, "battle GameManager present");
            Assert.IsNotNull(gm.CostRuntime, "CostRuntime wired");

            Assert.AreEqual(1.075f, gm.CostRuntime.RegenRateMultiplier, 0.001f,
                "StartSquadMatch routes CostRate stones to CostRuntime: stone_049 +7.5% => 1.075x regen");
        }

        // dreamstone-loadout — regression for a Codex external-review HIGH: stones
        // staged for a squad match must NOT leak into a subsequently-drafted match.
        // Root cause: DraftController.TryConfirm() never cleared BattleBridge's
        // pending stone list, so a stale staging survived into the next
        // BeginPlacement (draft mode's "드래프트 폴백 경로 미적용" contract violated).
        // Mirrors DraftFlowSmokeTest's discard/confirm technique, but driven against
        // the real scene's BattleBridge + DraftController (not a synthetic bare
        // GameObject) so it exercises the actual TryConfirm() -> SetDreamstones(null) fix.
        [UnityTest]
        public IEnumerator StonesDoNotLeakIntoRedraftedMatch()
        {
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
            var draftController = Object.FindObjectOfType<DraftController>();
            Assert.IsNotNull(draftController, "DraftController present in BattleScene");

            // 1) Stage 4x Unique ATK stone for a squad match and confirm it actually
            // applies (same staging as EquippedStones_ApplyStackAndSurviveRestart).
            var atkUnique = ScriptableObject.CreateInstance<DreamstoneData>();
            atkUnique.id = "stone_atk_unique_test";
            atkUnique.grade = DreamstoneGrade.Unique;
            atkUnique.effect = new CardEffect { kind = CardBuffKind.AttackDamage, percent = 7.5f };
            var fourStones = new List<DreamstoneData> { atkUnique, atkUnique, atkUnique, atkUnique };

            bridge.SetDreamstones(fourStones);
            bridge.SetDefenderPool(new[] { ranger });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            // dreamstone-loadout Unit 6 — this test drives BattleBridge directly
            // (bypasses GameManager.StartSquadMatch/ResolveCostRateMultiplier), so
            // stage the CostRate multiplier by hand here to simulate "a squad match
            // with an equipped Cost stone was in progress" before the redraft.
            gm.CostRuntime.SetRegenRateMultiplier(1.5f);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place ranger under squad staging");
            yield return null;
            yield return null;
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.AreEqual(1.30f, GetStat(bridge, em, "ranger").damageMul, 0.01f,
                "stone staging established before drafting a new match");

            // 2) Drive a full draft flow (DraftFlowSmokeTest's discard/confirm
            // technique) against the scene's real DraftController/BattleBridge wiring.
            draftController.BeginDraft(12345);
            yield return null;
            Assert.GreaterOrEqual(draftController.Session.Pool.Count, draftController.DiscardCount,
                "draft pool has enough entries to discard from");
            for (int i = 0; i < draftController.DiscardCount; i++)
                Assert.IsTrue(draftController.ToggleDiscard(draftController.Session.Pool[i]), $"discard {i} succeeds");
            Assert.IsTrue(draftController.Session.IsFull, "session full after discards");
            Assert.IsTrue(draftController.TryConfirm(), "draft confirms -- calls SetDreamstones(null) under the fix");
            yield return null;

            // dreamstone-loadout Unit 6 — TryConfirm's SetDreamstones(null) neighbor
            // line must have reset the CostRate multiplier too (same entry point,
            // same "drafted match carries no squad stone buffs" contract).
            Assert.AreEqual(1.0f, gm.CostRuntime.RegenRateMultiplier, 0.0001f,
                "draft confirm resets RegenRateMultiplier to 1.0 -- no cost-stone leak either");

            // 3) Enter placement for the drafted match and place a drafted unit --
            // must NOT inherit the stale stone staging from step 1.
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;

            var draftedUnits = draftController.Session.PickedArray();
            Assert.IsTrue(draftedUnits.Length > 0, "draft produced picks");
            var draftedUnit = draftedUnits[0];
            Assert.IsTrue(PlaceFirstValid(bridge, draftedUnit), "place drafted unit");
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(1.0f, GetStat(bridge, em, draftedUnit.id).damageMul, 0.01f,
                "drafted match must not inherit the previous squad match's stones (Codex review 2026-07-04)");
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
