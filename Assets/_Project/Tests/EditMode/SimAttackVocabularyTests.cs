using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 공격 루프 어휘의 오라클.
    ///
    /// #33 본체(1,729줄)를 옮기기 전에 **그 위에 설 토대**부터 고정한다. 여기서 지키는 것은
    /// 순수 판정 셋이다 — 최근접 랭킹의 동률 규칙 · CC 활성 판정 · 다중 셀 대상까지의 거리.
    /// 셋 다 sim-critical(타겟팅·사거리)이라 arm 이식 중 조용히 갈리면 안 된다.
    /// </summary>
    public class SimAttackVocabularyTests
    {
        private static NearestTargeting.Candidate C(bool eligible, int tileDist, float sqDist, int simId)
            => new NearestTargeting.Candidate { eligible = eligible, tileDist = tileDist, sqDist = sqDist, simId = simId };

        // ═════ NearestTargeting ══════════════════════════════════════════════

        [Test]
        public void SelectNearest_PicksClosest_ByXZSquaredDistance()
        {
            var c = new List<NearestTargeting.Candidate>
            {
                C(true, 3, 9f, 1),
                C(true, 1, 1f, 2),
                C(true, 2, 4f, 3),
            };
            Assert.AreEqual(1, NearestTargeting.SelectNearest(c, 5));
        }

        [Test]
        public void SelectNearest_Ties_GoToLowerSimId_NotArrayOrder()
        {
            // ⚠ 이 축이 없으면 결과가 스냅샷 순서에 걸려 같은 판이 실행마다 갈린다.
            var c = new List<NearestTargeting.Candidate>
            {
                C(true, 1, 4f, 9),
                C(true, 1, 4f, 2),   // 같은 거리, 낮은 simId — 배열에선 뒤
                C(true, 1, 4f, 7),
            };
            Assert.AreEqual(1, NearestTargeting.SelectNearest(c, 5));
        }

        [Test]
        public void SelectNearest_SkipsIneligible_AndOutOfRange()
        {
            var c = new List<NearestTargeting.Candidate>
            {
                C(false, 1, 1f, 1),  // 호출부 필터 탈락 — 가장 가까워도 제외
                C(true, 9, 4f, 2),   // 반경 밖
                C(true, 2, 9f, 3),
            };
            Assert.AreEqual(2, NearestTargeting.SelectNearest(c, 3));
        }

        [Test]
        public void SelectNearest_NonPositiveRange_SelectsNothing()
        {
            // ⚠ 계약이 **이 함수 안에** 있다. 0 을 "자기 셀만 검색" 으로 읽는 구현이 섞이면
            //   조용히 엉뚱한 대상이 뽑힌다 — 그래서 형제 유틸과 달리 반경 필터를 안에 둔다.
            var c = new List<NearestTargeting.Candidate> { C(true, 0, 0f, 1) };
            Assert.AreEqual(-1, NearestTargeting.SelectNearest(c, 0));
            Assert.AreEqual(-1, NearestTargeting.SelectNearest(c, -2));
        }

        [Test]
        public void SelectNearest_EmptyOrAllFiltered_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, NearestTargeting.SelectNearest(new List<NearestTargeting.Candidate>(), 5));
            Assert.AreEqual(-1, NearestTargeting.SelectNearest(
                new List<NearestTargeting.Candidate> { C(false, 1, 1f, 1) }, 5));
        }

        [Test]
        public void RanksBefore_IsAStrictDeterministicOrder()
        {
            Assert.IsTrue(NearestTargeting.RanksBefore(C(true, 1, 1f, 5), C(true, 1, 2f, 1)), "거리가 우선");
            Assert.IsFalse(NearestTargeting.RanksBefore(C(true, 1, 2f, 1), C(true, 1, 1f, 5)));
            Assert.IsTrue(NearestTargeting.RanksBefore(C(true, 1, 1f, 2), C(true, 1, 1f, 3)), "동거리는 simId");
            Assert.IsFalse(NearestTargeting.RanksBefore(C(true, 1, 1f, 3), C(true, 1, 1f, 3)), "자기 자신보다 앞서지 않는다");
        }

        // ═════ AttackMath.AnyActiveCc ════════════════════════════════════════

        [Test]
        public void AnyActiveCc_NeedsRemainingTime_NotMerePresence()
        {
            // 만료된 슬롯이 버퍼에 남아 있는 창이 있다(감쇠는 P11) — 존재만 보면 오판한다.
            Assert.IsFalse(AttackMath.AnyActiveCc(new List<CcEffect>
            {
                new CcEffect { kind = CcKind.Stun, remainingTime = 0f },
            }));
            Assert.IsTrue(AttackMath.AnyActiveCc(new List<CcEffect>
            {
                new CcEffect { kind = CcKind.Stun, remainingTime = 0f },
                new CcEffect { kind = CcKind.Sleep, remainingTime = 0.1f },
            }));
        }

        [Test]
        public void AnyActiveCc_TreatsAbsentBufferAsNone()
        {
            Assert.IsFalse(AttackMath.AnyActiveCc(null), "버퍼 부재 = CC 없음");
            Assert.IsFalse(AttackMath.AnyActiveCc(new List<CcEffect>()));
        }

        // ═════ AttackMath.DistanceSqToTarget ═════════════════════════════════

        private static FlowFieldSingleton Field(float tileSize = 1f) => new FlowFieldSingleton
        {
            flow = new SimVec2[1], dist = new int[1],
            gridSize = new SimInt2(64, 64), tileSize = tileSize, origin = default,
        };

        [Test]
        public void DistanceSq_IsXZOnly_AndIgnoresHeight()
        {
            float d = AttackMath.DistanceSqToTarget(
                new SimVec3(0f, 0f, 0f), SimEntityId.Null, new SimVec3(3f, 99f, 4f),
                null, false, default, out var nearest);

            Assert.AreEqual(25f, d, 1e-4f, "3-4-5 — Y 는 보지 않는다");
            Assert.AreEqual(new SimVec3(3f, 99f, 4f), nearest, "셀 후보가 없으면 대상 위치 그대로");
        }

        [Test]
        public void DistanceSq_UsesNearestOccupiedCell_ForMultiCellTargets()
        {
            // ⚠ 중심만 재면 큰 구조물의 옆면에 붙어도 사거리 밖으로 판정된다.
            var cells = new List<BlockingHazardCellsBuffer>
            {
                new BlockingHazardCellsBuffer { cell = new SimInt2(5, 0) },
                new BlockingHazardCellsBuffer { cell = new SimInt2(2, 0) }, // 더 가까운 점유 셀
                new BlockingHazardCellsBuffer { cell = new SimInt2(7, 0) },
            };

            float d = AttackMath.DistanceSqToTarget(
                new SimVec3(0f, 0f, 0f), SimEntityId.Null, new SimVec3(5f, 0f, 0f),
                cells, true, Field(), out var nearest);

            Assert.AreEqual(4f, d, 1e-4f, "중심(5)이 아니라 최근접 셀(2)");
            Assert.AreEqual(2f, nearest.x, 1e-4f);
        }

        [Test]
        public void DistanceSq_KeepsTargetHeight_OnCellSnap()
        {
            // 셀 중심으로 스냅해도 Y 는 대상의 것을 유지한다 — 발사 원점 조준이 지면으로 꺼지지 않게.
            var cells = new List<BlockingHazardCellsBuffer>
            {
                new BlockingHazardCellsBuffer { cell = new SimInt2(1, 0) },
            };
            AttackMath.DistanceSqToTarget(
                new SimVec3(0f, 0f, 0f), SimEntityId.Null, new SimVec3(9f, 1.5f, 0f),
                cells, true, Field(), out var nearest);

            Assert.AreEqual(1.5f, nearest.y, 1e-4f);
        }

        [Test]
        public void DistanceSq_WithoutFlowField_FallsBackToCenter()
        {
            // 필드가 없는 이른 프레임 — 셀 좌표를 월드로 못 바꾸므로 중심을 쓴다.
            var cells = new List<BlockingHazardCellsBuffer>
            {
                new BlockingHazardCellsBuffer { cell = new SimInt2(1, 0) },
            };
            float d = AttackMath.DistanceSqToTarget(
                new SimVec3(0f, 0f, 0f), SimEntityId.Null, new SimVec3(4f, 0f, 0f),
                cells, hasFlowField: false, default, out var nearest);

            Assert.AreEqual(16f, d, 1e-4f);
            Assert.AreEqual(4f, nearest.x, 1e-4f);
        }

        // ═════ 타입 계약 ══════════════════════════════════════════════════════

        [Test]
        public void BombLauncherState_CarriesItsOwnRngStream()
        {
            // ⚠ rng 가 상태 해시에 실린다 — 캐스터별 독립 스트림이라는 것이 계약이다.
            var a = new BombLauncherState { rng = new SimRandom(7u) };
            var b = new BombLauncherState { rng = new SimRandom(9u) };
            Assert.AreNotEqual(a.rng.state, b.rng.state, "캐스터마다 다른 시드 → 다른 스트림");

            uint before = a.rng.state;
            a.rng.NextUInt();
            Assert.AreNotEqual(before, a.rng.state, "소비하면 상태가 전진한다(write-back 대상)");
        }

        [Test]
        public void AttackModKind_ValuesAreStable()
        {
            // 저작 복제라 값이 갈리면 카드가 조용히 다른 모디파이어가 된다.
            Assert.AreEqual(0, (int)DcAttackModKind.None);
            Assert.AreEqual(1, (int)DcAttackModKind.ProjectileBounce);
            Assert.AreEqual(2, (int)DcAttackModKind.FrontmostTarget);
        }

        [Test]
        public void AttackModKind_MatchesTheAuthoringVocabulary()
        {
            // 어휘 평행성 — DcTriggerKind 계열과 같은 이유(복제본이 갈려도 컴파일러는 침묵한다).
            foreach (Wassup.Data.DcAttackModKind d in
                     System.Enum.GetValues(typeof(Wassup.Data.DcAttackModKind)))
            {
                string name = d.ToString();
                Assert.IsTrue(System.Enum.IsDefined(typeof(DcAttackModKind), name),
                    $"저작에 있는 {name} 이 sim 어휘에 없다");
                Assert.AreEqual((int)d, (int)System.Enum.Parse(typeof(DcAttackModKind), name),
                    $"{name}: 값이 갈렸다");
            }
        }
    }
}
