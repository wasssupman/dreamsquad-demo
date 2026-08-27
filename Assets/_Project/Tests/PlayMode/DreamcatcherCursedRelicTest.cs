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

        // skill-layer-migration unit 4a — **부착 seam.** 마지막 불꽃이 켜지는지, 그리고
        // **부착이 반환될 때 이미 켜져 있는지**를 묻는다.
        //
        // ⚠ 두 번째가 이 seam 의 존재 이유다. 다른 다섯 seam 은 다음 틱에 드레인해도 되지만
        // 부착은 동기 트랜잭션이라(가부와 코스트 환불을 그 호출이 결정한다) 프레임을 기다리면
        // 결정 뒤에 쓰기가 도착한다. 그래서 `yield` 없이 곧바로 단언한다 — 이 단언이
        // 초록이면 실행이 콜스택 안에서 끝났다는 뜻이고, 빨개지면 seam 이 비동기로 샌 것이다.
        [UnityTest]
        public IEnumerator LastFlame_IsAlreadyBurning_WhenAttachReturns()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gameManager = Object.FindObjectOfType<GameManager>();
            var guardian = FindDefenderCatalog().ById("guardian");
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
            Assert.IsFalse(em.HasComponent<LethalTimer>(defender), "전제: 아직 시한부가 아니다");

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();
            const float Seconds = 6f;
            Assert.GreaterOrEqual(
                bridge.ApplyDreamcatcherCardToUnit(defender, MakeLastFlameCard(Seconds)), 0,
                "마지막 불꽃 부착");

            // ⚠ **여기에 `yield` 가 없다.** 그것이 단언의 내용이다.
            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Immediate), 1,
                "부착 seam 이 concrete 를 안 거쳤다 — 라우팅이 조용히 죽었다");
            Assert.IsTrue(em.HasComponent<LethalTimer>(defender),
                "부착이 반환됐는데 시한부가 아직 아니다 — 실행이 콜스택 밖으로 샜다");
            Assert.AreEqual(Seconds, em.GetComponentData<LethalTimer>(defender).remaining, 1e-3f,
                "저작한 초가 그대로 타이머에 실려야 한다");

            // 공속 버프는 모디파이어 시계가 소유한다 — 그쪽은 다음 틱에 집계된다.
            float before = em.GetComponentData<ModifierStats>(defender).attackSpeedMul;
            for (int i = 0; i < 6; i++) yield return null;
            Assert.Greater(em.GetComponentData<ModifierStats>(defender).attackSpeedMul, before,
                "짧게 강해지는 것이 이 카드의 절반이다 — 공속이 안 올랐다");
        }

        private static DreamcatcherCard MakeLastFlameCard(float seconds)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.None },
                payload = new DcPayloadSpec
                {
                    kind = DcPayloadKind.SelfBuffLethal, magnitude = 80f, duration = seconds,
                },
            }};
            return card;
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
