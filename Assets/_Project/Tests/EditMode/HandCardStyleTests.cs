using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-hand-card-face unit 0 — 손패 카드 면의 스타일/라벨 선택 로직.
    // 색 상수 값 자체는 검증하지 않는다(튜닝 자유) — 타입/카테고리별 선택·분기만 고정.
    public class HandCardStyleTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        private DreamcatcherCard MakeCard(CardType type, CardTargetAxis axis = CardTargetAxis.All,
            CardCategory category = CardCategory.Normal, SkillData skill = null)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.type = type; c.axis = axis; c.category = category; c.skill = skill;
            _created.Add(c);
            return c;
        }

        private SkillData MakeSkill(SkillEffectType effect)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.effect = effect;
            _created.Add(s);
            return s;
        }

        [Test]
        public void HandHeader_ThreeTypes_AreDistinct()
        {
            var squad = CardCategoryStyle.HandHeader(CardType.Squad);
            var unit = CardCategoryStyle.HandHeader(CardType.Unit);
            var active = CardCategoryStyle.HandHeader(CardType.Active);
            Assert.AreNotEqual(squad, unit);
            Assert.AreNotEqual(squad, active);
            Assert.AreNotEqual(unit, active);
        }

        [Test]
        public void HandBorder_Subconscious_UsesSubconsciousFrame()
        {
            var cursed = MakeCard(CardType.Unit, category: CardCategory.Subconscious);
            var normal = MakeCard(CardType.Unit);
            // 무의식 테두리 = 아웃게임 무의식 프레임과 같은 보라(단일 소스 검증).
            Assert.AreEqual(CardCategoryStyle.Frame(cursed), CardCategoryStyle.HandBorder(cursed));
            Assert.AreNotEqual(CardCategoryStyle.HandBorder(cursed), CardCategoryStyle.HandBorder(normal));
        }

        [TestCase(CardTargetAxis.All, "전체 버프")]
        [TestCase(CardTargetAxis.ClassRanger, "레인저 버프")]
        [TestCase(CardTargetAxis.ClassGuardian, "가디언 버프")]
        [TestCase(CardTargetAxis.Cost1, "1코스트 버프")]
        public void TargetTag_Squad_AxisPlusRole(CardTargetAxis axis, string expected)
            => Assert.AreEqual(expected, CardCategoryStyle.TargetTag(MakeCard(CardType.Squad, axis)));

        [Test]
        public void TargetTag_Unit_IsAttach()
            => Assert.AreEqual("아군 부착", CardCategoryStyle.TargetTag(MakeCard(CardType.Unit)));

        [Test]
        public void TargetTag_UnitWithBountyMark_IsEnemyTarget()
        {
            // 적 지정은 전용 필드가 없다 — mechanics 의 BountyMark payload 파생(조준 라우팅과 동일 판별).
            var card = MakeCard(CardType.Unit);
            card.mechanics = new[]
            {
                new DcMechanic { payload = new DcPayloadSpec { kind = DcPayloadKind.BountyMark } },
            };
            Assert.AreEqual("적 지정", CardCategoryStyle.TargetTag(card));
        }

        [TestCase(SkillEffectType.Meteor, "타일 지정")]
        [TestCase(SkillEffectType.SlowField, "타일 지정")]
        [TestCase(SkillEffectType.Tornado, "타일 지정")]
        [TestCase(SkillEffectType.Portal, "타일 2개")]
        // active-dreamcatcher-tile-aim unit 0 — 아군 버프도 타일 대상(대상축 폐기).
        [TestCase(SkillEffectType.PowerSurge, "타일 지정")]
        [TestCase(SkillEffectType.RapidFire, "타일 지정")]
        public void TargetTag_Active_BySkillEffect(SkillEffectType effect, string expected)
            => Assert.AreEqual(expected,
                CardCategoryStyle.TargetTag(MakeCard(CardType.Active, skill: MakeSkill(effect))));

        [Test]
        public void TargetTag_Active_NullSkill_FallsBack()
            => Assert.AreEqual("필드", CardCategoryStyle.TargetTag(MakeCard(CardType.Active)));

        [Test]
        public void BodyLinesOnly_Squad_HasEffectLine_NoHeader()
        {
            var card = MakeCard(CardType.Squad, CardTargetAxis.ClassRanger);
            card.effects = new[] { new CardEffect { kind = CardBuffKind.AttackSpeed, percent = 10f } };
            string body = DreamcatcherCardText.BodyLinesOnly(card);
            StringAssert.Contains("레인저 아군 공격 속도 +10%", body);
            StringAssert.DoesNotContain("스쿼드 버프", body); // 타입 헤더 줄 없음
            StringAssert.DoesNotContain("<size", body);       // 리치텍스트 헤더 장식 없음
        }

        [Test]
        public void BodyLinesOnly_UnitWithoutData_FallsBackToDescription()
        {
            var card = MakeCard(CardType.Unit);
            card.description = "부착 즉시 → 수면 4초";
            // 화살표 강제 줄바꿈 — 폴백 description 도 같은 규칙을 탄다.
            Assert.AreEqual("부착 즉시 →\n수면 4초", DreamcatcherCardText.BodyLinesOnly(card));
        }

        [Test]
        public void BodyLinesOnly_BreaksLineAtArrow()
        {
            var card = MakeCard(CardType.Squad, CardTargetAxis.All);
            card.effects = new[] { new CardEffect { kind = CardBuffKind.MoveSpeed, percent = 10f } };
            StringAssert.Contains("항상 →\n", DreamcatcherCardText.BodyLinesOnly(card));
        }

        [Test]
        public void BodyLinesOnly_EmptyCard_IsEmpty()
            => Assert.AreEqual("", DreamcatcherCardText.BodyLinesOnly(MakeCard(CardType.Unit)));
    }
}
