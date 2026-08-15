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

namespace Wassup.Tests.PlayMode
{
    // defender-on-place-skills unit 4 — 전방 관통 일격이 **적을 향해** 나가는가.
    //
    // 회귀 대상은 실제로 발생한 결함이다: 방향이 "가장 가까운 길 칸"이었고, 그 탐색이 동점에서
    // 먼저 스캔된 칸(= 남쪽 이웃)을 항상 골라 총구가 사실상 남쪽에 고정돼 있었다. 맵 252칸 중
    // 173칸이 (0,-1) 이었고 사용자 플레이 4회 배치가 전부 affected=0 이었다.
    //
    // 그래서 세 케이스 모두 **적을 남쪽이 아닌 곳에 둔다** — 남쪽에 두면 고장난 코드도 통과한다.
    public class OnPlaceForwardProjectileTest
    {
        private const float Hp = 100000f;

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // 조준 UX 가 없는 유닛(마크스맨·피어서·스나이퍼)의 규칙: 가장 가까운 적 방향.
        [UnityTest]
        public IEnumerator NoFacing_FiresAtNearestEnemy_NotSouth()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var unit = MakeTestUnit("marksman", "test_onplace_forward_nofacing");
            Prepare(bridge, gm, unit);
            var cell = FindPlaceableCell(bridge, unit);
            Assert.AreNotEqual(new Vector2Int(int.MinValue, int.MinValue), cell, "placeable cell");

            // 적은 **북쪽**에만 있다. 남쪽 고정 코드는 여기서 반드시 0을 낸다.
            var north = SpawnDummy(em, bridge.GridToWorldCenterVector(new Vector2Int(cell.x, cell.y + 2)));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, unit), "place");
            yield return Frames(10);

            float dealt = Hp - em.GetComponentData<Health>(north).value;
            em.DestroyEntity(north);
            Object.Destroy(unit);

            Assert.Greater(dealt, 0f,
                "조준이 없으면 가장 가까운 적 방향으로 나가야 한다. 0 이면 총구가 여전히 고정 방향이다");
        }

        // 방향 지정 유닛(머신거너)의 규칙: 플레이어가 조준한 방향이 스킬에 실린다.
        //
        // ⚠ 조준 **한 방향만** 검사하면 안 된다. 고장난 코드도 셀마다 어떤 고정 방향을 갖고,
        // 그게 우연히 검사 방향과 같으면 통과한다 — 초판이 실제로 그렇게 새는 통과를 냈다.
        // 네 방향을 각각 다른 칸에서 검사하면 단일 고정 방향으로는 통과할 수 없다.
        [UnityTest]
        public IEnumerator Facing_AimedDirectionWins_AllFourWays()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var unit = MakeTestUnit("machine_gunner", "test_onplace_forward_facing");
            Prepare(bridge, gm, unit);

            var facings = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            var cells = FindPlaceableCells(bridge, unit, facings.Length);
            Assert.AreEqual(facings.Length, cells.Count, "서로 다른 배치 칸 4개");

            for (int i = 0; i < facings.Length; i++)
            {
                var cell = cells[i];
                var facing = facings[i];
                // 조준 방향 3칸 앞 = 맞아야 하는 적.
                var aimed = SpawnDummy(em, bridge.GridToWorldCenterVector(
                    new Vector2Int(cell.x + facing.x * 3, cell.y + facing.y * 3)));
                // 조준과 **직교** 1칸 = 더 가깝지만 맞으면 안 되는 적. 이 대조군이 없으면
                // 「조준을 무시하고 최근접 적만 쓰는」 구현도 4방향 전부 통과한다.
                var offAxis = SpawnDummy(em, bridge.GridToWorldCenterVector(
                    new Vector2Int(cell.x + facing.y, cell.y + facing.x)));

                Entity entity;
                Assert.IsTrue(bridge.TryBeginDefenderDeployment(cell.x, cell.y, unit, out entity),
                    $"begin deployment {cell}");
                bridge.ActivateDeployedDefender(cell, entity, facing);
                yield return Frames(10);

                float aimedDealt = Hp - em.GetComponentData<Health>(aimed).value;
                float offAxisDealt = Hp - em.GetComponentData<Health>(offAxis).value;
                em.DestroyEntity(aimed);
                em.DestroyEntity(offAxis);

                Assert.Greater(aimedDealt, 0f,
                    $"조준 {facing} 방향의 적이 맞아야 한다 (배치 {cell}). 0 이면 조준이 무시되고 있다");
                Assert.AreEqual(0f, offAxisDealt, 0.01f,
                    $"조준 {facing} 과 직교한 더 가까운 적은 맞으면 안 된다 (배치 {cell}). "
                    + "맞았다면 조준이 아니라 최근접 적을 쫓고 있거나 통로가 아니라 반경을 때리고 있다");
            }

            Object.Destroy(unit);
        }

        // 통로 폭 회귀: 적은 레인 오프셋으로 ±0.5 흩어져 걷는다. 반폭 0.45 시절엔 바로 옆 적이
        // lat≈1.0 으로 탈락했다(실측). 0.55 는 옛 폭에서 빠지고 새 폭에서 들어오는 값이다.
        [UnityTest]
        public IEnumerator LaneOffsetEnemy_IsInsideCorridor()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var unit = MakeTestUnit("marksman", "test_onplace_forward_width");
            Prepare(bridge, gm, unit);
            var cell = FindPlaceableCell(bridge, unit);
            Assert.AreNotEqual(new Vector2Int(int.MinValue, int.MinValue), cell, "placeable cell");

            // 전방(북) 3칸 · 옆으로 0.55칸.
            var basePos = bridge.GridToWorldCenterVector(new Vector2Int(cell.x, cell.y + 3));
            var offset = new Vector3(bridge.TileSize * 0.55f, 0f, 0f);
            var skewed = SpawnDummy(em, basePos + offset);

            Entity entity;
            Assert.IsTrue(bridge.TryBeginDefenderDeployment(cell.x, cell.y, unit, out entity), "begin deployment");
            bridge.ActivateDeployedDefender(cell, entity, new Vector2Int(0, 1)); // 북쪽 조준
            yield return Frames(10);

            float dealt = Hp - em.GetComponentData<Health>(skewed).value;
            em.DestroyEntity(skewed);
            Object.Destroy(unit);

            Assert.Greater(dealt, 0f, "레인 오프셋만큼 벗어난 적도 통로 안이어야 한다");
        }

        // 「이번 프레임 합법 후보」 회귀 — 판 밖(궁극기 도약 예고)인 적은 총구를 훔치면 안 된다.
        //
        // 그 상태의 적은 `DamageApplicationSystem` 이 IncomingDamage 를 통째로 비우므로 때릴 수
        // 없는 대상이다. 후보에 남겨 두면 더 가깝다는 이유로 방향을 가져가고, 진짜 적이 있는
        // 방향은 통째로 버려진다 — 이 커밋이 고치려던 affected=0 이 다른 원인으로 재발한다.
        [UnityTest]
        public IEnumerator OutOfPlayEnemy_DoesNotStealTheMuzzle()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var unit = MakeTestUnit("marksman", "test_onplace_forward_legal");
            Prepare(bridge, gm, unit);
            var cell = FindPlaceableCell(bridge, unit);
            Assert.AreNotEqual(new Vector2Int(int.MinValue, int.MinValue), cell, "placeable cell");

            // 판 밖(도약 예고) 적이 **더 가깝다**. remaining 을 길게 줘 테스트 동안 착지하지 않게.
            var outOfPlay = SpawnDummy(em, bridge.GridToWorldCenterVector(new Vector2Int(cell.x, cell.y + 2)));
            em.AddComponentData(outOfPlay, new Wassup.Battle.Combat.UltimateLeapState { remaining = 60f });
            // 진짜 적은 더 멀고 다른 방향.
            var real = SpawnDummy(em, bridge.GridToWorldCenterVector(new Vector2Int(cell.x + 4, cell.y)));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, unit), "place");
            yield return Frames(10);

            float realDealt = Hp - em.GetComponentData<Health>(real).value;
            em.DestroyEntity(outOfPlay);
            em.DestroyEntity(real);
            Object.Destroy(unit);

            Assert.Greater(realDealt, 0f,
                "판 밖 적이 더 가깝더라도 총구는 때릴 수 있는 적을 향해야 한다. "
                + "0 이면 후보 필터가 UltimateLeapState 를 빼지 않고 있다");
        }

        // 사거리 밖 적만 있으면 아무 일도 일어나지 않는다(후보 반경 필터 회귀).
        [UnityTest]
        public IEnumerator EnemyBeyondRange_IsNeverHit()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var unit = MakeTestUnit("marksman", "test_onplace_forward_range");
            Prepare(bridge, gm, unit);
            var cell = FindPlaceableCell(bridge, unit);
            Assert.AreNotEqual(new Vector2Int(int.MinValue, int.MinValue), cell, "placeable cell");

            // onPlaceRange(6)보다 훨씬 멀다.
            int far = (int)unit.onPlaceRange + 6;
            var beyond = SpawnDummy(em, bridge.GridToWorldCenterVector(new Vector2Int(cell.x, cell.y + far)));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, unit), "place");
            yield return Frames(10);

            float dealt = Hp - em.GetComponentData<Health>(beyond).value;
            em.DestroyEntity(beyond);
            Object.Destroy(unit);

            Assert.AreEqual(0f, dealt, 0.01f, "사거리 밖 적은 맞으면 안 된다");
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

        // 카탈로그 사본을 쓴다. 일반 공격이 섞이면 배치 일격분을 분리 측정할 수 없으므로
        // 사거리 0, 코스트 0(배치 경로가 코스트로 실패하지 않게).
        private static DefenderUnitData MakeTestUnit(string catalogId, string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById(catalogId));
            unit.id = testId;
            unit.attackRange = 0f;
            unit.cost = 0;
            unit.maxOnBoard = 100; // 판 위 동시 상한(기본 1)에 조준 4방향 검사가 막힌다
            Assert.AreEqual(OnPlaceEffectType.ForwardProjectile, unit.onPlaceEffect,
                catalogId + " 의 배치 스킬이 ForwardProjectile 이어야 한다");
            return unit;
        }

        private static void Prepare(BattleBridge bridge, GameManager gm, DefenderUnitData unit)
        {
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
        }

        private static Entity SpawnDummy(EntityManager em, Vector3 worldPos)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(worldPos.x, worldPos.y, worldPos.z)));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        private static Vector2Int FindPlaceableCell(BattleBridge bridge, DefenderUnitData u)
        {
            var found = FindPlaceableCells(bridge, u, 1);
            return found.Count > 0 ? found[0] : new Vector2Int(int.MinValue, int.MinValue);
        }

        // 서로 **떨어진** 칸을 고른다. 붙여 놓으면 앞 배치의 6칸 통로가 다음 칸의 적을 스쳐
        // 조준 검사가 오염된다.
        private static System.Collections.Generic.List<Vector2Int> FindPlaceableCells(
            BattleBridge bridge, DefenderUnitData u, int count)
        {
            var result = new System.Collections.Generic.List<Vector2Int>();
            for (int x = -24; x < 48 && result.Count < count; x++)
                for (int y = -24; y < 48 && result.Count < count; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, u, out _)) continue;
                    var cell = new Vector2Int(x, y);
                    bool tooClose = false;
                    for (int i = 0; i < result.Count; i++)
                        if (Mathf.Max(Mathf.Abs(result[i].x - x), Mathf.Abs(result[i].y - y)) < 4)
                            tooClose = true;
                    if (tooClose) continue;
                    result.Add(cell);
                }
            return result;
        }
    }
}
