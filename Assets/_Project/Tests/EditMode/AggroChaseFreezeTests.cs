using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    // distance-based-range unit 4c — 「어그로된 적이 가디언 옆에 멈춰 서서 아무것도 안 한다」의 회귀 고정.
    //
    // 추격 레인은 자가 셋이다: 획득 = 셀 · 정지 = 셀(추격 필드 소스 디스크) · 발사 = 월드 원
    // (unit 4a 이후). 원이 정사각형 모서리를 잘라내므로 「필드는 도착이라 하고 사거리는 밖이라
    // 하는」 칸이 생기고, 그 칸은 dist 0 이라 기울기가 없어 **영구 동결**이었다.
    //
    // ⚠ **정지 조건의 소유자는 FSM 이다.** 하네스가 `AttackReach.InReach` 를 직접 불러
    // 「쏠 수 있으면 정지」를 판정하면 **프로덕션에 없는 게이트**를 굽는 것이고, 그러면
    // `EnemyAiStateSystem` 쪽 동치가 깨져도 초록으로 통과한다. 그래서 여기도
    // `EnemyAiStateSystem.Evaluate` 를 지난다 — 프로덕션의 `ai == Chasing` 과 같은 경로다.
    public class AggroChaseFreezeTests
    {
        const int   N        = 9;
        const float TileSize = 1f;
        const float Step     = 0.05f;
        static readonly int2 Grid = new int2(N, N);
        static readonly int2 GuardianCell = new int2(4, 4);

        static float3 GuardianPos(float3 origin) =>
            origin + new float3(GuardianCell.x, 0f, GuardianCell.y);

        static NativeArray<byte> OpenMask(int2? wallCol = null)
        {
            var m = new NativeArray<byte>(N * N, Allocator.Temp);
            for (int i = 0; i < m.Length; i++) m[i] = 1;
            if (wallCol.HasValue)                      // 세로 벽 한 줄 — 폴백축을 강제한다
                for (int y = 0; y < N; y++) m[y * N + wallCol.Value.x] = 0;
            return m;
        }

        static NativeArray<int> BuildField(int tileRange, out int sourceCount, int2? wallCol = null)
        {
            var mask = OpenMask(wallCol);
            var flow = new NativeArray<float2>(N * N, Allocator.Temp);
            var dist = new NativeArray<int>(N * N, Allocator.Temp);
            sourceCount = AggroChaseMath.BuildChaseField(mask, Grid, GuardianCell, tileRange, flow, dist);
            mask.Dispose(); flow.Dispose();
            return dist;
        }

        // 프로덕션의 정지 조건. **여기가 유일한 사거리 질의다** — MovementSystem 은 이걸 안 하고
        // `ai` 를 읽기만 한다(`EnemyAiStateSystem` 이 [UpdateBefore] 로 굽는다).
        // ⚠ 대상 몸 반경을 **명시 인자로 뚫어 둔다.** 프로덕션은 `BodyRadiusOf(g, _bodyRadiusLookup)`
        // 를 넘기고(`EnemyAiStateSystem:109`), 오늘 방어유닛 `HitRadius` 저작이 0 이라 답이 같을 뿐이다
        // — unit 6 이 몸을 저작하는 순간 하네스가 조용히 갈린다.
        static bool StillChasing(float3 p, float3 origin, int tileRange, float guardianBody = 0f)
            => EnemyAiStateSystem.Evaluate(
                   aggroed: true,
                   guardianInRange: AttackReach.InReach(
                       p, GuardianPos(origin), tileRange, TileSize, guardianBody),
                   hasFireTarget: false) == AiState.Chasing;

        // 보정 **없이** 필드만 = unit 4a~4c 사이의 동작.
        static float3 WalkFieldOnly(in NativeArray<int> dist, float3 start, float3 origin, int maxSteps)
        {
            float3 p = start;
            for (int i = 0; i < maxSteps; i++)
            {
                float2 d = FlowRecovery.RecoveryDir(
                    GridMath.WorldToCell(p, TileSize, Grid, origin), dist, Grid);
                if (math.lengthsq(d) < 1e-6f) return p;
                p += new float3(d.x, 0f, d.y) * Step;
            }
            return p;
        }

        // MovementSystem 추격 분기 전체. 보정 게이트·폴백축·소스 이탈 금지까지 같은 구조.
        // ⚠ **벽 판정은 근사다** — 프로덕션은 `ComposeMove` → `AgentCollision.Resolve`(에이전트 반경
        // standoff · 교차축 스윕 · 프레임 변위 클램프)인데 여기는 도착 **셀** 질의 하나다.
        // 그래서 이 하네스는 사거리 기하를 증언하지, 충돌 해소를 증언하지 않는다.
        static float3 WalkWithCorrection(in NativeArray<int> dist, in NativeArray<byte> mask,
                                         float3 start, float3 origin, int tileRange,
                                         int maxSteps, out int corrections)
        {
            float3 p = start; corrections = 0;
            for (int i = 0; i < maxSteps; i++)
            {
                int2 cell = GridMath.WorldToCell(p, TileSize, Grid, origin);
                float2 dir = FlowRecovery.RecoveryDir(cell, dist, Grid);
                if (math.lengthsq(dir) > 1e-6f) { p += new float3(dir.x, 0f, dir.y) * Step; continue; }

                if (dist[GridMath.CellIndex(cell, Grid)] != 0) return p;   // 고립 — 종전대로 정지
                if (!StillChasing(p, origin, tileRange)) return p;         // FSM 이 정지를 소유한다

                float3 g = GuardianPos(origin);
                AggroChaseMath.CloseInCardinals(g.x - p.x, g.z - p.z, out var primary, out var secondary);
                float2 taken = float2.zero;
                for (int a = 0; a < 2 && math.lengthsq(taken) < 1e-6f; a++)
                {
                    float2 axis = a == 0 ? primary : secondary;
                    if (math.lengthsq(axis) < 1e-6f) continue;
                    float3 cand = p + new float3(axis.x, 0f, axis.y) * Step;
                    int2 cCell = GridMath.WorldToCell(cand, TileSize, Grid, origin);
                    if (mask[GridMath.CellIndex(cCell, Grid)] == 0) continue;            // 막혔다
                    if (dist[GridMath.CellIndex(cCell, Grid)] != 0) continue;            // 소스 이탈
                    taken = axis;
                }
                if (math.lengthsq(taken) < 1e-6f) return p;                // 양축 다 불가 — 정지
                p += new float3(taken.x, 0f, taken.y) * Step;
                corrections++;
            }
            return p;
        }

        static float Gap(float3 p, float3 origin) =>
            math.distance(new float2(p.x, p.z),
                          new float2(GuardianPos(origin).x, GuardianPos(origin).z));

        // ── 증상: 멈춘 자리에서 때릴 수 있어야 한다 ──────────────────────────
        [TestCase(6f, 6f, 0f, TestName = "대각 접근")]
        [TestCase(7f, 4f, 0f, TestName = "축 접근")]
        [TestCase(2f, 7f, 0f, TestName = "반대 대각")]
        [TestCase(6f, 6f, 17.5f, TestName = "대각 접근 · 보드 원점 비영")]   // L2 — origin 회귀
        public void AggroedEnemy_StopsWhereItCanFire(float sx, float sz, float ox)
        {
            const int tileRange = 1;
            var origin = new float3(ox, 0f, ox);
            var dist = BuildField(tileRange, out int sources);
            var mask = OpenMask();
            try
            {
                Assert.Greater(sources, 0, "소스 0 이면 어그로 자체가 거부된다 — 다른 증상이다");
                var stopped = WalkWithCorrection(dist, mask, origin + new float3(sx, 0f, sz),
                                                 origin, tileRange, 600, out _);
                Assert.IsFalse(StillChasing(stopped, origin, tileRange),
                    $"멈췄는데 아직 Chasing 이다: 실거리 {Gap(stopped, origin):F3}칸 > " +
                    $"도달 {tileRange + 0.5f:F2}칸. 자기 이동 0 + 발사 0 = 영구 동결.");
            }
            finally { dist.Dispose(); mask.Dispose(); }   // L1 — 단언 실패 시 Temp 누수 방지
        }

        // ── 보정이 왜 필요한가 ──────────────────────────────────────────────
        [Test]
        public void FieldOnly_ArrivesOutOfReach_HenceCorrectionRequired()
        {
            const int tileRange = 1;
            var o = float3.zero;
            var dist = BuildField(tileRange, out _);
            try
            {
                var stopped = WalkFieldOnly(dist, new float3(6f, 0f, 6f), o, 600);
                int2 cell = GridMath.WorldToCell(stopped, TileSize, Grid, o);
                Assert.AreEqual(0, dist[GridMath.CellIndex(cell, Grid)], "정지 지점이 사격 칸이 아니다");
                Assert.Greater(Gap(stopped, o), tileRange + 0.5f,
                    "필드만 따라가도 사거리 안이면 보정은 불필요하다 — 이 테스트를 지워라");
                Assert.LessOrEqual(
                    math.max(math.abs(stopped.x - 4f), math.abs(stopped.z - 4f)), tileRange + 0.5f,
                    "구 체비셰프로도 밖이면 unit 4a 회귀가 아니다 — 원인 재조사");
            }
            finally { dist.Dispose(); }
        }

        // ── 폴백축·소스 이탈 분기는 **테스트가 없다.** 거짓 초록을 두지 않는다 ──
        //
        // 처음에 `BlockedDominantAxis_FallsBackToSecondary` 를 썼다가 지웠다. 그 테스트는
        // `Assert.GreaterOrEqual(corr, 0)` 처럼 **어떤 구현으로도 실패할 수 없는** 단언을
        // 들고 있었고, 지형(관통 벽 반대편 출발) 때문에 보정 루프에 진입조차 안 했다.
        //
        // 그 뒤 사거리 1 에서 폴백축을 실제로 태우는 조합을 **전수 탐색했고 0 개**였다.
        // 이유는 기하다: 가디언 쪽 cardinal 스텝은 체비셰프 오프셋을 키우지 않으므로 도착 셀이
        // 항상 소스 디스크 안이고, 벽 칸은 `AgentCollision` 이 반경만큼 앞에서 세워 중심이
        // 그 칸에 못 들어간다. **두 분기는 사거리 1 · 정적 가디언에서 도달 불가한 방어 코드다.**
        //
        // 그래서 여기 남길 수 있는 정직한 것은 「테스트 없음」이라는 사실뿐이다.
        // 실제로 발화시키려면 (a) 사거리 ≥ 2 에 소스 링이 벽으로 끊긴 지형이거나
        // (b) 가디언이 움직여 필드가 stale 해진 경우다. 후자는 저작 0종이라 오늘 못 만든다.

        // ── 고립 칸은 보정 대상이 아니다 ────────────────────────────────────
        [Test]
        public void IsolatedCell_IsNotCorrected()
        {
            const int tileRange = 1;
            var o = float3.zero;
            var dist = BuildField(tileRange, out _);
            try
            {
                // 도달 불가 셀을 흉내 — 소스에서 먼 칸의 dist 를 MaxValue 로 덮는다.
                var probe = new int2(0, 8);
                for (int y = 7; y < N; y++) for (int x = 0; x < 2; x++)
                    dist[GridMath.CellIndex(new int2(x, y), Grid)] = int.MaxValue;
                Assert.AreEqual(float2.zero,
                    FlowRecovery.RecoveryDir(probe, dist, Grid),
                    "고립 칸인데 하강 방향이 있다 — 전제가 깨졌다");
                var mask = OpenMask();
                var stopped = WalkWithCorrection(dist, mask, new float3(probe.x, 0f, probe.y), o,
                                                 tileRange, 60, out int corr);
                mask.Dispose();
                Assert.AreEqual(0, corr, "고립 칸에서 보정이 돌았다 — unit 4a 이전에도 멈추던 자리다");
                Assert.AreEqual(new float3(probe.x, 0f, probe.y), stopped);
            }
            finally { dist.Dispose(); }
        }

        // ── 사냥 레인(보스/헌터)도 같은 결함·같은 처방 ──────────────────────
        //
        // 기하는 어그로와 **같은 소스 수집**(`CollectDefenderSources`)이라 위 시나리오가
        // 그대로 증언한다. 다른 것은 게이트뿐이다: 어그로는 `Chasing`, 사냥은 `Marching`.
        // 보스는 어그로 면역이라 추격 보정이 **구조적으로 못 닿는다** — 그래서 분기가 둘이고
        // 그 둘이 `MovementSystem.TryCloseIn` 하나를 공유한다.
        [Test]
        public void HuntLane_UsesMarchingGate_NotChasing()
        {
            // 사냥 적은 어그로되지 않는다 → 「사거리 안 대상 없음」이 Marching 이다.
            Assert.AreEqual(AiState.Marching,
                EnemyAiStateSystem.Evaluate(aggroed: false, guardianInRange: false, hasFireTarget: false),
                "사냥 레인 보정의 게이트가 Marching 이 아니면 보정이 영영 안 돈다");
            Assert.AreEqual(AiState.Engaging,
                EnemyAiStateSystem.Evaluate(aggroed: false, guardianInRange: false, hasFireTarget: true),
                "쏠 수 있으면 Engaging — 보정이 멈추는 근거다");
            // 어그로 게이트(Chasing)는 aggroed 일 때만 나온다 — 보스는 여기 못 온다.
            Assert.AreEqual(AiState.Chasing,
                EnemyAiStateSystem.Evaluate(aggroed: true, guardianInRange: false, hasFireTarget: false));
        }

        // 사냥 레인의 사거리(= 헌터 최소 사거리)로도 같은 동결 기하가 성립한다.
        [Test]
        public void HuntLane_SameSourceDisc_ArrivesOutOfReach()
        {
            const int hunterRange = 1;              // 보스 3종 전부 attackRange 1
            var o = float3.zero;
            var dist = BuildField(hunterRange, out int sources);
            try
            {
                Assert.Greater(sources, 0);
                var stopped = WalkFieldOnly(dist, new float3(6f, 0f, 6f), o, 600);
                Assert.Greater(Gap(stopped, o), hunterRange + 0.5f,
                    "사냥 레인 소스 디스크가 사거리 안에서 끝나면 이 보정은 불필요하다");
            }
            finally { dist.Dispose(); }
        }

        // ── 순수 방향 선택 ──────────────────────────────────────────────────
        [Test]
        public void CloseInCardinals_PicksDominantAxisFirst()
        {
            AggroChaseMath.CloseInCardinals(-1.45f, -0.5f, out var p, out var s);
            Assert.AreEqual(new float2(-1f, 0f), p); Assert.AreEqual(new float2(0f, -1f), s);

            AggroChaseMath.CloseInCardinals(0.2f, 1.7f, out p, out s);
            Assert.AreEqual(new float2(0f, 1f), p); Assert.AreEqual(new float2(1f, 0f), s);
        }

        // 경계: 동률이면 x 가 이기고, 성분 0 이면 + 를 고른다. 후자가 좌우 왕복의 씨앗이라 못박는다.
        [Test]
        public void CloseInCardinals_BoundaryCases()
        {
            AggroChaseMath.CloseInCardinals(1f, 1f, out var p, out _);
            Assert.AreEqual(new float2(1f, 0f), p, "|dx| == |dz| 는 x 승(>=)");

            AggroChaseMath.CloseInCardinals(0f, -2f, out p, out var s);
            Assert.AreEqual(new float2(0f, -1f), p, "dx 0 이면 z 가 지배축");
            Assert.AreEqual(new float2(1f, 0f), s, "dx == 0 의 부호는 + 로 고정");
        }
    }
}
