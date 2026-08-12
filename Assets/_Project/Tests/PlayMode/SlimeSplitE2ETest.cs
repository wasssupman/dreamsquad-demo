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
    // elite-enemy-tier unit 5/6 — 엘리트 슬라임 분열 e2e.
    //
    // 사슬 전체를 태운다: 배송 에셋(Enemy_Slime) 스폰 → 치사 피해 → 킬 이벤트 → 브리지 드레인이
    // **SO 를 직독**해 자식 2기 스폰 → 자식이 이동 → 자식은 다시 분열하지 않는다.
    //
    // 이 테스트가 지키는 것이 왜 «순수 함수 그린» 으로 대체되지 않나: 분열은 슬롯도 이벤트
    // 필드도 sim 변경도 없는 **브리지 드레인 한 곳**이라, 검증할 수 있는 순수 조각이 없다.
    // 유일한 증거는 «죽였더니 둘이 생겼다» 다.
    //
    // ⚠ 슬라임은 라이브 덱 풀에 없어서 BattleScene 로드로 메모리에 올라오지 않는다
    // (Resources.FindObjectsOfTypeAll 로는 못 찾는다) → AssetDatabase 로 직접 로드한다
    // (BossShieldTest 선례).
    public class SlimeSplitE2ETest
    {
        private const string ParentPath = "Assets/_Project/Data/Enemies/Enemy_Slime.asset";
        private const string ChildPath = "Assets/_Project/Data/Enemies/Enemy_Slime_Small.asset";

        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Slime_SplitsIntoTwoChildren_AtDeathSpot_AndChildrenDoNotResplit()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge");

            // ★배틀을 실제로 시작해야 한다 — Update 가 `if (!_running) return;` 으로 막혀 있어서
            // 시작하지 않으면 **브리지 드레인이 한 번도 돌지 않는다.** 분열은 그 드레인에 살아
            // 있으므로(unit 5 ②) 이 한 줄이 없으면 «자식 0» 이 되고, 원인이 구현처럼 보인다.
            // (순수 ECS 를 보는 테스트들 — 예: BossShieldTest — 은 이게 없어도 통과한다.)
            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var parentSo = LoadEnemy(ParentPath);
            var childSo = LoadEnemy(ChildPath);
            Assert.AreEqual(EnemyTier.Elite, parentSo.tier, "슬라임은 엘리트여야 한다");

            // 엘리트는 보스가 아니다 — 이 spec 의 핵심 계약. 스폰된 실체로 확인한다.
            var parent = SpawnEnemy(bridge, em, parentSo);
            Assert.AreNotEqual(Entity.Null, parent, "슬라임 스폰 실패");
            Assert.IsFalse(em.HasComponent<Wassup.Battle.Combat.BossTag>(parent),
                "엘리트에 BossTag 가 붙었다 — CC·어그로 면역이 딸려온다");

            var deathPos = em.GetComponentData<LocalTransform>(parent).Position;

            // 치사 피해 — 표준 경로(IncomingDamage → DamageApplicationSystem → EnemyKilledEvent).
            int killCountBefore = KillCount(bridge);
            var h = em.GetComponentData<Health>(parent);
            em.GetBuffer<IncomingDamage>(parent).Add(new IncomingDamage { amount = h.max * 10f });

            // 사망 → 파괴 → 브리지 드레인(Update 최상단) 까지 몇 프레임.
            List<Entity> children = null;
            for (int i = 0; i < 30 && (children == null || children.Count < 2); i++)
            {
                yield return null;
                children = FindEnemiesOfType(bridge, em, childSo);
            }

            // ★경계 계측 — 「킬 드레인이 돌았나」와 「분열이 돌았나」를 분리한다.
            // 이게 없으면 실패가 «자식 0» 한 덩어리로 뭉쳐서 어디서 끊겼는지 안 보인다.
            Assert.Greater(KillCount(bridge), killCountBefore,
                "킬 드레인 자체가 돌지 않았다(_killCount 불변) — EnemyKilledEvent 발화 또는 " +
                "DrainEnemyKilledEvents 호출 경로 문제. 분열 코드는 아직 의심 대상이 아니다");

            Assert.IsNotNull(children);
            Assert.AreEqual(2, children.Count,
                $"자식이 정확히 2기여야 한다(실제 {children?.Count}) — magnitude 저작 또는 드레인 배선");
            Assert.IsFalse(em.Exists(parent) && !em.HasComponent<DeadTag>(parent),
                "부모가 살아 있다");

            // 체력 50% + 죽은 자리
            foreach (var c in children)
            {
                var ch = em.GetComponentData<Health>(c);
                Assert.AreEqual(parentSo.health * 0.5f, ch.max, 0.01f, "자식 최대체력 = 부모의 50%");
                var p = em.GetComponentData<LocalTransform>(c).Position;
                float planar = Vector2.Distance(new Vector2(deathPos.x, deathPos.z), new Vector2(p.x, p.z));
                Assert.Less(planar, bridge.TileSize,
                    "자식은 부모가 죽은 자리(같은 셀 안)에 생겨야 한다");
            }

            // 자식이 실제로 **움직인다** — 스폰만 되고 굳는 계열 회귀 방지
            // (summon-patrol-defender 가 겪은 «뷰가 제자리에 선다» 와 같은 종류).
            var start = em.GetComponentData<LocalTransform>(children[0]).Position;
            for (int i = 0; i < 120; i++) yield return null;
            if (em.Exists(children[0]))
            {
                var now = em.GetComponentData<LocalTransform>(children[0]).Position;
                Assert.Greater(Vector3.Distance(start, now), 0.05f,
                    "자식이 한 칸도 움직이지 않았다 — PathFollowState bake 또는 임의 위치 스폰 문제");
            }

            // 자식을 죽여도 더 생기지 않는다(재귀 차단이 «자식은 메커닉이 없다» 로 성립).
            int beforeGrandkids = FindEnemiesOfType(bridge, em, childSo).Count;
            foreach (var c in children)
                if (em.Exists(c) && em.HasBuffer<IncomingDamage>(c))
                    em.GetBuffer<IncomingDamage>(c).Add(new IncomingDamage { amount = 99999f });

            for (int i = 0; i < 30; i++) yield return null;
            int afterGrandkids = FindEnemiesOfType(bridge, em, childSo).Count;
            Assert.Less(afterGrandkids, beforeGrandkids,
                "자식이 죽지 않았거나 손자가 생겼다 — 무한 분열 위험");
            Assert.AreEqual(0, afterGrandkids, "손자가 생겼다 — 자식의 nightmareMechanics 가 비어야 한다");
        }

        // 드레인 순서 계약(unit 5 ④). 「부모가 마지막 적일 때 웨이브가 안 넘어간다」를 직접
        // 재현하려면 웨이브 스케줄러 타이밍(_pending·상한 간격·전멸)에 의존해 flaky 해진다.
        // 그래서 **그 계약이 성립하는 근거 자체**를 단언한다: 킬이 집계된 것이 관측되는 시점에
        // 자식이 이미 존재한다 = 드레인이 같은 호출에서 스폰한다 = 그 뒤에 오는 QueueDueWaves /
        // CheckVictory 가 「부모는 없는데 자식도 없는」 틈을 **볼 수 없다**.
        //
        // 이 단언이 빨개지는 경우: 분열 스폰을 다음 프레임으로 미루거나(예: 요청 큐에 넣기),
        // 드레인을 QueueDueWaves 뒤로 되돌리는 변경.
        [UnityTest]
        public IEnumerator SplitChildren_ExistInTheSameObservationAsTheKillCount()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var parentSo = LoadEnemy(ParentPath);
            var childSo = LoadEnemy(ChildPath);

            var parent = SpawnEnemy(bridge, em, parentSo);
            Assert.AreNotEqual(Entity.Null, parent);
            for (int i = 0; i < 2; i++) yield return null;

            int killsBefore = KillCount(bridge);
            em.GetBuffer<IncomingDamage>(parent).Add(new IncomingDamage { amount = 999999f });

            bool observed = false;
            for (int i = 0; i < 40 && !observed; i++)
            {
                yield return null;
                if (KillCount(bridge) <= killsBefore) continue;
                // 킬이 집계된 첫 관측 — 이 순간 자식이 이미 있어야 한다.
                int childCount = FindEnemiesOfType(bridge, em, childSo).Count;
                Assert.AreEqual(2, childCount,
                    "킬이 집계된 시점에 자식이 없다 — 분열 스폰이 킬 드레인과 같은 호출에서 " +
                    "일어나지 않으면 QueueDueWaves/CheckVictory 가 「부모도 자식도 없는」 틈을 본다");
                observed = true;
            }
            Assert.IsTrue(observed, "킬이 집계되지 않았다 — 드레인 경로 문제");
        }

        // ── helpers ─────────────────────────────────────────────────────────────

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

            var known = SnapshotAttackers(em);
            bt.GetMethod("SpawnUnit", BindingFlags.NonPublic | BindingFlags.Instance)
              .Invoke(bridge, new[] { pending });

            foreach (var e in SnapshotAttackers(em))
                if (!known.Contains(e)) return e;
            return Entity.Null;
        }

        private static HashSet<Entity> SnapshotAttackers(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var set = new HashSet<Entity>();
            for (int i = 0; i < arr.Length; i++) set.Add(arr[i]);
            arr.Dispose();
            return set;
        }



        // 어느 엔티티가 어느 SO 에서 나왔는지는 브리지의 _enemyTypeByEntity 만 안다.
        private static List<Entity> FindEnemiesOfType(BattleBridge bridge, EntityManager em, AttackUnitData so)
        {
            var f = typeof(BattleBridge).GetField("_enemyTypeByEntity",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "_enemyTypeByEntity 를 찾지 못했다(이름 변경?)");
            var dict = (Dictionary<Entity, AttackUnitData>)f.GetValue(bridge);

            var result = new List<Entity>();
            foreach (var kv in dict)
                if (kv.Value == so && em.Exists(kv.Key) && !em.HasComponent<DeadTag>(kv.Key))
                    result.Add(kv.Key);
            return result;
        }

        // 킬 드레인이 실제로 돌았는지의 관측창(그 루프가 매 이벤트마다 올린다).
        private static int KillCount(BattleBridge bridge)
        {
            var f = typeof(BattleBridge).GetField("_killCount",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "_killCount 를 찾지 못했다(이름 변경?)");
            return (int)f.GetValue(bridge);
        }

    }
}
