using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // boss-defender-field unit 2 rev — zero-flow 복구 방향 순수함수 (ecs-review M3).
    public class FlowRecoveryTests
    {
        [Test]
        public void RecoveryDir_PicksSmallestDistNeighbor()
        {
            // 3x1: dist [2, 9, 5]. 가운데(9)에서 좌(2) < 우(5) → -x.
            var gridSize = new int2(3, 1);
            var dist = new NativeArray<int>(3, Allocator.Temp);
            try
            {
                dist[0] = 2; dist[1] = 9; dist[2] = 5;
                Assert.AreEqual(new float2(-1, 0), FlowRecovery.RecoveryDir(new int2(1, 0), dist, gridSize));
            }
            finally { dist.Dispose(); }
        }

        [Test]
        public void RecoveryDir_NoBetterNeighbor_ReturnsZero()
        {
            // 전부 MaxValue → 고립. dist 0 셀(모든 이웃이 크거나 같음)도 zero.
            var gridSize = new int2(3, 1);
            var dist = new NativeArray<int>(3, Allocator.Temp);
            try
            {
                for (int i = 0; i < 3; i++) dist[i] = int.MaxValue;
                Assert.AreEqual(float2.zero, FlowRecovery.RecoveryDir(new int2(1, 0), dist, gridSize), "all-MAX isolated");

                dist[0] = 5; dist[1] = 0; dist[2] = 5;
                Assert.AreEqual(float2.zero, FlowRecovery.RecoveryDir(new int2(1, 0), dist, gridSize), "already at minimum");
            }
            finally { dist.Dispose(); }
        }

        [Test]
        public void RecoveryDir_CornerCell_NoOutOfBounds()
        {
            // 2x2 코너(0,0): 이웃은 (1,0),(0,1)만. OOB 접근 없이 최소 선택.
            var gridSize = new int2(2, 2);
            var dist = new NativeArray<int>(4, Allocator.Temp);
            try
            {
                dist[0] = 7;              // (0,0)
                dist[1] = 3;              // (1,0)
                dist[2] = 5;              // (0,1)
                dist[3] = int.MaxValue;   // (1,1)
                Assert.AreEqual(new float2(1, 0), FlowRecovery.RecoveryDir(new int2(0, 0), dist, gridSize));
            }
            finally { dist.Dispose(); }
        }

        [Test]
        public void RecoveryDir_DistArraySwap_ChangesDirection()
        {
            // 사냥 분기 계약: 같은 셀이라도 goal dist ↔ defender dist 에 따라 방향이 바뀐다.
            var gridSize = new int2(3, 1);
            var goalDist = new NativeArray<int>(3, Allocator.Temp);
            var huntDist = new NativeArray<int>(3, Allocator.Temp);
            try
            {
                // goal: 우측이 가까움 / hunt(방어유닛): 좌측이 가까움.
                goalDist[0] = 9; goalDist[1] = 5; goalDist[2] = 1;
                huntDist[0] = 1; huntDist[1] = 5; huntDist[2] = 9;
                Assert.AreEqual(new float2( 1, 0), FlowRecovery.RecoveryDir(new int2(1, 0), goalDist, gridSize), "goal field → +x");
                Assert.AreEqual(new float2(-1, 0), FlowRecovery.RecoveryDir(new int2(1, 0), huntDist, gridSize), "defender field → -x");
            }
            finally { goalDist.Dispose(); huntDist.Dispose(); }
        }
    }
}
