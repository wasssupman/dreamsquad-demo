using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // squad-character-page Unit 0 — pins the data-derived kit summary contract.
    // The summary is the detail card's "설명문", assembled purely from existing
    // DefenderUnitData fields (no authored lore). Tests fix phrasing + order so
    // the sentence stays stable as the roster grows.
    public class UnitKitSummaryTests
    {
        private static DefenderUnitData Unit() => ScriptableObject.CreateInstance<DefenderUnitData>();
        private static ProjectileData Projectile() => ScriptableObject.CreateInstance<ProjectileData>();

        [Test]
        public void Null_ReturnsEmpty()
        {
            Assert.AreEqual("", UnitKitSummary.Build(null));
        }

        [Test]
        public void MeleeNoTraits_ClassAndArchetypeOnly()
        {
            var u = Unit();
            u.role = DefenderClass.Fighter;
            u.projectile = null;
            Assert.AreEqual("파이터 · 근접형.", UnitKitSummary.Build(u));
        }

        [Test]
        public void Ranged_UsesProjectilePresence()
        {
            var u = Unit();
            u.role = DefenderClass.Ranger;
            u.projectile = Projectile();
            Assert.AreEqual("레인저 · 원거리형.", UnitKitSummary.Build(u));
        }

        [Test]
        public void RoleNone_OmitsClassPrefix()
        {
            var u = Unit();
            u.role = DefenderClass.None;
            u.projectile = null;
            Assert.AreEqual("근접형.", UnitKitSummary.Build(u));
        }

        [Test]
        public void SupportWithHeal_IsHealArchetype()
        {
            var u = Unit();
            u.role = DefenderClass.Support;
            u.targetAllies = true;
            u.outputs = new[] { new AttackOutput { kind = AttackOutputKind.Heal, magnitude = 8f } };
            Assert.AreEqual("서포트 · 아군 치유형.", UnitKitSummary.Build(u));
        }

        [Test]
        public void SupportNoHeal_IsBuffArchetype()
        {
            var u = Unit();
            u.role = DefenderClass.Support;
            u.targetAllies = true;
            u.outputs = new[] { new AttackOutput { kind = AttackOutputKind.ApplyStat, magnitude = 1f } };
            Assert.AreEqual("서포트 · 아군 강화형.", UnitKitSummary.Build(u));
        }

        [Test]
        public void GuardianAggroBoost_ListsTraitsInOrder()
        {
            var u = Unit();
            u.role = DefenderClass.Guardian;
            u.projectile = null;
            u.attackTargetCount = 3;
            u.aggroCapacity = 3;
            u.onPlaceEffect = OnPlaceEffectType.BoostNearbyDefenders;
            Assert.AreEqual(
                "가디언 · 근접형. 최대 3체 동시 타격, 최대 3체 도발 유지, 배치 시 주변 아군 강화.",
                UnitKitSummary.Build(u));
        }

        [Test]
        public void DirectionalVolley_ShotCountPhrase_AndHazard()
        {
            var u = Unit();
            u.role = DefenderClass.Caster;
            u.projectile = Projectile();
            u.directionalAttack = true;
            u.shotCount = 10;
            u.hazardCastEnabled = true;
            Assert.AreEqual(
                "캐스터 · 원거리형. 지정 방향으로 10연발 사격, 지속 해저드 설치.",
                UnitKitSummary.Build(u));
        }

        [Test]
        public void SingleShotDirectional_OmitsCountPhrase()
        {
            var u = Unit();
            u.role = DefenderClass.Ranger;
            u.projectile = Projectile();
            u.directionalAttack = true;
            u.shotCount = 1;
            Assert.AreEqual("레인저 · 원거리형. 지정 방향 사격.", UnitKitSummary.Build(u));
        }

        [Test]
        public void Deterministic_SameInputSameOutput()
        {
            var u = Unit();
            u.role = DefenderClass.Guardian;
            u.aggroCapacity = 2;
            u.onPlaceEffect = OnPlaceEffectType.SlowPulse;
            Assert.AreEqual(UnitKitSummary.Build(u), UnitKitSummary.Build(u));
        }
    }
}
