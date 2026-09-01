using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // distance-based-range unit 4c — 「어그로된 적이 가디언 옆에 멈춰 서서 아무것도 안 한다」의 회귀 고정.
    //
    // 추격 레인은 세 판정이 한 루프를 이룬다: 획득 = 셀 · 정지 = 셀(추격 필드 소스 디스크) ·
    // 발사 = 월드 원(unit 4a 이후). 원이 정사각형 모서리를 잘라내므로 「필드는 도착이라 하고
    // 사거리는 밖이라 하는」 칸이 생기고, 그 칸은 dist 0 이라 기울기가 없어 **영구 동결**이었다.
    //
    // 여기서는 MovementSystem 추격 분기와 **같은 순수 조각**(FlowRecovery · AggroChaseMath ·
    // AttackReach)으로 그 프레임 루프를 축약 재생한다.
    public class AggroChaseFreezeTests
    {
        const int   N        = 9;      // 9×9 전면 walkable
        const float TileSize = 1f;
        const float Step     = 0.05f;  // ≒ speed 3 × dt(1/60)
        static readonly int2 Grid = new int2(N, N);
        static readonly int2 GuardianCell = new int2(4, 4);
        static float3 GuardianPos => new float3(GuardianCell.x, 0f, GuardianCell.y);

        static NativeArray<int> BuildField(int tileRange, out int sourceCount)
        {
            var mask = new NativeArray<byte>(N * N, Allocator.Temp);
            for (int i = 0; i < mask.Length; i++) mask[i] = 1;
            var flow = new NativeArray<float2>(N * N, Allocator.Temp);
            var dist = new NativeArray<int>(N * N, Allocator.Temp);
            sourceCount = AggroChaseMath.BuildChaseField(mask, Grid, GuardianCell, tileRange, flow, dist);
            mask.Dispose(); flow.Dispose();
            return dist;
        }

        // 보정 **없이** 필드만 따라간다 = unit 4a~4b 사이의 동작.
        static float3 WalkFieldOnly(in NativeArray<int> dist, float3 start, int maxSteps)
        {
            float3 p = start;
            for (int i = 0; i < maxSteps; i++)
            {
                float2 dir = FlowRecovery.RecoveryDir(
                    GridMath.WorldToCell(p, TileSize, Grid), dist, Grid);
                if (math.lengthsq(dir) < 1e-6f) return p;
                p += new float3(dir.x, 0f, dir.y) * Step;
            }
            return p;
        }

        // MovementSystem 추격 분기 전체(필드 하강 + unit 4c 접근 보정).
        // 보정 게이트는 시스템과 같다: 사격 칸 도착(dist 0) AND FSM 이 아직 Chasing
        // (= AttackReach.InReach 거짓). 여기 grid 는 전면 walkable 이라 폴백축은 안 쓰인다.
        static float3 WalkWithCorrection(in NativeArray<int> dist, float3 start, int tileRange, int maxSteps)
        {
            float3 p = start;
            for (int i = 0; i < maxSteps; i++)
            {
                int2 cell = GridMath.WorldToCell(p, TileSize, Grid);
                float2 dir = FlowRecovery.RecoveryDir(cell, dist, Grid);
                if (math.lengthsq(dir) > 1e-6f) { p += new float3(dir.x, 0f, dir.y) * Step; continue; }

                bool arrived = dist[GridMath.CellIndex(cell, Grid)] == 0;
                if (!arrived) return p;                                             // 고립 — 종전대로 정지
                if (AttackReach.InReach(p, GuardianPos, tileRange, TileSize)) return p;  // 쏠 수 있다 — 정지

                AggroChaseMath.CloseInCardinals(
                    GuardianPos.x - p.x, GuardianPos.z - p.z, out var primary, out _);
                p += new float3(primary.x, 0f, primary.y) * Step;
            }
            return p;
        }

        static float GapTo(float3 p) =>
            math.distance(new float2(p.x, p.z), new float2(GuardianPos.x, GuardianPos.z));

        // ── 증상: 어그로된 적이 멈춘 자리에서 때릴 수 있어야 한다 ────────────────
        [TestCase(6f, 6f, TestName = "대각 접근")]
        [TestCase(7f, 4f, TestName = "축 접근")]
        [TestCase(2f, 7f, TestName = "반대 대각 접근")]
        public void AggroedEnemy_StopsWhereItCanFire(float sx, float sz)
        {
            const int tileRange = 1;
            var dist = BuildField(tileRange, out int sources);
            Assert.Greater(sources, 0, "소스가 0이면 어그로 자체가 거부된다 — 다른 증상이다");

            var stopped = WalkWithCorrection(dist, new float3(sx, 0f, sz), tileRange, 600);
            bool canFire = AttackReach.InReach(stopped, GuardianPos, tileRange, TileSize);
            float gap = GapTo(stopped);
            dist.Dispose();

            Assert.IsTrue(canFire,
                $"멈췄는데 못 때린다: 실거리 {gap:F3}칸 > 도달 {tileRange + 0.5f:F2}칸. " +
                "필드는 도착이라 하고 사거리는 밖이라 한다 — 자기 이동 0 + 발사 0 = 영구 동결.");
        }

        // ── 보정이 왜 필요한가: 필드만 따라가면 사거리 밖에서 멈춘다 ──────────────
        // 이 단언이 초록인 동안 위 보정을 제거하면 안 된다. 우연한 좌표가 아니라
        // 「원이 정사각형 모서리를 잘라낸다」는 기하의 필연이다.
        [Test]
        public void FieldOnly_ArrivesOutOfReach_HenceCorrectionRequired()
        {
            const int tileRange = 1;
            var dist = BuildField(tileRange, out _);
            var stopped = WalkFieldOnly(dist, new float3(6f, 0f, 6f), 600);
            int2 cell = GridMath.WorldToCell(stopped, TileSize, Grid);
            int  d = dist[GridMath.CellIndex(cell, Grid)];
            float gap = GapTo(stopped);
            bool oldChebyshevWouldPass =
                math.max(math.abs(stopped.x - GuardianPos.x),
                         math.abs(stopped.z - GuardianPos.z)) <= tileRange + 0.5f;
            dist.Dispose();

            Assert.AreEqual(0, d, "정지 지점이 사격 칸이 아니다 — 전제가 깨졌다");
            Assert.Greater(gap, tileRange + 0.5f,
                "필드만 따라가도 사거리 안이면 보정은 불필요하다 — 이 테스트를 지워라");
            Assert.IsTrue(oldChebyshevWouldPass,
                "구 체비셰프 판정으로도 사거리 밖이면 unit 4a 회귀가 아니다 — 원인 재조사");
        }

        // ── 순수 방향 선택 ──────────────────────────────────────────────────
        [Test]
        public void CloseInCardinals_PicksDominantAxisFirst()
        {
            AggroChaseMath.CloseInCardinals(-1.45f, -0.5f, out var p, out var s);
            Assert.AreEqual(new float2(-1f, 0f), p, "x 가 지배축");
            Assert.AreEqual(new float2(0f, -1f), s, "폴백은 나머지 축");

            AggroChaseMath.CloseInCardinals(0.2f, 1.7f, out p, out s);
            Assert.AreEqual(new float2(0f, 1f), p, "z 가 지배축");
            Assert.AreEqual(new float2(1f, 0f), s, "폴백은 나머지 축");
        }
    }
}
