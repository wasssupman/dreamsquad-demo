using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // multi-goal-map 유닛 2 회귀 가드 — IsGoalCell(골 판정의 행동 핵심).
    // goals 멤버십 브랜치는 sim 픽스처(goalCell 만 세팅)가 안 타므로 직접 검증한다.
    public class FlowFieldSingletonTests
    {
        [Test]
        public void IsGoalCell_GoalsSet_TrueForEachGoal_FalseOtherwise()
        {
            var goals = new NativeArray<int2>(2, Allocator.Temp);
            goals[0] = new int2(1, 1);
            goals[1] = new int2(3, 3);
            var f = new FlowFieldSingleton { goals = goals, goalCell = new int2(1, 1) };
            try
            {
                Assert.IsTrue(f.IsGoalCell(new int2(1, 1)), "첫 골");
                Assert.IsTrue(f.IsGoalCell(new int2(3, 3)), "둘째 골");
                Assert.IsFalse(f.IsGoalCell(new int2(2, 2)), "비골");
                Assert.IsFalse(f.IsGoalCell(new int2(0, 0)), "비골");
            }
            finally { goals.Dispose(); }
        }

        [Test]
        public void IsGoalCell_GoalsUncreated_FallsBackToGoalCell()
        {
            var f = new FlowFieldSingleton { goalCell = new int2(5, 5) }; // goals = default(uncreated)
            Assert.IsFalse(f.goals.IsCreated);
            Assert.IsTrue(f.IsGoalCell(new int2(5, 5)), "폴백 goalCell 일치");
            Assert.IsFalse(f.IsGoalCell(new int2(1, 1)));
        }

        [Test]
        public void IsGoalCell_EmptyGoals_FallsBackToGoalCell()
        {
            var goals = new NativeArray<int2>(0, Allocator.Temp);
            var f = new FlowFieldSingleton { goals = goals, goalCell = new int2(4, 2) };
            try
            {
                Assert.IsTrue(f.IsGoalCell(new int2(4, 2)));
                Assert.IsFalse(f.IsGoalCell(new int2(0, 0)));
            }
            finally { goals.Dispose(); }
        }

        [Test]
        public void IsGoalCell_DuplicateGoals_Harmless()
        {
            var goals = new NativeArray<int2>(2, Allocator.Temp);
            goals[0] = new int2(2, 2);
            goals[1] = new int2(2, 2);
            var f = new FlowFieldSingleton { goals = goals, goalCell = new int2(2, 2) };
            try
            {
                Assert.IsTrue(f.IsGoalCell(new int2(2, 2)));
                Assert.IsFalse(f.IsGoalCell(new int2(1, 1)));
            }
            finally { goals.Dispose(); }
        }
    }
}
