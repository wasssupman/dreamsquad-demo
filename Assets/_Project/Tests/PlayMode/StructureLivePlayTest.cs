using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // battle-structures 스펙 종료 Play 검증 — 거점이 라이브 판에 실제로 선다.
    //
    // 저작물: MapDocument_Test(dev 슬롯, 30×30 전면 Walk) 에 적 본능
    // (Structure_TestInstinct — cannon_base_red 프랍, 포탑) 1기를 (15,15) 에 저작해 뒀다.
    // 이 테스트가 재는 것: 부팅 → 스폰(SO HP·3×3 차단) → 뷰 프랍 → 적이 블로커를
    // 우회해 여전히 골에 도달(연결성 생존). 본능의 발사 자체는 EditMode 가 실
    // AttackSystem 으로 이미 고정했다(ArmedInstinct_FiresProjectileRequest...).
    public class StructureLivePlayTest
    {
        private const float TimeoutSec = 90f;
        private int _savedIndex = -1;

        [SetUp]
        public void SetUp()
        {
            _savedIndex = DevMapOverride.Index;
            DevMapOverride.Index = 6;   // 메인 6장 뒤 dev[0] = MapDocument_Test
        }

        [TearDown]
        public void TearDown()
        {
            DevMapOverride.Index = _savedIndex;   // PlayerPrefs 는 머신 상태 — 반드시 원복
            LogAssert.ignoreFailingMessages = false;
        }

        private static object GetField(object target, string name)
        {
            var fi = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"Field '{name}' not found");
            return fi.GetValue(target);
        }

        [UnityTest]
        public IEnumerator Structures_BootOnDevMap_SpawnBlockAndSurviveConnectivity()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            bridge.BeginPlacement();
            yield return null;
            bridge.StartBattle();
            yield return null;

            var em = (EntityManager)GetField(bridge, "_em");

            // ── 스폰: 적 본능이 (15,15) 에 SO HP·3×3 차단으로 선다 ──
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<StructureTag>()))
            {
                var entities = q.ToEntityArray(Allocator.Temp);
                Entity instinct = Entity.Null;
                foreach (var e in entities)
                {
                    var st = em.GetComponentData<StructureTag>(e);
                    if (st.faction == Faction.EnemyInstinct) { instinct = e; break; }
                }
                entities.Dispose();

                Assert.AreNotEqual(Entity.Null, instinct, "저작된 적 본능이 라이브 판에 스폰된다");
                Assert.AreEqual(new int2(15, 15), em.GetComponentData<StructureTag>(instinct).cell);
                Assert.AreEqual(500f, em.GetComponentData<Health>(instinct).value, 1e-3f, "HP 는 SO(500)에서");
                Assert.AreEqual(9,
                    em.GetBuffer<Wassup.Battle.Effects.BlockingHazardCellsBuffer>(instinct).Length,
                    "3×3 본체 통행 차단");
                Assert.IsTrue(em.HasComponent<Wassup.Battle.Combat.AttackState>(instinct),
                    "공격 저작(damage 10 + fireball) → AttackState 베이크");
            }

            // ── 뷰: 프랍 인스턴스(cannon_base_red)가 브리지 아래 선다 ──
            bool viewFound = false;
            foreach (Transform child in bridge.transform)
                if (child.name.StartsWith("Structure_")) { viewFound = true; break; }
            Assert.IsTrue(viewFound, "SO.viewPrefab 프랍이 셀 중심에 인스턴스된다");

            // ── 연결성 생존: 3×3 블로커를 우회해 적이 여전히 골에 도달한다 ──
            // 디펜더 0 → 적이 골 타워를 공성해 안정도가 준다(= 도달의 관측치).
            for (int i = 0; i < 20 && bridge.NextWaveHasNext; i++) bridge.ForceNextWave();
            int startStability = bridge.GoalStabilityCurrent;
            float start = Time.unscaledTime;
            while (bridge.GoalStabilityCurrent >= startStability)
            {
                Assert.Less(Time.unscaledTime - start, TimeoutSec,
                    "적이 골에 도달하지 못한다 — 본능 3×3 블로커가 경로를 끊었을 수 있다(연결성 회귀)");
                yield return null;
            }
        }
    }
}
