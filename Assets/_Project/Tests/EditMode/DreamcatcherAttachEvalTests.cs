using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attach-lockon — 부착 조준 preflight 순수 판정 핀. ApplyDreamcatcherCardToUnit
    // 의 유닛-종속 게이트(통통구슬=ProjectileBounce→투사체 / 끝을 보는 눈=FrontmostTarget·
    // HeavyStrike→데미지 output / 이중 상태)와의 동기화를 게임플레이 케이스로 고정한다.
    public class DreamcatcherAttachEvalTests
    {
        private static DreamcatcherCard UnitCard(DcMechanic[] mech = null, DcAttackModSpec[] mods = null)
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.type = CardType.Unit;
            c.mechanics = mech;
            c.attackMods = mods;
            return c;
        }

        private static DcMechanic Mech(DcPayloadKind kind) =>
            new DcMechanic { payload = new DcPayloadSpec { kind = kind } };

        private static DcAttackModSpec Mod(DcAttackModKind kind, int count, float damageMul) =>
            new DcAttackModSpec { kind = kind, count = count, damageMul = damageMul };

        private static bool Eval(DreamcatcherCard card, bool proj, bool dmg,
            bool lethal = false, bool cocoon = false) =>
            DreamcatcherAttachEval.WouldApply(card, proj, dmg, lethal, cocoon);

        // ── 통통구슬 = ProjectileBounce: 투사체 유닛만 ──────────────────────────
        [Test]
        public void ProjectileBounce_OnProjectileUnit_Applies()
        {
            var card = UnitCard(mods: new[] { Mod(DcAttackModKind.ProjectileBounce, 2, 1f) });
            Assert.IsTrue(Eval(card, proj: true, dmg: true));
        }

        [Test]
        public void ProjectileBounce_OnMeleeUnit_Rejects()
        {
            var card = UnitCard(mods: new[] { Mod(DcAttackModKind.ProjectileBounce, 2, 1f) });
            Assert.IsFalse(Eval(card, proj: false, dmg: true), "가디언(근접, 투사체 없음)엔 부착 불가");
        }

        [Test]
        public void ProjectileBounce_ZeroCount_Rejects()
        {
            var card = UnitCard(mods: new[] { Mod(DcAttackModKind.ProjectileBounce, 0, 1f) });
            Assert.IsFalse(Eval(card, proj: true, dmg: true));
        }

        // ── FrontmostTarget / HeavyStrike: 데미지 output 필요 ──────────────────
        [Test]
        public void FrontmostTarget_NeedsDamageOutput()
        {
            var card = UnitCard(mods: new[] { Mod(DcAttackModKind.FrontmostTarget, 0, 1.2f) });
            Assert.IsTrue(Eval(card, proj: false, dmg: true));
            Assert.IsFalse(Eval(card, proj: false, dmg: false), "데미지 output 없는 서포트는 거부");
        }

        [Test]
        public void HeavyStrike_NeedsDamageOutput()
        {
            var card = UnitCard(mech: new[] { Mech(DcPayloadKind.HeavyStrike) });
            Assert.IsTrue(Eval(card, proj: false, dmg: true));
            Assert.IsFalse(Eval(card, proj: false, dmg: false));
        }

        // ── 유닛-무관 mechanic 은 아무 유닛에나 기여 ───────────────────────────
        [Test]
        public void GenericMechanic_AppliesToAnyUnit()
        {
            var card = UnitCard(mech: new[] { Mech(DcPayloadKind.SelfStatBuff) });
            Assert.IsTrue(Eval(card, proj: false, dmg: false), "클래스 무관 mechanic 은 근접에도 기여");
        }

        [Test]
        public void MechanicSavesMeleeFromProjectileMod()
        {
            // mechanic(무관) + ProjectileBounce(투사체 필요) 혼합 → 근접이어도 mechanic 이 살림.
            var card = UnitCard(
                mech: new[] { Mech(DcPayloadKind.SelfStatBuff) },
                mods: new[] { Mod(DcAttackModKind.ProjectileBounce, 2, 1f) });
            Assert.IsTrue(Eval(card, proj: false, dmg: false));
        }

        // ── 이중 상태 거부(카드 전체) ─────────────────────────────────────────
        [Test]
        public void DupLethalTimer_RejectsWholeCard()
        {
            var card = UnitCard(mech: new[] { Mech(DcPayloadKind.SelfBuffLethal) });
            Assert.IsFalse(Eval(card, proj: true, dmg: true, lethal: true), "이미 LethalTimer 면 거부");
            Assert.IsTrue(Eval(card, proj: true, dmg: true, lethal: false));
        }

        // ── Squad / 빈 카드 ───────────────────────────────────────────────────
        [Test]
        public void SquadCard_AlwaysApplies()
        {
            var c = ScriptableObject.CreateInstance<DreamcatcherCard>();
            c.type = CardType.Squad;
            Assert.IsTrue(DreamcatcherAttachEval.WouldApply(c, false, false, false, false));
        }

        [Test]
        public void NoEffects_Rejects()
        {
            Assert.IsFalse(Eval(UnitCard(), proj: true, dmg: true));
            Assert.IsFalse(DreamcatcherAttachEval.WouldApply(null, true, true, false, false));
        }
    }
}
