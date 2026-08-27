using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // on-place-shuttle-shotgun unit 1 — 배치 발사 조준 규칙의 순수 계약.
    // (skill-layer-migration unit 1 에서 `Wassup.Skills.SkillAim` 로 이사 — 규칙 무변경.)
    //
    // 고정하는 것 셋:
    //  ① **조준이 최근접보다 세다** — 조준 방향에 아무도 없어도 그쪽으로 쏜다(어디를 쏠지는
    //     플레이어 몫, 사용자 결정 2026-08-15).
    //  ② 조준이 없으면 **가장 가까운 후보**, 동률은 **낮은 index**(결정론).
    //  ③ 조준도 후보도 없으면 **false** — 규칙 경로는 발사를 취소하고, 브리지 레거시는
    //     자기 폴백 `(0,1)` 을 쓴다. 이 갈림이 계약이라 여기서 못박는다.
    public class SkillAimTests
    {
        private static bool Run(float2 host, bool hasAim, float2 aim, float2[] candidates,
                                out float2 dir, out int picked)
        {
            return SkillAim.TryResolve(host, hasAim, aim,
                                       candidates, candidates.Length, out dir, out picked);
        }

        [Test]
        public void Aim_Wins_Over_Nearest()
        {
            // 조준은 +X, 최근접 후보는 반대쪽(-X) 코앞.
            bool ok = Run(float2.zero, hasAim: true, aim: new float2(1f, 0f),
                          new[] { new float2(-1f, 0f) }, out var dir, out int picked);

            Assert.IsTrue(ok);
            Assert.AreEqual(1f, dir.x, 1e-4f, "조준 방향으로 나가야 한다");
            Assert.AreEqual(0f, dir.y, 1e-4f);
            Assert.AreEqual(-1, picked, "조준 경로는 후보를 고르지 않는다");
        }

        [Test]
        public void Aim_Wins_Even_With_No_Candidates()
        {
            // 사건 성립(후보 존재)은 호출처가 이미 판정했다 — 여긴 방향만 정한다.
            bool ok = Run(float2.zero, hasAim: true, aim: new float2(0f, -1f),
                          new float2[0], out var dir, out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(-1f, dir.y, 1e-4f);
        }

        [Test]
        public void Without_Aim_Picks_Nearest()
        {
            bool ok = Run(float2.zero, hasAim: false, aim: float2.zero,
                          new[] { new float2(5f, 0f), new float2(0f, 2f), new float2(-9f, 1f) },
                          out var dir, out int picked);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, picked, "0,2 가 가장 가깝다");
            Assert.AreEqual(0f, dir.x, 1e-4f);
            Assert.AreEqual(1f, dir.y, 1e-4f, "정규화된 방향이어야 한다");
        }

        [Test]
        public void Degenerate_Aim_Falls_Back_To_Nearest()
        {
            // 조준 컴포넌트는 있는데 값이 0 인 경우(미배치·퇴화) — 최근접으로 흐른다.
            bool ok = Run(float2.zero, hasAim: true, aim: float2.zero,
                          new[] { new float2(0f, 3f) }, out var dir, out int picked);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, picked);
            Assert.AreEqual(1f, dir.y, 1e-4f);
        }

        [Test]
        public void Ties_Break_By_Lowest_Index()
        {
            // 정확히 같은 거리의 두 후보 — 순서가 결과를 정하면 안 되므로 낮은 index 고정.
            bool ok = Run(float2.zero, hasAim: false, aim: float2.zero,
                          new[] { new float2(2f, 0f), new float2(0f, 2f) },
                          out _, out int picked);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, picked);
        }

        [Test]
        public void Candidate_On_Host_Is_Skipped()
        {
            // host 와 같은 지점은 방향을 못 준다(정규화 NaN). 배제하고 다음 후보를 본다.
            bool ok = Run(float2.zero, hasAim: false, aim: float2.zero,
                          new[] { float2.zero, new float2(0f, 4f) }, out var dir, out int picked);

            Assert.IsTrue(ok);
            Assert.AreEqual(1, picked);
            Assert.IsFalse(float.IsNaN(dir.x) || float.IsNaN(dir.y));
        }

        // ── `count` 축 (skill-layer-migration unit 1 에서 신설) ──────────────
        // 배열 길이가 아니라 `count` 가 유효 범위다. 호출처가 **재사용 버퍼**를 쓰기
        // 때문에 둘이 다르고, 배열 길이를 믿으면 **지난 발사의 후보가 총구를 가져간다**.
        // 이동은 무회귀였지만 이 파라미터는 새로 생긴 것이라 따로 지킨다(리뷰 M3).

        [Test]
        public void Only_The_First_Count_Entries_Are_Candidates()
        {
            // 버퍼에 옛 후보(코앞 +X)가 남아 있지만 이번 후보는 앞 1칸뿐이다.
            var buf = new[] { new float2(0f, 5f), new float2(1f, 0f), new float2(2f, 0f) };
            bool ok = SkillAim.TryResolve(float2.zero, hasAim: false, aim: float2.zero,
                                          buf, count: 1, out var dir, out int picked);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, picked, "count 밖의 «더 가까운» 잔재를 골랐다 — 배열 길이를 믿었다");
            Assert.AreEqual(1f, dir.y, 1e-4f);
        }

        [Test]
        public void Count_Zero_Behaves_As_No_Candidate()
        {
            var buf = new[] { new float2(1f, 0f) };
            bool ok = SkillAim.TryResolve(float2.zero, hasAim: false, aim: float2.zero,
                                          buf, count: 0, out _, out int picked);

            Assert.IsFalse(ok);
            Assert.AreEqual(-1, picked);
        }

        [Test]
        public void Count_Beyond_Length_Is_Clamped_Not_Crashed()
        {
            // 호출처 실수를 예외로 바꾸지 않는다 — 배열 끝까지만 본다.
            var buf = new[] { new float2(0f, 2f) };
            bool ok = SkillAim.TryResolve(float2.zero, hasAim: false, aim: float2.zero,
                                          buf, count: 99, out _, out int picked);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, picked);
        }

        [Test]
        public void Null_Buffer_Is_No_Candidate()
        {
            bool ok = SkillAim.TryResolve(float2.zero, hasAim: false, aim: float2.zero,
                                          null, count: 3, out _, out int picked);

            Assert.IsFalse(ok);
            Assert.AreEqual(-1, picked);
        }

        [Test]
        public void No_Aim_No_Candidate_Returns_False()
        {
            bool ok = Run(float2.zero, hasAim: false, aim: float2.zero,
                          new float2[0], out _, out int picked);

            Assert.IsFalse(ok, "쏠 방향이 없다 — 호출처가 취소/폴백을 정한다");
            Assert.AreEqual(-1, picked);
        }

        [Test]
        public void All_Candidates_On_Host_Returns_False()
        {
            bool ok = Run(float2.zero, hasAim: false, aim: float2.zero,
                          new[] { float2.zero, float2.zero }, out _, out _);

            Assert.IsFalse(ok);
        }
    }
}
