using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.BattleCore.Trigger;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attach-requirement — 부착 제한(정적 술어) 핀. host 종속 판정(옛 `WouldApply`)은 코어 `Applicability` 의
    // 몫이라 그 케이스들은 코어 테스트(`EditModeCore/ApplicabilityTests`)가 잰다 — skill-data-table 4-정리(B21)에서 옛 사본과 함께 옮겼다.
    public class DreamcatcherAttachEvalTests
    {
        private static DreamcatcherCard UnitCard(DcMechanic[] mech = null)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.type = CardType.Unit;
            TestBindings.Attach(c, mech);
            return c;
        }

        private static DcMechanic Mech(EffectKind kind) =>
            new DcMechanic { payload = new DcPayloadSpec { kind = kind } };

        // ── dreamcatcher-attach-requirement unit 0: 부착 대상 제한(정적 술어) ──────

        private static DreamcatcherCard RequireCard(DcAttachType type, string value = null)
        {
            // 제한 외 조건은 통과하는 카드로 둔다(제한이 유일한 변수).
            var c = UnitCard(mech: new[] { Mech(EffectKind.SelfStatBuff) });
            c.attachType = type;
            c.attachValue = value;
            return c;
        }

        private static bool Meets(DreamcatcherCard card, DefenderClass role, string unitId = "") =>
            DreamcatcherAttachEval.MeetsAttachRequirement(card, role, unitId);

        [Test]
        public void NoRequirement_AllowsAnyHost()
        {
            var card = RequireCard(DcAttachType.None);
            Assert.IsTrue(Meets(card, DefenderClass.Ranger, "archer"));
            Assert.IsTrue(Meets(card, DefenderClass.Guardian, "shield_shuttle"),
                "기존 카드(zero-init)는 모든 유닛에 부착 가능해야 한다");
        }

        [Test]
        public void ClassRequirement_GatesByRole()
        {
            var card = RequireCard(DcAttachType.Class, "Guardian");
            Assert.IsTrue(Meets(card, DefenderClass.Guardian, "shield_shuttle"));
            Assert.IsFalse(Meets(card, DefenderClass.Ranger, "archer"), "가디언 전용 카드는 레인저에 불가");
            Assert.IsFalse(Meets(card, DefenderClass.Support, "healer"));
        }

        // unit 7 rev — 값 칸이 string 이라 "읽을 수 없는 값" 도 여기서 막아야 한다.
        // 구 설계에선 import 예외가 걸러줬지만 이제 판정 자체가 fail-closed 로 처리한다.
        [Test]
        public void ClassRequirement_UnreadableValue_FailsClosed()
        {
            // "None" 은 파싱은 되지만 제한으로서 무의미 → 어디에도 안 붙는다.
            Assert.IsFalse(Meets(RequireCard(DcAttachType.Class, "None"),
                DefenderClass.Guardian, "shield_shuttle"));
            Assert.IsFalse(Meets(RequireCard(DcAttachType.Class, null),
                DefenderClass.Guardian, "shield_shuttle"), "빈 값");
            Assert.IsFalse(Meets(RequireCard(DcAttachType.Class, "Gaurdian"),
                DefenderClass.Guardian, "shield_shuttle"), "오타는 조용히 통과하지 않는다");
            // 숫자 문자열은 Enum.TryParse 를 통과하지만 이름 왕복 검사가 막는다 —
            // 시트에 2 를 적었을 때 우연히 Guardian 이 되면 추적 불가한 버그가 된다.
            Assert.IsFalse(Meets(RequireCard(DcAttachType.Class, "2"),
                DefenderClass.Guardian, "shield_shuttle"), "숫자 값 금지");
        }

        [Test]
        public void ClassRequirement_IsCaseInsensitive()
        {
            // 시트에 손으로 적는 값이라 대소문자는 허용한다(유닛 id 와 달리 이름 매칭).
            Assert.IsTrue(Meets(RequireCard(DcAttachType.Class, "guardian"),
                DefenderClass.Guardian, "shield_shuttle"));
            Assert.IsTrue(Meets(RequireCard(DcAttachType.Class, "GUARDIAN"),
                DefenderClass.Guardian, "shield_shuttle"));
        }

        [Test]
        public void TryParseAttachClass_ContractIsSingleSource()
        {
            Assert.IsTrue(DreamcatcherAttachEval.TryParseAttachClass("Support", out var s));
            Assert.AreEqual(DefenderClass.Support, s);
            Assert.IsFalse(DreamcatcherAttachEval.TryParseAttachClass("None", out _), "None = 제한 아님");
            Assert.IsFalse(DreamcatcherAttachEval.TryParseAttachClass("", out _));
            Assert.IsFalse(DreamcatcherAttachEval.TryParseAttachClass("1", out _), "숫자 배제");
            Assert.IsFalse(DreamcatcherAttachEval.TryParseAttachClass("Healer", out _), "없는 이름");
        }

        [Test]
        public void UnitIdRequirement_GatesById()
        {
            var card = RequireCard(DcAttachType.UnitId, "shield_shuttle");
            Assert.IsTrue(Meets(card, DefenderClass.Guardian, "shield_shuttle"));
            Assert.IsFalse(Meets(card, DefenderClass.Guardian, "guardian"), "다른 유닛 id 는 불가");
            Assert.IsFalse(Meets(card, DefenderClass.Guardian, "Shield_Shuttle"),
                "id 는 ordinal 비교 — 대소문자가 다르면 다른 유닛이다");
        }

        [Test]
        public void UnitIdRequirement_BlankId_FailsClosed()
        {
            Assert.IsFalse(Meets(RequireCard(DcAttachType.UnitId, null),
                DefenderClass.Guardian, "shield_shuttle"));
            Assert.IsFalse(Meets(RequireCard(DcAttachType.UnitId, ""),
                DefenderClass.Guardian, ""), "빈 요구 id 는 빈 host id 와도 매칭되지 않는다");
        }

        [Test]
        public void MeetsAttachRequirement_NullCard_FailsClosed()
        {
            Assert.IsFalse(Meets(null, DefenderClass.Guardian, "shield_shuttle"));
        }

        // unit 1 — 무효 설정 판별(브리지 경고 문구 분기 + unit 3 validator 공유 정의).
        // "제한 불일치(정상 거절)"와 "데이터 실수"를 구분하는 것이 목적이다.
        [Test]
        public void HasInvalidAttachRequirement_DetectsUnusableValues()
        {
            Assert.IsTrue(DreamcatcherAttachEval.HasInvalidAttachRequirement(
                RequireCard(DcAttachType.Class, "None")));
            Assert.IsTrue(DreamcatcherAttachEval.HasInvalidAttachRequirement(
                RequireCard(DcAttachType.UnitId, null)));
            Assert.IsTrue(DreamcatcherAttachEval.HasInvalidAttachRequirement(
                RequireCard(DcAttachType.UnitId, "")));
            Assert.IsTrue(DreamcatcherAttachEval.HasInvalidAttachRequirement(
                RequireCard(DcAttachType.Class, "Gaurdian")), "읽을 수 없는 클래스 이름도 무효");
        }

        [Test]
        public void HasInvalidAttachRequirement_ValidAndUnrestricted_AreNotInvalid()
        {
            Assert.IsFalse(DreamcatcherAttachEval.HasInvalidAttachRequirement(
                RequireCard(DcAttachType.Class, "Guardian")));
            Assert.IsFalse(DreamcatcherAttachEval.HasInvalidAttachRequirement(
                RequireCard(DcAttachType.UnitId, "guardian")));
            Assert.IsFalse(DreamcatcherAttachEval.HasInvalidAttachRequirement(
                RequireCard(DcAttachType.None)), "제한 없음은 '무효'가 아니다");
            Assert.IsFalse(DreamcatcherAttachEval.HasInvalidAttachRequirement(null));
        }
    }
}
