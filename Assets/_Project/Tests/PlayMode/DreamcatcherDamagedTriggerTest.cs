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
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-trigger-gates unit 0 — OnDamagedN 경로 회귀 핀 (리팩터 전 선행 작성).
    // 가시반격 계열(OnDamagedN×NextAttackDoubleFire)의 seam 계약을 고정한다:
    // N번째 피격 프레임에 DamagedCounter 가 발동해 NextAttackDoubleFire charge 가
    // 부착되는가. DamagedCounter 위드닝(payload 개통) 전/후로 이 테스트가 동일하게
    // green 이어야 한다 — bake 경로·counter 소유(Units)·charge handoff 불변 검증.
    public class DreamcatcherDamagedTriggerTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator OnDamagedN_SecondDamagedFrame_GrantsDoubleFireCharge()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var guardian = cat.ById("guardian");

            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            int handle = bridge.ApplyDreamcatcherCardToUnit(defender, MakeThornCard(period: 2));
            Assert.GreaterOrEqual(handle, 0, "OnDamagedN×NextAttackDoubleFire attached (bake ok)");
            yield return null;

            // 1번째 피격 프레임 — 아직 발동 없음 (period 2).
            em.GetBuffer<IncomingDamage>(defender).Add(new IncomingDamage { amount = 3f });
            yield return null; yield return null;
            Assert.IsFalse(em.HasComponent<Wassup.Battle.Combat.NextAttackDoubleFire>(defender),
                "1회 피격으로는 발동하지 않는다 (period 2)");

            // 2번째 피격 프레임 — 발동 → 더블파이어 charge 부착.
            em.GetBuffer<IncomingDamage>(defender).Add(new IncomingDamage { amount = 3f });
            float t = 0f;
            while (t < 2f && !em.HasComponent<Wassup.Battle.Combat.NextAttackDoubleFire>(defender))
            { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(em.HasComponent<Wassup.Battle.Combat.NextAttackDoubleFire>(defender),
                "2번째 피격 프레임에 NextAttackDoubleFire charge 부착");
        }

        private static DreamcatcherCard MakeThornCard(int period)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnDamagedN, period = period },
                payload = new DcPayloadSpec { kind = DcPayloadKind.NextAttackDoubleFire },
            }};
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
