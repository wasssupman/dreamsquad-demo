using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Combat;
using Wassup.Skills;

namespace Wassup.Tests.EditMode
{
    // distance-based-range unit 9 — **화면이 판정을 좁게 가르치지 않는다**를 고정한다.
    //
    // 도달 = 사거리 + 내몸 + 상대몸이다. 표기가 상대를 **점**으로 두면 실제보다 0.25칸
    // 좁아지고, 사거리 1 에서는 **대각 4칸이 통째로 빠진다**(1.414 > 1.25).
    // 「대각 인접도 사거리 1」은 이 게임의 오래된 계약이라 화면이 그걸 부정하면
    // 규칙을 **틀리게** 가르치는 것이다 — unit 5 가 존재하는 이유가 그거다.
    //
    // ⚠ 실제로 한 번 그렇게 만들었다(unit 9 중간 상태). 링이 1.25 로 줄어 말파이트의
    // 대각이 안 그려졌다. 사용자가 「말파 1 캐논 2 범위가 맞나」로 짚었다.
    public class RangeDisplayContractTests
    {
        [Test]
        public void StandardBody_MatchesEveryAuthoringDefault()
        {
            // rev 3(2026-09-01) — 표준 상대 = **적 티어 「소」** 하나다. 방어유닛·구조물의 몸은
            // 저작이 아니라 footprint 파생식이 됐으므로 이 드리프트 감시의 대상에서 빠졌다
            // (파생식 자체는 `AttackReachTests.DerivedBody_IsInscribedCircle` 이 고정한다).
            var atk = ScriptableObject.CreateInstance<Wassup.Data.AttackUnitData>();
            try
            {
                Assert.AreEqual(SkillMath.StandardBodyRadiusTiles, atk.BodyRadiusTiles, 1e-6f,
                    "AttackUnitData 기본 몸(티어 소)이 표기 기준과 갈렸다");
                // unit 13 — 티어표 그 자체. 보스만 개별 float 를 읽는다.
                atk.bodySize = Wassup.Data.AttackUnitData.BodySize.Medium;
                Assert.AreEqual(0.5f, atk.BodyRadiusTiles, 1e-6f);
                atk.bodySize = Wassup.Data.AttackUnitData.BodySize.Large;
                Assert.AreEqual(1.0f, atk.BodyRadiusTiles, 1e-6f);
                atk.bodySize = Wassup.Data.AttackUnitData.BodySize.Boss;
                atk.bodyRadius = 0.615f;
                Assert.AreEqual(0.615f, atk.BodyRadiusTiles, 1e-6f);
            }
            finally
            {
                Object.DestroyImmediate(atk);
            }
        }

        // 표기가 쓰는 술어(`InCellReach` + 표준 상대)와 판정이 쓰는 술어(`InReach` + 실제 몸)가
        // **표준 유닛끼리는 같은 답**을 내야 한다. 이게 「화면이 참말을 한다」의 정의다.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Preview_MatchesJudgement_ForStandardUnits(int range)
        {
            const float b = SkillMath.StandardBodyRadiusTiles;
            for (int dx = -6; dx <= 6; dx++)
            for (int dz = -6; dz <= 6; dz++)
            {
                bool painted = AttackReach.InCellReach(int2.zero, new int2(dx, dz), range, b, b);
                bool hits = AttackReach.InReach(
                    float3.zero, new float3(dx, 0f, dz), range, 1f, b, b);
                Assert.AreEqual(hits, painted,
                    $"사거리 {range}, 칸 ({dx},{dz}) — 화면과 판정이 갈렸다");
            }
        }

        // 「대각 인접도 사거리 1」 — 격자 게임의 오래된 계약. 화면에서도 참이어야 한다.
        [Test]
        public void Range1_PaintsAllEightNeighbours()
        {
            const float b = SkillMath.StandardBodyRadiusTiles;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                Assert.IsTrue(AttackReach.InCellReach(int2.zero, new int2(dx, dz), 1, b, b),
                    $"사거리 1 인데 ({dx},{dz}) 가 안 칠해진다 — 대각을 잃으면 십자가 된다");
            }
            Assert.IsFalse(AttackReach.InCellReach(int2.zero, new int2(2, 2), 1, b, b),
                "두 칸 대각까지 칠해지면 반대로 넓게 가르치는 것이다");
        }
    }
}
