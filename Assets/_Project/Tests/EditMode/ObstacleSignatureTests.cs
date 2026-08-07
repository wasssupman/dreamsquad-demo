using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    // continuous-agent-movement unit 5 — 차단 셀 시그니처의 결정론 계약.
    public class ObstacleSignatureTests
    {
        private static uint Sig(params int2[] cells)
        {
            using var set = new NativeHashSet<int2>(8, Allocator.Temp);
            foreach (var c in cells) set.Add(c);
            return ObstacleSignature.Compute(set, hasObstacles: true);
        }

        [Test]
        public void InsertionOrder_DoesNotChangeSignature()
        {
            // 집합 순회 순서에 의존하면 내용이 같은데도 시그니처가 흔들려 헛 재빌드가 나고
            // 결정론이 깨진다. 이 성질이 이 함수의 존재 이유다.
            var a = Sig(new int2(1, 2), new int2(5, 0), new int2(3, 3));
            var b = Sig(new int2(3, 3), new int2(1, 2), new int2(5, 0));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void DifferentContent_DiffersSignature()
        {
            Assert.AreNotEqual(Sig(new int2(1, 2)), Sig(new int2(2, 1)),
                "대칭 좌표쌍이 상쇄되면 안 된다");
            Assert.AreNotEqual(Sig(new int2(1, 2)), Sig(new int2(1, 3)));
        }

        [Test]
        public void CountIsPartOfSignature()
        {
            // 해시 충돌로 변경을 영영 놓치는 사고를 개수로 완화한다.
            var one = Sig(new int2(1, 1));
            var two = Sig(new int2(1, 1), new int2(4, 4));
            Assert.AreNotEqual(one, two);
        }

        [Test]
        public void EmptySet_IsZero()
        {
            using var set = new NativeHashSet<int2>(4, Allocator.Temp);
            Assert.AreEqual(0u, ObstacleSignature.Compute(set, hasObstacles: true));
        }

        [Test]
        public void NoObstacles_IsZero_EvenWithNonEmptySet()
        {
            using var set = new NativeHashSet<int2>(4, Allocator.Temp);
            set.Add(new int2(1, 1));
            Assert.AreEqual(0u, ObstacleSignature.Compute(set, hasObstacles: false));
        }

        [Test]
        public void UncreatedSet_IsHarmless()
        {
            Assert.AreEqual(0u, ObstacleSignature.Compute(default, hasObstacles: true));
        }

        [Test]
        public void AddThenRemove_RestoresOriginalSignature()
        {
            // 해저드가 생겼다 부서지면 필드가 원래대로 돌아와야 한다.
            var before = Sig(new int2(2, 2));
            var during = Sig(new int2(2, 2), new int2(3, 1));
            var after  = Sig(new int2(2, 2));

            Assert.AreNotEqual(before, during);
            Assert.AreEqual(before, after);
        }
    }
}
