using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // instinct-content unit 3 — 거점 목적지의 두 축.
    //   (1) 「어느 거점으로 갈까」 규칙 — 순수 함수
    //   (2) 「그 거점으로 흐르는 필드가 실제로 구워지나」 — 중심이 통행 불가일 때의 함정
    public class StructureDestinationTests
    {
        // ───────────────────── (1) 선택 규칙 ─────────────────────

        private const int All = ~0;

        private static NativeArray<int> Factions(params Faction[] f)
        {
            var a = new NativeArray<int>(f.Length, Allocator.Temp);
            for (int i = 0; i < f.Length; i++) a[i] = (int)f[i];
            return a;
        }

        [Test]
        public void NearestIndex_PicksClosest()
        {
            var cands = new NativeArray<float2>(3, Allocator.Temp);
            cands[0] = new float2(10f, 0f);
            cands[1] = new float2(2f, 0f);
            cands[2] = new float2(5f, 0f);
            var fac = Factions(Faction.DefenderCore, Faction.DefenderInstinct, Faction.DefenderInstinct);
            try
            {
                Assert.AreEqual(1, StructureChoice.NearestIndex(float2.zero, cands, fac, All));
                Assert.AreEqual(0, StructureChoice.NearestIndex(new float2(11f, 0f), cands, fac, All));
            }
            finally { fac.Dispose(); cands.Dispose(); }
        }

        // 규칙은 종류가 아니라 **마스크**다. 마음도 본능도 같은 후보로 경쟁하고, 코앞의 마음이
        // 먼 본능을 이긴다 — 이게 「거점은 거리순」의 실체다(종류 우선순위가 아니다).
        [Test]
        public void NearestIndex_TheHeartCompetesLikeAnyOtherStructure()
        {
            var cands = new NativeArray<float2>(2, Allocator.Temp);
            cands[0] = new float2(1f, 0f);    // 코앞의 마음
            cands[1] = new float2(9f, 0f);    // 멀리 있는 본능
            var fac = Factions(Faction.DefenderCore, Faction.DefenderInstinct);
            try
            {
                Assert.AreEqual(0, StructureChoice.NearestIndex(float2.zero, cands, fac, All),
                    "코앞의 마음을 두고 먼 본능으로 걸어가면 안 된다");
            }
            finally { fac.Dispose(); cands.Dispose(); }
        }

        // heart-stress-axis unit 6 — 방패가 서면 마음이 **후보 수집 단계에서** 빠진다
        // (`StructureDestinationSystem` 의 `.WithNone<CoreShielded>()`). 그러면 위
        // 「코앞의 마음이 먼 본능을 이긴다」 규칙이 그대로 뒤집힌 답을 낸다 —
        // 선택 함수는 한 줄도 안 바뀌고 후보 집합만 바뀐다. 이게 이 설계의 값어치다.
        //
        // ⚠ 이 테스트가 고정하는 것은 **결과**이지 배제 자체가 아니다. 쿼리에서 정말
        // 빠지는지는 월드가 필요해 여기서 못 잰다 — README 후속 후보의 PlayMode 통합 단언 몫.
        [Test]
        public void NearestIndex_WithShieldedHeartRemoved_PicksTheInstinct()
        {
            var cands = new NativeArray<float2>(1, Allocator.Temp);
            cands[0] = new float2(9f, 0f);    // 먼 본능 — 마음은 방패 때문에 후보에 없다
            var fac = Factions(Faction.DefenderInstinct);
            try
            {
                Assert.AreEqual(0, StructureChoice.NearestIndex(float2.zero, cands, fac, All),
                    "마음이 빠지면 아무리 멀어도 본능이 목적지가 된다 — 「부숴야 닿는다」의 실체");
            }
            finally { fac.Dispose(); cands.Dispose(); }
        }

        // 방패가 걷힌 뒤 — 마음이 후보로 돌아오면 다시 거리순이다(위 규칙 복귀).
        [Test]
        public void NearestIndex_AfterShieldDrops_HeartCompetesAgain()
        {
            var cands = new NativeArray<float2>(2, Allocator.Temp);
            cands[0] = new float2(1f, 0f);    // 마음
            cands[1] = new float2(9f, 0f);    // 본능(이미 파괴됐다면 후보에 없겠지만 규칙 확인용)
            var fac = Factions(Faction.DefenderCore, Faction.DefenderInstinct);
            try
            {
                Assert.AreEqual(0, StructureChoice.NearestIndex(float2.zero, cands, fac, All));
            }
            finally { fac.Dispose(); cands.Dispose(); }
        }

        // 못 부수는 거점은 후보에서 빠진다 — 그 앞에서 굳지 않게. 마음사냥꾼(마스크 28)처럼
        // 좁게 저작된 적도 같은 함수 하나로 처리된다.
        [Test]
        public void NearestIndex_SkipsStructuresOutsideTheMask()
        {
            var cands = new NativeArray<float2>(2, Allocator.Temp);
            cands[0] = new float2(1f, 0f);    // 적 본능 — 적 입장에선 자기 편
            cands[1] = new float2(9f, 0f);    // 방어 본능
            var fac = Factions(Faction.EnemyInstinct, Faction.DefenderInstinct);
            try
            {
                Assert.AreEqual(1, StructureChoice.NearestIndex(
                        float2.zero, cands, fac, EnemyTargetDefaults.DefaultEnemyMask),
                    "적은 자기 편 포탑으로 걸어가지 않는다 — 종류 열거 없이 마스크만으로 갈린다");
                Assert.AreEqual(-1, StructureChoice.NearestIndex(float2.zero, cands, fac, 0),
                    "마스크가 비면 갈 곳이 없다");
            }
            finally { fac.Dispose(); cands.Dispose(); }
        }

        // 동률은 **먼저 온 후보**가 이긴다. 「먼저」의 기준은 호출자가 정한다 —
        // `StructureDestinationSystem` 은 후보를 **셀 사전순으로 정렬한 뒤** 넘긴다.
        //
        // 처음엔 여기 주석에 «후보 순서 = 저작 순서» 라고 적었는데 **검증 안 한 주장이었다.**
        // 후보는 ECS 쿼리에서 오고 그 순서는 청크 순서 = 스폰·사망 이력이다. 본능 하나가
        // 죽어 DeadTag 로 청크가 갈리면 살아남은 후보들의 상대 순서가 조용히 뒤바뀐다.
        [Test]
        public void NearestIndex_TieGoesToTheEarlierCandidate_ForDeterminism()
        {
            var cands = new NativeArray<float2>(2, Allocator.Temp);
            cands[0] = new float2(0f, 3f);
            cands[1] = new float2(3f, 0f);   // 원점에서 거리 동일
            var fac = Factions(Faction.DefenderInstinct, Faction.DefenderInstinct);
            try { Assert.AreEqual(0, StructureChoice.NearestIndex(float2.zero, cands, fac, All)); }
            finally { fac.Dispose(); cands.Dispose(); }
        }

        [Test]
        public void NearestIndex_NoCandidates_ReturnsMinusOne()
        {
            var empty = new NativeArray<float2>(0, Allocator.Temp);
            var fac = new NativeArray<int>(0, Allocator.Temp);
            try { Assert.AreEqual(-1, StructureChoice.NearestIndex(float2.zero, empty, fac, All)); }
            finally { fac.Dispose(); empty.Dispose(); }
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
