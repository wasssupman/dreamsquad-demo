using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // wave-concept-blocks unit 0 — 컨셉 데이터 모델의 기본값과 파생값.
    //
    // 기본값을 테스트하는 이유는 취향이 아니다. `SlotAltitude` 기본이 Ground 가 아니면
    // 「평소」 컨셉이 비행을 뽑아 웨이브 1~3 에 대공 없이 막을 수 없는 적이 나온다(계약 10).
    // `laneGroup` 기본이 -1 이 아니면 저작하지 않은 슬롯이 lane 을 요구해 컨셉이 스폰 2개인
    // 맵에서 조용히 후보에서 빠진다. 기본값이 곧 안전장치라서 고정한다.
    public class WaveConceptDataTests
    {
        private static WaveConceptData NewConcept(params int[] laneGroups)
        {
            var concept = ScriptableObject.CreateInstance<WaveConceptData>();
            var slots = new WaveConceptSlot[laneGroups.Length];
            for (int i = 0; i < laneGroups.Length; i++)
                slots[i] = new WaveConceptSlot { laneGroup = laneGroups[i] };
            concept.slots = slots;
            return concept;
        }

        [Test]
        public void SlotDefaults_AreSafe()
        {
            var slot = new WaveConceptSlot();
            Assert.AreEqual(-1, slot.laneGroup, "laneGroup 기본은 무지정(-1)이어야 한다");
            Assert.AreEqual(EnemyClass.None, slot.classFilter, "classFilter 기본은 무필터");
            Assert.AreEqual(SlotAltitude.Ground, slot.altitude,
                "altitude 기본이 Ground 가 아니면 저작하지 않은 슬롯이 비행을 뽑는다");
        }

        [Test]
        public void ConceptDefaults_MatchSpec()
        {
            var concept = ScriptableObject.CreateInstance<WaveConceptData>();
            Assert.AreEqual(1f, concept.countMul, "countMul 기본 1 = 곡선 총량 그대로");
            Assert.AreEqual(1, concept.minWaveNumber, "minWaveNumber 기본 1 = 게이트 없음");
            Assert.AreEqual(1f, concept.weight, "weight 기본 1");
            Assert.IsNotNull(concept.slots, "slots 는 null 이 아니라 빈 배열이어야 한다");
            Object.DestroyImmediate(concept);
        }

        [Test]
        public void DeckDefaults_HoldThreeWavesAndEmptyPool()
        {
            var deck = ScriptableObject.CreateInstance<AttackDeck>();
            Assert.AreEqual(3, deck.conceptHoldWaves, "블록 길이 기본 3");
            Assert.IsNotNull(deck.waveConceptPool, "풀은 null 이 아니라 빈 배열(폴백 경로)");
            Assert.AreEqual(0, deck.waveConceptPool.Length, "기본은 컨셉 없음 = 현행 동작");
            Object.DestroyImmediate(deck);
        }

        // RequiredLaneCount 는 저작값이 아니라 slots 파생이다 — 두 곳에 두면 갈린다.
        [Test]
        public void RequiredLaneCount_CountsDistinctNonNegativeGroups()
        {
            var spread = NewConcept(-1, -1);
            var single = NewConcept(0);
            var pincer = NewConcept(0, 1);
            var sameLane = NewConcept(0, 0);
            var mixed = NewConcept(-1, 0, 0, 2);

            Assert.AreEqual(0, spread.RequiredLaneCount, "전부 무지정이면 lane 요구 없음");
            Assert.AreEqual(1, single.RequiredLaneCount);
            Assert.AreEqual(2, pincer.RequiredLaneCount, "협공은 서로 다른 두 lane");
            Assert.AreEqual(1, sameLane.RequiredLaneCount, "같은 laneGroup 은 한 lane");
            Assert.AreEqual(2, mixed.RequiredLaneCount, "무지정은 세지 않고 중복은 접는다");

            foreach (var c in new[] { spread, single, pincer, sameLane, mixed })
                Object.DestroyImmediate(c);
        }

        [Test]
        public void EffectiveSlotCount_IgnoresNullSlots()
        {
            var concept = ScriptableObject.CreateInstance<WaveConceptData>();
            concept.slots = new[] { new WaveConceptSlot(), null, new WaveConceptSlot() };
            Assert.AreEqual(2, concept.EffectiveSlotCount,
                "null 슬롯이 수량 분배의 하한을 부풀리면 저작 실수가 난이도로 새어나간다");
            Object.DestroyImmediate(concept);
        }

        [Test]
        public void NullSlotsArray_IsHandled()
        {
            var concept = ScriptableObject.CreateInstance<WaveConceptData>();
            concept.slots = null;
            Assert.AreEqual(0, concept.RequiredLaneCount);
            Assert.AreEqual(0, concept.EffectiveSlotCount);
            Object.DestroyImmediate(concept);
        }
    }
}
