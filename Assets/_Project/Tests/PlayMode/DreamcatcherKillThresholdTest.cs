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
    // dreamcatcher-kill-and-threshold unit 3 — Spec B 두 능력의 실전투 통합 검증.
    // last_stand: HP 임계 돌파 시 자기 공격력 버프가 실제 ModifierStats 에 올라오는가.
    // devouring_craving: 적 처치 시 killer(=공격자) 에게 공속 버프가 붙는가(OnKill+킬귀속).
    // 코어 IncomingDamage.source 수술이 기존 데미지 경로를 깨지 않는지도 여기서 커버.
    public class DreamcatcherKillThresholdTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // last_stand — HealthThreshold(fraction=0.7 → HP 30% 이하) × SelfStatBuff(공격력 +30%, 영구).
        [UnityTest]
        public IEnumerator LastStand_BelowHpThreshold_BuffsAttackDamage()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
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

            int handle = bridge.ApplyDreamcatcherCardToUnit(defender, MakeLastStandCard());
            Assert.GreaterOrEqual(handle, 0, "last_stand attached (bake ok, maxHp snapshot)");

            // on-place 버프 감쇠 → damageMul baseline 안정화.
            yield return RunSeconds(8f);
            float preMul = em.GetComponentData<ModifierStats>(defender).damageMul;

            // 30% 경계 돌파(생존 유지) — HP 를 25% 로. 적이 없어 HP 는 고정.
            var hp = em.GetComponentData<Health>(defender);
            em.SetComponentData(defender, new Health { value = hp.max * 0.25f, max = hp.max });
            yield return RunSeconds(1.5f); // HealthThresholdSystem fire → ModifierApply → Aggregate
            float postMul = em.GetComponentData<ModifierStats>(defender).damageMul;

            Assert.Greater(postMul, preMul * 1.2f,
                $"last_stand: HP<30% 돌파 후 damageMul 상승 예상 ({preMul:0.00}->{postMul:0.00})");
        }

        // devouring_craving — OnKill × SelfStatBuff(공속 +8%, 4s). 처치 시 killer 에 부착.
        [UnityTest]
        public IEnumerator DevouringCraving_OnKill_BuffsAttackSpeed()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var guardian = cat.ById("guardian"); // melee → 직접 IncomingDamage(source=attacker)

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

            int handle = bridge.ApplyDreamcatcherCardToUnit(defender, MakeDevouringCard());
            Assert.GreaterOrEqual(handle, 0, "devouring attached");

            yield return RunSeconds(8f); // on-place 감쇠
            float preAs = em.GetComponentData<ModifierStats>(defender).attackSpeedMul;

            // 약한 적(HP 1) 을 사거리 안에 → guardian 이 한 방에 처치 → OnKill 발동.
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(defPos + new float3(0.05f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = 1f, max = 1f });
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(enemy);
            // 스킬 레이어는 두 풀 안의 엔티티만 다룬다(unit 3a 함정) — 실적 아키타입 모사.
            em.AddComponent<Wassup.Battle.Units.AttackUnitTag>(enemy);
            BattleBridgeTestAccess.AttachSimEntityId(bridge, enemy);

            // ⚠ **죽음 seam 의 첫 증인.** 이 단언이 없으면 라우팅이 끊기고 legacy arm 이
            // 대신 처리해도 아래 결과 단언이 초록이다.
            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();

            float t = 0f;
            while (t < 5f && em.Exists(enemy) && em.GetComponentData<Health>(enemy).value > 0f)
            { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(!em.Exists(enemy) || em.GetComponentData<Health>(enemy).value <= 0f,
                "guardian 이 약한 적을 처치");

            for (int i = 0; i < 4; i++) yield return null; // ModifierApply/Aggregate 정착
            float postAs = em.GetComponentData<ModifierStats>(defender).attackSpeedMul;

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Death), 1,
                "죽음 seam 이 concrete 를 안 거쳤다 — legacy arm 이 대신 처리했다면 아래 단언은 "
                + "라우팅이 끊겨도 초록이 된다");
            Assert.Greater(postAs, preAs * 1.03f,
                $"devouring: 처치 후 attackSpeedMul 상승 예상 ({preAs:0.000}->{postAs:0.000})");
        }

        private static DreamcatcherCard MakeLastStandCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.HealthThreshold, fraction = 0.7f },
                payload = new DcPayloadSpec { kind = DcPayloadKind.SelfStatBuff, buffStat = CardBuffKind.AttackDamage, magnitude = 30f, duration = 0f },
            }};
            return card;
        }

        private static DreamcatcherCard MakeDevouringCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnKill },
                payload = new DcPayloadSpec { kind = DcPayloadKind.SelfStatBuff, buffStat = CardBuffKind.AttackSpeed, magnitude = 8f, duration = 4f },
            }};
            return card;
        }

        private static IEnumerator RunSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
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
