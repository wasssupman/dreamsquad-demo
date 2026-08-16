using System.Collections;
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
using Wassup.Battle.Movement;

namespace Wassup.Tests.PlayMode
{
    // on-place-skill-rework unit 5 — 말파이트의 배치 스킬(반경 2 · 3초 정지).
    //
    // 바뀐 것은 값 둘과 **띄움 길이 분리** 한 줄이다. 그런데 `StunNearby` 는 지금까지
    // PlayMode 커버리지가 아예 없었다(기존 `KnockupOnHitTest` 는 **평타** 넉업이다) —
    // 그래서 이 파일은 신규 작성이고, 값 조정이 아니라 **경로 자체**를 처음 고정한다.
    //
    // ⚠ 핵심은 「CC 가 붙었다」가 아니라 **「적이 실제로 멈춘다」**다. 지속만 늘리고 정지가
    // 안 되면 화면에서 아무 의미가 없다.
    public class OnPlaceStunNearbyTest
    {
        private const float Hp = 100000f;

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // 반경 2 안 적이 멈추고, 반경 밖 적은 계속 간다.
        [UnityTest]
        public IEnumerator Stun_FreezesEnemiesInRange_ButNotOutside()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var malphite = MakeMalphite("test_stun_freeze");
            Prepare(bridge, gm, malphite);
            var cell = FindCellWithWalkNeighbours(bridge, em, malphite, 1, 2, out var near, out var far);

            var inRange = SpawnWalker(em, bridge, near);
            var outFar = SpawnWalker(em, bridge, far);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, malphite), "배치");
            yield return Frames(6);

            Assert.IsTrue(HasStun(em, inRange), "반경 안 적에 Stun 이 안 붙었다");
            Assert.IsFalse(HasStun(em, outFar), "반경 밖 적에 Stun 이 붙었다");

            float3 P(Entity e) => em.GetComponentData<LocalTransform>(e).Position;
            float3 nearBefore = P(inRange), farBefore = P(outFar);

            yield return Frames(60);   // 3초 스턴의 한복판

            float nearMoved = math.distance(P(inRange), nearBefore);
            float farMoved = math.distance(P(outFar), farBefore);
            em.DestroyEntity(inRange); em.DestroyEntity(outFar);
            Object.Destroy(malphite);

            Assert.Less(nearMoved, 0.05f,
                $"스턴 중인 적이 {nearMoved:F2} 움직였다 — 지속만 늘고 정지가 안 되면 의미가 없다");
            Assert.Greater(farMoved, 0.1f,
                "반경 밖 적이 안 움직였다 — 대조군이 죽으면 위 단언이 «둘 다 안 움직인다» 로도 통과한다");
        }

        // 저작 지속이 끝나면 다시 움직인다.
        [UnityTest]
        public IEnumerator Stun_WearsOff_AndTheEnemyMovesAgain()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var malphite = MakeMalphite("test_stun_wearoff");
            float stun = malphite.onPlaceDuration;
            Assert.Greater(stun, 1f, "3초급 정지가 이 unit 의 사양이다");
            Prepare(bridge, gm, malphite);
            var cell = FindCellWithWalkNeighbours(bridge, em, malphite, 1, 2, out var near, out _);
            var enemy = SpawnWalker(em, bridge, near);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, malphite), "배치");
            yield return Frames(6);
            Assert.IsTrue(HasStun(em, enemy), "부착");

            float until = Time.realtimeSinceStartup + stun + 2f;
            while (Time.realtimeSinceStartup < until && HasStun(em, enemy)) yield return null;
            Assert.IsFalse(HasStun(em, enemy), $"{stun}초가 지났는데 스턴이 안 풀렸다");

            float3 before = em.GetComponentData<LocalTransform>(enemy).Position;
            yield return Frames(40);
            float moved = math.distance(em.GetComponentData<LocalTransform>(enemy).Position, before);
            em.DestroyEntity(enemy);
            Object.Destroy(malphite);

            Assert.Greater(moved, 0.1f, "스턴이 풀렸는데 적이 안 움직인다(얼어붙음)");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static bool HasStun(EntityManager em, Entity e)
        {
            if (!em.HasBuffer<CcEffect>(e)) return false;
            var buf = em.GetBuffer<CcEffect>(e);
            for (int i = 0; i < buf.Length; i++)
                if (buf[i].kind == CcKind.Stun && buf[i].remainingTime > 0f) return true;
            return false;
        }

        private static IEnumerator LoadBattle()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private static DefenderUnitData MakeMalphite(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("malphite"));
            unit.id = testId;
            unit.attackRange = 0f;   // 평타가 섞이면 배치 스킬분을 분리 측정할 수 없다
            unit.cost = 0;
            unit.maxOnBoard = 100;
            Assert.AreEqual(OnPlaceEffectType.StunNearby, unit.onPlaceEffect);
            return unit;
        }

        private static void Prepare(BattleBridge bridge, GameManager gm, DefenderUnitData unit)
        {
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            bridge.StartBattle();   // 적이 «멈췄나» 를 재려면 sim 이 돌아야 한다
        }

        // 반경 안(Walk) 한 칸과 반경 밖(Walk) 한 칸을 함께 고른다 — 적은 걸을 수 있는 칸에
        // 있어야 실제로 이동하고, 그래야 «멈췄다» 가 의미를 갖는다.
        private static Vector2Int FindCellWithWalkNeighbours(
            BattleBridge bridge, EntityManager em, DefenderUnitData u,
            int nearMaxRange, int farMinRange, out Vector2Int near, out Vector2Int far)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, q.CalculateEntityCount(), "flow field 싱글턴");
            var ff = q.GetSingleton<FlowFieldSingleton>();
            Assert.IsTrue(ff.walkMask.IsCreated, "walkMask");

            bool IsWalk(int x, int y)
                => x >= 0 && y >= 0 && x < ff.gridSize.x && y < ff.gridSize.y
                   && ff.walkMask[y * ff.gridSize.x + x] != 0;

            for (int x = 0; x < ff.gridSize.x; x++)
                for (int y = 0; y < ff.gridSize.y; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, u, out _)) continue;
                    Vector2Int? n = null, f = null;
                    for (int dx = -6; dx <= 6; dx++)
                        for (int dy = -6; dy <= 6; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (!IsWalk(x + dx, y + dy)) continue;
                            int cheb = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                            if (n == null && cheb <= nearMaxRange) n = new Vector2Int(x + dx, y + dy);
                            else if (f == null && cheb > farMinRange) f = new Vector2Int(x + dx, y + dy);
                        }
                    if (n != null && f != null)
                    {
                        near = n.Value; far = f.Value;
                        return new Vector2Int(x, y);
                    }
                }
            Assert.Fail("반경 안팎 Walk 칸을 가진 배치 칸이 없다");
            near = default; far = default;
            return default;
        }

        // 실제 적이 갖는 부품을 갖춘 더미 — 하나라도 빠지면 sim 게이트가 조기 통과/거절해
        // 제품이 멀쩡해도 결과가 뒤집힌다(unit 4 에서 셋에 연달아 걸렸다).
        private static Entity SpawnWalker(EntityManager em, BattleBridge bridge, Vector2Int cell)
        {
            var w = bridge.GridToWorldCenterVector(cell);
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(w.x, w.y, w.z)));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            em.AddComponentData(e, new PathFollowState { speed = 2f, traversalLayers = TraversalSlots.DefaultMask });
            em.AddComponentData(e, new Wassup.Battle.Combat.EnemyAiState { value = Wassup.Battle.Combat.AiState.Marching });
            return e;
        }
    }
}
