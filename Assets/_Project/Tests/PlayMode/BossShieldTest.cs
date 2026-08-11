using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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
    // boss-mamemo unit 3 — 꿈의 장막(경계 × 자기 실드) · 악몽의 가호(주기 × 주변 호위 실드).
    //
    // 두 능력이 **같은 payload kind** 라서, 이 테스트가 실제로 지키는 것은 둘이 서로를 망치지
    // 않는다는 것이다: ShieldMath 는 source 를 병합 키로 쓰므로 가호가 host 를 포함하면 장막의
    // 잔량을 매 주기 재충전해 「경계에 생기는 벽」이 「상시 실드」로 붕괴한다.
    public class BossShieldTest
    {
        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        // ② 꿈의 장막 — 체력 경계를 넘으면 자기 실드가 붙는다.
        [UnityTest]
        public IEnumerator DreamVale_ShieldsSelf_WhenHealthCrossesBoundary()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            var boss = SpawnMamemo(bridge, em);
            float amount = SelfShieldAmount(em, boss);
            Assert.Greater(amount, 0f, "꿈의 장막 슬롯이 베이크됐다 (tileRange 0 = 자기)");
            Assert.AreEqual(0f, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(boss)), 0.01f, "시작 시 실드 없음");

            // 경계 아래로 떨어뜨린다 — fraction 0.34 → 첫 경계 = maxHp × 0.66.
            var h = em.GetComponentData<Health>(boss);
            float toBoundary = h.value - (h.max * 0.66f) + 5f;
            em.GetBuffer<IncomingDamage>(boss).Add(new IncomingDamage { amount = toBoundary });

            bool shielded = false;
            for (int i = 0; i < 10 && !shielded; i++)
            {
                yield return null;
                shielded = ShieldMath.Sum(em.GetBuffer<ShieldSlot>(boss)) > 0f;
            }
            Assert.IsTrue(shielded, "경계 관통 후 자기 실드가 붙는다");
            Assert.AreEqual(amount, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(boss)), 0.51f, "저작된 실드량");
        }

        // ③ 악몽의 가호 — 주변 호위는 받고, **마메모 자신은 못 받는다**.
        // host 제외가 깨지면 장막(②)과 슬롯을 공유해 상시 실드가 된다.
        [UnityTest]
        public IEnumerator NightWard_ShieldsEscorts_ButNotTheBossItself()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;

            var boss = SpawnMamemo(bridge, em);
            var escort = SpawnEnemy(bridge, em, "Assets/_Project/Data/Enemies/Enemy_Basic.asset");
            Assert.AreNotEqual(Entity.Null, escort);

            // 가호를 즉발로 만든다. 반경·실드량은 에셋 값 그대로 — 저작이 틀리면 빨개진다.
            int wardRange = 0; float wardAmount = 0f;
            var slots = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss);
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s.payload != DcPayloadKind.GrantShield || s.trigger != DcTriggerKind.PeriodicTimer) continue;
                wardRange = s.tileRange; wardAmount = s.magnitude;
                s.periodSeconds = 0.01f; s.elapsed = 0f;
                slots[i] = s;
            }
            Assert.Greater(wardRange, 0, "악몽의 가호 슬롯이 베이크됐다 (tileRange>0 = 반경 확산)");
            Assert.Greater(wardAmount, 0f, "실드량 저작됨");

            // 호위를 반경 안으로. 마메모는 만피라 ②(경계)는 발동하지 않는다 → host 제외가 고립 검증된다.
            float tile = ResolveTileSize(em);
            float3 bossPos = em.GetComponentData<LocalTransform>(boss).Position;
            MoveTo(em, escort, bossPos + new float3(tile, 0f, 0f));

            bool escortShielded = false;
            for (int i = 0; i < 12 && !escortShielded; i++)
            {
                yield return null;
                escortShielded = ShieldMath.Sum(em.GetBuffer<ShieldSlot>(escort)) > 0f;
            }
            Assert.IsTrue(escortShielded, "반경 안 호위가 실드를 받는다");
            Assert.AreEqual(wardAmount, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(escort)), 0.51f, "저작된 실드량");
            Assert.AreEqual(0f, ShieldMath.Sum(em.GetBuffer<ShieldSlot>(boss)), 0.01f,
                "마메모 자신은 가호를 못 받는다 — 받으면 꿈의 장막과 슬롯을 공유해 상시 실드가 된다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static IEnumerator LoadBattle()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static float SelfShieldAmount(EntityManager em, Entity boss)
        {
            var slots = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].payload == DcPayloadKind.GrantShield
                    && slots[i].trigger == DcTriggerKind.HealthThreshold)
                    return slots[i].magnitude;
            return 0f;
        }

        private static float ResolveTileSize(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<Wassup.Battle.Effects.FlowFieldSingleton>());
            if (q.CalculateEntityCount() == 0) return 1f;
            return q.GetSingleton<Wassup.Battle.Effects.FlowFieldSingleton>().tileSize;
        }

        private static void MoveTo(EntityManager em, Entity e, float3 pos)
        {
            var t = em.GetComponentData<LocalTransform>(e);
            t.Position = pos;
            em.SetComponentData(e, t);
        }

        private static Entity SpawnMamemo(BattleBridge bridge, EntityManager em)
        {
            var boss = SpawnEnemy(bridge, em, "Assets/_Project/Data/Enemies/Enemy_Boss_Mamemo.asset");
            Assert.AreNotEqual(Entity.Null, boss, "마메모 스폰");
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.DcTriggerSlot>(boss), "mechanics 베이크됨");
            return boss;
        }

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
