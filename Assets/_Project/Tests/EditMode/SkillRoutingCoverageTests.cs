using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Battle.Combat;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // skill-layer-migration unit 8 — **라우팅 전수. 「0 이 무엇을 뜻하는가」가 바뀌었다.**
    //
    // 이전 중에는 `skillId == 0` 이 「아직 arm 이 처리한다」였고, 그래서 라우팅이 0 을
    // 돌려주는 조합은 안전했다. arm 이 철거된 지금 그 값은 **「아무도 처리 안 한다」**다 —
    // 슬롯은 구워지고, 트리거는 발화하고, 그 다음에 아무 일도 일어나지 않는다.
    // 게다가 arm 과 함께 「미처리 payload」 경고도 사라져서 **로그조차 없다.**
    //
    // ⚠ 이 그물은 실제 사고에서 나왔다. `OnPlace × NextAttackDoubleFire`(배치하면 다음
    // 공격이 2연발)가 정확히 그렇게 죽어 있었다 — 충전 payload 가 `OnDamagedN` 블록
    // 안에만 라우팅돼 있었고, 그 트리거 밖 조합은 0 으로 떨어졌다. EditMode 는 전부
    // 초록이었고 PlayMode 한 건이 잡았다.
    //
    // 그래서 규칙: **감지자가 있는 조합은 스킬로 라우팅되거나, 「스킬이 아니다」 목록에
    // 이름이 있어야 한다.** 침묵은 둘 중 어느 쪽도 아니다.
    public class SkillRoutingCoverageTests
    {
        // ⚠ **목록은 여기 없다.** 정본은 `SkillPayloadPolicy` 이고 **bake 가 같은 술어로
        // 거절**한다 — 그물과 게이트가 각자 목록을 들면 한쪽만 낡아서, 테스트는 초록인데
        // 라이브가 조용히 죽는 바로 그 형태가 재현된다.
        [Test]
        public void EveryDetectableCombination_RoutesToASkill_OrIsNamedNotASkill()
        {
            var holes = new List<string>();
            foreach (DcTriggerKind t in Enum.GetValues(typeof(DcTriggerKind)))
            foreach (DcPayloadKind pk in Enum.GetValues(typeof(DcPayloadKind)))
            {
                // 감지자가 없으면 bake 가 거절한다 — 침묵이 아니라 loud 거절이라 안전하다.
                if (!DcTrigger.HasDetector(t, hostIsEnemy: false)) continue;
                if (!SkillPayloadPolicy.IsSkill(pk) || SkillPayloadPolicy.IsAttachOnly(pk)) continue;

                int id = BattleBridge.RoutingProbe(t, pk);
                if (id == Wassup.Skills.SkillRegistry.NotRouted)
                    holes.Add($"{t} × {pk}");
            }

            Assert.IsEmpty(holes,
                "이 조합들은 슬롯이 구워지고 트리거가 발화한 뒤 **아무 일도 안 일어난다** — " +
                "게다가 arm 과 함께 경고도 사라져 로그조차 없다.\n" +
                "고치는 법 둘: (a) 트리거 무관 concrete 면 `SkillIdForPayload` 스위치로 옮긴다, " +
                "(b) 정말 스킬이 아니면 이 테스트의 목록에 **이유와 함께** 올린다.\n" +
                "구멍: " + string.Join(", ", holes));
        }

        // ⚠ 반증 대조군 — 위 그물이 항상 초록인 «빈» 그물이 아님을 보인다.
        // 「스킬이 아닌 것」이 정말 0 을 돌려주는지 확인한다.
        [Test]
        public void NotSkills_ActuallyReturnZero()
        {
            Assert.AreEqual(Wassup.Skills.SkillRegistry.NotRouted,
                BattleBridge.RoutingProbe(DcTriggerKind.OnPlace, DcPayloadKind.PlacementAura));
            Assert.AreEqual(Wassup.Skills.SkillRegistry.NotRouted,
                BattleBridge.RoutingProbe(DcTriggerKind.AttackN, DcPayloadKind.HeavyStrike));
        }

        // 사고 재현 — 이 조합이 다시 0 이 되면 여기가 먼저 빨개진다(PlayMode 8분 전에).
        [Test]
        public void OnPlaceCharge_IsRouted_TheBugThisNetWasBornFrom()
        {
            Assert.AreNotEqual(Wassup.Skills.SkillRegistry.NotRouted,
                BattleBridge.RoutingProbe(DcTriggerKind.OnPlace, DcPayloadKind.NextAttackDoubleFire),
                "배치하면 다음 공격이 2연발 — 이게 0 이면 배치해도 아무 일이 안 일어난다");
        }
    }
}
