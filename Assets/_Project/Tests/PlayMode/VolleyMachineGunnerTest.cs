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
    // skill-layer-foundation unit 1 — DirectionalVolleyAbility(requiresFacing=1) 특성화
    // (머신거너 다연발).
    //
    // facing 유닛의 계약(AttackSystem + LaneMath):
    //  · 타겟팅 규칙은 **레인이 전부**다 — facing 축 1타일 폭 × [1..사거리] 안에 적이
    //    있어야 START 하고, 레인 밖 적은 사거리 안이어도 존재하지 않는 것과 같다.
    //  · 한 번의 trigger 가 패턴 SO 의 shots 를 **전부** 발사한다(다연발). 탄당 피해는
    //    유닛의 실효 output damage 로 덮는다(패턴 damage 가 아니라 — RESOLVE 스냅샷).
    //
    // ⚠ 단언은 «연발이 실제로 여러 발 나가 맞았다»다. 레인의 외로운 적 하나가 받은
    // 총피해 == shots.Length × 탄당 피해 — 이 등식 하나가 (a) 연발이 1발로 접힘,
    // (b) 여분 발 샘, (c) 피해 출처가 output 이 아님 셋을 동시에 잡는다.
    public class VolleyMachineGunnerTest
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
        public IEnumerator LaneEnemy_TakesFullAuthoredBurst_OffLaneUntouched()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var gunner = MakeGunner("test_volley_mg_lane");
            var pattern = gunner.GetAbility<DirectionalVolleyAbility>().pattern;
            float perShot = AuthoredPerShot(gunner);
            Prepare(bridge, gm, gunner);
            var cell = FindLaneLayout(bridge, em, gunner, laneDist: 2, offDist: 3,
                out var facing, out var laneCell, out var offCell);
            float tile = TileSize(bridge);
            Assert.Greater(3f * tile, pattern.barrel.hitThreshold + 0.5f,
                "전제: 레인 밖 3타일 이격 > 탄 스침 반경 — 깨지면 배치 거리 재계산");

            var inLane = SpawnDummy(em, bridge, laneCell);
            var offLane = SpawnDummy(em, bridge, offCell);

            var entity = PlaceWithFacing(bridge, cell, gunner, facing);
            Assert.IsTrue(em.HasComponent<DeployedFacing>(entity),
                "facing 배치인데 DeployedFacing 이 없다 — requiresFacing 배선이 끊겼다");

            // 1버스트를 끝까지 받는다: 첫 피해까지 대기 → 1.0초 무변화면 종료.
            // 무변화 창(1.0s)은 버스트 간 간격(쿨다운 1.9s + 연발 길이)보다 짧아
            // 두 번째 버스트가 합산되기 전에 반드시 빠져나온다.
            float dLane = 0f;
            float t = 0f;
            while (t < 6f && dLane <= 0f)
            {
                t += Time.deltaTime;
                dLane = Hp - em.GetComponentData<Health>(inLane).value;
                yield return null;
            }
            float stable = 0f;
            while (stable < 1.0f && t < 12f)
            {
                yield return null;
                t += Time.deltaTime;
                float now = Hp - em.GetComponentData<Health>(inLane).value;
                if (now > dLane + 0.001f) { dLane = now; stable = 0f; }
                else stable += Time.deltaTime;
            }

            float dOff = Hp - em.GetComponentData<Health>(offLane).value;
            em.DestroyEntity(inLane); em.DestroyEntity(offLane);
            Object.Destroy(gunner);

            float expected = pattern.shots.Length * perShot;
            Assert.Greater(dLane, 0f, "레인 안 적이 아예 안 맞았다 — START 또는 발사가 죽었다");
            Assert.AreEqual(expected, dLane, 0.05f,
                $"레인의 외로운 적이 받은 총피해({dLane})가 연발 저작({pattern.shots.Length}발 × {perShot})과"
                + " 다르다 — 적으면 연발이 접힌 것(또는 탄 유실), 많으면 여분이 새는 것");
            Assert.AreEqual(0f, dOff, 0.001f,
                "레인 밖(측면) 적이 맞았다 — facing 유닛의 탄이 레인 축을 벗어났다");
        }

        // facing 유닛의 정의 그 자체: 사거리 **안**이어도 레인 밖이면 발사하지 않는다.
        // 샷건맨(requiresFacing=0)은 같은 상황에서 쏜다 — 두 파일이 함께 facing 축의
        // 차이를 관측한다(VolleyShotgunnerTest 참조).
        [UnityTest]
        public IEnumerator EnemyInRangeButOffLane_NeverFires()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var gunner = MakeGunner("test_volley_mg_gate");
            Prepare(bridge, gm, gunner);
            var cell = FindLaneLayout(bridge, em, gunner, laneDist: 2, offDist: 2,
                out var facing, out _, out var offCell);
            Assert.LessOrEqual(2, (int)gunner.attackRange,
                "전제: 측면 적이 사거리 안이어야 «레인 게이트»와 «사거리 게이트»가 구분된다");

            var offLane = SpawnDummy(em, bridge, offCell);
            PlaceWithFacing(bridge, cell, gunner, facing);

            // 초탄 쿨다운(attackCooldown)을 넉넉히 넘겨 기다린다 — 그동안 한 발도 안
            // 나가야 «레인 밖 = 존재하지 않는 것과 같다» 가 박제된다.
            yield return Seconds(gunner.attackCooldown + 1.5f);

            float d = Hp - em.GetComponentData<Health>(offLane).value;
            em.DestroyEntity(offLane);
            Object.Destroy(gunner);

            Assert.AreEqual(0f, d, 0.001f,
                "레인 밖(사거리 안) 적이 맞았다 — facing 유닛이 레인 게이트를 무시하고 START 했다");
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

        private static DefenderUnitData MakeGunner(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("machine_gunner"));
            unit.id = testId;
            unit.cost = 0;
            unit.maxOnBoard = 100;
            // 레거시 배치 효과(ForwardProjectile)가 배치 순간 자기 탄을 쏴 버스트 합산을
            // 오염시킨다 — 이 파일이 재는 것은 **평타 다연발**뿐이므로 사본에서 끈다.
            unit.onPlaceEffect = OnPlaceEffectType.None;
            var volley = unit.GetAbility<DirectionalVolleyAbility>();
            Assert.IsNotNull(volley, "머신거너에 DirectionalVolleyAbility 가 배선돼야 한다");
            Assert.IsTrue(volley.requiresFacing, "머신거너는 facing 유닛이다(requiresFacing=1 저작)");
            Assert.IsNotNull(volley.pattern, "볼리 패턴 미배선");
            Assert.Greater(volley.pattern.shots.Length, 1, "다연발 사양 — shot 이 여러 발이어야 한다");
            // bake 는 barrel ≠ unit.projectile 이면 패턴을 조용히 스킵한다 — 여기서 loud 하게.
            Assert.AreEqual(unit.projectile, volley.pattern.barrel,
                "볼리 barrel 은 유닛 projectile 과 같아야 bake 가 패턴을 받는다");
            return unit;
        }

        // 탄당 피해의 권위는 유닛의 Damage output 이다(패턴 SO damage 는 defender 평타에서
        // 실효값으로 덮인다). 모디파이어 없는 시험판이라 실효값 = 저작값.
        private static float AuthoredPerShot(DefenderUnitData unit)
        {
            float per = 0f;
            Assert.IsNotNull(unit.outputs, "outputs");
            foreach (var o in unit.outputs)
                if (o.kind == AttackOutputKind.Damage) per += o.magnitude;
            Assert.Greater(per, 0f, "Damage output 이 저작돼 있어야 발수 산술이 성립한다");
            return per;
        }

        // 격리 이유는 AbilityOnPlaceBlastTest.Prepare 주석 참조. 여기는 웨이브 차단이
        // 더 절실하다 — 웨이브 적이 레인에 들어오면 (a) 탄(pierce 저작)을 가로채 등식을
        // 깨고, (b) 레인 witness 가 되어 «레인 밖 무발사» 대조를 거짓으로 만든다.
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

        // facing 은 실사용(드래그 배치 + 조준 페이즈)이 쓰는 그 경로로 기록한다:
        // TryBeginDefenderDeployment → ActivateDeployedDefender(cell, entity, facing).
        // PlaceDefenderAs(탭)에는 facing 인자가 없다 — facing 유닛은 이 경로가 정본이다.
        private static Entity PlaceWithFacing(
            BattleBridge bridge, Vector2Int cell, DefenderUnitData unit, Vector2Int facing)
        {
            Assert.IsTrue(bridge.TryBeginDefenderDeployment(cell.x, cell.y, unit, out var entity),
                "pending 배치 시작");
            bridge.ActivateDeployedDefender(cell, entity, facing);
            return entity;
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

        // 배치 칸 + facing 방향: 레인 안(laneDist 전방) 셀과 레인 밖(수직 offDist) 셀이
        // 전부 그리드 안에 들어가는 조합을 고른다. 레인은 facing 축 1타일 폭이므로
        // 수직 오프셋 ≥ 1 이면 이미 레인 밖이다(LaneMath side ≠ 0).
        private static Vector2Int FindLaneLayout(
            BattleBridge bridge, EntityManager em, DefenderUnitData u, int laneDist, int offDist,
            out Vector2Int facing, out Vector2Int laneCell, out Vector2Int offCell)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, q.CalculateEntityCount(), "flow field 싱글턴");
            var ff = q.GetSingleton<FlowFieldSingleton>();

            bool InGrid(Vector2Int c)
                => c.x >= 0 && c.y >= 0 && c.x < ff.gridSize.x && c.y < ff.gridSize.y;

            var dirs = new[]
            {
                new Vector2Int(1, 0), new Vector2Int(-1, 0),
                new Vector2Int(0, 1), new Vector2Int(0, -1),
            };
            for (int x = 0; x < ff.gridSize.x; x++)
                for (int y = 0; y < ff.gridSize.y; y++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, u, out _)) continue;
                    var cell = new Vector2Int(x, y);
                    foreach (var f in dirs)
                    {
                        var perp = new Vector2Int(-f.y, f.x);
                        var lane = cell + f * laneDist;
                        var off = cell + perp * offDist;
                        if (!InGrid(lane) || !InGrid(off)) continue;
                        facing = f; laneCell = lane; offCell = off;
                        return cell;
                    }
                }
            Assert.Fail("레인/측면 셀이 그리드 안에 들어가는 배치 칸이 없다");
            facing = default; laneCell = default; offCell = default;
            return default;
        }
    }
}
