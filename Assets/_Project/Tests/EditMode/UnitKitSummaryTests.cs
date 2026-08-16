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
            u.onPlaceEffect = OnPlaceEffectType.SlowPulse;
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

        // on-place-skill-rework unit 6 — **전수 순회로 «조용히 빈 문안» 을 막는다.**
        //
        // `OnPlaceClause` 의 `default: return ""` 는 신규 enum 멤버가 아무 말 없이 설명을
        // 비우게 한다(그 파일 주석이 스스로 경고한다). 규칙 경로(`OnPlaceRuleClause`)도
        // 같은 형태를 갖게 됐으므로, 두 어휘를 함께 순회해 배선 누락을 컴파일이 아니라
        // 테스트로 잡는다 — `DcApplicabilityTests` 의 전수 검사와 같은 안전망이다.
        [Test]
        public void EveryOnPlaceEffectKind_HasAClause()
        {
            var missing = new System.Collections.Generic.List<string>();
            foreach (OnPlaceEffectType kind in System.Enum.GetValues(typeof(OnPlaceEffectType)))
            {
                if (kind == OnPlaceEffectType.None) continue;
                var probe = ScriptableObject.CreateInstance<DefenderUnitData>();
                probe.onPlaceEffect = kind;
                string s = UnitKitSummary.Build(probe);
                if (!s.Contains("배치")) missing.Add(kind.ToString());
                Object.DestroyImmediate(probe);
            }
            CollectionAssert.IsEmpty(missing,
                "배치 문안이 없는 OnPlaceEffectType 이 있다 — 신규 멤버를 추가하고 " +
                "OnPlaceClause 를 안 늘리면 설명이 조용히 빈다: " + string.Join(", ", missing));
        }

        // 규칙 경로의 같은 안전망(`RuleDrivenOnPlaceUnits_HaveAClause`)은 실제
        // DefenderCatalog 를 읽으므로 EditModeAssets/UnitKitCatalogTests.cs 에 있다
        // (test-suite-fast-lane unit 0 — 코어 lane 은 실에셋을 로드하지 않는다).
    }
}
