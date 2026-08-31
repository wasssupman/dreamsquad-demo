using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;

namespace Wassup.Tests.PlayMode
{
    // distance-based-range unit 0 — **교착 카나리아**.
    //
    // 사거리 술어를 보는 곳이 13지점(경유 6 / 미경유 7)인데, 그 중 하나만 조여도
    // 「멈추는 근거」와 「쏘는 근거」가 갈려 유닛이 얼어붙는다. 2026-08-12 에 두 번 났고
    // (`summon-patrol-defender` unit 11) **두 번 다 사람 눈으로만** 발견됐다 — 얼어붙은 유닛도
    // 스폰·컴포넌트·앵커 단언은 전부 통과하기 때문이다.
    //
    // 그래서 이 테스트는 상태가 아니라 **「멈췄는데 안 쏜다」는 사건**을 본다.
    // 실패하면 최소 접근거리와 AI 상태 궤적을 같이 찍는다 — 그게 없으면 원인을 못 찾는다.
    //
    // ⚠ 이 테스트는 **절대값을 안 박는다.** 자가 바뀌어도(unit 4a) 초록이어야 한다.
    // 절대값 회귀는 골든이 진다(spec 계약 13).
    public class RangePredicateMirrorTest
    {
        // 멈춘 상태가 이만큼 이어지는데 공격이 한 번도 없으면 교착으로 본다.
        // 2026-08-12 실측 교착이 182프레임이었다 — 그보다 넉넉히 위.
        private const int StuckFrameBudget = 300;
        private const int TotalFrames = 900;

        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoppedEnemy_AlwaysEventuallyFires()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindFirstObjectByType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge 를 못 찾았다");

            bridge.BeginPlacement();
            var gm = Object.FindFirstObjectByType<GameManager>();
            if (gm != null) { gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000); }
            yield return null;

            // ⚠ **골 근처에 놓는다.** 첫 배치 가능 칸에 놓으면 맵에 따라 적 경로에서 멀어져
            // 「교전 자체가 없는」 판이 되고, 그러면 이 카나리아가 통과해도 아무것도 증언하지
            // 않는다 — 골든 하네스가 정확히 그 상태로 203 커밋을 보냈다(`89e65d05`).
            int placed = PlaceNearGoal(bridge, 3);
            Assert.Greater(placed, 0, "방어유닛을 한 기도 못 놓았다 — 이 판은 아무것도 증언 못 한다");

            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            // ⚠ **알려진 한계 — 기믹이 신호를 흐린다.** 이 카나리아의 「피해가 들어갔나」는
            // 적 체력 델타인데, 시즌 기믹 중 사직서(메테오)는 방어유닛과 무관하게 적을 깎는다.
            // 그러면 정지-무피해 연속이 끊겨 **교착을 놓칠 수** 있다(거짓 통과 방향).
            // 기믹을 걷어내려면 `SetAssignedGimmick(null)` 만으로는 부족하고 config 엔티티까지
            // 파괴해야 한다(`WhirlpotLiveRepro` 가 그 절차를 갖고 있다) — 이 카나리아의 목적에
            // 비해 과한 결합이라 **한계로 남긴다.** 실패 쪽으로는 안 기울므로 그물이 거짓 빨강을
            // 내지는 않는다.
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            int damageFrames = 0;
            float prevHp, prevMax;
            EnemyHp(em, out prevHp, out prevMax);
            int stuckStreak = 0, worstStreak = 0;
            float minApproach = float.MaxValue;
            var trail = new StringBuilder();

            for (int f = 0; f < TotalFrames; f++)
            {
                yield return null;

                // ⚠ `IncomingDamage` 버퍼를 세면 안 된다 — `DamageApplicationSystem` 이 **같은
                // 프레임에** 비우므로 코루틴 시점엔 항상 0 이고, 그러면 이 카나리아가 전 프레임을
                // 「공격 없음」으로 읽어 거짓 교착을 낸다. 대신 **적 총 체력이 줄었나**를 본다 —
                // 큐 타이밍에 안 기대고 「실제로 피해가 들어갔나」를 직접 말한다.
                // ⚠ 합계만 보면 **유출로 사라진 적**도 「피해」로 읽혀 교착 판정이 리셋된다.
                // 최대체력 합과 나란히 본다 — 소멸은 둘 다 줄이고, **피해는 현재값만** 줄인다.
                float hp, max;
                EnemyHp(em, out hp, out max);
                bool damaged = (prevHp - hp) > (prevMax - max) + 0.01f;
                prevHp = hp; prevMax = max;
                if (damaged) damageFrames++;

                bool anyStopped = AnyEnemyStopped(em, out var state, out float approach);
                if (approach < minApproach) minApproach = approach;

                if (anyStopped && !damaged)
                {
                    stuckStreak++;
                    if (stuckStreak > worstStreak) worstStreak = stuckStreak;
                    if (trail.Length < 900 && (stuckStreak % 60 == 1))
                        trail.Append($"f{f}:{state}/d{approach:0.00} ");
                }
                else stuckStreak = 0;

                Assert.Less(stuckStreak, StuckFrameBudget,
                    $"교착: 적이 {stuckStreak}프레임 멈춰 있는데 피해 0. "
                    + $"최소 접근거리={minApproach:0.00}칸 · 상태궤적=[{trail}] "
                    + "— 「멈추는 근거」와 「쏘는 근거」가 갈렸다(사거리 술어 미러 확인).");
            }

            // 판이 성립했는지 — 교전이 없으면 위 단언이 공허하게 통과한다.
            Assert.Greater(damageFrames, 0,
                $"{TotalFrames}프레임 동안 피해가 한 번도 안 들어갔다(최소 접근거리 {minApproach:0.00}칸). "
                + "교착이 아니라 **이 판이 아무것도 증언하지 않는다** — 배치가 교전에 닿았는지 확인하라. "
                + "골든 하네스가 정확히 이 상태로 203 커밋을 보냈다(`89e65d05`).");
            Debug.Log($"[RangeMirror] 피해 프레임 {damageFrames} · 최장 정지-무피해 {worstStreak}프레임 "
                      + $"· 최소 접근거리 {minApproach:0.00}칸");
        }

        // ── 헬퍼 ────────────────────────────────────────────

        // 골에 가장 가까운 배치 가능 칸부터 채운다(하네스 `SimHarnessRunner` 와 같은 규칙).
        private static int PlaceNearGoal(BattleBridge bridge, int count)
        {
            var pool = bridge.DefenderPool;
            if (pool == null || pool.Length == 0) return 0;
            var goals = bridge.DebugGoalCells;
            var grid = bridge.DebugGridSize;
            int placed = 0;
            for (int slot = 0; slot < count; slot++)
            {
                var unit = pool[slot % pool.Length];
                int bestX = -1, bestY = -1, bestD = int.MaxValue;
                for (int y = 0; y < grid.y; y++)
                for (int x = 0; x < grid.x; x++)
                {
                    if (!bridge.CanPlaceDefenderAt(x, y, unit, out _)) continue;
                    int d = int.MaxValue;
                    for (int g = 0; g < goals.Length; g++)
                    {
                        int dx = Mathf.Abs(x - goals[g].x), dy = Mathf.Abs(y - goals[g].y);
                        d = Mathf.Min(d, Mathf.Max(dx, dy));
                    }
                    if (goals.Length == 0) d = y * grid.x + x;
                    if (d < bestD) { bestD = d; bestX = x; bestY = y; }
                }
                if (bestX < 0) break;
                bridge.PlaceDefenderAs(bestX, bestY, unit);
                placed++;
            }
            return placed;
        }

        // 「멈춘 적」 = 이동을 포기한 상태(Engaging/Standoff). 그 상태가 곧 «쏠 수 있다» 는
        // 판단의 결과이므로, 멈췄는데 안 쏘면 두 판단이 갈린 것이다.
        private static bool AnyEnemyStopped(EntityManager em, out AiState state, out float approach)
        {
            state = AiState.Marching;
            approach = float.MaxValue;
            var q = em.CreateEntityQuery(typeof(EnemyAiState), typeof(LocalTransform), typeof(AttackUnitTag));
            var ents = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var qd = em.CreateEntityQuery(typeof(DefenderUnitTag), typeof(LocalTransform));
            var defs = qd.ToComponentDataArray<LocalTransform>(Unity.Collections.Allocator.Temp);
            bool any = false;
            for (int i = 0; i < ents.Length; i++)
            {
                var s = em.GetComponentData<EnemyAiState>(ents[i]).value;
                var p = em.GetComponentData<LocalTransform>(ents[i]).Position;
                for (int d = 0; d < defs.Length; d++)
                {
                    var q2 = defs[d].Position;
                    float cheb = Mathf.Max(Mathf.Abs(p.x - q2.x), Mathf.Abs(p.z - q2.z));
                    if (cheb < approach) approach = cheb;
                }
                if (s == AiState.Engaging || s == AiState.Standoff) { any = true; state = s; }
            }
            ents.Dispose(); defs.Dispose();
            return any;
        }

        // 살아 있는 적의 (현재체력 합, 최대체력 합). 큐 소비 타이밍에 안 기댄다.
        // **둘을 같이 내는 이유**: 적이 사라지면 둘 다 줄지만 피해는 현재값만 줄인다.
        // 그 차이가 곧 「이 프레임에 실제로 피해가 들어갔나」다 — 유출·처치와 피해를 가른다.
        private static void EnemyHp(EntityManager em, out float value, out float max)
        {
            var q = em.CreateEntityQuery(typeof(Health), typeof(AttackUnitTag));
            var arr = q.ToComponentDataArray<Health>(Unity.Collections.Allocator.Temp);
            value = 0f; max = 0f;
            for (int i = 0; i < arr.Length; i++) { value += arr[i].value; max += arr[i].max; }
            arr.Dispose();
        }
    }
}
