using AttackShapeBaked = Wassup.Data.AttackShapeBaked;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // directional-attack-shape unit 0 — 도형 게이트의 **절대값**을 못박는다.
    //
    // `SkillMath` 게이트는 **+X 고정 프레임**이다(along = 주 대상 방향 성분, across = 수직 성분). 회전은
    // `AttackReach.InReachShaped` 가 한다 — 주 대상 방향 벡터를 받아 Δ 를 그 프레임으로 내린다(rev 3).
    //
    // 도형은 반경 판정(`Reach`) **뒤에 AND 로 곱해지는 항**이고 **부가 타격에만** 쓰인다 — 획득은 원이다.
    // 그래서 Omni(360°)에서 `InReachShaped` 는 `InReach` 와 비트 단위로 같다.
    public class AttackShapeGateTests
    {
        private const float Tr = 0.5f;        // 중형 적의 몸
        private const float SelfR = 0.5f;     // 1×1 방어유닛 (가로/2)
        private const float Tile = 1f;

        private static float Sin(float deg) => math.sin(math.radians(deg));
        private static float Cos(float deg) => math.cos(math.radians(deg));

        private static bool Sector(float along, float across, float fullAngleDeg, float tr = Tr)
            => SkillMath.SectorGateX(along, across, Sin(fullAngleDeg * 0.5f), Cos(fullAngleDeg * 0.5f), tr);

        private static bool Band(float along, float across, float halfWidth, float length, float tr = Tr)
            => SkillMath.BandGateX(along, across, halfWidth, length, tr);

        private static AttackShapeBaked SectorShape(float fullAngleDeg) => new AttackShapeBaked
        {
            kind = AttackShapeBaked.SectorKind,
            sinHalf = Sin(fullAngleDeg * 0.5f),
            cosHalf = Cos(fullAngleDeg * 0.5f),
        };

        private static AttackShapeBaked BandShape(float halfWidth) => new AttackShapeBaked
        {
            kind = AttackShapeBaked.BandKind,
            halfWidth = halfWidth,
        };

        private static float3 At(float x, float z) => new float3(x, 0f, z);
        private static readonly float2 Right = new float2(1f, 0f);
        private static readonly float2 Up = new float2(0f, 1f);

        // ── 부채꼴 ─────────────────────────────────────────────────────────────

        [Test]
        public void Sector_OnAxis_IsInside()
        {
            Assert.IsTrue(Sector(2f, 0f, 90f));
            Assert.IsTrue(Sector(0.1f, 0f, 30f), "꼭짓점 바로 앞도 안");
        }

        [Test]
        public void Sector_EdgeAngle_PointOnEdgeIsBoundaryIn_BodyStraddlesIn_BeyondBodyOut()
        {
            // 90° 전체각 → 가장자리 = 45° 선. (1,1) 은 정확히 선 위.
            Assert.IsTrue(Sector(1f, 1f, 90f, tr: 0f), "점이면 경계 포함");
            // (1, 1.3): 가장자리까지 거리 = (1.3−1)·cos45 = 0.212 ≤ 0.5 → 몸이 걸친다
            Assert.IsTrue(Sector(1f, 1.3f, 90f), "몸이 가장자리에 걸치면 안");
            // (1, 1.9): 거리 = 0.636 > 0.5 → 몸도 안 닿는다
            Assert.IsFalse(Sector(1f, 1.9f, 90f), "몸도 안 닿으면 밖");
            Assert.IsTrue(Sector(1f, -1.3f, 90f));
            Assert.IsFalse(Sector(1f, -1.9f, 90f));
        }

        [Test]
        public void Sector_BehindApex_UsesApexDistance_NotEdgeApproximation()
        {
            // ★ 꼭짓점 뒤 영역의 회귀 그물. 가장자리 식 `b·cosθ − along·sinθ` 로 근사하면
            //   (−0.7, 0) 에서 0.495 ≤ 0.5 로 **등 뒤 적이 샌다.** 정확한 SDF 는 꼭짓점까지 거리 0.7 > 0.5.
            Assert.IsFalse(Sector(-0.7f, 0f, 90f), "등 뒤 0.7 — 몸(0.5)이 꼭짓점에 안 닿는다");
            Assert.IsTrue(Sector(-0.3f, 0f, 90f), "등 뒤 0.3 — 몸이 꼭짓점을 덮는다");
            Assert.IsFalse(Sector(-0.7f, 0f, 30f), "좁은 각에서도 같은 답");
        }

        [Test]
        public void Sector_180_DegeneratesToHalfPlane()
        {
            Assert.IsTrue(Sector(0f, 5f, 180f), "옆도 안");
            Assert.IsTrue(Sector(-0.4f, 5f, 180f), "살짝 뒤라도 몸이 걸치면 안");
            Assert.IsFalse(Sector(-0.6f, 5f, 180f), "몸 반경 너머 뒤는 밖");
        }

        // ── 띠 ─────────────────────────────────────────────────────────────────

        [Test]
        public void Band_LateralEdge_IsHalfWidthPlusBody()
        {
            Assert.IsTrue(Band(1f, 0.9f, 0.5f, 2f), "0.9 — 몸이 띠에 걸친다");
            Assert.IsFalse(Band(1f, 1.1f, 0.5f, 2f), "1.1 — 몸도 안 닿는다");
            Assert.IsTrue(Band(1f, -0.9f, 0.5f, 2f), "아래쪽 대칭");
        }

        [Test]
        public void Band_Behind_IsOutBeyondBody()
        {
            Assert.IsTrue(Band(-0.4f, 0f, 0.5f, 2f));
            Assert.IsFalse(Band(-0.6f, 0f, 0.5f, 2f));
        }

        [Test]
        public void Band_FrontEdge_IsLengthPlusBody()
        {
            Assert.IsTrue(Band(2.4f, 0f, 0.5f, 2f));
            Assert.IsFalse(Band(2.6f, 0f, 0.5f, 2f));
        }

        [Test]
        public void Band_ZeroHalfWidth_IsValid_BodyOnAxisHits()
        {
            Assert.IsTrue(Band(1f, 0.4f, 0f, 2f));
            Assert.IsFalse(Band(1f, 0.6f, 0f, 2f));
        }

        // ── AttackReach.InReachShaped (rev 3 — 방향 벡터 회전) ─────────────────

        [Test]
        public void InReachShaped_Omni_IsBitIdenticalToInReach()
        {
            // 360° 항등 — 저작 안 한 유닛의 부가 타격은 오늘과 같다. 격자를 훑어 원 진입점과 전건 대조.
            var omni = AttackShapeBaked.Omni;
            int checkedCount = 0;
            for (float range = 0f; range <= 4f; range += 0.5f)
                for (int x = -6; x <= 6; x++)
                    for (int z = -6; z <= 6; z++)
                    {
                        bool expected = AttackReach.InReach(At(0, 0), At(x, z), range, Tile, SelfR, Tr);
                        bool actual = AttackReach.InReachShaped(At(0, 0), At(x, z), range, Tile, SelfR, Tr, in omni, Right);
                        Assert.AreEqual(expected, actual, $"range={range} Δ=({x},{z})");
                        checkedCount++;
                    }
            Assert.Greater(checkedCount, 1000);
        }

        [Test]
        public void InReachShaped_RotatesWithPrimaryDirection()
        {
            // 주 대상이 **위**(+Z)에 있으면 도형도 위를 향한다 — 회전 불변: (Δ, u) 를 같이 돌려도 답이 같다.
            var s = SectorShape(90f);
            for (int x = -4; x <= 4; x++)
                for (int z = -4; z <= 4; z++)
                    Assert.AreEqual(
                        AttackReach.InReachShaped(At(0, 0), At(x, z), 4f, Tile, SelfR, Tr, in s, Right),
                        AttackReach.InReachShaped(At(0, 0), At(-z, x), 4f, Tile, SelfR, Tr, in s, Up),
                        $"Δ=({x},{z})@Right vs ({-z},{x})@Up 이 달라진다");
        }

        [Test]
        public void InReachShaped_OppositeSide_IsOut_SameSideIn()
        {
            var s = SectorShape(90f);
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(2, 0), 4f, Tile, SelfR, Tr, in s, Right), "주 대상 쪽");
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(-2, 0), 4f, Tile, SelfR, Tr, in s, Right), "반대편은 밖");
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(-2, 0), 4f, Tile, SelfR, Tr, in s, new float2(-1f, 0f)), "주 대상이 왼쪽이면 왼쪽이 안");
        }

        [Test]
        public void InReachShaped_DirectionScaleDoesNotMatter()
        {
            // 정규화 불필요 — 월드든 타일이든 방향만 쓴다.
            var s = SectorShape(60f);
            Assert.AreEqual(
                AttackReach.InReachShaped(At(0, 0), At(2, 1), 4f, Tile, SelfR, Tr, in s, new float2(1f, 0.2f)),
                AttackReach.InReachShaped(At(0, 0), At(2, 1), 4f, Tile, SelfR, Tr, in s, new float2(37f, 7.4f)));
        }

        [Test]
        public void InReachShaped_UndefinedDirection_PassesShapeTerm()
        {
            // 주 대상이 같은 자리 → 방향 없음 → 도형 항 통과(원 항만). 계약 7.
            var s = SectorShape(30f);
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(-2, 0), 4f, Tile, SelfR, Tr, in s, float2.zero));
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(-9, 0), 4f, Tile, SelfR, Tr, in s, float2.zero), "원 항은 여전히 본다");
        }

        [Test]
        public void InReachShaped_Band_OnAxisLength_MatchesReach()
        {
            var b = BandShape(0.5f);
            float edge = 2f + SelfR + Tr;   // Reach 경계 = 3.0
            Assert.IsTrue(AttackReach.InReachShaped(At(0, 0), At(edge, 0), 2f, Tile, SelfR, Tr, in b, Right), "경계 포함");
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(edge + 0.02f, 0), 2f, Tile, SelfR, Tr, in b, Right), "경계 너머");
            Assert.IsFalse(AttackReach.InReachShaped(At(0, 0), At(0, 2), 2f, Tile, SelfR, Tr, in b, Right), "축 밖 세로 2 는 띠 밖");
        }

        [Test]
        public void InReachShaped_TileSizeIsDividedOut_BeforeTheGate()
        {
            var s = SectorShape(90f);
            Assert.AreEqual(
                AttackReach.InReachShaped(At(0, 0), At(1, 1.3f), 4f, 1f, SelfR, Tr, in s, Right),
                AttackReach.InReachShaped(At(0, 0), At(2, 2.6f), 4f, 2f, SelfR, Tr, in s, Right));
        }

        [Test]
        public void Gates_DegenerateInputs_ReturnBoolWithoutThrowing()
        {
            Assert.DoesNotThrow(() => Sector(0f, 0f, 90f, tr: 0f));
            Assert.DoesNotThrow(() => Band(0f, 0f, 0f, 0f, tr: 0f));
            Assert.IsTrue(Sector(0f, 0f, 90f, tr: 0f), "같은 자리 = 안");
            Assert.IsTrue(Band(0f, 0f, 0f, 0f, tr: 0f), "같은 자리 = 안");
        }
    }
}
