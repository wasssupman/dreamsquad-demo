using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-concept-blocks unit 1 — 컨셉 해석 순수 함수 3개.
    //
    // 셋 다 rng 를 받지 않으므로 «같은 입력 → 같은 출력»을 여기서 못박는다. 생성기 통합
    // (unit 2)이 이 함수들을 호출하므로 여기가 깨지면 6맵의 난이도 곡선이 조용히 바뀐다.
    public class WaveConceptMathTests
    {
        private static WaveConceptData Concept(
            string id, float weight, int minWave, float countMul, params int[] laneGroups)
        {
            var concept = ScriptableObject.CreateInstance<WaveConceptData>();
            concept.id = id;
            concept.displayName = id;
            concept.weight = weight;
            concept.minWaveNumber = minWave;
            concept.countMul = countMul;
            var slots = new WaveConceptSlot[laneGroups.Length];
            for (int i = 0; i < laneGroups.Length; i++)
                slots[i] = new WaveConceptSlot { laneGroup = laneGroups[i] };
            concept.slots = slots;
            return concept;
        }

        private static void Destroy(params WaveConceptData[] concepts)
        {
            foreach (var c in concepts)
                if (c != null) Object.DestroyImmediate(c);
        }

        // ---------------- DistributeSlotCounts ----------------

        [Test]
        public void Distribute_SumsToScaledTotal_AndEverySlotGetsAtLeastOne()
        {
            var counts = new int[3];
            int scaled = WavePatternGenerator.DistributeSlotCounts(19, 1f, 3, 24, counts);

            Assert.AreEqual(19, scaled);
            Assert.AreEqual(19, counts[0] + counts[1] + counts[2], "합이 scaled 와 같아야 한다");
            foreach (var c in counts) Assert.GreaterOrEqual(c, 1, "빈 슬롯을 만들지 않는다");
        }

        [Test]
        public void Distribute_RemainderGoesToLeadingSlots()
        {
            var counts = new int[3];
            WavePatternGenerator.DistributeSlotCounts(20, 1f, 3, 24, counts);

            // 20 / 3 = 6 나머지 2 → 7, 7, 6
            Assert.AreEqual(new[] { 7, 7, 6 }, counts);
        }

        // countMul 이 없으면 성질을 통일한 순간 난이도가 성질에 끌려간다
        // (Runner 20hp × 19 = 380 vs Tanker 100hp × 19 = 1,900).
        [Test]
        public void Distribute_CountMulScalesDown()
        {
            var heavy = new int[1];
            var swarm = new int[1];
            WavePatternGenerator.DistributeSlotCounts(19, 0.4f, 1, 24, heavy);
            WavePatternGenerator.DistributeSlotCounts(19, 1.3f, 1, 24, swarm);

            Assert.AreEqual(8, heavy[0], "19 × 0.4 = 7.6 → 8");
            Assert.AreEqual(24, swarm[0], "19 × 1.3 = 24.7 → 25 지만 상한 24 로 잘린다");
        }

        // 하한이 minUnitsPerWave 가 아니라 slotCount 인 것이 배율을 살린다.
        [Test]
        public void Distribute_LowerBoundIsSlotCount_NotMinUnits()
        {
            var counts = new int[2];
            int scaled = WavePatternGenerator.DistributeSlotCounts(5, 0.3f, 2, 24, counts);

            Assert.AreEqual(2, scaled, "5 × 0.3 = 1.5 → 2(슬롯 수)로만 올라간다");
            Assert.AreEqual(new[] { 1, 1 }, counts);
        }

        [Test]
        public void Distribute_RespectsUpperBound()
        {
            var counts = new int[2];
            int scaled = WavePatternGenerator.DistributeSlotCounts(100, 1f, 2, 24, counts);

            Assert.AreEqual(24, scaled);
            Assert.AreEqual(24, counts[0] + counts[1]);
        }

        [Test]
        public void Distribute_IsDeterministic()
        {
            var a = new int[3];
            var b = new int[3];
            WavePatternGenerator.DistributeSlotCounts(17, 0.7f, 3, 24, a);
            WavePatternGenerator.DistributeSlotCounts(17, 0.7f, 3, 24, b);
            Assert.AreEqual(a, b);
        }

        // ---------------- AssignLanes ----------------

        [Test]
        public void AssignLanes_SameGroupSameLane_DifferentGroupDifferentLane()
        {
            var lanes = new int[4];
            bool ok = WavePatternGenerator.AssignLanes(new[] { 0, 1, 0, 1 }, 3, 0, lanes);

            Assert.IsTrue(ok);
            Assert.AreEqual(lanes[0], lanes[2], "같은 laneGroup 은 같은 lane");
            Assert.AreEqual(lanes[1], lanes[3], "같은 laneGroup 은 같은 lane");
            Assert.AreNotEqual(lanes[0], lanes[1], "다른 laneGroup 은 다른 lane");
        }

        [Test]
        public void AssignLanes_UnassignedStaysMinusOne()
        {
            var lanes = new int[3];
            bool ok = WavePatternGenerator.AssignLanes(new[] { -1, -1, -1 }, 2, 1, lanes);

            Assert.IsTrue(ok, "무지정만 있으면 lane 요구가 없다");
            Assert.AreEqual(new[] { -1, -1, -1 }, lanes,
                "무지정은 -1 로 통과해 기존 EffectiveSpawnIndex 경로를 탄다");
        }

        [Test]
        public void AssignLanes_FailsWhenMapHasTooFewSpawns()
        {
            var lanes = new int[2];
            Assert.IsFalse(WavePatternGenerator.AssignLanes(new[] { 0, 1 }, 1, 0, lanes),
                "협공(2 lane)은 스폰 1개 맵에서 성립하지 않는다");
        }

        [Test]
        public void AssignLanes_IsDeterministicForSameRoll()
        {
            var a = new int[2];
            var b = new int[2];
            WavePatternGenerator.AssignLanes(new[] { 0, 1 }, 4, 7, a);
            WavePatternGenerator.AssignLanes(new[] { 0, 1 }, 4, 7, b);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void AssignLanes_RollShiftsTheLanePair()
        {
            var a = new int[2];
            var b = new int[2];
            WavePatternGenerator.AssignLanes(new[] { 0, 1 }, 4, 0, a);
            WavePatternGenerator.AssignLanes(new[] { 0, 1 }, 4, 1, b);
            Assert.AreNotEqual(a, b, "roll 이 다르면 같은 컨셉이 다른 복도 쌍을 받는다");
        }

        [Test]
        public void AssignLanes_NegativeRollIsHandled()
        {
            var lanes = new int[1];
            Assert.IsTrue(WavePatternGenerator.AssignLanes(new[] { 0 }, 3, -5, lanes));
            Assert.GreaterOrEqual(lanes[0], 0);
            Assert.Less(lanes[0], 3);
        }

        // ---------------- PickConcept ----------------

        [Test]
        public void Pick_MinWaveNumberGate_KeepsLateConceptsOut()
        {
            var early = Concept("early", 1f, 1, 1f, -1);
            var late = Concept("late", 1f, 7, 1f, -1);
            var pool = new[] { early, late };

            // 블록 0 의 첫 웨이브 = 1 → late 는 후보 밖
            Assert.AreSame(early, WavePatternGenerator.PickConcept(pool, 1, 2, null, 0.99f));
            Destroy(early, late);
        }

        [Test]
        public void Pick_LaneRequirementGate_UsesDerivedCount()
        {
            var pincer = Concept("pincer", 1f, 1, 1f, 0, 1);
            var single = Concept("single", 1f, 1, 1f, 0);
            var pool = new[] { pincer, single };

            Assert.AreSame(single, WavePatternGenerator.PickConcept(pool, 1, 1, null, 0.99f),
                "스폰 1개 맵에서는 협공이 빠진다");
            Destroy(pincer, single);
        }

        [Test]
        public void Pick_ZeroWeight_IsNeverChosen()
        {
            var off = Concept("off", 0f, 1, 1f, -1);
            var on = Concept("on", 1f, 1, 1f, -1);
            var pool = new[] { off, on };

            for (float roll = 0f; roll < 1f; roll += 0.1f)
                Assert.AreSame(on, WavePatternGenerator.PickConcept(pool, 1, 2, null, roll));
            Destroy(off, on);
        }

        // 같은 컨셉이 두 블록 연속이면 그것이 기본값이 되어 인상이 죽는다.
        [Test]
        public void Pick_ExcludesPreviousConcept()
        {
            var a = Concept("a", 1f, 1, 1f, -1);
            var b = Concept("b", 1f, 1, 1f, -1);
            var pool = new[] { a, b };

            for (float roll = 0f; roll < 1f; roll += 0.1f)
                Assert.AreSame(b, WavePatternGenerator.PickConcept(pool, 1, 2, a, roll),
                    "직전 컨셉은 후보에서 빠진다");
            Destroy(a, b);
        }

        [Test]
        public void Pick_ExclusionFailsOpenWhenPoolHasOnlyOne()
        {
            var only = Concept("only", 1f, 1, 1f, -1);
            var pool = new[] { only };

            Assert.AreSame(only, WavePatternGenerator.PickConcept(pool, 1, 2, only, 0.5f),
                "후보가 0 이 되면 배제를 풀어야 웨이브가 비지 않는다");
            Destroy(only);
        }

        [Test]
        public void Pick_ReturnsNullWhenNothingIsEligible()
        {
            var late = Concept("late", 1f, 99, 1f, -1);
            Assert.IsNull(WavePatternGenerator.PickConcept(new[] { late }, 1, 2, null, 0.5f),
                "후보 없음은 null — 호출측이 구조적 폴백으로 떨어진다");
            Destroy(late);
        }

        [Test]
        public void Pick_EmptySlots_IsNotEligible()
        {
            var hollow = Concept("hollow", 1f, 1, 1f);   // 슬롯 0개
            Assert.IsNull(WavePatternGenerator.PickConcept(new[] { hollow }, 1, 2, null, 0.5f),
                "슬롯 없는 컨셉은 편성을 만들 수 없다");
            Destroy(hollow);
        }

        [Test]
        public void Pick_WeightsSkewTheDistribution()
        {
            var rare = Concept("rare", 1f, 1, 1f, -1);
            var common = Concept("common", 9f, 1, 1f, -1);
            var pool = new[] { rare, common };

            int rareHits = 0;
            for (int i = 0; i < 100; i++)
                if (ReferenceEquals(WavePatternGenerator.PickConcept(pool, 1, 2, null, i / 100f), rare))
                    rareHits++;

            Assert.AreEqual(10, rareHits, 1, "가중치 1:9 면 대략 10% 만 rare");
            Destroy(rare, common);
        }

        [Test]
        public void Pick_IsDeterministicForSameRoll()
        {
            var a = Concept("a", 2f, 1, 1f, -1);
            var b = Concept("b", 3f, 1, 1f, -1);
            var pool = new[] { a, b };

            var first = WavePatternGenerator.PickConcept(pool, 1, 2, null, 0.42f);
            var second = WavePatternGenerator.PickConcept(pool, 1, 2, null, 0.42f);
            Assert.AreSame(first, second);
            Destroy(a, b);
        }

        [Test]
        public void Pick_NullPool_ReturnsNull()
        {
            Assert.IsNull(WavePatternGenerator.PickConcept(null, 1, 2, null, 0.5f));
        }
    }
}
