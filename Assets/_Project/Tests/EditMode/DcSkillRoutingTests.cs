using NUnit.Framework;
using Wassup.Core;
using Wassup.Data;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attach-range-preview unit 1 — 트리거×페이로드 → concrete 라우팅의 핀.
    //
    // 이 함수는 bake(`BattleBridge`)와 범위 카탈로그가 **같이** 부른다. 두 소비처가 각자 표를 들면
    // 「자리의 주인이 다른 폭발 셋」 같은 특수 케이스가 한쪽에만 추가되어 같은 저작이 host 에 따라
    // 다른 스킬로 간다(skill-layer-migration 3g 의 경고). 여기 핀은 그 특수 케이스들이다.
    public class DcSkillRoutingTests
    {
        [TestCase(DcTriggerKind.OnKill)]
        [TestCase(DcTriggerKind.OnDeath)]
        [TestCase(DcTriggerKind.OnRetire)]
        public void SelfTileAoe_OnDeathSiteTriggers_RoutesToDeathSiteBlast(DcTriggerKind trigger)
        {
            // 「실려 온 자리에서 터진다」 — 드레인 시점엔 시전자가 없어 살아 있는 발밑을 물을 수 없다.
            Assert.AreEqual(DeathSiteBlastSkill.Id, DcSkillRouting.SkillIdFor(trigger, DcPayloadKind.SelfTileAoe));
        }

        [TestCase(DcTriggerKind.OnDamagedN)]
        [TestCase(DcTriggerKind.OnShieldBreak)]
        [TestCase(DcTriggerKind.HealthThreshold)]
        public void SelfTileAoe_OnLiveTriggers_RoutesToSelfAreaBlast(DcTriggerKind trigger)
        {
            Assert.AreEqual(SelfAreaBlastSkill.Id, DcSkillRouting.SkillIdFor(trigger, DcPayloadKind.SelfTileAoe));
        }

        [Test]
        public void OnKill_SpawnHazard_RoutesToDeathSiteHazard()
        {
            Assert.AreEqual(DeathSiteHazardSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.OnKill, DcPayloadKind.SpawnHazard));
        }

        [Test]
        public void TriggerlessImmediates_RouteToTheirOwnConcretes()
        {
            Assert.AreEqual(SelfBuffLethalSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.None, DcPayloadKind.SelfBuffLethal));
            Assert.AreEqual(DreamCocoonSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.None, DcPayloadKind.DreamCocoon));
            Assert.AreEqual(BountyMarkSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.None, DcPayloadKind.BountyMark));
        }

        [Test]
        public void SelfStatBuff_SplitsOnHealthThreshold()
        {
            Assert.AreEqual(ThresholdSelfBuffSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.HealthThreshold, DcPayloadKind.SelfStatBuff));
            Assert.AreEqual(SelfStatBuffSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.AttackN, DcPayloadKind.SelfStatBuff));
        }

        [Test]
        public void PayloadOnlyKinds_FollowThePayloadTable()
        {
            Assert.AreEqual(AreaSleepSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.OnShieldBreak, DcPayloadKind.AreaSleep));
            Assert.AreEqual(AreaSleepSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.PeriodicTimer, DcPayloadKind.AreaSleep));
            Assert.AreEqual(EmitPatternSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.PeriodicTimer, DcPayloadKind.EmitProjectilePattern));
            Assert.AreEqual(GrantShieldSkill.Id, DcSkillRouting.SkillIdFor(DcTriggerKind.OnPlace, DcPayloadKind.GrantShield));
        }

        [Test]
        public void UnroutedPayload_IsNotRouted()
        {
            Assert.AreEqual(SkillRegistry.NotRouted, DcSkillRouting.SkillIdFor(DcTriggerKind.AttackN, DcPayloadKind.None));
        }
    }
}
