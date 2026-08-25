using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
    // skill-layer-foundation unit 1 — OnPlaceEffectType.BindNearby (아처) 특성화.
    //
    // 이 arm 은 ISkill 이전 대상이고, 지금까지 PlayMode 커버리지가 없었다. 이 파일은
    // **이전하기 전의 동작을 박제**한다 — 새 동작을 정의하는 것이 아니다.
    //
    // ⚠ 핵심은 「MoveSpeedMul 슬롯이 붙었다」가 아니라 **「반경 안 적이 실제로 거의 못
    // 움직인다」**다(위치 델타). 또한 BindNearby 는 이름과 달리 **정지(root)가 아니라
    // 감속**이다 — arm 자체가 SlowPulse 와 한 분기(같은 효과)이고, 집계 프레임워크의
    // move floor 가 완전 정지를 막는다. 이전이 bind 를 정지로 «강화»하면 그것도 동작
    // 변화라, 「그래도 긴다」를 함께 박제한다.
    public class OnPlaceBindNearbyTest
    {
        private const float Hp = 100000f;

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

        // 반경 안 적은 감속으로 거의 멈추고, 반경 밖 적은 평속으로 간다.
        [UnityTest]
        public IEnumerator Bind_SlowsEnemiesInRange_ButNotOutside()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var archer = MakeArcher("test_bind_slow");
            Prepare(bridge, gm, archer);
            // 아처 onPlaceRange 3 → 안쪽은 cheb ≤ 2(경계 안 여유), 바깥은 cheb > 3.
            // 적은 월드 좌표로 판정되므로 정확한 경계 칸 대신 여유를 둔다(스턴 테스트 선례).
            var cell = FindCellWithWalkNeighbours(bridge, em, archer, 2, 3, out var near, out var far);

            var inRange = SpawnWalker(em, bridge, near);
            var outFar = SpawnWalker(em, bridge, far);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, archer), "배치");
            yield return Frames(6);

            // 감속이 이동이 실제로 읽는 스탯(집계 결과)에 앉았는지 먼저 확인 — 위치 델타
            // 단언이 빨간 날, 원인이 «파이프라인 미도달»인지 «이동이 안 읽음»인지 가른다.
            // 정확한 값은 못 박지 않는다: 저작 0.1 은 프레임워크 move floor 에 걸려 그대로
            // 실효가 되지 않는다(값 박제는 floor 상수 복제라 여기선 방향만 본다).
            Assert.Less(MoveMul(em, inRange), 0.6f, "반경 안 적의 실효 이속 배율이 크게 깎여야 한다");
            Assert.AreEqual(1f, MoveMul(em, outFar), 0.01f, "반경 밖 적의 이속 배율이 깎였다 — 반경이 넓다");

            float3 P(Entity e) => em.GetComponentData<LocalTransform>(e).Position;
            float3 nearBefore = P(inRange), farBefore = P(outFar);

            // 저작 지속(1.5초급) 안에서만 잰다 — 프레임 수 대신 시뮬 시간으로 창을 재
            // 느린 에디터에서 감속이 끝난 뒤를 재는 거짓 실패를 막는다.
            float window = 0f;
            int guard = 0;
            while (window < 0.5f && guard++ < 90)
            {
                yield return null;
                window += Time.deltaTime;
            }

            float nearMoved = math.distance(P(inRange), nearBefore);
            float farMoved = math.distance(P(outFar), farBefore);
            em.DestroyEntity(inRange); em.DestroyEntity(outFar);
            Object.Destroy(archer);

            Assert.Greater(farMoved, 0.1f,
                "반경 밖 적이 안 움직였다 — 대조군이 죽으면 아래 단언이 «둘 다 안 움직인다» 로도 통과한다");
            Assert.Less(nearMoved, farMoved * 0.45f,
                $"반경 안 적이 대조군 대비 {nearMoved / math.max(farMoved, 0.001f):P0} 움직였다 — 감속이 화면에 안 보인다");
            Assert.Greater(nearMoved, 0.02f,
                "반경 안 적이 완전히 멈췄다 — bind 는 root 가 아니라 감속이다(현행 동작 박제)");
        }

        // 저작 지속이 끝나면 평속으로 돌아온다.
        [UnityTest]
        public IEnumerator Bind_WearsOff_AndTheEnemyMovesAgain()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var archer = MakeArcher("test_bind_wearoff");
            float duration = archer.onPlaceDuration;
            Assert.Greater(duration, 1f, "1초급 감속이어야 측정 창이 선다(저작이 바뀌면 창부터 다시 잡는다)");
            Prepare(bridge, gm, archer);
            var cell = FindCellWithWalkNeighbours(bridge, em, archer, 2, 3, out var near, out _);
            var enemy = SpawnWalker(em, bridge, near);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, archer), "배치");
            yield return Frames(6);
            Assert.Less(MoveMul(em, enemy), 0.6f, "부착(감속이 실효 스탯에 앉음)");

            float until = Time.realtimeSinceStartup + duration + 2.5f;
            while (Time.realtimeSinceStartup < until && MoveMul(em, enemy) < 0.99f) yield return null;
            Assert.GreaterOrEqual(MoveMul(em, enemy), 0.99f, $"{duration}초가 지났는데 감속이 안 풀렸다");

            float3 before = em.GetComponentData<LocalTransform>(enemy).Position;
            yield return Frames(40);
            float moved = math.distance(em.GetComponentData<LocalTransform>(enemy).Position, before);
            em.DestroyEntity(enemy);
            Object.Destroy(archer);

            Assert.Greater(moved, 0.1f, "감속이 풀렸는데 적이 안 움직인다(얼어붙음)");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static float MoveMul(EntityManager em, Entity e)
            => em.HasComponent<ModifierStats>(e) ? em.GetComponentData<ModifierStats>(e).moveSpeedMul : 1f;

        private static IEnumerator LoadBattle()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        private static DefenderUnitData MakeArcher(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("archer"));
            unit.id = testId;
            unit.attackRange = 0f;   // 평타가 섞이면 배치 스킬분을 분리 측정할 수 없다
            unit.cost = 0;
            unit.maxOnBoard = 100;
            Assert.AreEqual(OnPlaceEffectType.BindNearby, unit.onPlaceEffect,
                "아처의 배치 효과가 BindNearby 여야 이 특성화가 성립한다");
            // 감속 배율이 저작돼 있어야 위 단언들이 의미를 갖는다(1 이상이면 감속이 아니다).
            Assert.Greater(unit.onPlaceMagnitude, 0f);
            Assert.Less(unit.onPlaceMagnitude, 1f);
            return unit;
        }

        private static void Prepare(BattleBridge bridge, GameManager gm, DefenderUnitData unit)
        {
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            bridge.StartBattle();   // 적이 «느려졌나» 를 재려면 sim 이 돌아야 한다
        }

        // 반경 안(Walk) 한 칸과 반경 밖(Walk) 한 칸을 함께 고른다 — 적은 걸을 수 있는 칸에
        // 있어야 실제로 이동하고, 그래야 «느려졌다» 가 의미를 갖는다. (OnPlaceStunNearbyTest 선례)
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

        // 실제 적이 갖는 부품을 갖춘 더미(OnPlaceStunNearbyTest 선례) — 하나라도 빠지면
        // sim 게이트가 조기 통과/거절해 제품이 멀쩡해도 결과가 뒤집힌다.
        //
        // ⚠ 스턴 테스트 더미와의 차이: **ModifierStats + ModifierStatsDirty 를 반드시 얹는다.**
        // BindNearby 는 CC 버퍼가 아니라 StatModifier 파이프라인을 타는데,
        // ModifierStatsAggregateSystem 의 쿼리는 ModifierStats 보유자만 집계한다 — 없으면
        // 슬롯이 붙고도 실효 이속이 영원히 1 로 남아 테스트가 vacuous 하게 빨개진다.
        // (실제 적 스폰 경로가 세팅하는 기본값을 그대로 미러링한다.)
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
            em.AddComponentData(e, new ModifierStats
            {
                damageMul      = 1f,
                attackSpeedMul = 1f,
                dmgTakenMul    = 1f,
                regenPerSec    = 0f,
                moveSpeedMul   = 1f,
                damageVsCcMul  = 1f,
                maxHealthMul   = 1f,
            });
            em.AddComponent<ModifierStatsDirty>(e);
            em.SetComponentEnabled<ModifierStatsDirty>(e, false);
            return e;
        }
    }
}
