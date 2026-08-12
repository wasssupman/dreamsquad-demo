using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // instinct-content unit 3 — 거점 목적지의 두 축.
    //   (1) 「어느 거점으로 갈까」 규칙 — 순수 함수
    //   (2) 「그 거점으로 흐르는 필드가 실제로 구워지나」 — 중심이 통행 불가일 때의 함정
    public class StructureDestinationTests
    {
        // ───────────────────── (1) 선택 규칙 ─────────────────────

        [Test]
        public void NearestIndex_PicksClosest()
        {
            var cands = new NativeArray<float2>(3, Allocator.Temp);
            cands[0] = new float2(10f, 0f);
            cands[1] = new float2(2f, 0f);
            cands[2] = new float2(5f, 0f);
            try
            {
                Assert.AreEqual(1, StructureChoice.NearestIndex(float2.zero, cands));
                Assert.AreEqual(0, StructureChoice.NearestIndex(new float2(11f, 0f), cands));
            }
            finally { cands.Dispose(); }
        }

        // 동률은 **먼저 온 후보**가 이긴다. 후보 순서 = 저작 순서라 같은 판이 같은 답을 낸다.
        // 흔들면(랜덤 타이브레이크) 리플레이가 갈린다.
        [Test]
        public void NearestIndex_TieGoesToTheEarlierCandidate_ForDeterminism()
        {
            var cands = new NativeArray<float2>(2, Allocator.Temp);
            cands[0] = new float2(0f, 3f);
            cands[1] = new float2(3f, 0f);   // 원점에서 거리 동일
            try { Assert.AreEqual(0, StructureChoice.NearestIndex(float2.zero, cands)); }
            finally { cands.Dispose(); }
        }

        [Test]
        public void NearestIndex_NoCandidates_ReturnsMinusOne()
        {
            var empty = new NativeArray<float2>(0, Allocator.Temp);
            try { Assert.AreEqual(-1, StructureChoice.NearestIndex(float2.zero, empty)); }
            finally { empty.Dispose(); }
        }

        // ───────────────────── (2) 다중 소스 ─────────────────────

        // 거점 목적지의 BFS 소스는 footprint **전체**다. 중심 1칸으로 쓰면 안 된다 —
        // Coil 의 본능 중심 (10,6) 은 Place 타일이라 그 슬롯이 통째로 빈 필드가 된다.
        // Duel 은 footprint 9/9 가 Walk 라 이 함정을 혼자서는 못 잡는다. 두 형태를 다 잰다.
        [Test]
        public void FootprintSources_StillBuildAField_WhenTheCenterIsNotWalkable()
        {
            const int w = 7, h = 7, n = w * h;
            var walk = new NativeArray<byte>(n, Allocator.Temp);
            var sources = new NativeArray<int2>(9, Allocator.Temp);
            var flow = new NativeArray<float2>(n, Allocator.Temp);
            var dist = new NativeArray<int>(n, Allocator.Temp);
            try
            {
                for (int i = 0; i < n; i++) walk[i] = 1;
                var center = new int2(3, 3);
                walk[center.y * w + center.x] = 0;   // Coil 형태 — 중심만 통행 불가

                int k = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        sources[k++] = new int2(center.x + dx, center.y + dy);

                FlowFieldBuilder.BuildFromSources(walk, new int2(w, h), sources, flow, dist);

                Assert.AreEqual(0, dist[(center.y - 1) * w + center.x],
                    "통행 가능한 footprint 칸은 소스가 된다");
                Assert.AreNotEqual(int.MaxValue, dist[0],
                    "먼 칸까지 거리가 퍼져야 한다 — 중심만 보고 소스를 잡으면 여기가 MaxValue 다");
                Assert.AreEqual(int.MaxValue, dist[center.y * w + center.x],
                    "통행 불가한 중심 자신은 도달 대상이 아니다");
            }
            finally
            {
                dist.Dispose(); flow.Dispose(); sources.Dispose(); walk.Dispose();
            }
        }

        // 그 통행 층으로 아무 칸도 못 여는 거점 = 유효 소스 0 → 전 셀 int.MaxValue.
        // MovementSystem 은 이 값을 보고 골로 되돌아간다(빈 슬롯이 «이미 도착» 으로 읽히면 안 된다).
        [Test]
        public void UnreachableStructure_LeavesTheWholeFieldAtMaxValue_AsGoalFallbackSignal()
        {
            const int w = 5, h = 5, n = w * h;
            var walk = new NativeArray<byte>(n, Allocator.Temp);
            var sources = new NativeArray<int2>(9, Allocator.Temp);
            var flow = new NativeArray<float2>(n, Allocator.Temp);
            var dist = new NativeArray<int>(n, Allocator.Temp);
            try
            {
                for (int i = 0; i < n; i++) walk[i] = 0;   // 이 층으로는 아무 칸도 못 연다
                var center = new int2(2, 2);
                int k = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        sources[k++] = new int2(center.x + dx, center.y + dy);

                FlowFieldBuilder.BuildFromSources(walk, new int2(w, h), sources, flow, dist);

                for (int i = 0; i < n; i++)
                    Assert.AreEqual(int.MaxValue, dist[i],
                        "유효 소스 0 이면 전 셀이 MaxValue 다 — 0 으로 남으면 «이미 도착» 으로 읽힌다");
            }
            finally
            {
                dist.Dispose(); flow.Dispose(); sources.Dispose(); walk.Dispose();
            }
        }
    }
}
