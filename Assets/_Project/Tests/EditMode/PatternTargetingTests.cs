using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat.Projectile;
using Wassup.Battle.Combat.Projectile.Emission;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // projectile-emission-pattern unit 0 — 타겟 선택 결정론. BarrageEpicenter
    // 흡수 무회귀(unit 4 이관 근거) + 셔플 결정론/분포 + 청크 순서 무관성.
    public class PatternTargetingTests
    {
        private static readonly int2 Grid = new int2(16, 16);

        private static int Select(int2[] cells, PatternSelectionRule rule, int fireCount)
        {
            using var na = new NativeArray<int2>(cells, Allocator.Temp);
            return PatternTargeting.Select(na, rule, fireCount, Grid);
        }

        [Test]
        public void EmptyPool_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, Select(new int2[0], PatternSelectionRule.RoundRobin, 0));
            Assert.AreEqual(-1, Select(new int2[0], PatternSelectionRule.DeterministicShuffle, 3));
        }

        [Test]
        public void RoundRobin_WalksRowMajorOrder_AndWraps()
        {
            // row-major 오름차순: (1,0) → (3,0) → (0,2) → 다시 (1,0).
            var cells = new[] { new int2(0, 2), new int2(1, 0), new int2(3, 0) };
            Assert.AreEqual(1, Select(cells, PatternSelectionRule.RoundRobin, 0));
            Assert.AreEqual(2, Select(cells, PatternSelectionRule.RoundRobin, 1));
            Assert.AreEqual(0, Select(cells, PatternSelectionRule.RoundRobin, 2));
            Assert.AreEqual(1, Select(cells, PatternSelectionRule.RoundRobin, 3));
        }

        // 흡수 전 BarrageEpicenter 와의 결과 일치는 f787c607(unit 0) 시점에 동일성
        // 테스트로 검증한 뒤 원본을 삭제했다(unit 4). 계약 자체는 위 row-major 순회
        // 테스트와 아래 스냅샷 순서 무관성 테스트가 계속 고정한다.
        [Test]
        public void Shuffle_IsDeterministic_SameFireCountSameResult()
        {
            var cells = new[] { new int2(2, 1), new int2(7, 4), new int2(3, 9), new int2(11, 2) };
            for (int fire = 0; fire < 8; fire++)
                Assert.AreEqual(
                    Select(cells, PatternSelectionRule.DeterministicShuffle, fire),
                    Select(cells, PatternSelectionRule.DeterministicShuffle, fire));
        }

        [Test]
        public void Shuffle_IsNotAPlainRotation()
        {
            // 셔플이 사실상 round-robin 이면 "랜덤" 체감이 없다 — 최소 한 지점에서 갈려야 한다.
            var cells = new[] { new int2(2, 1), new int2(7, 4), new int2(3, 9), new int2(11, 2) };
            bool diverged = false;
            for (int fire = 0; fire < 16 && !diverged; fire++)
                diverged = Select(cells, PatternSelectionRule.DeterministicShuffle, fire)
                        != Select(cells, PatternSelectionRule.RoundRobin, fire);
            Assert.IsTrue(diverged);
        }

        [Test]
        public void Shuffle_CoversEveryCandidate_OverManyShots()
        {
            var cells = new[] { new int2(2, 1), new int2(7, 4), new int2(3, 9), new int2(11, 2) };
            var seen = new bool[cells.Length];
            for (int fire = 0; fire < 200; fire++)
            {
                int idx = Select(cells, PatternSelectionRule.DeterministicShuffle, fire);
                Assert.GreaterOrEqual(idx, 0);
                seen[idx] = true;
            }
            for (int i = 0; i < seen.Length; i++)
                Assert.IsTrue(seen[i], $"후보 {i} 가 200발 동안 한 번도 선택되지 않았다(편향)");
        }

        // README 계약 6 — ECS 청크 순서에 의존하면 같은 index 가 프레임마다 다른
        // 대상을 가리킨다. 스냅샷 순서를 뒤집어도 "같은 셀" 이 선택돼야 한다.
        [Test]
        public void SnapshotOrder_DoesNotAffectSelectedCell()
        {
            var a = new[] { new int2(0, 2), new int2(1, 0), new int2(3, 0) };
            var b = new[] { new int2(3, 0), new int2(0, 2), new int2(1, 0) };

            foreach (var rule in new[] { PatternSelectionRule.RoundRobin, PatternSelectionRule.DeterministicShuffle })
                for (int fire = 0; fire < 6; fire++)
                {
                    int2 pickedA = a[Select(a, rule, fire)];
                    int2 pickedB = b[Select(b, rule, fire)];
                    Assert.AreEqual(pickedA, pickedB, $"{rule} fire={fire}: 스냅샷 순서가 선택을 바꿨다");
                }
        }

        // 리뷰 MEDIUM — 같은 셀에 후보가 둘 이상이면 tie-break 가 스냅샷 index 로 갈린다.
        // 방어유닛은 타일 고정이라 유일하지만 적 후보(자유 이동)는 셀 공유가 상시다.
        // 현재는 bake 가 host=enemy 로 고정돼 미도달이고, defender 패턴을 여는 시점에
        // 안정 키(예: 병렬 배열로 받은 entity index)를 함께 넘겨야 한다. 이 테스트는
        // 그 한계를 **문서화**한다 — 통과가 곧 "셀 단위까지만 결정론"이라는 뜻이다.
        [Test]
        public void DuplicateCells_SelectionIsCellStable_ButNotEntityStable()
        {
            var a = new[] { new int2(4, 4), new int2(4, 4), new int2(9, 1) };
            var b = new[] { new int2(4, 4), new int2(9, 1), new int2(4, 4) };

            for (int fire = 0; fire < 6; fire++)
            {
                int2 pickedA = a[Select(a, PatternSelectionRule.RoundRobin, fire)];
                int2 pickedB = b[Select(b, PatternSelectionRule.RoundRobin, fire)];
                Assert.AreEqual(pickedA, pickedB, "선택된 '셀' 은 스냅샷 순서와 무관해야 한다");
            }
        }

        [Test]
        public void NegativeFireCount_StaysInRange()
        {
            var cells = new[] { new int2(1, 1), new int2(2, 2) };
            foreach (var rule in new[] { PatternSelectionRule.RoundRobin, PatternSelectionRule.DeterministicShuffle })
            {
                int idx = Select(cells, rule, -3);
                Assert.GreaterOrEqual(idx, 0);
                Assert.Less(idx, cells.Length);
            }
        }

        // MovementBinding 분류 누락 핀 — C# 은 enum switch 전수성을 강제하지 못하므로
        // 새 MovementKind 가 추가되면 이 테스트가 실패해 분류 갱신을 강제한다.
        [Test]
        public void MovementBinding_ClassifiesEveryKnownKind()
        {
            var kinds = (MovementKind[])Enum.GetValues(typeof(MovementKind));
            Assert.AreEqual(MovementBinding.KnownKindCount, kinds.Length,
                "MovementKind 가 늘었다 — MovementBinding.Of 분류와 KnownKindCount 를 갱신하라");

            Assert.AreEqual(BindingClass.Entity, MovementBinding.Of(MovementKind.HomingToEntity));
            Assert.AreEqual(BindingClass.Entity, MovementBinding.Of(MovementKind.BezierHomingToEntity));
            Assert.AreEqual(BindingClass.Cell, MovementBinding.Of(MovementKind.BallisticArcToPoint));
            Assert.AreEqual(BindingClass.Cell, MovementBinding.Of(MovementKind.SkyFall));
            Assert.AreEqual(BindingClass.Cell, MovementBinding.Of(MovementKind.GrenadeToCell));
            Assert.AreEqual(BindingClass.Direction, MovementBinding.Of(MovementKind.DirectionalLinear));
        }
    }
}
