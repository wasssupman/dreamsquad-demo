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

        // targetsEnemies 기본 true = "적을 때리는 보통 유닛" — 기존 케이스의 전제.
        private static bool Eval(DreamcatcherCard card, bool proj, bool dmg,
            bool lethal = false, bool cocoon = false, bool targetsEnemies = true) =>
            DreamcatcherAttachEval.WouldApply(card, proj, dmg, lethal, cocoon, targetsEnemies);

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

        // ── 비수 = ProjectileToTarget: 적을 타겟하는 유닛만 ────────────────────
        // 니들은 그 공격의 대상으로 날아가므로, 대상이 아군인 힐러(targetAllies)에
        // 붙으면 회복 대상을 때린다. 근접/원거리/머신거너는 전부 적을 타겟한다.
        [Test]
        public void PokeNeedle_OnEnemyTargetingUnit_Applies()
        {
            var card = UnitCard(mech: new[] { Mech(DcPayloadKind.ProjectileToTarget) });
            Assert.IsTrue(Eval(card, proj: true, dmg: true), "원거리 유닛");
            Assert.IsTrue(Eval(card, proj: false, dmg: true), "근접 유닛도 5회째에 니들을 쏜다");
        }

        [Test]
        public void PokeNeedle_OnAllyTargetingUnit_Rejects()
        {
            var card = UnitCard(mech: new[] { Mech(DcPayloadKind.ProjectileToTarget) });
            Assert.IsFalse(Eval(card, proj: false, dmg: false, targetsEnemies: false),
                "힐러(아군 타겟)에 붙으면 니들이 아군을 때린다 — 부착 거절");
        }

        [Test]
        public void PokeNeedle_MixedCard_StillAppliesOnHealerViaOtherMechanic()
        {
            // ProjectileToTarget 만 거절되고 카드 전체가 죽지는 않는다(부분 skip = apply 와 동일 결).
            var card = UnitCard(mech: new[]
            {
                Mech(DcPayloadKind.ProjectileToTarget),
                Mech(DcPayloadKind.SelfStatBuff),
            });
            Assert.IsTrue(Eval(card, proj: false, dmg: false, targetsEnemies: false));
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
            Assert.IsTrue(DreamcatcherAttachEval.WouldApply(c, false, false, false, false, false));
        }

        [Test]
        public void NoEffects_Rejects()
        {
            Assert.IsFalse(Eval(UnitCard(), proj: true, dmg: true));
            Assert.IsFalse(DreamcatcherAttachEval.WouldApply(null, true, true, false, false, true));
        }

        // ── dreamcatcher-attach-requirement unit 0: 부착 대상 제한(정적 술어) ──────
        // WouldApply 와 독립 함수라 위 케이스들은 무영향 — 제한만 따로 핀한다.

        private static DreamcatcherCard RequireCard(DcAttachType type, string value = null)
        {
            // 제한 외 조건은 통과하는 카드로 둔다(제한이 유일한 변수).
            var c = UnitCard(mech: new[] { Mech(DcPayloadKind.SelfStatBuff) });
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
