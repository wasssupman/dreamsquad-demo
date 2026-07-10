using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // enemy-hunter-targeting unit 0 — hunter transition axis on Evaluate +
    // nearest-pick determinism. The 3-arg Evaluate overload (non-hunter) is
    // pinned separately by EnemyAiStateTransitionTests (무회귀 보증).
    public class HunterTargetingTests
    {
        // ── Evaluate hunter axis ────────────────────────────────────────────

        [Test]
        public void Hunter_NoFireTarget_HasHuntTarget_Chasing()
        {
            Assert.AreEqual(AiState.Chasing, EnemyAiStateSystem.Evaluate(
                aggroed: false, guardianInRange: false, hasFireTarget: false,
                isHunter: true, hasHuntTarget: true));
        }

        [Test]
        public void Hunter_NoFireTarget_NoHuntTarget_Marching()
        {
            // 방어유닛 0 → 추격 대상 없음 → goal.
            Assert.AreEqual(AiState.Marching, EnemyAiStateSystem.Evaluate(
                aggroed: false, guardianInRange: false, hasFireTarget: false,
                isHunter: true, hasHuntTarget: false));
        }

        [Test]
        public void Hunter_FireTargetInRange_Engaging_OverridesHunt()
        {
            // 사거리 내 타겟이 추격보다 우선(멈춰 공격).
            Assert.AreEqual(AiState.Engaging, EnemyAiStateSystem.Evaluate(
                aggroed: false, guardianInRange: false, hasFireTarget: true,
                isHunter: true, hasHuntTarget: true));
        }

        [Test]
        public void Hunter_Aggroed_AggroWins_OverHunt()
        {
            // aggro 우선 — 헌터여도 가디언 체이스.
            Assert.AreEqual(AiState.Chasing, EnemyAiStateSystem.Evaluate(
                aggroed: true, guardianInRange: false, hasFireTarget: false,
                isHunter: true, hasHuntTarget: true));
            Assert.AreEqual(AiState.Standoff, EnemyAiStateSystem.Evaluate(
                aggroed: true, guardianInRange: true, hasFireTarget: false,
                isHunter: true, hasHuntTarget: true));
        }

        [Test]
        public void NonHunter_HasHuntTargetFlag_IgnoredStaysMarching()
        {
            // isHunter=false 면 hasHuntTarget 무시 — 기존 경로 무회귀.
            Assert.AreEqual(AiState.Marching, EnemyAiStateSystem.Evaluate(
                aggroed: false, guardianInRange: false, hasFireTarget: false,
                isHunter: false, hasHuntTarget: true));
        }

        // ── NearestIndex determinism ────────────────────────────────────────

        private static int Nearest(int2 origin, int2[] cells, int[] keys)
        {
            using var c = new NativeArray<int2>(cells, Allocator.Temp);
            using var k = new NativeArray<int>(keys, Allocator.Temp);
            return HunterTargeting.NearestIndex(origin, c, k);
        }

        [Test]
        public void Nearest_Empty_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, Nearest(new int2(0, 0), new int2[0], new int[0]));
        }

        [Test]
        public void Nearest_PicksChebyshevMinimum()
        {
            var cells = new[] { new int2(5, 0), new int2(2, 1), new int2(0, 4) };
            // Chebyshev from (0,0): 5, 2, 4 → index 1.
            Assert.AreEqual(1, Nearest(new int2(0, 0), cells, new[] { 10, 20, 30 }));
        }

        [Test]
        public void Nearest_DistanceTie_BreaksToLowerKey_OrderIndependent()
        {
            // (3,0) and (0,3) both Chebyshev 3 from origin; lower key wins.
            var a = new[] { new int2(3, 0), new int2(0, 3) };
            var b = new[] { new int2(0, 3), new int2(3, 0) };
            Assert.AreEqual(7, KeyAt(a, new[] { 9, 7 }));   // (0,3) has key 7
            Assert.AreEqual(7, KeyAt(b, new[] { 7, 9 }));   // same cell, key 7, other order
        }

        private static int KeyAt(int2[] cells, int[] keys)
        {
            int idx = Nearest(new int2(0, 0), cells, keys);
            return keys[idx];
        }
    }
}
