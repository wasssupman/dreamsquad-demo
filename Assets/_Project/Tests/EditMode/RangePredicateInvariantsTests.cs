using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // distance-based-range unit 0 — 사거리 술어의 **상대 불변식**.
    //
    // ⚠ `AttackReachTests` 와 성격이 다르다. 저쪽은 **절대값**을 못박는다("대각 1칸은 닿는다",
    // "상한은 1.5칸") — 그래서 unit 4a 가 자를 바꾸면 **뒤집히는 것이 정상**이다.
    // 이 파일은 **자가 바뀌어도 참이어야 하는 성질**만 단언한다. 그래서 unit 0(현행)에서 초록이고
    // unit 4a(몸 기준) 이후에도 초록이어야 한다 — 여기가 빨개지면 그건 metric 교체가 아니라
    // **술어가 술어이기를 그만둔 것**이다.
    //
    // 절대값 회귀는 이 파일이 아니라 골든이 진다(spec 계약 13).
    public class RangePredicateInvariantsTests
    {
        private const float Tile = 1f;
        private static float3 At(int2 c) => new float3(c.x, 0f, c.y);

        // ⚠ unit 4a 로 시그니처가 줄었다 — 셀도 `bothContinuous` 도 없다.
        // **그 축소 자체가 unit 4a 다**(누가 묻느냐로 답이 갈리지 않는다). 두 헬퍼는 남기되
        // 둘 다 같은 함수를 부른다 — 「연속/고정이 이제 같은 자를 쓴다」를 눈에 보이게 두려고.
        private static bool Locked(int2 a, int2 b, int range)
            => AttackReach.InReach(At(a), At(b), range, Tile);

        private static bool Continuous(int2 a, int2 b, float3 pa, float3 pb, int range)
            => AttackReach.InReach(pa, pb, range, Tile);

        // ── 1. 사거리 단조성 ────────────────────────────────
        // 사거리를 키우면 대상 집합은 줄어들 수 없다. 어떤 자를 쓰든 참이어야 한다.
        [Test]
        public void Range_IsMonotone_LongerNeverLosesTargets()
        {
            var a = new int2(6, 6);
            for (int r = 0; r <= 5; r++)
            for (int dy = -7; dy <= 7; dy++)
            for (int dx = -7; dx <= 7; dx++)
            {
                var b = new int2(a.x + dx, a.y + dy);
                if (Locked(a, b, r))
                    Assert.IsTrue(Locked(a, b, r + 1),
                        $"사거리 {r} 에서 닿던 {b} 가 {r + 1} 에서 빠졌다 — 단조성 위반");
            }
        }

        // ── 2. 대칭 ────────────────────────────────────────
        // 비대칭이면 «A는 B를 때리는데 B는 A를 못 때리는» 상태가 생긴다.
        // ⚠ 이 불변식은 **양쪽 몸이 같을 때만** 성립한다. unit 4a 가 대상 쪽에만 몸을 주면
        // (보스 0.9 vs 방어유닛 0) 비대칭이 **의도적으로** 생기므로, 그때 이 테스트는
        // 「같은 몸끼리는 대칭」으로 좁혀야 한다. 좁힐 때 그 사실을 spec 계약에 적을 것.
        [Test]
        public void IsSymmetric_AcrossTheGrid()
        {
            for (int r = 0; r <= 4; r++)
            for (int dy = -6; dy <= 6; dy++)
            for (int dx = -6; dx <= 6; dx++)
            {
                var a = new int2(6, 6);
                var b = new int2(a.x + dx, a.y + dy);
                Assert.AreEqual(Locked(a, b, r), Locked(b, a, r),
                    $"비대칭: r={r} a={a} b={b}");
            }
        }

        // ── 3. 격자 계층과 한 칸 이상 어긋나지 않는다 ─────────
        // ⚠ **unit 4a 로 이 불변식의 문장이 바뀌었다.** 옛 문장은 「2차 게이트는 좁히기만
        // 한다(사거리 술어 ⊆ 셀 체비셰프)」였는데, 자를 몸 기준으로 바꾸면서 **거짓이 됐다** —
        // 그리고 그 거짓이 이 spec 이 고치려던 문제다.
        //
        // 반례(테스트가 실제로 잡아낸 것): 사거리 0, 두 유닛이 **0.02칸** 떨어져 있는데
        // 칸 경계를 사이에 두고 선다. 몸 기준으로는 붙어 있으니 닿고(맞다), 셀 체비셰프로는
        // 1칸이라 안 닿는다. 「반 칸 움직였을 뿐인데 판정이 뒤집힌다」가 바로 그 튐이다.
        //
        // 그래서 새 문장은 **어긋남의 상한**이다: 사거리 술어가 통과하면 셀 거리는 최대
        // `range + 1` 이다. 왜 이 상한이 필요한가 — 순찰 이동의 추격 필드가 **셀 디스크**로
        // 사격 칸을 세운다(`FlowFieldBuilder.CollectDefenderSources`, 결정 4). 술어가 그
        // 디스크보다 두 칸 이상 넓어지면 「필드가 세우지 않은 칸에서 쏜다」가 되어
        // 이동과 사격이 다시 갈린다 — 182프레임 교착의 반대 방향이다.
        [Test]
        public void NeverDisagreesWithTheGridLayer_ByMoreThanOneCell()
        {
            for (int r = 0; r <= 4; r++)
            for (int dy = -6; dy <= 6; dy++)
            for (int dx = -6; dx <= 6; dx++)
            {
                var a = new int2(6, 6);
                var b = new int2(a.x + dx, a.y + dy);
                // 각자 상대 쪽으로 반 칸 가까이 — 셀과 몸 거리가 가장 크게 어긋나는 배치.
                var pa = new float3(a.x + 0.49f, 0f, a.y + 0.49f);
                var pb = new float3(b.x - 0.49f, 0f, b.y - 0.49f);
                if (!Continuous(a, b, pa, pb, r)) continue;
                Assert.IsTrue(AttackReach.InCellRange(a, b, r + 1),
                    $"사거리 술어가 격자 자보다 두 칸 이상 넓다: r={r} a={a} b={b} — "
                    + "이동(BFS 소스 = 셀 디스크)이 세우지 않은 칸에서 쏘게 된다");
            }
        }

        // ── 4. 자기 자신 ───────────────────────────────────
        // 사거리 0 이어도 자기 칸은 사거리 안이다. 광역·자가버프가 이 성질에 기댄다.
        [Test]
        public void SelfIsAlwaysInReach()
        {
            var a = new int2(4, 9);
            for (int r = 0; r <= 5; r++)
                Assert.IsTrue(Locked(a, a, r), $"자기 자신이 사거리 {r} 밖으로 나갔다");
        }

        // ── 5. 축 방향 거리 단조성 ──────────────────────────
        // 한 축으로 더 멀어졌는데 다시 사거리 안이 되는 일은 없다.
        // 「구멍 뚫린 사거리」(도넛)를 구조적으로 배제한다.
        [Test]
        public void FartherAlongAnAxis_NeverComesBackIntoReach()
        {
            var a = new int2(6, 6);
            for (int r = 0; r <= 4; r++)
            {
                bool wasOut = false;
                for (int dx = 0; dx <= 8; dx++)
                {
                    bool inReach = Locked(a, new int2(a.x + dx, a.y), r);
                    if (!inReach) wasOut = true;
                    else Assert.IsFalse(wasOut,
                        $"r={r}: dx={dx} 에서 사거리 안으로 되돌아왔다 — 도넛 사거리");
                }
            }
        }

        // ── 6. 상한의 존재 ─────────────────────────────────
        // 충분히 멀면 반드시 밖이다. 상한이 없으면 「사거리」가 아니다.
        [Test]
        public void FarEnough_IsAlwaysOut()
        {
            var a = new int2(0, 0);
            for (int r = 0; r <= 5; r++)
            {
                var far = new int2(r + 40, r + 40);
                Assert.IsFalse(Locked(a, far, r), $"r={r} 에서 {far} 가 사거리 안이다");
                Assert.IsFalse(Continuous(a, far, At(a), At(far), r));
            }
        }
    }
}
