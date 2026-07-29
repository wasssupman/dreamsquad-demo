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
    // bleed-fighter-defender unit 0 — 공격 `outputs` 의 ApplyStack 분기(AttackSystem RESOLVE)를
    // 실사용 유닛이 생기기 **전에** 고정한다. 지금까지 이 분기는 코드만 있고 소비 유닛이 0이었고,
    // ModifierFrameworkTests 의 대응 케이스는 "full combat world 필요" 사유로 Ignored 스텁이다.
    //
    // DreamcatcherOnHitTest(ember_bite)와 헷갈리지 말 것: 그쪽은 **카드 payload**
    // (DcPayloadKind.ApplyStackToTarget) 경로이고, 여기는 **유닛 outputs** 경로다 — 같은 큐로
    // 합류하지만 enqueue 하는 코드가 서로 다르다.
    //
    // 관측 범위는 ember 테스트보다 반 발짝 넓다: 스택 슬롯 부여에서 멈추지 않고 Bleed 임계가
    // 발화해 DoT 까지 붙는 것을 본다(임계 규칙 배선이 죽으면 스택만 쌓이고 아무 일도 안 일어난다).
    public class DefenderApplyStackOutputTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator ApplyStackOutput_AppliesBleedStack_ThenDotFires()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            // Bleed 임계 규칙이 배선돼 있어야 DoT 단언이 의미를 갖는다 — 회귀 가드.
            Assert.Greater(BattleBridge.GetStackThresholds(StackKind.Bleed).Length, 0,
                "Bleed ThresholdRule(ApplyDot) 이 BattleBridge 에 배선돼 있어야 함");

            // 카탈로그 가디언을 복제해 outputs 만 갈아끼운다 — 유닛 에셋(unit 2)에 선행 의존하지
            // 않으면서 멜리(직접 IncomingDamage) 경로를 그대로 쓴다.
            var catalog = FindDefenderCatalog();
            var bleeder = Object.Instantiate(catalog.ById("guardian"));
            bleeder.id = "test_bleeder";
            bleeder.attackTargetCount = 1;
            bleeder.outputs = new[]
            {
                new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 5f },
                new AttackOutput
                {
                    kind          = AttackOutputKind.ApplyStack,
                    // 한 방에 임계까지 채운다. Bleed 는 **누적형**(atStack 5 Consume)이라
                    // 1스택만 주면 임계에 못 닿아 DoT 가 안 나온다 — 이 테스트가 보려는 건
                    // "outputs 의 ApplyStack 이 큐로 나가는가"이지 누적 속도가 아니다.
                    magnitude     = 5f,      // countDelta = 임계치
                    duration      = 2f,      // perAppDuration
                    stackKind     = StackKind.Bleed,
                    stackMaxStack = 5,
                },
            };

            bridge.SetDefenderPool(new[] { bleeder });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            Assert.IsTrue(PlaceFirstValid(bridge, bleeder), "place bleeder");

            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            var enemy = SpawnDummyEnemy(em, defender);

            bool sawBleed = false;
            bool sawDot = false;
            float t = 0f;
            while (t < 8f && !sawDot)
            {
                t += Time.deltaTime;
                if (em.Exists(enemy))
                {
                    if (!sawBleed && em.HasBuffer<StackModifierSlot>(enemy))
                    {
                        var st = em.GetBuffer<StackModifierSlot>(enemy);
                        for (int i = 0; i < st.Length; i++)
                            if (st[i].kind == StackKind.Bleed) { sawBleed = true; break; }
                    }
                    if (em.HasBuffer<DotEffect>(enemy))
                    {
                        var cc = em.GetBuffer<DotEffect>(enemy);
                        for (int i = 0; i < cc.Length; i++)
                            if (cc[i].remainingTime > 0f) { sawDot = true; break; }
                    }
                }
                yield return null;
            }
            if (em.Exists(enemy)) em.DestroyEntity(enemy);
            Object.Destroy(bleeder);

            Assert.IsTrue(sawBleed, "outputs 의 ApplyStack 이 대상에 Bleed StackModifierSlot 을 부여해야 함");
            Assert.IsTrue(sawDot, "Bleed 임계가 발화해 DoT(DotEffect) 까지 이어져야 함");
        }

        // ── helpers (DreamcatcherOnHitTest 와 같은 형태 — 그쪽은 카드 경로, 여기는 outputs 경로) ──
        private static Entity SpawnDummyEnemy(EntityManager em, Entity defender)
        {
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            const float Hp = 1_000_000f; // 죽지 않게 — 공격이 계속 이어지도록
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(defPos + new float3(0.05f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = Hp, max = Hp });
            em.AddComponentData(enemy, new FactionTag { value = Faction.Enemy });
            em.AddBuffer<IncomingDamage>(enemy);
            em.AddBuffer<CcEffect>(enemy);
            em.AddBuffer<DotEffect>(enemy); // 임계 파생 DoT 의 소비처(dot-effect-extraction unit 0)
            return enemy;
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
