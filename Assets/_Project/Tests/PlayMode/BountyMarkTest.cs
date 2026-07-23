using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Effects;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // subconscious-curse-expansion unit 2 — 살찌운 제물(BountyMark) 메커니즘 검증.
    // ① 표식: AwakeningReward ×3 베이크 + 받는 피해 −30% 실측 + 이중 표식 거절
    // ② 처치: 배율된 보상 지급 + EnemyGone(회수 키) / 유출: 무보상 + EnemyGone
    //    — 드레인은 _running 게이트 안이라 리플렉션 직접 호출(기존 테스트 관례).
    public class BountyMarkTest
    {
        private BattleBridge _bridge;
        private EntityManager _em;
        private Entity _defender;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mark_MultipliesRewardAndReducesDamage()
        {
            yield return Setup();

            var enemy = CreateEnemy(FarPos(), reward: 5);
            var card = MakeBountyCard();

            Assert.AreEqual(0, _bridge.ApplyBountyMark(enemy, card), "표식 성공(무회수 핸들 0)");
            Assert.AreEqual(15, _em.GetComponentData<AwakeningReward>(enemy).value, "보상 ×3 베이크(5→15)");

            // 이중 표식 — 이중 배율 방지 사전검증.
            LogAssert.Expect(LogType.Warning,
                "[BattleBridge] ApplyBountyMark('bounty_test'): enemy already marked — not marked.");
            Assert.AreEqual(-1, _bridge.ApplyBountyMark(enemy, card), "이중 표식 거절");
            Assert.AreEqual(15, _em.GetComponentData<AwakeningReward>(enemy).value, "이중 배율 없음(15 유지)");

            for (int i = 0; i < 4; i++) yield return null; // ModifierApply/Aggregate 정착

            // 받는 피해 −30% 실측: 10 데미지 → 실수령 7.
            _em.GetBuffer<IncomingDamage>(enemy).Add(new IncomingDamage { amount = 10f });
            for (int i = 0; i < 3; i++) yield return null;
            float hp = _em.GetComponentData<Health>(enemy).value;
            Assert.AreEqual(93f, hp, 0.5f, "표식 악몽 실수령 피해 7(=10×0.7)");

            Object.Destroy(card);
        }

        [UnityTest]
        public IEnumerator KillAndLeak_FireEnemyGoneWithBakedRewardSplit()
        {
            yield return Setup();

            var enemyA = CreateEnemy(FarPos(), reward: 5);           // 처치 경로
            var enemyB = CreateEnemy(FarPos() + new float3(2f, 0f, 0f), reward: 7); // 유출 경로
            var card = MakeBountyCard();
            Assert.AreEqual(0, _bridge.ApplyBountyMark(enemyA, card), "A 표식");
            Assert.AreEqual(0, _bridge.ApplyBountyMark(enemyB, card), "B 표식(서로 다른 적은 각각 1개 허용)");

            var awakened = new List<int>();
            var gone = new List<Entity>();
            System.Action<int, UnityEngine.Vector3> onAwaken = (r, pos) => awakened.Add(r);
            System.Action<Entity> onGone = e => gone.Add(e);
            _bridge.EnemyKilledAwakening += onAwaken;
            _bridge.EnemyGone += onGone;
            try
            {
                // 처치: 치명 피해 → DamageApplicationSystem 이 배율된 baked 보상 + entity 를
                // EnemyKilledEvent 에 스탬프 → 드레인(리플렉션 호출)이 보상 relay + 회수 알림.
                _em.GetBuffer<IncomingDamage>(enemyA).Add(new IncomingDamage { amount = 9999f });
                float t = 0f;
                while (t < 3f && _em.Exists(enemyA) && _em.GetComponentData<Health>(enemyA).value > 0f)
                { t += Time.deltaTime; yield return null; }
                for (int i = 0; i < 3; i++) yield return null;

                Invoke(_bridge, "DrainEnemyKilledEvents");
                Assert.Contains(15, awakened, "처치 보상 = 배율된 baked 값(5×3)");
                Assert.Contains(enemyA, gone, "처치 → EnemyGone(회수 키)");

                // 유출: GoalReachedEvent 를 큐에 직접 주입(Movement 골인 대체) → 드레인.
                int awakeCountBefore = awakened.Count;
                EnqueueGoalReached(_bridge, enemyB);
                Invoke(_bridge, "DrainGoalEvents");
                Assert.Contains(enemyB, gone, "유출 → EnemyGone(무보상 회수 키)");
                Assert.AreEqual(awakeCountBefore, awakened.Count, "유출은 각성 보상 없음");
            }
            finally
            {
                _bridge.EnemyKilledAwakening -= onAwaken;
                _bridge.EnemyGone -= onGone;
                Object.Destroy(card);
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            _bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindCatalog();
            Assert.IsNotNull(_bridge, "BattleBridge present");
            Assert.IsNotNull(gm, "GameManager present");
            Assert.IsNotNull(cat, "DefenderCatalog present");

            var guardian = cat.ById("guardian");
            _bridge.SetDefenderPool(new[] { guardian });
            _bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(_bridge, guardian), "place guardian");
            yield return null;

            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _defender = FindDefender(_bridge, _em);
            Assert.AreNotEqual(Entity.Null, _defender, "defender resolved");
        }

        // guardian 사거리 밖(+30 유닛) — 테스트 중 교전 개입 차단.
        private float3 FarPos()
            => _em.GetComponentData<LocalTransform>(_defender).Position + new float3(30f, 0f, 30f);

        // 스폰 베이크 최소 미러(SpawnUnit 선례): 태그/체력/보상/버퍼 + ModifierStats
        // (identity, SpawnUnit:4654 와 동일 — 집계 시스템이 기존 보유를 요구한다).
        private Entity CreateEnemy(float3 pos, int reward)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponent<AttackUnitTag>(e);
            _em.AddComponentData(e, new Health { value = 100f, max = 100f });
            _em.AddComponentData(e, new FactionTag { value = Faction.Enemy });
            _em.AddComponentData(e, new AwakeningReward { value = reward });
            _em.AddBuffer<IncomingDamage>(e);
            _em.AddBuffer<CcEffect>(e);
            _em.AddComponentData(e, new ModifierStats
            {
                damageMul = 1f,
                attackSpeedMul = 1f,
                dmgTakenMul = 1f,
                regenPerSec = 0f,
                moveSpeedMul = 1f,
                damageVsCcMul = 1f,
                maxHealthMul = 1f,
            });
            _em.AddComponent<ModifierStatsDirty>(e);
            _em.SetComponentEnabled<ModifierStatsDirty>(e, false);
            return e;
        }

        private static DreamcatcherCard MakeBountyCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.id = "bounty_test";
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
                        kind = DcPayloadKind.BountyMark,
                        magnitude = 3f,
                        tileRange = 30,
                    },
                },
            };
            return card;
        }

        private static void Invoke(BattleBridge bridge, string method)
            => typeof(BattleBridge).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(bridge, null);

        private static void EnqueueGoalReached(BattleBridge bridge, Entity entity)
        {
            var field = typeof(BattleBridge).GetField("_goalEventQueue", BindingFlags.NonPublic | BindingFlags.Instance);
            object queue = field.GetValue(bridge); // NativeQueue 는 내부 포인터 핸들 — boxed 사본도 같은 큐
            queue.GetType().GetMethod("Enqueue").Invoke(queue, new object[] { new GoalReachedEvent { entity = entity } });
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
