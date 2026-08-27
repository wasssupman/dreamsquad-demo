using System.Collections;
using System.Collections.Generic;
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
    // skill-layer-foundation unit 1 — EmitProjectilePattern arm 특성화
    // (폭탄맨 배치 스킬 = OnPlace × 배럴 투척 → **길막 설치물**).
    //
    // 샷건맨 블라스트와 같은 arm 을 타지만 탄이 다르다: 배럴은 BallisticBlocker
    // (BallisticArcToPoint × SpawnBlocker) — 곡사로 날아가 **착탄 칸에 길막 설치물을
    // 세운다**. 패턴은 scopeTileRange 안 **최근접**(selection=Nearest) 적의 칸을 겨눈다.
    //
    // ⚠ 이 arm 의 관측 가능한 결과는 피해가 아니라 **설치물**이다:
    //  · BlockingHazard 엔티티가 적의 칸에 실제로 선다 — «탄이 떴다»로 끝내면 착탄 칸
    //    해석·해저드 큐·브리지 드레인·스폰 검증 중 무엇이 죽어도 초록이 난다.
    //  · **피해 0 이 계약이다** — "배럴은 폭탄이 아니라 물건이고, 터지는 것은 부서질
    //    때다"(ProjectileHitSystem SpawnBlocker 주석). 적 체력 무변이 그 박제다.
    public class AbilityBombManBarrelTest
    {
        private const float Hp = 100000f;

        private int _savedMap;
        [SetUp]
        public void PinMap() => _savedMap = BattleBridgeTestAccess.PinMap();

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            BattleBridgeTestAccess.RestoreMap(_savedMap);
        }

        [UnityTest]
        public IEnumerator Barrel_ErectsBlockerAtNearestEnemyCell_WithZeroDamage()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var bombMan = MakeBarrelMan("test_barrel_erect");
            int scope = ScopeTiles(bombMan);
            Prepare(bridge, gm, bombMan);
            var cell = FindCellWithWalkTarget(bridge, em, bombMan,
                minCheb: 1, maxCheb: scope, out var targetCell);

            var victim = SpawnDummy(em, bridge, targetCell);
            var before = SnapshotBlockers(em);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, bombMan), "배치");

            // 곡사 비행(짧다) + 해저드 큐 → 브리지 드레인 → 스폰까지 넉넉히.
            yield return Seconds(2.5f);

            var newBlockers = NewBlockers(em, before);
            float dmg = Hp - em.GetComponentData<Health>(victim).value;
            float3 blockerPos = newBlockers.Count == 1
                ? em.GetComponentData<LocalTransform>(newBlockers[0]).Position : default;
            em.DestroyEntity(victim);
            Object.Destroy(bombMan);

            Assert.AreEqual(1, newBlockers.Count,
                $"배치 후 길막 설치물이 {newBlockers.Count}개 생겼다 — 1개여야 한다"
                + " (0 = 투척→착탄→해저드 큐→드레인 사슬 어딘가가 끊김, 2+ = 여분이 샘)");

            // 착탄 칸 = 발사 시점 최근접 적의 칸. 더미는 안 움직이므로 그 칸에 서야 한다.
            var want = bridge.GridToWorldCenterVector(targetCell);
            float tile = TileSize(bridge);
            float apart = math.distance(new float2(blockerPos.x, blockerPos.z),
                                        new float2(want.x, want.z));
            Assert.Less(apart, 0.6f * tile,
                $"설치물이 적의 칸에서 {apart / tile:F2}타일 떨어져 섰다 — 착탄 칸 해석이 어긋났다");

            // 피해 0 계약 — 배럴이 «맞으면 아픈 폭탄»으로 퇴화하면 여기서 빨개진다.
            Assert.AreEqual(0f, dmg, 0.001f,
                $"배럴 투척이 적에게 {dmg} 피해를 줬다 — 배럴은 물건이고 터지는 것은 부서질 때다(피해 0 계약)");
        }

        // 스코프 밖 → 후보 0 → 조용히 소모(설치물 없음). 최근접 선택의 반경 게이트 박제.
        [UnityTest]
        public IEnumerator NoEnemyInScope_ErectsNothing()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var bombMan = MakeBarrelMan("test_barrel_scope");
            int scope = ScopeTiles(bombMan);
            Prepare(bridge, gm, bombMan);
            var cell = FindCellWithWalkTarget(bridge, em, bombMan,
                minCheb: scope + 2, maxCheb: scope + 6, out var farCell);

            var far = SpawnDummy(em, bridge, farCell);
            var before = SnapshotBlockers(em);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, bombMan), "배치");
            yield return Seconds(2f);

            var newBlockers = NewBlockers(em, before);
            float dmg = Hp - em.GetComponentData<Health>(far).value;
            em.DestroyEntity(far);
            Object.Destroy(bombMan);

            Assert.AreEqual(0, newBlockers.Count,
                $"스코프({scope}타일) 밖에만 적이 있는데 설치물이 {newBlockers.Count}개 섰다 — 반경 게이트가 죽었다");
            Assert.AreEqual(0f, dmg, 0.001f, "스코프 밖 적이 피해까지 받았다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static float TileSize(BattleBridge bridge)
            => (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;

        private static IEnumerator LoadBattle()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static IEnumerator Seconds(float sec)
        {
            float t = 0f;
            while (t < sec) { t += Time.deltaTime; yield return null; }
        }

        private static DefenderUnitData MakeBarrelMan(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("bomb_man"));
            unit.id = testId;
            // 평타(BombThrowAbility 폭탄)가 섞이면 «배치 배럴»분을 분리 측정할 수 없다.
            // 능력을 떼는 대신 사거리를 0 으로 — 폭탄 발사는 attack.range 게이트라 이걸로 죽는다.
            unit.attackRange = 0f;
            unit.cost = 0;
            unit.maxOnBoard = 100;
            var skill = unit.GetAbility<UnitSkillAbility>();
            Assert.IsNotNull(skill, "폭탄맨에 UnitSkillAbility(배치 배럴)가 배선돼야 한다");
            Assert.AreEqual(DcTriggerKind.OnPlace, skill.mechanics[0].trigger.kind, "트리거 = 배치");
            Assert.AreEqual(DcPayloadKind.EmitProjectilePattern, skill.mechanics[0].payload.kind,
                "페이로드 = 발사 명세 트리거");
            Assert.IsNotNull(skill.mechanics[0].payload.pattern, "payload.pattern 미배선");
            return unit;
        }

        // 최근접 선택 반경은 **패턴 SO 의 scopeTileRange** 소유다(payload.tileRange 가 아니라).
        private static int ScopeTiles(DefenderUnitData unit)
        {
            int scope = unit.GetAbility<UnitSkillAbility>().mechanics[0].payload.pattern.scopeTileRange;
            Assert.Greater(scope, 0, "배럴 패턴의 scopeTileRange 가 저작돼 있어야 한다");
            return scope;
        }

        // 블라스트 테스트와 같은 격리(왜는 그 파일 주석 참조). 배럴의 설치물 스폰은
        // 해저드 큐 → 브리지 드레인이라 전투가 돌아야 한다(_running 게이트).
        private static void Prepare(BattleBridge bridge, GameManager gm, DefenderUnitData unit)
        {
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            bridge.StartBattle();
            MuteWaves(bridge);
            SilenceOtherAttackers();
        }

        private static void MuteWaves(BattleBridge bridge)
        {
            BattleBridgeTestAccess.SetField(bridge, "_usingGeneratedWaves", false);
            ((System.Collections.IList)BattleBridgeTestAccess.Field(bridge, "_pending")).Clear();
        }

        private static void SilenceOtherAttackers()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using (var attackers = em.CreateEntityQuery(
                       ComponentType.ReadOnly<Wassup.Battle.Combat.AttackState>()))
            {
                if (!attackers.IsEmpty)
                    em.RemoveComponent<Wassup.Battle.Combat.AttackState>(attackers);
            }
        }

        private static HashSet<Entity> SnapshotBlockers(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<BlockingHazard>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var set = new HashSet<Entity>();
            for (int i = 0; i < arr.Length; i++) set.Add(arr[i]);
            arr.Dispose();
            return set;
        }

        private static List<Entity> NewBlockers(EntityManager em, HashSet<Entity> before)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<BlockingHazard>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var list = new List<Entity>();
            for (int i = 0; i < arr.Length; i++)
                if (!before.Contains(arr[i])) list.Add(arr[i]);
            arr.Dispose();
            return list;
        }

        private static Entity SpawnDummy(EntityManager em, BattleBridge bridge, Vector2Int cell)
        {
            var w = bridge.GridToWorldCenterVector(cell);
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(w.x, w.y, w.z)));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        // 배치 칸 + Chebyshev [minCheb..maxCheb] 의 **설치물이 설 수 있는** 표적 칸.
        // 표적은 Walk 이며 골 셀이 아니어야 한다 — 설치물 스폰 검증(EffectSpawner)이
        // 골/차단 중첩을 거절하므로, 표적을 잘못 고르면 «사슬이 끊겼다»로 오진된다.
        private static Vector2Int FindCellWithWalkTarget(
            BattleBridge bridge, EntityManager em, DefenderUnitData u,
            int minCheb, int maxCheb, out Vector2Int targetCell)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, q.CalculateEntityCount(), "flow field 싱글턴");
            var ff = q.GetSingleton<FlowFieldSingleton>();
            Assert.IsTrue(ff.walkMask.IsCreated, "walkMask");

            bool GoodTarget(int x, int y)
                => x >= 0 && y >= 0 && x < ff.gridSize.x && y < ff.gridSize.y
                   && ff.walkMask[y * ff.gridSize.x + x] != 0
                   && !ff.IsGoalCell(new int2(x, y));

            for (int x = 0; x < ff.gridSize.x; x++)
                for (int y = 0; y < ff.gridSize.y; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, u, out _)) continue;
                    for (int dx = -maxCheb; dx <= maxCheb; dx++)
                        for (int dy = -maxCheb; dy <= maxCheb; dy++)
                        {
                            int cheb = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                            if (cheb < minCheb || cheb > maxCheb) continue;
                            if (!GoodTarget(x + dx, y + dy)) continue;
                            targetCell = new Vector2Int(x + dx, y + dy);
                            return new Vector2Int(x, y);
                        }
                }
            Assert.Fail($"Chebyshev [{minCheb}..{maxCheb}] Walk 표적을 가진 배치 칸이 없다");
            targetCell = default;
            return default;
        }
    }
}
