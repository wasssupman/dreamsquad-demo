using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // boss-mamemo unit 2 — 적 실드 개통.
    //
    // 흡수 산식(ShieldMath)은 EditMode 가 이미 지킨다. 여기서 묻는 건 **배선**이다:
    // 적 스폰이 버퍼 쌍을 붙이는가 · 그 버퍼가 실제로 드레인·흡수되는가 · 오버헤드
    // 게이지가 적에게도 값을 내는가(적 분기가 리터럴 0f 였던 자리).
    //
    // arm 은 unit 3 이다 — 여기서는 unit 3 이 쓸 seam(IncomingShield append)을 직접 때린다.
    public class EnemyShieldTest
    {
        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnemySpawn_HasShieldBufferPair_AndAbsorbsBeforeHealth()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var enemy = SpawnEnemy(bridge, em, "Assets/_Project/Data/Enemies/Enemy_Tanker.asset");
            Assert.AreNotEqual(Entity.Null, enemy);

            // 쌍으로 붙어야 한다 — IncomingShield 드레인이 ShieldSlot 존재로 게이팅돼 있어
            // 한쪽만 있으면 부여가 영영 안 빠지고 버퍼가 무한 성장한다.
            Assert.IsTrue(em.HasBuffer<ShieldSlot>(enemy), "적에 ShieldSlot 부착");
            Assert.IsTrue(em.HasBuffer<IncomingShield>(enemy), "적에 IncomingShield 부착");

            float hp0 = em.GetComponentData<Health>(enemy).value;

            // 부여 → 다음 드레인에서 슬롯으로 병합된다.
            em.GetBuffer<IncomingShield>(enemy).Add(new IncomingShield { source = Entity.Null, amount = 50f });
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(0, em.GetBuffer<IncomingShield>(enemy).Length, "부여 버퍼가 드레인된다(무한 성장 아님)");
            Assert.AreEqual(50f, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(enemy)), 0.01f, "실드 50 적립");

            // 오버헤드 게이지 — 적 분기가 리터럴 0f 였던 자리(계약 9 정정).
            var m = typeof(BattleBridge).GetMethod("ShieldRatioOf", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, "ShieldRatioOf 헬퍼");
            var h = em.GetComponentData<Health>(enemy);
            float ratio = (float)m.Invoke(bridge, new object[] { enemy, h });
            Assert.Greater(ratio, 0f, "적 오버헤드 실드 비율이 0이 아니다");

            // 부분 흡수 — 체력은 안 깎인다.
            em.GetBuffer<IncomingDamage>(enemy).Add(new IncomingDamage { amount = 30f });
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(20f, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(enemy)), 0.01f, "실드가 30 흡수");
            Assert.AreEqual(hp0, em.GetComponentData<Health>(enemy).value, 0.01f, "완전 흡수 히트는 체력을 안 깎는다");

            // 관통 — 남은 20 을 넘긴 만큼만 체력으로 간다.
            em.GetBuffer<IncomingDamage>(enemy).Add(new IncomingDamage { amount = 30f });
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(0f, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(enemy)), 0.01f, "실드 소진");
            Assert.AreEqual(hp0 - 10f, em.GetComponentData<Health>(enemy).value, 0.51f, "관통분 10 만 체력에서 깎인다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static Entity SpawnEnemy(BattleBridge bridge, EntityManager em, string assetPath)
        {
            var unit = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackUnitData>(assetPath);
            Assert.IsNotNull(unit, assetPath);

            var bt = typeof(BattleBridge);
            var pendingType = bt.GetNestedType("PendingSpawnEntry", BindingFlags.NonPublic);
            var pending = System.Activator.CreateInstance(pendingType);
            pendingType.GetField("entry").SetValue(pending,
                new SpawnEntry { triggerTimeSec = 0f, unitType = unit, spawnIndex = 0 });
            pendingType.GetField("laneIndex").SetValue(pending, 0);

            var q = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            var before = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var known = new System.Collections.Generic.HashSet<Entity>();
            for (int i = 0; i < before.Length; i++) known.Add(before[i]);
            before.Dispose();

            bt.GetMethod("SpawnUnit", BindingFlags.NonPublic | BindingFlags.Instance)
              .Invoke(bridge, new[] { pending });

            var after = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            Entity spawned = Entity.Null;
            for (int i = 0; i < after.Length; i++) if (!known.Contains(after[i])) spawned = after[i];
            after.Dispose();
            return spawned;
        }
    }
}
