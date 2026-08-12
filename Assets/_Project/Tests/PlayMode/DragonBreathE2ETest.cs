using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // elite-enemy-tier unit 4/7 — 드래곤 화염 브레스 e2e.
    //
    // 사슬: 배송 에셋(Enemy_Dragon) 스폰 → AttackN(3) 카운트 → 부채꼴 안 방어유닛만 피해.
    //
    // ★이 테스트의 **본질은 아군 오사 회귀 방지**다. 초판 스펙은 「후보 배열이 시전자 마스크로
    // 걸러진 진영 대칭 풀」이라고 잘못 적었는데, `targetCandidatesQuery` 는 전 진영 통합 풀이고
    // 진영 판정은 공격자 루프 안의 `targetMask` 가 한다. 그 사실을 놓치면 드래곤이 같은 웨이브
    // 동료와 적 마음을 태운다 — 그래서 콘 순회의 세 술어(진영·통행층·자기제외)를 여기서 못 박는다.
    public class DragonBreathE2ETest
    {
        private const string DragonPath = "Assets/_Project/Data/Enemies/Enemy_Dragon.asset";
        private const string SlimePath = "Assets/_Project/Data/Enemies/Enemy_Slime.asset";

        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        // 드래곤은 엘리트다 — 보스 특권(CC·어그로 면역)을 받지 않는다.
        [UnityTest]
        public IEnumerator Dragon_SpawnsAsElite_WithoutBossTag_OnAirLayer()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var so = LoadEnemy(DragonPath);
            var dragon = SpawnEnemy(bridge, em, so);
            Assert.AreNotEqual(Entity.Null, dragon, "드래곤 스폰 실패");

            Assert.IsFalse(em.HasComponent<Wassup.Battle.Combat.BossTag>(dragon),
                "엘리트에 BossTag 가 붙었다 — CC·어그로 면역이 딸려온다");
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.DcTriggerSlot>(dragon),
                "메커닉 슬롯이 없으면 브레스가 아예 안 돈다");

            // Air 통행층이 실제로 베이크됐는가 — 이동 규칙의 출처다.
            var pf = em.GetComponentData<Wassup.Battle.Movement.PathFollowState>(dragon);
            Assert.AreEqual((byte)PlacementLayer.Air, pf.traversalLayers,
                "Air 층이 안 베이크되면 지상 차단에 막히고 대공사수 전용도 성립하지 않는다");

            // 브레스 슬롯의 cosSq 가 bake 에서 변환됐는가(저작은 도, 런타임은 코사인²).
            var slots = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(dragon);
            bool found = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].payload != DcPayloadKind.AreaBreath) continue;
                found = true;
                float expected = Mathf.Cos(Mathf.Deg2Rad * slots[i].coneHalfAngleDeg);
                Assert.AreEqual(expected * expected, slots[i].coneCosSq, 0.0001f,
                    "bake 가 반각 → cos² 변환을 하지 않았다");
                Assert.Greater(slots[i].coneCosSq, 0f);
            }
            Assert.IsTrue(found, "AreaBreath 슬롯이 베이크되지 않았다");
        }

        // ★아군 오사 — 콘 안에 **다른 적**을 두고 브레스를 발동시켜도 무피해여야 한다.
        [UnityTest]
        public IEnumerator DragonBreath_DoesNotDamageOtherEnemies()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var dragon = SpawnEnemy(bridge, em, LoadEnemy(DragonPath));
            var victimSo = LoadEnemy(SlimePath);
            var neighbour = SpawnEnemy(bridge, em, victimSo);
            Assert.AreNotEqual(Entity.Null, dragon);
            Assert.AreNotEqual(Entity.Null, neighbour);

            // 같은 레인 스폰이라 서로 붙어 있다 = 콘 안에 들 조건. 체력을 기록해 둔다.
            float before = em.GetComponentData<Health>(neighbour).value;

            // 브레스가 발동할 만큼(3타 × cd) 충분히 돌린다.
            for (int i = 0; i < 420; i++) yield return null;

            if (em.Exists(neighbour) && !em.HasComponent<DeadTag>(neighbour))
            {
                float after = em.GetComponentData<Health>(neighbour).value;
                Assert.AreEqual(before, after, 0.01f,
                    "다른 적이 브레스에 맞았다 — 콘 순회의 진영 마스크 술어가 빠졌다(초판 스펙의 거짓 전제)");
            }
            // 이웃이 사라졌다면 브레스가 아니라 유출/골 도달이므로 단언 대상이 아니다.
        }

        // ── helpers (SlimeSplitE2ETest 와 같은 레시피) ──────────────────────────

        private static IEnumerator LoadBattle()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static AttackUnitData LoadEnemy(string path)
        {
            var u = UnityEditor.AssetDatabase.LoadAssetAtPath<AttackUnitData>(path);
            Assert.IsNotNull(u, path);
            return u;
        }

        private static Entity SpawnEnemy(BattleBridge bridge, EntityManager em, AttackUnitData unit)
        {
            var bt = typeof(BattleBridge);
            var pendingType = bt.GetNestedType("PendingSpawnEntry", BindingFlags.NonPublic);
            var pending = System.Activator.CreateInstance(pendingType);
            pendingType.GetField("entry").SetValue(pending,
                new SpawnEntry { triggerTimeSec = 0f, unitType = unit, spawnIndex = 0 });
            pendingType.GetField("laneIndex").SetValue(pending, 0);

            var known = Snapshot(em);
            bt.GetMethod("SpawnUnit", BindingFlags.NonPublic | BindingFlags.Instance)
              .Invoke(bridge, new[] { pending });

            foreach (var e in Snapshot(em))
                if (!known.Contains(e)) return e;
            return Entity.Null;
        }

        private static HashSet<Entity> Snapshot(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var set = new HashSet<Entity>();
            for (int i = 0; i < arr.Length; i++) set.Add(arr[i]);
            arr.Dispose();
            return set;
        }
    }
}
