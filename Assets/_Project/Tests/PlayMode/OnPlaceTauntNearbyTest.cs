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
    // on-place-skill-rework units 3~4 — 배스티온의 배치 도발을 **끝에서 한 번에** 검증한다.
    //   unit 3 — 시한 어그로(상한 우회 · 만료 · 선점)
    //   unit 4 — AreaTaunt 페이로드가 반경 안 적을 그 상태로 몰아넣는다
    //
    // ⚠ **핵심은 「상태가 붙었다」가 아니라 「적이 온다」다.** 상태만 확인하면 어그로가
    // 이동으로 연결되지 않아도 초록이 난다 — 이 spec 이 고치려는 증상(배치 스킬이 화면에서
    // 안 읽힌다)이 바로 그것이다. 그래서 거리 감소를 단언한다.
    public class OnPlaceTauntNearbyTest
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

        // 상한 2 인 가디언이 배치 순간 5기를 한꺼번에 붙잡는다.
        [UnityTest]
        public IEnumerator Taunt_GrabsEveryEnemyInRange_BeyondCapacity()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var bastion = MakeBastion("test_taunt_capacity");
            Assert.AreEqual(2, bastion.aggroCapacity, "평시 상한은 2 여야 한다(도발이 우회하는 값)");
            Prepare(bridge, gm, bastion);
            var cell = FindBastionCell(bridge, em, bastion, 5, out var walk);

            var inRange = new Entity[5];
            for (int i = 0; i < 5; i++) inRange[i] = SpawnWalker(em, bridge, walk[i]);
            var outFar = SpawnWalker(em, bridge, new Vector2Int(cell.x + 6, cell.y + 6));

            // ⚠ **라우팅 증인.** 이 단언이 없으면 도발이 concrete 를 거치는지 legacy arm 이
            // 대신 처리하는지 그물이 구분하지 못한다 — legacy 가 그대로 잘 도니까 아래
            // 결과 단언은 라우팅이 끊겨도 초록이다. `7f902e55` 가 잡은 실패 유형 그 자체다.
            // (arm 은 unit 8 철거까지 살아 있으므로 이 구멍은 지금 실재한다.)
            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, bastion), "배치");
            yield return Frames(20);

            int seamRuns = Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                Wassup.Battle.Skills.SkillSeam.Periodic);

            int aggroed = 0;
            for (int i = 0; i < 5; i++) if (em.HasComponent<Aggroed>(inRange[i])) aggroed++;
            bool farAggroed = em.HasComponent<Aggroed>(outFar);
            foreach (var e in inRange) em.DestroyEntity(e);
            em.DestroyEntity(outFar);
            Object.Destroy(bastion);

            Assert.GreaterOrEqual(seamRuns, 1,
                "도발이 스킬 레이어를 안 거쳤다 — legacy arm 이 대신 처리했다면 아래 단언은 "
                + "라우팅이 끊겨도 초록이 된다(이전이 안 된 상태를 못 가른다)");
            Assert.AreEqual(5, aggroed,
                "반경 2 안 5기가 전부 도발돼야 한다 — 2기만 걸렸다면 capacity 우회가 죽은 것이다");
            Assert.IsFalse(farAggroed, "반경 밖 적이 도발됐다");
        }

        // ⚠ 이 unit 의 핵심 핀 — **도발된 적이 배스티온 쪽으로 실제로 온다.**
        [UnityTest]
        public IEnumerator TauntedEnemy_WalksTowardTheBastion()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var bastion = MakeBastion("test_taunt_walk");
            Prepare(bridge, gm, bastion);
            var cell = FindBastionCell(bridge, em, bastion, 1, out var walk);

            // rev 2026-09-04(몸 반경 가로/2) — 산술로 자리를 고른다. 기준은 앵커가 아니라
            // **베이스**(발밑 = 앵커+(1,0), 3×2)다: 도발엔 잡히되(원 2.5 = tileRange 2 + 칸 반폭,
            // 더미 몸 0) 정지 도달(0.3 + 0 + 몸 1.5 = 1.8) **밖**인 밴드 (1.95, 2.4] 의 walk 칸.
            // 붙어 있으면 거리 감소가 측정되지 않고, 정지 도달 안이면 배치 즉시 Standoff 라
            // 한 발짝도 안 걷는다 — 옛 「앵커 체비셰프 최원거리」 선택이 그 함정을 밟았다.
            var baseCell = new Vector2Int(cell.x + 1, cell.y);
            var basePos = bridge.GridToWorldCenterVector(baseCell);
            Vector2Int spawn = default; bool foundSpawn = false;
            foreach (var w in walk)
            {
                var wp = bridge.GridToWorldCenterVector(w);
                float d = math.distance(new float3(wp.x, wp.y, wp.z), new float3(basePos.x, basePos.y, basePos.z));
                if (d > 1.95f && d <= 2.4f) { spawn = w; foundSpawn = true; break; }
            }
            Assert.IsTrue(foundSpawn, "밴드 (1.95, 2.4] 의 walk 칸이 없다 — 맵 기하가 바뀌었나?");
            var enemy = SpawnWalker(em, bridge, spawn, range: 0.3f);
            float Dist() => math.distance(em.GetComponentData<LocalTransform>(enemy).Position,
                                          new float3(basePos.x, basePos.y, basePos.z));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, bastion), "배치");
            yield return Frames(5);
            float before = Dist();
            Assert.IsTrue(em.HasComponent<Aggroed>(enemy), "도발 상태");

            yield return Frames(90);
            float after = Dist();
            em.DestroyEntity(enemy);
            Object.Destroy(bastion);

            Assert.Less(after, before - 0.1f,
                $"도발된 적이 배스티온 쪽으로 오지 않았다({before:F2} → {after:F2}). " +
                "상태만 붙고 이동이 안 따라오면 화면에서 아무것도 안 읽힌다 — 이 spec 이 고치려는 증상 그대로다.");
        }

        // 지속이 끝나면 풀린다.
        [UnityTest]
        public IEnumerator Taunt_ExpiresAndReleases()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var bastion = MakeBastion("test_taunt_expire");
            float duration = AuthoredDuration(bastion);
            Prepare(bridge, gm, bastion);
            var cell = FindBastionCell(bridge, em, bastion, 1, out var walk);
            var enemy = SpawnWalker(em, bridge, walk[0]);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, bastion), "배치");
            yield return Frames(10);
            Assert.IsTrue(em.HasComponent<Aggroed>(enemy), "부착");

            // 지속 + 여유. 프레임 수로 세지 않고 실시간으로 기다린다(배틀 시계 스케일 무관하게
            // 넉넉히) — 이 테스트가 재는 것은 «언젠가 풀린다» 이지 정확한 만료 프레임이 아니다.
            float until = Time.realtimeSinceStartup + duration + 2f;
            while (Time.realtimeSinceStartup < until && em.HasComponent<Aggroed>(enemy))
                yield return null;

            bool stillAggroed = em.HasComponent<Aggroed>(enemy);
            em.DestroyEntity(enemy);
            Object.Destroy(bastion);

            Assert.IsFalse(stillAggroed, $"도발 {duration}초가 지났는데 안 풀렸다");
        }

        // 비행 적은 근접 가디언에게 끌려오지 않는다 — 통행 층 게이트.
        [UnityTest]
        public IEnumerator FlyingEnemy_IsNotTaunted_MeleeBastionIsGroundOnly()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var bastion = MakeBastion("test_taunt_flying");
            Prepare(bridge, gm, bastion);
            var cell = FindBastionCell(bridge, em, bastion, 2, out var walk);

            var ground = SpawnWalker(em, bridge, walk[0]);
            var flying = SpawnWalker(em, bridge, walk[1], (byte)PlacementLayer.Air);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, bastion), "배치");
            yield return Frames(20);

            bool groundTaunted = em.HasComponent<Aggroed>(ground);
            bool flyingTaunted = em.HasComponent<Aggroed>(flying);
            em.DestroyEntity(ground); em.DestroyEntity(flying);
            Object.Destroy(bastion);

            Assert.IsTrue(groundTaunted, "지상 적이 안 걸렸다 — 대조군이 죽으면 아래 단언이 공짜로 통과한다");
            // ⚠ **이 단언의 불변식은 「도발 축 = 공격 축」이고, 방향은 저작을 따라 두 번 뒤집혔다.**
            //
            // 2026-08-25: `47d24c15`(전 방어유닛 공중 개방)로 「비행도 걸린다」로 뒤집힘
            // (사용자 판정: 공중을 공격하면 도발도 공중에 걸리는 게 당연하다).
            // 2026-09-03: `bc37b57c`(근접은 지상 전용 재개정)로 배스티온 마스크가 Path 로
            // 돌아와 다시 「비행은 안 걸린다」 — 같은 불변식, 저작만 바뀐 것이다.
            // 되돌리려면 이 테스트가 아니라 근접 지상 전용 콘텐츠 결정부터 뒤집어야 한다.
            Assert.IsFalse(flyingTaunted,
                "비행 적이 도발에 걸렸다 — 배스티온은 지상 전용(근접)인데 도발이 공중을 부르면 " +
                "「도발 축 = 공격 축」 불변식이 깨진다(때리지도 못할 대상을 부른다)");
        }

        // ── helpers ──────────────────────────────────────────────────────────

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

        private static DefenderUnitData MakeBastion(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("bastion"));
            unit.id = testId;
            unit.cost = 0;
            unit.maxOnBoard = 100;
            // skill-layer-migration unit 2g — 「밀쳐냄이 꺼져 있다」 단언은 은퇴했다.
            // 그 필드군이 철거돼(저작 소비자 0) 켤 방법이 없다. 「끌어오기와 정반대 힘을
            // 같이 걸면 어느 쪽도 안 읽힌다」는 판단은 그대로 유효하고, 밀쳐냄이 규칙
            // 어휘로 돌아오는 날 그 자리에서 다시 세워야 한다.
            Assert.IsNotNull(unit.GetAbility<UnitSkillAbility>(), "UnitSkillAbility 배선");
            return unit;
        }

        // 저작값을 하드코딩하지 않는다 — 밸런스 튜닝이 테스트를 깨면 안 된다.
        private static float AuthoredDuration(DefenderUnitData unit)
        {
            var m = unit.GetAbility<UnitSkillAbility>().mechanics[0];
            Assert.AreEqual(DcTriggerKind.OnPlace, m.trigger.kind);
            Assert.AreEqual(DcPayloadKind.AreaTaunt, m.payload.kind);
            Assert.Greater(m.payload.tileRange, 0, "반경");
            return m.payload.duration;
        }

        private static void Prepare(BattleBridge bridge, GameManager gm, DefenderUnitData unit)
        {
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            bridge.StartBattle();   // 어그로 이동은 sim 이 돌아야 보인다
        }


        // 실제로 걸어오는지 볼 때 쓰는 더미. range 는 정지 도달(= range + 0 + 배스티온 몸)을
        // 테스트가 통제하기 위한 노브 — 몸 반경이 가로/2(3×2 → 1.5)로 커진 뒤 기본 1 로는
        // 「도발 잡힘(≤2.5)」과 「정지 밖」이 같은 값이 돼 걷기 관측이 산술적으로 불가능하다.
        private static Entity SpawnWalker(EntityManager em, BattleBridge bridge, Vector2Int cell, byte layers = 0, float range = 1f)
            => SpawnBase(em, bridge, cell,
                         layers: layers == 0 ? TraversalSlots.DefaultMask : layers, range: range);

        private static Entity SpawnBase(EntityManager em, BattleBridge bridge, Vector2Int cell, byte layers, float range = 1f)
        {
            var w = bridge.GridToWorldCenterVector(cell);
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(w.x, w.y, w.z)));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            // 전투 수단이 없는 적은 어그로 획득이 거부된다(좀비 방지 게이트) — 실데이터처럼
            // 도발 공격 프로파일을 준다.
            em.AddComponentData(e, new Wassup.Battle.Combat.AggroAttackProfile { damage = 5f, cooldown = 1f, range = range });
            // 통행 층은 도발의 층 게이트가 읽는 값이다(없으면 0 = 무제한으로 조기 통과해
            // 게이트가 죽어도 테스트가 초록이 된다 — unit 4 후속 후보의 «더미가 통행 층을
            // 안 가진다» 공백).
            em.AddComponentData(e, new PathFollowState { speed = 2f, traversalLayers = layers });
            // ⚠ 스킬 레이어의 핸들 축 — 없으면 도발 후보가 못 된다(어댑터가 `SimEntityId`
            // 로 역변환한다). 예전 arm 은 `Entity` 를 직접 들었기에 필요 없었다.
            BattleBridgeTestAccess.AttachSimEntityId(bridge, e);
            // ⚠ **EnemyAiState 가 없으면 도발이 이동으로 이어지지 않는다.** MovementSystem 은
            // 이 컴포넌트가 없으면 Marching 으로 떨어뜨려 그냥 골로 걸어간다 — 어그로 상태는
            // 붙었는데 적이 멀어지는 그림이 나온다(실측 2.46 → 5.39). 실제 적은 스폰 시 받는다.
            em.AddComponentData(e, new Wassup.Battle.Combat.EnemyAiState { value = Wassup.Battle.Combat.AiState.Marching });
            return e;
        }

        // ⚠ **적은 «걸을 수 있는 칸» 에 놓아야 한다.** 어그로 획득에는 도달 가능 게이트가
        // 있고(aggro-tile-chase 의 좀비 방지), 그 판정은 flow field 의 walkMask 로 chase field 를
        // 구워 대상 셀의 거리가 유한한지 본다. 배치 칸 주변이라고 다 Walk 는 아니다 —
        // 초판이 아무 이웃 칸에나 더미를 뿌렸다가 5기 중 1기만 걸렸다(나머지는 도달 불가 거절).
        //
        // 그래서 **Walk 이웃이 충분한 배치 칸**을 고르고, 그 Walk 칸에만 적을 놓는다.
        private static Vector2Int FindBastionCell(
            BattleBridge bridge, EntityManager em, DefenderUnitData u, int needWalkNeighbours,
            out System.Collections.Generic.List<Vector2Int> walkNeighbours)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, q.CalculateEntityCount(), "flow field 싱글턴");
            var ff = q.GetSingleton<FlowFieldSingleton>();
            Assert.IsTrue(ff.walkMask.IsCreated, "walkMask");

            bool IsWalk(int x, int y)
            {
                if (x < 0 || y < 0 || x >= ff.gridSize.x || y >= ff.gridSize.y) return false;
                return ff.walkMask[y * ff.gridSize.x + x] != 0;
            }

            for (int x = 0; x < ff.gridSize.x; x++)
                for (int y = 0; y < ff.gridSize.y; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, u, out _)) continue;
                    var found = new System.Collections.Generic.List<Vector2Int>();
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            if (IsWalk(x + dx, y + dy)) found.Add(new Vector2Int(x + dx, y + dy));
                        }
                    if (found.Count >= needWalkNeighbours)
                    {
                        walkNeighbours = found;
                        return new Vector2Int(x, y);
                    }
                }
            Assert.Fail($"Walk 이웃 {needWalkNeighbours}개를 가진 배치 칸이 없다");
            walkNeighbours = null;
            return default;
        }
    }
}
