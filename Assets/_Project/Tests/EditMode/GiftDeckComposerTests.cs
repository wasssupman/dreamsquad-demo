using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // gift-phase unit 1 — 선물 조합의 순수 결정론 회귀 고정. 아키텍처 타입 없이
    // plain 입력만으로 이벤트 선택/무의식 추출/폴백을 검증한다.
    public class GiftDeckComposerTests
    {
        private static DreamcatcherCard Card(string id, CardCategory cat = CardCategory.Normal,
                                             CardType type = CardType.Squad)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = id; c.category = cat; c.type = type;
            return c;
        }

        // ── PickKind ────────────────────────────────────────────────────────
        [Test]
        public void PickKind_Deterministic_SameSeedSameKind()
            => Assert.AreEqual(GiftDeckComposer.PickKind(1234, 1f, 1f),
                               GiftDeckComposer.PickKind(1234, 1f, 1f));

        [Test]
        public void PickKind_RimWeightZero_AlwaysLucid()
        {
            for (int seed = 0; seed < 50; seed++)
                Assert.AreEqual(GiftKind.Lucid, GiftDeckComposer.PickKind(seed, 1f, 0f));
        }

        [Test]
        public void PickKind_LucidWeightZero_AlwaysRim()
        {
            for (int seed = 0; seed < 50; seed++)
                Assert.AreEqual(GiftKind.Rim, GiftDeckComposer.PickKind(seed, 0f, 1f));
        }

        [Test]
        public void PickKind_BothZero_FallsBackLucid()
            => Assert.AreEqual(GiftKind.Lucid, GiftDeckComposer.PickKind(7, 0f, 0f));

        // ── PickRim ─────────────────────────────────────────────────────────
        [Test]
        public void PickRim_Deterministic_SameSeedSameCards()
        {
            var pool = new List<DreamcatcherCard>
            {
                Card("s1", CardCategory.Subconscious), Card("s2", CardCategory.Subconscious),
                Card("s3", CardCategory.Subconscious)
            };
            var a = GiftDeckComposer.PickRim(pool, null, 2, 999);
            var b = GiftDeckComposer.PickRim(pool, null, 2, 999);
            CollectionAssert.AreEqual(a, b);
            Assert.AreEqual(2, a.Count);
        }

        [Test]
        public void PickRim_PoolSufficient_DistinctNoFallback()
        {
            var pool = new List<DreamcatcherCard>
            {
                Card("s1", CardCategory.Subconscious), Card("s2", CardCategory.Subconscious),
                Card("s3", CardCategory.Subconscious)
            };
            var fallback = new List<DreamcatcherCard> { Card("f1") };
            var r = GiftDeckComposer.PickRim(pool, fallback, 2, 5);
            Assert.AreEqual(2, r.Count);
            Assert.AreNotSame(r[0], r[1]);
            CollectionAssert.DoesNotContain(r, fallback[0]);
        }

        [Test]
        public void PickRim_PoolShort_FillsFromFallback()
        {
            var pool = new List<DreamcatcherCard> { Card("s1", CardCategory.Subconscious) };
            var fallback = new List<DreamcatcherCard> { Card("f1"), Card("f2") };
            var r = GiftDeckComposer.PickRim(pool, fallback, 2, 5);
            Assert.AreEqual(2, r.Count);
            Assert.AreSame(pool[0], r[0]);
        }

        [Test]
        public void PickRim_EmptyEverything_ReturnsEmptyNoHang()
            => Assert.AreEqual(0, GiftDeckComposer.PickRim(null, null, 2, 5).Count);

        [Test]
        public void PickRim_ZeroCount_ReturnsEmpty()
        {
            var pool = new List<DreamcatcherCard> { Card("s1", CardCategory.Subconscious) };
            Assert.AreEqual(0, GiftDeckComposer.PickRim(pool, null, 0, 5).Count);
        }
    }
}
