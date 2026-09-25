using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.BattleCoreUnity.Cards;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 9 구현 2 — 옛 `SkillLoadoutControllerTests` 의 「숨긴 카드의 스킬을 풀에서 뺀다」
    // (S4) 단언을 그 규칙의 새 주인 `CoreDeckComposition.FilterHiddenSkills` 로 옮긴다(8c 가 본문을 이사했고
    // 옛 쪽은 위임만 남았다 — 옛 파일이 지워지면 이 규칙을 부르는 테스트가 0 이 된다).
    //
    // 이 폴더(`Wassup.Tests.EditMode.Assets`)인 이유: 대상이 Unity 층(`Wassup.Runtime` · SO 입력)이라 헤드리스
    // `EditModeCore`(엔진 참조 없음 · `Wassup.Runtime` 미참조)에서는 컴파일되지 않고, `CoreDeckComposition` 의
    // 다른 규칙(굴림 결정론·확정 덱 검증)이 이미 `CardViewAssetTests` 로 여기 있다. 에셋 로드는 쓰지 않는다 —
    // SO 를 메모리에서 만든다.
    public class RetiredDeckFilterPortTests
    {
        private readonly List<Object> _created = new List<Object>();
        private List<SkillData> _pool;

        [SetUp]
        public void SetUp()
        {
            _pool = new List<SkillData>();
            for (int i = 0; i < 6; i++)
            {
                var s = New<SkillData>();
                s.id = $"skill_{i}";
                s.displayName = $"Skill {i}";
                _pool.Add(s);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private T New<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _created.Add(o);
            return o;
        }

        private DreamcatcherCard ActiveCard(SkillData skill, int visible)
        {
            var card = New<DreamcatcherCard>();
            card.type = CardType.Active;
            card.skill = skill;
            card.visible = visible;
            return card;
        }

        private DreamcatcherCardCatalog Catalog(params DreamcatcherCard[] cards)
        {
            var catalog = New<DreamcatcherCardCatalog>();
            catalog.cards = cards;
            return catalog;
        }

        // 옛 SkillLoadoutControllerTests::FilterHiddenSkills_Excludes_Skill_Wrapped_Only_By_Hidden_Card — 숨긴 카드만 감싼 스킬은 빠진다
        [Test]
        public void 숨긴_카드만_감싼_스킬은_풀에서_빠진다()
        {
            var filtered = CoreDeckComposition.FilterHiddenSkills(_pool, Catalog(ActiveCard(_pool[0], visible: 0)));
            Assert.AreEqual(_pool.Count - 1, filtered.Count);
            CollectionAssert.DoesNotContain(filtered, _pool[0]);
        }

        // 옛 SkillLoadoutControllerTests::FilterHiddenSkills_Keeps_Skill_With_Visible_Wrapping_Card — 보이는 카드가 감싸면 남는다
        [Test]
        public void 보이는_카드가_감싼_스킬은_남는다()
        {
            var filtered = CoreDeckComposition.FilterHiddenSkills(_pool, Catalog(
                ActiveCard(_pool[0], visible: 1),
                ActiveCard(_pool[1], visible: 0)));
            CollectionAssert.Contains(filtered, _pool[0]);
            CollectionAssert.DoesNotContain(filtered, _pool[1]);
        }

        // 옛 SkillLoadoutControllerTests::FilterHiddenSkills_Keeps_Skill_When_Any_Of_Multiple_Wrappers_Visible — 감싼 카드 중 하나라도 보이면 남는다
        [Test]
        public void 감싼_카드_중_하나라도_보이면_남는다()
        {
            var filtered = CoreDeckComposition.FilterHiddenSkills(_pool, Catalog(
                ActiveCard(_pool[0], visible: 0),
                ActiveCard(_pool[0], visible: 1)));
            CollectionAssert.Contains(filtered, _pool[0]);
        }

        // 옛 SkillLoadoutControllerTests::FilterHiddenSkills_Keeps_Unwrapped_Skills — 감싼 카드가 없는 스킬은 남는다
        [Test]
        public void 감싼_카드가_없는_스킬은_남는다()
        {
            CollectionAssert.AreEqual(_pool, CoreDeckComposition.FilterHiddenSkills(_pool, Catalog()));
        }

        // 옛 SkillLoadoutControllerTests::FilterHiddenSkills_Ignores_NonActive_Hidden_Card — 액티브가 아닌 숨김 카드는 무시
        [Test]
        public void 액티브가_아닌_숨김_카드는_스킬을_가리지_않는다()
        {
            var squad = ActiveCard(_pool[0], visible: 0);
            squad.type = CardType.Squad;   // 숨김이지만 액티브 래퍼가 아니다
            CollectionAssert.Contains(CoreDeckComposition.FilterHiddenSkills(_pool, Catalog(squad)), _pool[0]);
        }

        // 옛 SkillLoadoutControllerTests::FilterHiddenSkills_Null_Catalog_Passes_Through — 카탈로그 없음 = 무필터
        [Test]
        public void 카탈로그가_없으면_무필터다()
        {
            CollectionAssert.AreEqual(_pool, CoreDeckComposition.FilterHiddenSkills(_pool, null));
        }

        // 옛 SkillLoadoutControllerTests::Configure_Filters_Hidden_Skill_From_Roll_Pool_When_Catalog_Wired — 판의 덱 조립에서도 숨김 스킬은 안 굴린다
        [Test]
        public void 덱_조립의_액티브_굴림에서도_숨긴_스킬은_나오지_않는다()
        {
            var hidden = ActiveCard(_pool[0], visible: 0);
            var activeCards = new List<DreamcatcherCard>();
            for (int i = 1; i < _pool.Count; i++) activeCards.Add(ActiveCard(_pool[i], visible: 1));
            // 감싸는 카드 목록에 숨김 카드도 넣는다 — 굴림이 그 스킬을 뽑으면 카드가 실제로 덱에 든다.
            var withHidden = new List<DreamcatcherCard>(activeCards) { hidden };
            var catalog = Catalog(withHidden.ToArray());

            var deck = CoreDeckComposition.Compose(null, catalog, _pool, _pool.Count, withHidden, seed: 7);

            Assert.AreEqual(_pool.Count - 1, deck.Count, "숨긴 스킬 하나만 빠지고 나머지는 전부 굴린다");
            foreach (var card in deck) Assert.AreNotSame(_pool[0], card.skill, "숨긴 카드의 스킬이 판의 덱에 들었다");
        }
    }
}
