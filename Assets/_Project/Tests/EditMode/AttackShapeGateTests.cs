using AttackShapeBaked = Wassup.Data.AttackShapeBaked;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // directional-attack-shape unit 0 — 좌/우 도형 게이트의 **절대값**을 못박는다.
    //
    // 도형은 항상 **+X 방향**이다. 호출부가 `side` 로 dx 부호를 접어 넘긴다:
    //   along = side == 0 ? |dx| : side·dx   ·   across = dz
    // 0 = 양쪽 합집합(획득) · ±1 = 한쪽(부가 타격). 유닛이 타겟 쪽으로 좌/우 반전하므로 회전 수학이 없다.
    //
    // 게이트는 반경 판정(`Reach`) **뒤에 AND 로 곱해지는 항**이다 — 길이는 원이 정하고, 도형은
    // 「보는 쪽으로 얼마나 좁게」만 정한다. 그래서 Omni(360°)에서 `InReach` 는 옛 답과 비트 단위로 같다.
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
            // 아래쪽(−dz)도 대칭
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
            // 반각 90° → 「앞쪽 반평면 + 몸」: along ≥ −tr.
            Assert.IsTrue(Sector(0f, 5f, 180f), "옆(정확히 위)도 안");
            Assert.IsTrue(Sector(-0.4f, 5f, 180f), "살짝 뒤라도 몸이 걸치면 안");
            Assert.IsFalse(Sector(-0.6f, 5f, 180f), "몸 반경 너머 뒤는 밖");
        }

        // ── 띠 ─────────────────────────────────────────────────────────────────

        [Test]
        public void Band_LateralEdge_IsHalfWidthPlusBody()
        {
            // 반폭 0.5 + 몸 0.5 = 1.0 까지.
            Assert.IsTrue(Band(1f, 0.9f, 0.5f, 2f), "0.9 — 몸이 띠에 걸친다");
            Assert.IsFalse(Band(1f, 1.1f, 0.5f, 2f), "1.1 — 몸도 안 닿는다");
            Assert.IsTrue(Band(1f, -0.9f, 0.5f, 2f), "아래쪽 대칭");
        }

        [Test]
        public void Band_Behind_IsOutBeyondBody()
        {
            Assert.IsTrue(Band(-0.4f, 0f, 0.5f, 2f), "뒤 0.4 — 몸이 상자 뒷면에 걸친다");
            Assert.IsFalse(Band(-0.6f, 0f, 0.5f, 2f), "뒤 0.6 — 밖");
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
            Assert.IsTrue(Band(1f, 0.4f, 0f, 2f), "폭 0 이어도 몸이 축에 걸치면 안");
            Assert.IsFalse(Band(1f, 0.6f, 0f, 2f));
        }

        // ── AttackReach 래퍼 ───────────────────────────────────────────────────

        [Test]
        public void InReach_Omni_IsBitIdenticalToReachFromUnit()
        {
            // 360° 항등 — 저작 안 한 유닛은 오늘과 같다. 격자를 훑어 옛 본체와 전건 대조.
            var omni = AttackShapeBaked.Omni;
            int checkedCount = 0;
            for (float range = 0f; range <= 4f; range += 0.5f)
                for (int x = -6; x <= 6; x++)
                    for (int z = -6; z <= 6; z++)
                    {
                        bool expected = SkillMath.ReachFromUnit(x, z, range, SelfR, Tr);
                        bool actual = AttackReach.InReach(At(0, 0), At(x, z), range, Tile, SelfR, Tr, in omni, 0);
                        Assert.AreEqual(expected, actual, $"range={range} Δ=({x},{z})");
                        checkedCount++;
                    }
            Assert.Greater(checkedCount, 1000);
        }

        [Test]
        public void InReach_SideZero_IsLeftRightSymmetric()
        {
            var s = SectorShape(90f);
            for (int x = -4; x <= 4; x++)
                for (int z = -4; z <= 4; z++)
                    Assert.AreEqual(
                        AttackReach.InReach(At(0, 0), At(x, z), 4f, Tile, SelfR, Tr, in s, 0),
                        AttackReach.InReach(At(0, 0), At(-x, z), 4f, Tile, SelfR, Tr, in s, 0),
                        $"Δ=({x},{z}) 와 ({-x},{z}) 가 달라진다");
        }

        [Test]
        public void InReach_SidePlus_RejectsTargetOnTheLeft_SideZeroAccepts()
        {
            var s = SectorShape(90f);
            Assert.IsTrue(AttackReach.InReach(At(0, 0), At(-2, 0), 4f, Tile, SelfR, Tr, in s, 0), "합집합은 왼쪽도");
            Assert.IsFalse(AttackReach.InReach(At(0, 0), At(-2, 0), 4f, Tile, SelfR, Tr, in s, +1), "오른쪽만 볼 땐 왼쪽 밖");
            Assert.IsTrue(AttackReach.InReach(At(0, 0), At(-2, 0), 4f, Tile, SelfR, Tr, in s, -1), "왼쪽만 볼 땐 안");
        }

        [Test]
        public void InReach_Sector_DirectlyAbove_IsNotACandidate()
        {
            // 검증 질문의 얼굴 — 머리 위 두 칸은 보는 쪽 부채꼴(90°) 어디에도 없다. 합집합으로 봐도 없다.
            var s = SectorShape(90f);
            Assert.IsFalse(AttackReach.InReach(At(0, 0), At(0, 2), 4f, Tile, SelfR, Tr, in s, 0));
            Assert.IsFalse(AttackReach.InReach(At(0, 0), At(0, -2), 4f, Tile, SelfR, Tr, in s, 0));
            var omni = AttackShapeBaked.Omni;
            Assert.IsTrue(AttackReach.InReach(At(0, 0), At(0, 2), 4f, Tile, SelfR, Tr, in omni, 0), "Omni 는 잡는다");
        }

        [Test]
        public void InReach_Band_OnAxisLength_MatchesReach()
        {
            // 띠의 길이 = 사거리 + 원점 몸. 축 위에서 원(Reach)과 정확히 같은 곳에서 끝난다.
            var b = BandShape(0.5f);
            float edge = 2f + SelfR + Tr;   // Reach 경계 = 3.0
            Assert.IsTrue(AttackReach.InReach(At(0, 0), At(edge, 0), 2f, Tile, SelfR, Tr, in b, +1), "경계 포함");
            Assert.IsFalse(AttackReach.InReach(At(0, 0), At(edge + 0.02f, 0), 2f, Tile, SelfR, Tr, in b, +1), "경계 너머");
            Assert.IsFalse(AttackReach.InReach(At(0, 0), At(0, 2), 2f, Tile, SelfR, Tr, in b, 0), "정확히 위 2칸은 후보 아님");
        }

        [Test]
        public void InReach_TileSizeIsDividedOut_BeforeTheGate()
        {
            // 월드 2배 스케일에서도 같은 답 — 게이트가 타일 단위를 받는지 확인.
            var s = SectorShape(90f);
            Assert.AreEqual(
                AttackReach.InReach(At(0, 0), At(1, 1.3f), 4f, 1f, SelfR, Tr, in s, +1),
                AttackReach.InReach(At(0, 0), At(2, 2.6f), 4f, 2f, SelfR, Tr, in s, +1));
        }

        // 계약 4 — 결정론. `dx == 0`(정확히 위/아래)은 +X. ECS 리뷰 L2: 간접 검증만 있어 직접 못박는다.
        [TestCase(0f, ExpectedResult = 1)]
        [TestCase(-0.01f, ExpectedResult = -1)]
        [TestCase(0.01f, ExpectedResult = 1)]
        [TestCase(float.NegativeInfinity, ExpectedResult = -1)]
        public int SideOf_SignOfDx_TieGoesRight(float dx) => AttackReach.SideOf(dx);

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
