using System.Collections;
using System.Collections.Generic;
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

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-trigger-gates units 2·3 — 게이트 카드 2장의 e2e.
    // 궁지폭발: 게이트 밖(만피) 피격은 카운트되지 않고, 게이트 안(HP 20%)에서 2피격에
    //   자기 폭발이 인접 적에게 정확히 20 을 입힌다. 호스트=힐러(적 공격 출력 없음)라
    //   인접 더미의 유일한 데미지 소스가 폭발 — 수치 귀속이 결정론적.
    // 처형타: 대상 HP 25% 초과에선 평타, 이하에선 그 공격 피해가 ~2배.
    // 드레인(_running) 요구로 bridge.StartBattle() 후 검증한다 (실웨이브 공존 허용).
    public class DreamcatcherGateE2ETest
    {
        // duel-live-focus — 이 계측은 자기 판을 선언한다(라이브 풀이 바뀌어도 같은 판에서 잰다).
        private int _savedMap;
        [SetUp]
        public void PinMap() => _savedMap = BattleBridgeTestAccess.PinMap();

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            BattleBridgeTestAccess.RestoreMap(_savedMap);
        }

        [UnityTest]
        public IEnumerator CorneredBurst_CountsOnlyBelowGate_AndBlastsAdjacent()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var healer = cat.ById("healer");
            Assert.IsNotNull(healer, "healer defender data");

            bridge.SetDefenderPool(new[] { healer });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, healer), "place healer");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var host = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, host, "host resolved");

            int handle = bridge.ApplyDreamcatcherCardToUnit(host, MakeCorneredCard());
            Assert.GreaterOrEqual(handle, 0, "cornered attached (bake ok)");
            Assert.IsTrue(em.HasBuffer<DamagedCounter>(host), "DamagedCounter baked");

            bridge.StartBattle(); // 드레인(_running) 활성 — 폭발 실행 경로 요구
            yield return null;

            // 인접 더미 — 폭발 반경 1 안. 힐러는 공격 출력이 없어 이 더미를 때리지 않는다.
            var hostPos = em.GetComponentData<LocalTransform>(host).Position;
            var dummy = MakeEnemyDummy(em, hostPos + new float3(1f, 0f, 0f), 5000f);

            // ── 게이트 밖(만피): 2회 피격 프레임 — 카운트도 폭발도 없어야 한다.
            em.GetBuffer<IncomingDamage>(host).Add(new IncomingDamage { amount = 2f });
            yield return RunSeconds(0.3f);
            em.GetBuffer<IncomingDamage>(host).Add(new IncomingDamage { amount = 2f });
            yield return RunSeconds(0.5f);
            Assert.AreEqual(0, em.GetBuffer<DamagedCounter>(host)[0].counter,
                "만피 피격은 카운트되지 않는다 (카운트 게이트)");
            Assert.AreEqual(5000f, em.GetComponentData<Health>(dummy).value, 0.5f,
                "게이트 밖에서는 폭발 없음");

            // ── 게이트 안(HP 20%): 1피격 = 카운트 1, 2피격 = 발동(리셋) + 인접 −20.
            var hh = em.GetComponentData<Health>(host);
            em.SetComponentData(host, new Health { value = hh.max * 0.2f, max = hh.max });
            em.GetBuffer<IncomingDamage>(host).Add(new IncomingDamage { amount = 2f });
            yield return RunSeconds(0.3f);
            Assert.AreEqual(1, em.GetBuffer<DamagedCounter>(host)[0].counter,
                "게이트 안 피격은 카운트된다");
            float before = em.GetComponentData<Health>(dummy).value;
            em.GetBuffer<IncomingDamage>(host).Add(new IncomingDamage { amount = 2f });
            float t = 0f;
            while (t < 3f && em.GetComponentData<Health>(dummy).value > before - 1f)
            { t += Time.deltaTime; yield return null; }
            Assert.AreEqual(0, em.GetBuffer<DamagedCounter>(host)[0].counter,
                "period 2 도달 — 발동 후 카운터 리셋");
            Assert.AreEqual(before - 20f, em.GetComponentData<Health>(dummy).value, 0.5f,
                "궁지폭발 flat 20 이 인접 더미에 착탄 (유일 데미지 소스)");
        }

        [UnityTest]
        public IEnumerator ExecutionStrike_DoublesDamage_OnlyBelowQuarterHp()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var ranger = cat.ById("ranger");

            bridge.SetDefenderPool(new[] { ranger });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "place ranger");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var host = FindDefender(bridge, em);
            // 이 테스트는 최대 10초 동안 실웨이브와 공존한다. 선행 PlayMode 테스트와
            // 연속 실행해도 호스트 사망이 두 번째(게이트 안) 관측 구간을 끊지 않도록
            // 생존성은 테스트 입력으로 고정한다 — 검증 대상은 처형타의 출력 배율이다.
            em.SetComponentData(host, new Health { value = 1_000_000f, max = 1_000_000f });
            int handle = bridge.ApplyDreamcatcherCardToUnit(host, MakeExecutionCard());
            Assert.GreaterOrEqual(handle, 0, "execution attached (bake ok)");

            bridge.StartBattle(); // 원거리 공격은 투사체 드레인(_running) 필요
            yield return null;

            // 레인저 발밑 더미(최근접 = 타겟 고정). 60% HP = 게이트 밖.
            var hostPos = em.GetComponentData<LocalTransform>(host).Position;
            var dummy = MakeEnemyDummy(em, hostPos + new float3(0.05f, 0f, 0f), 5000f);
            em.SetComponentData(dummy, new Health { value = 3000f, max = 5000f });

            float baselineMax = 0f;
            yield return CollectMaxFrameDelta(em, dummy, 5f, v => baselineMax = v);
            Assert.Greater(baselineMax, 0f, "게이트 밖 평타가 관측돼야 한다");

            // 24% = 게이트 안 → 같은 공격의 피해가 ~2배.
            em.SetComponentData(dummy, new Health { value = 1200f, max = 5000f });
            float gatedMax = 0f;
            yield return CollectMaxFrameDelta(em, dummy, 5f, v => gatedMax = v);
            Assert.Greater(gatedMax, 0f, "게이트 안 강타가 관측돼야 한다");

            float ratio = gatedMax / baselineMax;
            Assert.That(ratio, Is.InRange(1.7f, 2.3f),
                $"처형타: 25% 이하 피해 ≈ 2배 (baseline {baselineMax:0.0}, gated {gatedMax:0.0}, ratio {ratio:0.00})");
        }

        private static IEnumerator CollectMaxFrameDelta(EntityManager em, Entity e, float seconds, System.Action<float> result)
        {
            float max = 0f, t = 0f;
            float prev = em.GetComponentData<Health>(e).value;
            while (t < seconds && em.Exists(e))
            {
                t += Time.deltaTime;
                yield return null;
                if (!em.Exists(e)) break;
                float cur = em.GetComponentData<Health>(e).value;
                float d = prev - cur;
                if (d > max) max = d;
                prev = cur;
            }
            result(max);
        }

        private static Entity MakeEnemyDummy(EntityManager em, float3 pos, float hp)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            em.AddComponentData(e, new Health { value = hp, max = hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddComponent<AttackUnitTag>(e);
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<Wassup.Battle.Effects.CcEffect>(e);
            em.AddBuffer<Wassup.Battle.Effects.StackModifierSlot>(e);
            return e;
        }

        private static DreamcatcherCard MakeCorneredCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            var aoeView = UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectileData>(
                UnityEditor.AssetDatabase.GUIDToAssetPath("1705ed345dda4014bc5b6019f1a84e77"));
            Assert.IsNotNull(aoeView, "AOE-view ProjectileData (작별 선물 재사용)");
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec
                {
                    kind = DcTriggerKind.OnDamagedN, period = 2,
                    gate = DcGateKind.HpBelow, gateSubject = DcGateSubject.Self, gateValue = 0.30f,
                },
                payload = new DcPayloadSpec { kind = DcPayloadKind.SelfTileAoe, magnitude = 20f, tileRange = 1, projectile = aoeView },
            }};
            return card;
        }

        private static DreamcatcherCard MakeExecutionCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec
                {
                    kind = DcTriggerKind.AttackN, period = 1,
                    gate = DcGateKind.HpBelow, gateSubject = DcGateSubject.EventTarget, gateValue = 0.25f,
                },
                payload = new DcPayloadSpec { kind = DcPayloadKind.HeavyStrike, magnitude = 2f },
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
            // map-diorama-stage US-004b — 이격(스트라이드 3) 배치: 열린 마당에서 연속 셀 배치가
            // 인접 오라류 효과(OnPlace 등)를 서로에게 옮겨 고정값 계측을 오염시켰다(프로브 실증).
            // 구 복도 맵의 «배치지가 흩어져 있던» 기하 의미론을 테스트 쪽에서 복원한다.
            for (int x = -24; x < 48; x += 3)
                for (int y = -24; y < 48; y += 3)
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
