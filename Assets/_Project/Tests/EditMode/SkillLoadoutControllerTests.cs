using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class SkillLoadoutControllerTests
    {
        private GameObject _host;
        private SkillLoadoutController _ctl;
        private List<SkillData> _pool;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("SkillLoadoutHost");
            _ctl = _host.AddComponent<SkillLoadoutController>();
            _pool = new List<SkillData>();
            for (int i = 0; i < 6; i++)
            {
                var s = ScriptableObject.CreateInstance<SkillData>();
                s.id = $"skill_{i}";
                s.displayName = $"Skill {i}";
                _pool.Add(s);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _pool) Object.DestroyImmediate(s);
            _pool = null;
            foreach (var so in _extraSos) Object.DestroyImmediate(so);
            _extraSos.Clear();
            Object.DestroyImmediate(_host);
        }

        // dreamcatcher-card-visibility unit 4 — FilterHiddenSkills 픽스처.
        private readonly List<Object> _extraSos = new List<Object>();

        private DreamcatcherCard MakeActiveCard(SkillData skill, int visible)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.type = CardType.Active;
            card.skill = skill;
            card.visible = visible;
            _extraSos.Add(card);
            return card;
        }

        private DreamcatcherCard MakeCard(CardCategory category, CardType type, int visible)
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.category = category;
            card.type = type;
            card.visible = visible;
            _extraSos.Add(card);
            return card;
        }

        private DreamcatcherCardCatalog MakeCatalog(params DreamcatcherCard[] cards)
        {
            var catalog = ScriptableObject.CreateInstance<DreamcatcherCardCatalog>();
            catalog.cards = cards;
            _extraSos.Add(catalog);
            return catalog;
        }

        [Test]
        public void Roll_Same_Seed_Produces_Same_Picks()
        {
            _ctl.Configure(_pool, 2, seed: 12345);
            var first = new List<SkillData>(_ctl.Roll());

            _ctl.Configure(_pool, 2, seed: 12345);
            var second = new List<SkillData>(_ctl.Roll());

            CollectionAssert.AreEqual(first, second);
            Assert.AreEqual(2, first.Count);
        }

        [Test]
        public void Roll_Different_Seed_May_Produce_Different_Picks()
        {
            _ctl.Configure(_pool, 2, seed: 1);
            var a = new List<SkillData>(_ctl.Roll());

            _ctl.Configure(_pool, 2, seed: 2);
            var b = new List<SkillData>(_ctl.Roll());

            // With a 6-item pool and k=2, two different seeds can collide, but
            // seeds 1 and 2 on System.Random diverge in early output — assert the
            // diff and pin this as a deterministic regression guard.
            Assert.IsFalse(a[0] == b[0] && a[1] == b[1], "Seeds 1 and 2 should not collide with this pool.");
        }

        [Test]
        public void Roll_Picks_Are_Unique()
        {
            _ctl.Configure(_pool, 2, seed: 42);
            var picked = _ctl.Roll();
            Assert.AreEqual(2, picked.Count);
            Assert.AreNotEqual(picked[0], picked[1]);
        }

        [Test]
        public void Roll_Count_Greater_Than_Pool_Caps_To_Pool()
        {
            _ctl.Configure(_pool, 20, seed: 7);
            var picked = _ctl.Roll();
            Assert.AreEqual(_pool.Count, picked.Count);
            var unique = new HashSet<SkillData>(picked);
            Assert.AreEqual(_pool.Count, unique.Count);
        }

        [Test]
        public void Roll_Empty_Pool_Returns_Empty()
        {
            _ctl.Configure(new List<SkillData>(), 2, seed: 1);
            var picked = _ctl.Roll();
            Assert.AreEqual(0, picked.Count);
            Assert.IsTrue(_ctl.HasRolled);
        }

        [Test]
        public void Seed_Zero_Is_Replaced_With_Nonzero_At_Roll()
        {
            _ctl.Configure(_pool, 2, seed: 0);
            _ctl.Roll();
            Assert.AreNotEqual(0, _ctl.Seed);
        }

        [Test]
        public void ResetRollState_Clears_Picks_And_Seed()
        {
            _ctl.Configure(_pool, 2, seed: 99);
            _ctl.Roll();
            Assert.AreEqual(99, _ctl.Seed);

            _ctl.ResetRollState();
            Assert.AreEqual(0, _ctl.Picked.Count);
            Assert.IsFalse(_ctl.HasRolled);
            Assert.AreEqual(0, _ctl.Seed);
        }

        // ── FilterHiddenSkills (dreamcatcher-card-visibility unit 4) ─────────

        [Test]
        public void FilterHiddenSkills_Excludes_Skill_Wrapped_Only_By_Hidden_Card()
        {
            var catalog = MakeCatalog(MakeActiveCard(_pool[0], visible: 0));
            var filtered = SkillLoadoutController.FilterHiddenSkills(_pool, catalog);
            Assert.AreEqual(_pool.Count - 1, filtered.Count);
            CollectionAssert.DoesNotContain(filtered, _pool[0]);
        }

        [Test]
        public void FilterHiddenSkills_Keeps_Skill_With_Visible_Wrapping_Card()
        {
            var catalog = MakeCatalog(
                MakeActiveCard(_pool[0], visible: 1),
                MakeActiveCard(_pool[1], visible: 0));
            var filtered = SkillLoadoutController.FilterHiddenSkills(_pool, catalog);
            CollectionAssert.Contains(filtered, _pool[0]);
            CollectionAssert.DoesNotContain(filtered, _pool[1]);
        }

        [Test]
        public void FilterHiddenSkills_Keeps_Skill_When_Any_Of_Multiple_Wrappers_Visible()
        {
            var catalog = MakeCatalog(
                MakeActiveCard(_pool[0], visible: 0),
                MakeActiveCard(_pool[0], visible: 1));
            var filtered = SkillLoadoutController.FilterHiddenSkills(_pool, catalog);
            CollectionAssert.Contains(filtered, _pool[0]);
        }

        [Test]
        public void FilterHiddenSkills_Keeps_Unwrapped_Skills()
        {
            var catalog = MakeCatalog(); // 카탈로그에 래핑 카드 없음
            var filtered = SkillLoadoutController.FilterHiddenSkills(_pool, catalog);
            CollectionAssert.AreEqual(_pool, filtered);
        }

        [Test]
        public void FilterHiddenSkills_Ignores_NonActive_Hidden_Card()
        {
            var squadCard = MakeActiveCard(_pool[0], visible: 0);
            squadCard.type = CardType.Squad; // 숨김이지만 Active 래퍼가 아님 — 스킬 보존
            var catalog = MakeCatalog(squadCard);
            var filtered = SkillLoadoutController.FilterHiddenSkills(_pool, catalog);
            CollectionAssert.Contains(filtered, _pool[0]);
        }

        [Test]
        public void FilterHiddenSkills_Null_Catalog_Passes_Through()
        {
            var filtered = SkillLoadoutController.FilterHiddenSkills(_pool, null);
            CollectionAssert.AreEqual(_pool, filtered);
        }

        [Test]
        public void Configure_Filters_Hidden_Skill_From_Roll_Pool_When_Catalog_Wired()
        {
            // 컴포넌트 통합 경로: private cardCatalog 를 리플렉션으로 주입해 Configure →
            // Pool/Roll 모두에서 숨김 스킬이 사라지는지 본다(씬 배선과 같은 경로).
            var catalog = MakeCatalog(MakeActiveCard(_pool[0], visible: 0));
            typeof(SkillLoadoutController)
                .GetField("cardCatalog", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(_ctl, catalog);

            _ctl.Configure(_pool, _pool.Count, seed: 7);
            CollectionAssert.DoesNotContain(_ctl.Pool, _pool[0]);
            var picked = _ctl.Roll();
            CollectionAssert.DoesNotContain(picked, _pool[0]);
            Assert.AreEqual(_pool.Count - 1, picked.Count);
        }

        [Test]
        public void ResolveRimGift_Excludes_Hidden_Cards_From_Pool_And_Fallback()
        {
            // DreamcatcherHandController가 만드는 두 후보 풀 모두에서 visible == 0을
            // 제외한다. 무의식 풀이 부족하면 fallback을 쓰는 경계도 함께 고정한다.
            var visibleSubconscious = MakeCard(CardCategory.Subconscious, CardType.Unit, visible: 1);
            var hiddenSubconscious = MakeCard(CardCategory.Subconscious, CardType.Unit, visible: 0);
            var visibleFallback = MakeCard(CardCategory.Normal, CardType.Squad, visible: 1);
            var hiddenFallback = MakeCard(CardCategory.Normal, CardType.Squad, visible: 0);
            var catalog = MakeCatalog(visibleSubconscious, hiddenSubconscious, visibleFallback, hiddenFallback);
            var hand = _host.AddComponent<DreamcatcherHandController>();

            typeof(DreamcatcherHandController)
                .GetField("cardCatalog", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(hand, catalog);
            var resolve = typeof(DreamcatcherHandController)
                .GetMethod("ResolveRimGift", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var picked = (List<DreamcatcherCard>)resolve.Invoke(hand, new object[] { 17 });

            CollectionAssert.AreEquivalent(new[] { visibleSubconscious, visibleFallback }, picked);
            CollectionAssert.DoesNotContain(picked, hiddenSubconscious);
            CollectionAssert.DoesNotContain(picked, hiddenFallback);
        }
    }
}
