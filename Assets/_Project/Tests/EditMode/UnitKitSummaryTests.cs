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
            Assert.AreEqual(
                "가디언 · 근접형. 최대 3체 동시 타격, 최대 3체 도발 유지.",
                UnitKitSummary.Build(u));
        }

        // defender-ability-assets unit 2 — trait 소스가 능력 서브에셋으로 이동. 문구 불변.
        private static DirectionalVolleyAbility Volley(int shots, bool requiresFacing = true)
        {
            var a = ScriptableObject.CreateInstance<DirectionalVolleyAbility>();
            a.pattern = ScriptableObject.CreateInstance<ProjectilePatternData>();
            a.pattern.shots = new ProjectileShotStep[shots];
            a.requiresFacing = requiresFacing;
            return a;
        }

        [Test]
        public void DirectionalVolley_ShotCountPhrase_AndHazard()
        {
            var u = Unit();
            u.role = DefenderClass.Caster;
            u.projectile = Projectile();
            u.abilities.Add(Volley(10));
            u.abilities.Add(ScriptableObject.CreateInstance<HazardCastAbility>());
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
            u.abilities.Add(Volley(1));
            Assert.AreEqual("레인저 · 원거리형. 지정 방향 사격.", UnitKitSummary.Build(u));
        }

        [Test]
        public void AutoTargetVolley_UsesTargetDirectionPhrase()
        {
            var u = Unit();
            u.role = DefenderClass.Ranger;
            u.projectile = Projectile();
            u.abilities.Add(Volley(10, requiresFacing: false));
            Assert.AreEqual(
                "레인저 · 원거리형. 대상 방향으로 10연발 사격.",
                UnitKitSummary.Build(u));
        }

        [Test]
        public void Deterministic_SameInputSameOutput()
        {
            var u = Unit();
            u.role = DefenderClass.Guardian;
            u.aggroCapacity = 2;
            Assert.AreEqual(UnitKitSummary.Build(u), UnitKitSummary.Build(u));
        }

        // unit 6 — Describe: authored desc wins, empty falls back to Build.
        [Test]
        public void Describe_Null_ReturnsEmpty()
        {
            Assert.AreEqual("", UnitKitSummary.Describe(null));
        }

        [Test]
        public void Describe_AuthoredDesc_ReturnsDesc()
        {
            var u = Unit();
            u.role = DefenderClass.Ranger;
            u.desc = "직접 쓴 설명";
            Assert.AreEqual("직접 쓴 설명", UnitKitSummary.Describe(u));
        }

        [Test]
        public void Describe_EmptyDesc_FallsBackToBuild()
        {
            var u = Unit();
            u.role = DefenderClass.Fighter;
            u.projectile = null;
            u.desc = "";
            Assert.AreEqual(UnitKitSummary.Build(u), UnitKitSummary.Describe(u));
            Assert.AreEqual("파이터 · 근접형.", UnitKitSummary.Describe(u));
        }

        // skill-layer-migration unit 2g — **레거시 어휘 전수 순회가 은퇴했다.**
        // `OnPlaceEffectType` 자체가 철거돼 순회할 어휘가 없다. 같은 안전망은
        // 규칙 경로 쪽에 그대로 살아 있다(아래 참조) — 어휘가 하나로 줄었을 뿐
        // 「조용히 빈 문안」을 막는 규율은 유지된다.

        // 규칙 경로의 같은 안전망(`RuleDrivenOnPlaceUnits_HaveAClause`)은 실제
        // DefenderCatalog 를 읽으므로 EditModeAssets/UnitKitCatalogTests.cs 에 있다
        // (test-suite-fast-lane unit 0 — 코어 lane 은 실에셋을 로드하지 않는다).
    }
}
