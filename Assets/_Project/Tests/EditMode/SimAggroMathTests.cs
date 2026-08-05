// battle-sim-extraction unit 18-F/1 — 어그로 토대의 오라클 복제 + 이식 핀.
// 구 오라클 `AggroChaseMathTests` 는 unit 20 까지 계속 진다.
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Movement;

namespace Wassup.Tests.EditMode
{
    public class SimAggroPolicyTests
    {
        [Test]
        public void CanAcquire_RequiresRoom_AndPreemption()
        {
            Assert.IsTrue(AggroPolicy.CanAcquire(held: 0, capacity: 1, alreadyAggroed: false));
            Assert.IsFalse(AggroPolicy.CanAcquire(held: 1, capacity: 1, alreadyAggroed: false),
                "상한 도달 — 획득 불가.");
            Assert.IsFalse(AggroPolicy.CanAcquire(held: 0, capacity: 1, alreadyAggroed: true),
                "선점 — 이미 걸린 적은 다른 가디언이 뺏지 못한다(first-come, sticky).");
        }

        [Test]
        public void CanAcquire_ZeroCapacity_NeverAcquires()
            => Assert.IsFalse(AggroPolicy.CanAcquire(0, 0, false));

        [Test]
        public void ShouldRelease_IsExactlyGuardianDeath()
        {
            Assert.IsTrue(AggroPolicy.ShouldRelease(guardianAlive: false));
            Assert.IsFalse(AggroPolicy.ShouldRelease(guardianAlive: true));
        }
    }

    public class SimAggroChaseMathTests
    {
        private static readonly SimInt2 Grid = new SimInt2(9, 9);

        private static byte[] OpenMask()
        {
            var m = new byte[Grid.x * Grid.y];
            for (int i = 0; i < m.Length; i++) m[i] = 1;
            return m;
        }

        [Test]
        public void ResolveTileRange_PrefersNativeAttackState_OverTauntProfile()
        {
            Assert.AreEqual(GridMath.RangeToTiles(3f),
                AggroChaseMath.ResolveTileRange(true, 3f, true, 9f), "AttackState 가 우선.");
            Assert.AreEqual(GridMath.RangeToTiles(9f),
                AggroChaseMath.ResolveTileRange(false, 0f, true, 9f), "없으면 도발 프로파일 폴백.");
        }

        [Test]
        public void ResolveTileRange_NoAttackMeans_RefuseAggro()
        {
            // 이 거부가 없으면 때리지도 못하면서 영원히 쫓는 Chasing 고착이 난다.
            Assert.AreEqual(AggroChaseMath.NoAttack,
                AggroChaseMath.ResolveTileRange(false, 0f, false, 0f));
            Assert.AreEqual(-1, AggroChaseMath.NoAttack, "센티넬 값도 계약이다(호출자가 == 로 본다).");
        }

        [Test]
        public void BuildChaseField_SourcesAreTheGuardiansFiringDisc_ExcludingItsOwnCell()
        {
            byte[] mask = OpenMask();
            var flow = new SimVec2[mask.Length];
            var dist = new int[mask.Length];
            var srcBuf = new List<SimInt2>();
            var srcArr = new SimInt2[16];

            int count = AggroChaseMath.BuildChaseField(mask, Grid, new SimInt2(4, 4), 1,
                                                       flow, dist, srcBuf, ref srcArr);

            Assert.AreEqual(8, count, "3×3 디스크에서 가디언 자기 셀 제외 = 8");
            Assert.AreEqual(0, dist[GridMath.CellIndex(new SimInt2(5, 4), Grid)], "소스는 dist 0");
            Assert.AreNotEqual(int.MaxValue, dist[GridMath.CellIndex(new SimInt2(8, 8), Grid)],
                "열린 격자에선 전 셀이 도달 가능.");
        }

        [Test]
        public void BuildChaseField_NoWalkableCandidate_ReturnsZero_AndMarksEverythingUnreachable()
        {
            // 가디언 주변이 전부 벽 = 목적지 후보 0 → 어그로 거부 신호.
            byte[] mask = OpenMask();
            var g = new SimInt2(4, 4);
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                mask[GridMath.CellIndex(new SimInt2(g.x + dx, g.y + dy), Grid)] = 0;

            var flow = new SimVec2[mask.Length];
            var dist = new int[mask.Length];
            for (int i = 0; i < dist.Length; i++) dist[i] = 42;
            var srcBuf = new List<SimInt2>();
            var srcArr = new SimInt2[16];

            int count = AggroChaseMath.BuildChaseField(mask, Grid, g, 1, flow, dist, srcBuf, ref srcArr);

            Assert.AreEqual(0, count);
            Assert.AreEqual(int.MaxValue, dist[0],
                "후보 0 이면 **전 셀을 도달 불가로 명시**한다 — 호출자가 dist 를 본다.");
        }

        [Test]
        public void BuildChaseField_WalledOffEnemy_IsUnreachable_SoAggroIsRefused()
        {
            // 좀비 추격 금지: 벽으로 갈린 적은 dist == MaxValue 라 호출자가 거부한다.
            byte[] mask = OpenMask();
            for (int y = 0; y < Grid.y; y++)                       // x=5 세로 벽
                mask[GridMath.CellIndex(new SimInt2(5, y), Grid)] = 0;

            var flow = new SimVec2[mask.Length];
            var dist = new int[mask.Length];
            var srcBuf = new List<SimInt2>();
            var srcArr = new SimInt2[16];

            int count = AggroChaseMath.BuildChaseField(mask, Grid, new SimInt2(2, 4), 1,
                                                       flow, dist, srcBuf, ref srcArr);

            Assert.Greater(count, 0, "가디언 쪽엔 후보가 있다.");
            Assert.AreEqual(int.MaxValue, dist[GridMath.CellIndex(new SimInt2(8, 4), Grid)],
                "벽 너머 적은 도달 불가 — 어그로를 거부해야 한다.");
        }

        [Test]
        public void BuildChaseField_ReachDistanceMatchesFiringRange_ByConstruction()
        {
            // 소스 도달 ⟺ 발사 가능이 정의상 일치해야 한다 — 메트릭이 갈리면
            // "도착했는데 못 쏘는" 스톨이 생긴다. 사거리 2 면 체비셰프 2 디스크가 전부 dist 0.
            byte[] mask = OpenMask();
            var flow = new SimVec2[mask.Length];
            var dist = new int[mask.Length];
            var srcBuf = new List<SimInt2>();
            var srcArr = new SimInt2[64];

            AggroChaseMath.BuildChaseField(mask, Grid, new SimInt2(4, 4), 2,
                                           flow, dist, srcBuf, ref srcArr);

            Assert.AreEqual(0, dist[GridMath.CellIndex(new SimInt2(6, 6), Grid)],
                "체비셰프 2 = 사거리 안 = 소스(dist 0).");
            Assert.AreNotEqual(0, dist[GridMath.CellIndex(new SimInt2(7, 4), Grid)],
                "체비셰프 3 = 사거리 밖 = 소스 아님.");
        }
    }
}
