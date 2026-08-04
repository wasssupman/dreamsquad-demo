using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-new-abilities unit 3 — 온-히트 발동 통합 검증(two-track review test-gap).
    // frost_arrow: AttackN(3) 마다 대상에 CcEffect(Stun) 부여(EnemyCc 채널 → CcApplySystem).
    // ember_bite: AttackN(3) 마다 대상에 StackModifierSlot(Bleed) 부여(StackModifier 채널)
    //             + Bleed ThresholdRule(ApplyDot) 이 실제로 배선돼 있는지.
    // 멜리 guardian(직접 AttackSystem RESOLVE)로 구동. MapDcCc/MapDcStack 번역도 커버.
    public class DreamcatcherOnHitTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator FrostArrow_EveryThirdAttack_StunsTarget()
        {
            yield return Setup();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var (bridge, defender) = PlaceGuardian(em);

            var card = MakeUnitCard(new DcMechanic
            {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 3 },
                payload = new DcPayloadSpec { kind = DcPayloadKind.ApplyCcToTarget, ccKind = DcCcKind.Stun, duration = 0.6f },
            });
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(defender, card), 0, "frost attached");

            var enemy = SpawnDummyEnemy(em, defender, withCcBuffer: true);

            // 여러 공격 사이클 동안 대상이 Stun CcEffect 를 얻는 프레임이 있는지.
            bool sawStun = false;
            float t = 0f;
            while (t < 8f && !sawStun)
            {
                t += Time.deltaTime;
                if (em.Exists(enemy) && em.HasBuffer<CcEffect>(enemy))
                {
                    var cc = em.GetBuffer<CcEffect>(enemy);
                    for (int i = 0; i < cc.Length; i++)
                        if (cc[i].kind == CcKind.Stun && cc[i].remainingTime > 0f) { sawStun = true; break; }
                }
                yield return null;
            }
            if (em.Exists(enemy)) em.DestroyEntity(enemy);
            Assert.IsTrue(sawStun, "frost_arrow: N번째 공격에 대상이 Stun(CcEffect) 걸려야 함");
        }

        [UnityTest]
        public IEnumerator EmberBite_EveryThirdAttack_AppliesBleedStack_WithDotRule()
        {
            yield return Setup();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var (bridge, defender) = PlaceGuardian(em);

            // Bleed DoT 규칙이 배선돼 있어야 ember 가 실효 — 회귀 가드.
            // (unit 11 머지 2: 규칙 소유가 sim 레지스트리로 넘어갔다.)
            Assert.Greater(StackThresholdRegistry.Get(StackKind.Bleed).Length, 0,
                "Bleed ThresholdRule(ApplyDot) 이 StackThresholdRegistry 에 등록돼 있어야 함");

            var card = MakeUnitCard(new DcMechanic
            {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.AttackN, period = 3 },
                payload = new DcPayloadSpec { kind = DcPayloadKind.ApplyStackToTarget, stackKind = DcStackKind.Bleed, magnitude = 1f, duration = 4f, tileRange = 0 },
            });
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(defender, card), 0, "ember attached");

            var enemy = SpawnDummyEnemy(em, defender, withCcBuffer: false);

            bool sawBleed = false;
            float t = 0f;
            while (t < 8f && !sawBleed)
            {
                t += Time.deltaTime;
                if (em.Exists(enemy) && em.HasBuffer<StackModifierSlot>(enemy))
                {
                    var st = em.GetBuffer<StackModifierSlot>(enemy);
                    for (int i = 0; i < st.Length; i++)
                        if (st[i].kind == StackKind.Bleed) { sawBleed = true; break; }
                }
                yield return null;
            }
            if (em.Exists(enemy)) em.DestroyEntity(enemy);
            Assert.IsTrue(sawBleed, "ember_bite: N번째 공격에 대상이 Bleed StackModifierSlot 을 얻어야 함");
        }

        // ── helpers ──────────────────────────────────────────────────────────
        private static IEnumerator Setup()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static (BattleBridge, Entity) PlaceGuardian(EntityManager em)
        {
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var guardian = FindDefenderCatalog().ById("guardian"); // 멜리 → 직접 IncomingDamage 경로
            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");
            return (bridge, defender);
        }

        private static Entity SpawnDummyEnemy(EntityManager em, Entity defender, bool withCcBuffer)
        {
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            const float Hp = 1_000_000f; // 죽지 않게 — 공격이 계속 이어지도록
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(defPos + new float3(0.05f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = Hp, max = Hp });
            em.AddComponentData(enemy, new FactionTag { value = Faction.Enemy });
            em.AddBuffer<IncomingDamage>(enemy);
            if (withCcBuffer) em.AddBuffer<CcEffect>(enemy); // CcApplySystem 소비처(실적 아키타입 모사)
            return enemy;
        }

        private static DreamcatcherCard MakeUnitCard(DcMechanic mech)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { mech };
            return card;
        }

        private static DefenderCatalog FindDefenderCatalog()
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

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var entity = (Entity)val.GetType().GetField("Item1").GetValue(val);
                if (em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
