using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    public class DreamcatcherCursedRelicTest
    {
        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExistingLethalTimer_RejectsCompositeCardWithoutPartialWrites()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gameManager = Object.FindObjectOfType<GameManager>();
            var catalog = FindDefenderCatalog();
            Assert.IsNotNull(bridge, "BattleBridge present");
            Assert.IsNotNull(gameManager, "GameManager present");
            Assert.IsNotNull(catalog, "DefenderCatalog present");

            var guardian = catalog.ById("guardian");
            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gameManager.CostRuntime.ResetToStart();
            gameManager.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            const float originalRemaining = 4.25f;
            em.AddComponentData(defender, new LethalTimer { remaining = originalRemaining });
            float attackSpeedBefore = em.GetComponentData<ModifierStats>(defender).attackSpeedMul;
            bool hadSlotsBefore = em.HasBuffer<DcTriggerSlot>(defender);
            int slotsBefore = hadSlotsBefore ? em.GetBuffer<DcTriggerSlot>(defender).Length : 0;

            var card = MakeCompositeLethalCard();
            // 문구 정본은 BattleBridge.Dreamcatcher.cs 의 DuplicateState 가드 —
            // «already has {payload.kind} state» 형식 (fast-lane unit 2 에서 동기).
            LogAssert.Expect(LogType.Warning,
                "[BattleBridge] ApplyDreamcatcherCardToUnit('calamity_test'): target already has SelfBuffLethal state — card not attached.");
            int handle = bridge.ApplyDreamcatcherCardToUnit(defender, card);

            Assert.AreEqual(-1, handle);
            Assert.AreEqual(originalRemaining, em.GetComponentData<LethalTimer>(defender).remaining, 0.0001f);
            Assert.AreEqual(attackSpeedBefore, em.GetComponentData<ModifierStats>(defender).attackSpeedMul, 0.0001f);
            Assert.AreEqual(hadSlotsBefore, em.HasBuffer<DcTriggerSlot>(defender));
            if (hadSlotsBefore)
                Assert.AreEqual(slotsBefore, em.GetBuffer<DcTriggerSlot>(defender).Length);

            Object.Destroy(card);
        }

        private static DreamcatcherCard MakeCompositeLethalCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "calamity_test";
            card.type = CardType.Unit;
            card.axis = CardTargetAxis.All;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[]
            {
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.None },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.SelfBuffLethal,
                        magnitude = 100f,
                        duration = 6f,
                    },
                },
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 3 },
                    payload = new DcPayloadSpec { kind = DcPayloadKind.HeavyStrike, magnitude = 2f },
                },
                new DcMechanic
                {
                    trigger = new DcTriggerSpec { kind = DcTriggerKind.OnDeath },
                    payload = new DcPayloadSpec
                    {
                        kind = DcPayloadKind.SelfTileAoe,
                        magnitude = 400f,
                        tileRange = 2,
                    },
                },
            };
            return card;
        }

        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData unit)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, unit, out _))
                        return bridge.PlaceDefenderAs(x, y, unit);
            return false;
        }

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            var field = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var defenders = (System.Collections.IDictionary)field.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry entry in defenders)
            {
                var value = entry.Value;
                var entity = (Entity)value.GetType().GetField("Item1").GetValue(value);
                if (em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
