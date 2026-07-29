using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Editor.UnitStatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // dreamcatcher-attach-requirement unit 3 — validator 규칙 핀. 위반 카드는 코드로
    // 만든다(에셋 무오염). 각 위반이 정확히 1건씩 잡히는지, 정상 카드는 조용한지 고정.
    public class DcAttachRequirementValidatorTests
    {
        private static readonly ICollection<string> KnownIds =
            new HashSet<string> { "guardian", "ranger", "shield_shuttle" };

        private static DreamcatcherCard Card(DcAttachType attachType, string value = null,
            CardType type = CardType.Unit, bool bountyMark = false)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.id = "test_card";
            c.type = type;
            c.attachType = attachType;
            c.attachValue = value;
            c.mechanics = bountyMark
                ? new[] { new DcMechanic { payload = new DcPayloadSpec { kind = DcPayloadKind.BountyMark } } }
                : new[] { new DcMechanic { payload = new DcPayloadSpec { kind = DcPayloadKind.SelfStatBuff } } };
            return c;
        }

        private static List<string> Warn(DreamcatcherCard c, bool withIds = true) =>
            DcAttachRequirementValidator.CollectWarnings(c, withIds ? KnownIds : null);

        [Test]
        public void ValidCards_AndUnrestricted_ProduceNoWarnings()
        {
            Assert.IsEmpty(Warn(Card(DcAttachType.None)), "제한 없음 = 검증 대상 아님");
            Assert.IsEmpty(Warn(Card(DcAttachType.Class, "Guardian")));
            Assert.IsEmpty(Warn(Card(DcAttachType.UnitId, "shield_shuttle")));
            Assert.IsEmpty(Warn(null), "null 카드는 조용히 통과");
            // unit 10 — Squad도 defender-hosted 제한을 정상 소비한다.
            Assert.IsEmpty(Warn(Card(DcAttachType.None, type: CardType.Squad)));
            Assert.IsEmpty(Warn(Card(DcAttachType.Class, "Ranger", type: CardType.Squad)));
        }

        [Test]
        public void InvalidValues_AreFlaggedOnce()
        {
            var cls = Warn(Card(DcAttachType.Class, ""));
            Assert.AreEqual(1, cls.Count, "Class×None 은 1건");
            Assert.That(cls[0], Does.Contain("fail-closed"));

            var uid = Warn(Card(DcAttachType.UnitId, ""));
            Assert.AreEqual(1, uid.Count, "UnitId×빈문자열 은 1건");
            Assert.That(uid[0], Does.Contain("fail-closed"));
        }

        [Test]
        public void UnknownUnitId_IsFlagged_AndSkippedWithoutCatalog()
        {
            var card = Card(DcAttachType.UnitId, "no_such_unit");

            var withCatalog = Warn(card);
            Assert.AreEqual(1, withCatalog.Count, "없는 id 는 1건");
            Assert.That(withCatalog[0], Does.Contain("no_such_unit"));

            Assert.IsEmpty(Warn(card, withIds: false),
                "카탈로그를 못 찾으면 id 검사를 건너뛴다 — 오탐 금지");
        }

        [Test]
        public void ActiveAndBountyMark_OutOfScopeSettings_AreFlagged()
        {
            var active = Warn(Card(DcAttachType.Class, "Guardian", type: CardType.Active));
            Assert.AreEqual(1, active.Count);
            Assert.That(active[0], Does.Contain("type=Active"));

            // 적 지정 카드도 defender 게이트를 안 탄다.
            var bounty = Warn(Card(DcAttachType.Class, "Guardian", bountyMark: true));
            Assert.AreEqual(1, bounty.Count);
            Assert.That(bounty[0], Does.Contain("BountyMark"));
        }

        // unit 7 rev — attachValue 가 string 이 된 뒤로 클래스 오타는 import 예외가 아니라
        // validator 가 잡는다. 어떤 값이 문제인지 + 허용 값을 문구가 알려줘야 한다.
        [Test]
        public void UnreadableClassValue_IsFlaggedWithValueAndAllowedNames()
        {
            var w = Warn(Card(DcAttachType.Class, "Gaurdian"));
            Assert.AreEqual(1, w.Count);
            Assert.That(w[0], Does.Contain("Gaurdian"));
            Assert.That(w[0], Does.Contain("Ranger/Guardian"));

            Assert.AreEqual(1, Warn(Card(DcAttachType.Class, "2")).Count, "숫자 값도 무효");
            Assert.IsEmpty(Warn(Card(DcAttachType.Class, "guardian")), "대소문자는 허용");
        }

        [Test]
        public void OutOfScopeAndInvalid_AreReportedTogether()
        {
            // 두 축은 독립 — 둘 다 어긋나면 2건 모두 보고해야 한다(하나가 다른 하나를 가리면
            // 고친 뒤 또 다른 경고가 나와 왕복이 생긴다).
            var w = Warn(Card(DcAttachType.Class, "", type: CardType.Active));
            Assert.AreEqual(2, w.Count, "범위 밖 + 무효 = 2건");
        }
    }
}
