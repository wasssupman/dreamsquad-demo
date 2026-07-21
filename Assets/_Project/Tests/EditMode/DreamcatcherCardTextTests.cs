using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-hand-drag-tooltip unit 0 — shared card body formatter.
    // The literal expected strings below double as the deck-builder regression
    // guard: Squad/Unit output must match the legacy PopupBody byte-for-byte
    // (the DamageVsCc label is the single intended fix — was "Cost Rate").
    public class DreamcatcherCardTextTests
    {
        private readonly List<Object> _cleanup = new List<Object>();

        private DreamcatcherCard Card(CardType type, CardTargetAxis axis = CardTargetAxis.All,
            CardEffect[] effects = null, string description = "")
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.type = type;
            card.axis = axis;
            card.effects = effects;
            card.description = description;
            _cleanup.Add(card);
            return card;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup)
                if (obj != null) Object.DestroyImmediate(obj);
            _cleanup.Clear();
        }

        [Test]
        public void Squad_MixedEffects_MatchesLegacyPopupBodyFormat()
        {
            var card = Card(CardType.Squad, CardTargetAxis.ClassRanger, new[]
            {
                new CardEffect { kind = CardBuffKind.AttackDamage, percent = 20f },
                new CardEffect { kind = CardBuffKind.MoveSpeed, percent = -10f },
            });

            Assert.AreEqual(
                "<size=22><color=#F5D480><b>RANGER</b></color>  ·  <color=#9AA6C0>SQUAD</color></size>"
                + "\n\nAttack  <color=#8BE28B>+20%</color>"
                + "\nMove Speed  <color=#E28B8B>-10%</color>",
                DreamcatcherCardText.Body(card));
        }

        // unit 4 — 인게임 툴팁 전용 compact 변형. 블록 구분만 빈 줄 → 단일 개행이고
        // 내용/라벨/색은 Body() 와 동일해야 한다(같은 카드를 두 화면에서 볼 때 텍스트가
        // 달라 보이면 안 된다).
        [Test]
        public void BodyCompact_DropsBlankLines_ButKeepsSameContent()
        {
            var card = Card(CardType.Squad, CardTargetAxis.ClassRanger, new[]
            {
                new CardEffect { kind = CardBuffKind.AttackDamage, percent = 20f },
            }, "궁수의 사거리가 늘어난다.");

            // unit 6 — 축·타입 줄은 compact 에서만 상대 크기(115%). 본문 폰트를 키워도
            // 이 줄이 본문보다 작아지지 않게 하기 위함이고, Body() 의 절대 22 는 유지된다.
            Assert.AreEqual(
                "<size=115%><color=#F5D480><b>RANGER</b></color>  ·  <color=#9AA6C0>SQUAD</color></size>"
                + "\nAttack  <color=#8BE28B>+20%</color>"
                + "\n<color=#D4DAE8>궁수의 사거리가 늘어난다.</color>",
                DreamcatcherCardText.BodyCompact(card));

            // 빈 줄과 축·타입 줄 크기만 다른지 — 나머지 내용은 완전히 같아야 한다.
            Assert.AreEqual(
                DreamcatcherCardText.Body(card).Replace("\n\n", "\n").Replace("<size=22>", "<size=115%>"),
                DreamcatcherCardText.BodyCompact(card));
        }

        [Test]
        public void BodyCompact_NoEffectsNoDescription_SameAsBody()
        {
            var card = Card(CardType.Active);

            // 블록이 하나뿐이면 구분자가 안 쓰이므로 축·타입 줄 크기만 다르다.
            Assert.AreEqual(
                DreamcatcherCardText.Body(card).Replace("<size=22>", "<size=115%>"),
                DreamcatcherCardText.BodyCompact(card));
        }

        [Test]
        public void Unit_DescriptionOnly_NoAxisChip_NoEffectLines()
        {
            var card = Card(CardType.Unit, CardTargetAxis.ClassGuardian, null, "적 처치 시 공격력이 오른다.");

            Assert.AreEqual(
                "<size=22><color=#F0B44E>UNIT</color></size>"
                + "\n\n<color=#D4DAE8>적 처치 시 공격력이 오른다.</color>",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void Active_GetsOwnLabel_NotSquadFallback()
        {
            var card = Card(CardType.Active, description: "지정 타일에 운석을 떨어뜨린다.");

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("ACTIVE", body);
            StringAssert.DoesNotContain("SQUAD", body);
            StringAssert.Contains("지정 타일에 운석을 떨어뜨린다.", body);
        }

        [Test]
        public void DamageVsCc_LabeledExplicitly_NotCostRate()
        {
            var card = Card(CardType.Squad, CardTargetAxis.All, new[]
            {
                new CardEffect { kind = CardBuffKind.DamageVsCc, percent = 15f },
            });

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("Damage vs CC  <color=#8BE28B>+15%</color>", body);
            StringAssert.DoesNotContain("Cost Rate", body);
        }

        [Test]
        public void EmptyDescription_OmitsDescriptionBlock()
        {
            var card = Card(CardType.Squad, CardTargetAxis.Cost1, new[]
            {
                new CardEffect { kind = CardBuffKind.CostRate, percent = 30f },
            });

            Assert.AreEqual(
                "<size=22><color=#F5D480><b>COST-1 UNITS</b></color>  ·  <color=#9AA6C0>SQUAD</color></size>"
                + "\n\nCost Rate  <color=#8BE28B>+30%</color>",
                DreamcatcherCardText.Body(card));
        }

        [Test]
        public void CrackedGrail_ShowsRewardRiskAndDescription()
        {
            var card = Card(CardType.Squad, CardTargetAxis.All, new[]
            {
                new CardEffect { kind = CardBuffKind.AttackDamage, percent = 70f },
                new CardEffect { kind = CardBuffKind.EffectiveHealth, percent = -40f },
            }, "호스트가 살아있는 동안 모든 아군의 공격력이 70% 증가하지만 체력이 40% 감소한다.");

            string body = DreamcatcherCardText.Body(card);
            StringAssert.Contains("Attack  <color=#8BE28B>+70%</color>", body);
            StringAssert.Contains("Health  <color=#E28B8B>-40%</color>", body);
            StringAssert.Contains("호스트가 살아있는 동안 모든 아군의 공격력이 70% 증가하지만 체력이 40% 감소한다.", body);
        }
    }
}
