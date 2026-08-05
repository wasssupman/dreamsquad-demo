using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-awakening-hand unit 3 — CR-style cycle queue invariants:
    // seeded-shuffle determinism, front-N hand, use→back recycle (Squad/Active),
    // attach→out-of-pool→recover→back (Unit), empty hand when drained,
    // independent duplicate entries, out-of-hand rejection.
    public class DreamcatcherCycleDeckTests
    {
        private const int HandSize = 5;
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private DreamcatcherCard MakeCard(string id)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id;
            _created.Add(c);
            return c;
        }

        // 12 distinct cards (10 deck + 2 active stand-ins) unless stated otherwise.
        private List<DreamcatcherCard> MakeCards(int n)
        {
            var list = new List<DreamcatcherCard>(n);
            for (int i = 0; i < n; i++) list.Add(MakeCard($"c{i}"));
            return list;
        }

        private static List<string> Ids(DreamcatcherCycleDeck deck, int handSize) =>
            deck.Hand(handSize).Select(e => e.card.id).ToList();

        [Test]
        public void Shuffle_SameSeed_SameOrder_AndPreservesAll()
        {
            var cards = MakeCards(12);
            var a = new DreamcatcherCycleDeck(cards, seed: 1234);
            var b = new DreamcatcherCycleDeck(cards, seed: 1234);
            Assert.AreEqual(12, a.TotalCount);
            CollectionAssert.AreEqual(Ids(a, 12), Ids(b, 12));

            var c = new DreamcatcherCycleDeck(cards, seed: 9999);
            Assert.AreEqual(12, c.TotalCount);
            CollectionAssert.AreEquivalent(Ids(a, 12), Ids(c, 12)); // same set, any order
        }

        [Test]
        public void Hand_IsFrontN_AndSlidesAfterUse()
        {
            var cards = MakeCards(12);
            var deck = new DreamcatcherCycleDeck(cards, seed: 7);
            var before = deck.Hand(HandSize);
            Assert.AreEqual(HandSize, before.Count);

            var sixth = deck.Hand(6)[5]; // next card outside the hand
            Assert.IsTrue(deck.UseAndRecycle(before[0].entryId, HandSize));

            var after = deck.Hand(HandSize);
            Assert.AreEqual(before[1].entryId, after[0].entryId); // shifted left
            Assert.AreEqual(sixth.entryId, after[4].entryId);     // slid in
        }

        [Test]
        public void UseAndRecycle_MovesToBack()
        {
            var cards = MakeCards(12);
            var deck = new DreamcatcherCycleDeck(cards, seed: 7);
            var used = deck.Hand(HandSize)[0];

            Assert.IsTrue(deck.UseAndRecycle(used.entryId, HandSize));
            Assert.AreEqual(12, deck.QueueCount); // still cycling, nothing attached
            Assert.AreEqual(used.entryId, deck.Hand(12)[11].entryId); // now last
        }

        [Test]
        public void UseUnit_LeavesQueue_RecoverAppendsBack()
        {
            var cards = MakeCards(12);
            var deck = new DreamcatcherCycleDeck(cards, seed: 7);
            var used = deck.Hand(HandSize)[2];

            Assert.IsTrue(deck.UseUnit(used.entryId, HandSize));
            Assert.AreEqual(11, deck.QueueCount);
            Assert.AreEqual(1, deck.AttachedCount);
            Assert.IsFalse(Ids(deck, 12).Contains(used.card.id));

            Assert.IsTrue(deck.Recover(used.entryId));
            Assert.AreEqual(12, deck.QueueCount);
            Assert.AreEqual(0, deck.AttachedCount);
            Assert.AreEqual(used.entryId, deck.Hand(12)[11].entryId); // back of queue
        }

        [Test]
        public void AllAttached_HandIsEmpty()
        {
            var cards = MakeCards(3);
            var deck = new DreamcatcherCycleDeck(cards, seed: 7);
            for (int i = 0; i < 3; i++)
            {
                var front = deck.Hand(HandSize)[0];
                Assert.IsTrue(deck.UseUnit(front.entryId, HandSize));
            }
            Assert.AreEqual(0, deck.QueueCount);
            Assert.AreEqual(3, deck.AttachedCount);
            Assert.AreEqual(0, deck.Hand(HandSize).Count); // empty hand
        }

        [Test]
        public void DuplicateCard_TwoIndependentEntries()
        {
            var dup = MakeCard("dup");
            var deck = new DreamcatcherCycleDeck(new List<DreamcatcherCard> { dup, dup }, seed: 7);
            var hand = deck.Hand(HandSize);
            Assert.AreEqual(2, hand.Count);
            Assert.AreNotEqual(hand[0].entryId, hand[1].entryId);

            Assert.IsTrue(deck.UseUnit(hand[0].entryId, HandSize));
            // The other copy keeps cycling independently.
            Assert.AreEqual(1, deck.QueueCount);
            Assert.AreEqual(hand[1].entryId, deck.Hand(HandSize)[0].entryId);
        }

        [Test]
        public void OutsideHand_OrUnknown_IsRejected()
        {
            var cards = MakeCards(12);
            var deck = new DreamcatcherCycleDeck(cards, seed: 7);
            var seventh = deck.Hand(7)[6]; // beyond hand region

            Assert.IsFalse(deck.UseAndRecycle(seventh.entryId, HandSize));
            Assert.IsFalse(deck.UseUnit(seventh.entryId, HandSize));
            Assert.IsFalse(deck.Recover(seventh.entryId)); // not attached
            Assert.IsFalse(deck.UseAndRecycle(entryId: 999, HandSize)); // unknown id
            Assert.AreEqual(12, deck.QueueCount); // untouched
        }

        // ── battle-sim-extraction unit 16 — 부분 커밋 구멍의 근원 ────────────────
        //
        // `TryGetCard`(큐 **또는** 부착)와 커밋(`UseUnit`/`UseAndRecycle` → 큐 **앞 N칸**)이
        // 서로 다른 조건을 봤다. 그래서 손패 밖·이미 부착된 entryId 가 컨트롤러의 사용 가능
        // 판정을 통과한 뒤 커밋에서만 실패했고, 그 시점엔 효과 적용과 유출 허용치 **비가역
        // 차감**이 이미 끝나 있었다. `IsInHand` 가 그 비대칭을 닫는 술어다 —
        // 아래 두 테스트가 "커밋 가능성 == IsInHand" 를 고정한다.

        [Test]
        public void IsInHand_MatchesWhatCommitAccepts_NotWhatTryGetCardFinds()
        {
            var cards = MakeCards(12);
            var deck = new DreamcatcherCycleDeck(cards, seed: 7);
            var seventh = deck.Hand(7)[6]; // 손패 밖(큐 7번째)

            // TryGetCard 는 찾아준다 — 이것만 보면 "쓸 수 있다" 로 오판한다.
            Assert.IsTrue(deck.TryGetCard(seventh.entryId, out _));
            // IsInHand 는 커밋과 같은 답을 준다.
            Assert.IsFalse(deck.IsInHand(seventh.entryId, HandSize));
            Assert.IsFalse(deck.UseUnit(seventh.entryId, HandSize), "IsInHand 와 커밋이 일치해야 한다");
        }

        [Test]
        public void AttachedEntry_IsFoundButNotInHand()
        {
            var cards = MakeCards(12);
            var deck = new DreamcatcherCycleDeck(cards, seed: 7);
            var front = deck.Hand(HandSize)[0];
            Assert.IsTrue(deck.UseUnit(front.entryId, HandSize)); // 부착 → 풀 이탈

            // 부착분은 여전히 조회된다(아이콘 스냅샷이 이것을 읽는다 — 이 동작은 유지해야 한다).
            Assert.IsTrue(deck.TryGetCard(front.entryId, out _));
            // 그러나 손패에는 없다 = 다시 커밋할 수 없다.
            Assert.IsFalse(deck.IsInHand(front.entryId, HandSize));
            Assert.IsFalse(deck.UseUnit(front.entryId, HandSize),
                "이미 부착된 카드를 다시 커밋하면 이중 적용 + 비가역 차감이 난다");
        }
    }
}
