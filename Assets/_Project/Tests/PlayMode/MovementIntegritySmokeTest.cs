using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.PlayMode
{
    // enemy-tile-movement-integrity / aggro-standoff / attack-hit-delay / enemy-ai-fsm — 합성 경로 통합 smoke (투트랙 리뷰 M2).
    // pure-math seam(MovementCellTrim/SpawnSpread/PathSmoothing)은 EditMode 로 검증됨. 여기서는 라이브 합성:
    // (LateralRecenter 는 continuous-agent-movement unit 10 에서 은퇴 — 코너 밀착 목표와 정면 충돌.)
    //  (a) tile invariant — 실 전투 동안 모든 적이 walk 타일 위 유지(aggro chase→cell-trim, flow→recenter→cell-trim, zeroFlow skip).
    //  (b) aggro 전투 — 더미 guardian 에 aggro 적이 tile-Chebyshev 사거리 도달(M1) → AttackSystem RESOLVE 로 데미지(unit 1).
    // enemy-ai-fsm: aggro 경로는 이제 EnemyAiState FSM 으로 흐른다 — guardian aggro 시 Chasing(self-walk) →
    // 사거리 도달 시 Standoff(정지 공격). 레거시 movePause/aimMode 가정 없음. per-state 결정성(Marching/
    // Engaging-Halt/Engaging-Advance/Standoff/Chasing self-walk/Halt 직교성)은 MovementSystemTests EditMode 가 잠금.
    public class MovementIntegritySmokeTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator Battle_StaysOnTiles_AndAggroDealsDamage()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            bridge.StartBattle();
            bridge.ForceNextWave();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var enemyQ = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>());

            // wait until a wave has spawned
            float waited = 0f;
            while (enemyQ.CalculateEntityCount() == 0 && waited < 12f) { waited += Time.deltaTime; yield return null; }
            Assert.Greater(enemyQ.CalculateEntityCount(), 0, "enemies spawned");

            var ffQ = em.CreateEntityQuery(typeof(FlowFieldSingleton));
            Assert.AreEqual(1, ffQ.CalculateEntityCount(), "flow field present");
            var field = ffQ.GetSingleton<FlowFieldSingleton>();

            // dummy guardian on a non-walk cell near the enemy centroid (wide range → aggros many).
            int2 gcell = FindGuardianCell(em, field);
            Assert.GreaterOrEqual(gcell.x, 0, "found a non-walk guardian cell near enemies");
            var gpos = new float3(field.origin.x + gcell.x * field.tileSize, 0f, field.origin.z + gcell.y * field.tileSize);
            var guardian = em.CreateEntity();
            em.AddComponentData(guardian, LocalTransform.FromPosition(gpos));
            em.AddComponentData(guardian, new AggroCapacity { max = 16, held = 0 });
            em.AddComponentData(guardian, new Health { value = 100000f, max = 100000f });
            em.AddComponentData(guardian, new FactionTag { value = Faction.DefenderUnit });
            em.AddBuffer<IncomingDamage>(guardian);
            // aggro-tile-chase unit 4 — 히트 구동(b84b6887) 이후 어그로는 "가디언이 때려야"
            // 발생한다. 무장 없는 더미는 AggroAcquireEvent 를 못 내 sawAggro 가 구조적으로 false
            // 였다(스모크 부채). 광역 공격을 부여해 히트→어그로→추격 체인을 실제로 돌린다.
            em.AddComponentData(guardian, new Wassup.Battle.Combat.AttackState
            {
                range = 8f, cooldownDuration = 0.5f, cooldownRemaining = 0f,
                attackTargetCount = 4, targetMask = (int)Faction.EnemyUnit,
            });
            var gOut = em.AddBuffer<Wassup.Battle.Combat.AttackOutputElement>(guardian);
            gOut.Add(new Wassup.Battle.Combat.AttackOutputElement
            {
                value = new Wassup.Data.AttackOutput
                {
                    kind = Wassup.Data.AttackOutputKind.Damage,
                    magnitude = 1f,
                },
            });

            // run window: every frame assert tile invariant; track aggro + guardian damage.
            float t = 0f; int worstOffWalk = 0; bool sawAggro = false;
            var aggroQ = em.CreateEntityQuery(typeof(Aggroed));
            while (t < 5f)
            {
                t += Time.deltaTime;
                int off = CountOffWalk(em, field);
                if (off > worstOffWalk) worstOffWalk = off;
                if (aggroQ.CalculateEntityCount() > 0) sawAggro = true;
                yield return null;
            }
            float gh = em.Exists(guardian) ? em.GetComponentData<Health>(guardian).value : 0f;
            if (em.Exists(guardian)) em.DestroyEntity(guardian);

            // (a) tile invariant — cell-trim (aggro + flow recenter) keeps every active enemy on walk tiles.
            Assert.AreEqual(0, worstOffWalk, "no enemy ever left walk tiles during the window");
            // (b) aggro reach + RESOLVE damage.
            Assert.IsTrue(sawAggro, "guardian aggroed at least one enemy");
            Assert.Less(gh, 100000f, "aggroed enemies reached range and dealt damage to the guardian");
        }

        // active(non-PastGoal) 적 중 walk 아닌 셀에 있는 수.
        private static int CountOffWalk(EntityManager em, FlowFieldSingleton field)
        {
            var q = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<AttackUnitTag>(), ComponentType.ReadOnly<LocalTransform>() },
                None = new[] { ComponentType.ReadOnly<PastGoalTag>() },
            });
            var lts = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            int off = 0;
            for (int i = 0; i < lts.Length; i++)
            {
                var c = GridMath.WorldToCell(lts[i].Position, field.tileSize, field.gridSize, field.origin);
                var f = field.flow[GridMath.CellIndex(c, field.gridSize)];
                bool walk = (f.x != 0f || f.y != 0f) || field.IsGoalCell(c);   // multi-goal
                if (!walk) off++;
            }
            lts.Dispose();
            return off;
        }

        private static int2 FindGuardianCell(EntityManager em, FlowFieldSingleton field)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<AttackUnitTag>(), ComponentType.ReadOnly<LocalTransform>());
            var lts = q.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            if (lts.Length == 0) { lts.Dispose(); return new int2(-1, -1); }
            float sx = 0, sy = 0;
            for (int i = 0; i < lts.Length; i++)
            {
                var c = GridMath.WorldToCell(lts[i].Position, field.tileSize, field.gridSize, field.origin);
                sx += c.x; sy += c.y;
            }
            int cx = (int)math.round(sx / lts.Length), cy = (int)math.round(sy / lts.Length);
            lts.Dispose();
            int[] ox = { 1, -1, 0, 0 }, oy = { 0, 0, 1, -1 };
            for (int r = 0; r <= 6; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int X = cx + dx, Y = cy + dy;
                        if (X < 0 || X >= field.gridSize.x || Y < 0 || Y >= field.gridSize.y) continue;
                        var f = field.flow[Y * field.gridSize.x + X];
                        bool walk = (f.x != 0f || f.y != 0f) || field.IsGoalCell(new int2(X, Y));   // multi-goal
                        if (walk) continue;
                        for (int k = 0; k < 4; k++)
                        {
                            int nx = X + ox[k], ny = Y + oy[k];
                            if (nx < 0 || nx >= field.gridSize.x || ny < 0 || ny >= field.gridSize.y) continue;
                            var nf = field.flow[ny * field.gridSize.x + nx];
                            if (nf.x != 0f || nf.y != 0f) return new int2(X, Y);
                        }
                    }
            return new int2(-1, -1);
        }
    }
}
