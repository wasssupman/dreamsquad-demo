using System;
using NUnit.Framework;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attack-decoupling unit 0 — 지원 행렬의 회귀 핀.
    // total 어서션이 핵심이다: 새 DcPayloadKind/DcAttackModKind 를 추가하고
    // 행렬을 잊으면 여기서 먼저 실패한다(런타임에 조용히 무효가 되는 대신).
    public class DcApplicabilityTests
    {
        private static DcHostProfile Host(DcHostArchetype archetype,
            DcProjectileRoute route = DcProjectileRoute.Homing,
            bool targetsEnemies = true, bool hasDamageOutput = true,
            bool lethal = false, bool cocoon = false) =>
            new DcHostProfile
            {
                archetype = archetype,
                route = route,
                targetsEnemies = targetsEnemies,
                hasDamageOutput = hasDamageOutput,
                hasLethalTimer = lethal,
                hasDreamCocoon = cocoon,
            };

        // 대표 host 들 — 실제 카탈로그 유닛에 대응.
        private static DcHostProfile Archer() => Host(DcHostArchetype.Standard);
        private static DcHostProfile Guardian() => Host(DcHostArchetype.Standard, DcProjectileRoute.None);
        private static DcHostProfile MachineGunner() => Host(DcHostArchetype.FacingVolley, DcProjectileRoute.Directional);
        private static DcHostProfile Artillery() => Host(DcHostArchetype.Standard, DcProjectileRoute.Ballistic);
        private static DcHostProfile BombMan() => Host(DcHostArchetype.BombThrow, DcProjectileRoute.Grenade);
        private static DcHostProfile Caster() => Host(DcHostArchetype.HazardCast, DcProjectileRoute.None, hasDamageOutput: false);
        private static DcHostProfile Healer() => Host(DcHostArchetype.Standard, DcProjectileRoute.None,
            targetsEnemies: false, hasDamageOutput: false);

        // ── total: 미분류 조합 0 ────────────────────────────────────────────
        [Test]
        public void EvaluateMechanic_IsTotalOverAllKindAndArchetypePairs()
        {
            foreach (DcPayloadKind payload in Enum.GetValues(typeof(DcPayloadKind)))
            foreach (DcTriggerKind trigger in Enum.GetValues(typeof(DcTriggerKind)))
            foreach (DcHostArchetype archetype in Enum.GetValues(typeof(DcHostArchetype)))
            {
                var reason = DcApplicability.EvaluateMechanic(payload, trigger, Host(archetype));
                Assert.IsTrue(Enum.IsDefined(typeof(DcRejectReason), reason),
                    $"미분류 조합: {payload} × {trigger} × {archetype}");
            }
        }

        [Test]
        public void EvaluateAttackMod_IsTotalOverAllKindAndRoutePairs()
        {
            foreach (DcAttackModKind kind in Enum.GetValues(typeof(DcAttackModKind)))
            foreach (DcProjectileRoute route in Enum.GetValues(typeof(DcProjectileRoute)))
            {
                var reason = DcApplicability.EvaluateAttackMod(kind, Host(DcHostArchetype.Standard, route));
                Assert.IsTrue(Enum.IsDefined(typeof(DcRejectReason), reason),
                    $"미분류 조합: {kind} × {route}");
            }
        }

        // ── 현행 동작 미러 ─────────────────────────────────────────────────
        [Test]
        public void PokeNeedle_RejectsAllyTargetingHost()
        {
            Assert.AreEqual(DcRejectReason.NeedsEnemyTargeting,
                DcApplicability.EvaluateMechanic(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN, Healer()),
                "힐러는 아군을 겨눠 니들이 아군을 때린다");
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN, Archer()));
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN, Guardian()),
                "근접도 5회째에 니들을 쏜다");
        }

        [Test]
        public void BouncyBead_NeedsHomingRoute()
        {
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateAttackMod(DcAttackModKind.ProjectileBounce, Archer()));
            Assert.AreEqual(DcRejectReason.NeedsHomingRoute,
                DcApplicability.EvaluateAttackMod(DcAttackModKind.ProjectileBounce, MachineGunner()),
                "방향탄 개통은 별도 spec — 지금은 거절이 정답");
            Assert.AreEqual(DcRejectReason.NeedsHomingRoute,
                DcApplicability.EvaluateAttackMod(DcAttackModKind.ProjectileBounce, Artillery()));
            Assert.AreEqual(DcRejectReason.NeedsHomingRoute,
                DcApplicability.EvaluateAttackMod(DcAttackModKind.ProjectileBounce, BombMan()),
                "폭탄맨은 ProjectileRef 를 갖지만 GrenadeToCell 로 쏜다");
        }

        [Test]
        public void HeavyStrike_NeedsDamageOutput()
        {
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(DcPayloadKind.HeavyStrike, DcTriggerKind.AttackN, Archer()));
            Assert.AreEqual(DcRejectReason.NeedsDamageOutput,
                DcApplicability.EvaluateMechanic(DcPayloadKind.HeavyStrike, DcTriggerKind.AttackN,
                    Host(DcHostArchetype.Standard, hasDamageOutput: false)));
        }

        [Test]
        public void DuplicateState_Rejects()
        {
            Assert.AreEqual(DcRejectReason.DuplicateState,
                DcApplicability.EvaluateMechanic(DcPayloadKind.SelfBuffLethal, DcTriggerKind.None,
                    Host(DcHostArchetype.Standard, lethal: true)));
            Assert.AreEqual(DcRejectReason.DuplicateState,
                DcApplicability.EvaluateMechanic(DcPayloadKind.DreamCocoon, DcTriggerKind.None,
                    Host(DcHostArchetype.Standard, cocoon: true)));
        }

        // ── 잠금 상태 고정 (unit 3·4 가 이 기대값을 뒤집는다) ──────────────
        [Test]
        public void AttackN_HasNoEventPoint_OnBombThrowAndHazardCast()
        {
            foreach (var host in new[] { BombMan(), Caster() })
            {
                Assert.AreEqual(DcRejectReason.NoEventPoint,
                    DcApplicability.EvaluateMechanic(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN, host),
                    "unit 3·4 가 사건 지점을 만들면 이 기대값이 None 으로 바뀐다");
                Assert.AreEqual(DcRejectReason.NoEventPoint,
                    DcApplicability.EvaluateMechanic(DcPayloadKind.ApplyCcToTarget, DcTriggerKind.AttackN, host));
            }
        }

        [Test]
        public void ApplyCcToTarget_NeedsTargetContext_EvenAfterEventPointOpens()
        {
            // 사건 지점이 있어도(Standard/FacingVolley) 대상 문맥이 필요하다는 축은 별개다.
            // 폭탄맨/캐스터가 unit 3·4 로 열려도 이 페이로드는 영구 거절 대상(계약 9).
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(DcPayloadKind.ApplyCcToTarget, DcTriggerKind.AttackN, MachineGunner()));
            Assert.AreEqual(DcRejectReason.NeedsEnemyTargeting,
                DcApplicability.EvaluateMechanic(DcPayloadKind.ApplyStackToTarget, DcTriggerKind.AttackN, Healer()));
        }

        // ── 공격 경로와 무관한 트리거는 아키타입을 가리지 않는다 ───────────
        [Test]
        public void NonAttackTriggers_HaveEventPointOnEveryArchetype()
        {
            var nonAttack = new[]
            {
                DcTriggerKind.OnDamagedN, DcTriggerKind.OnDeath, DcTriggerKind.PeriodicTimer,
                DcTriggerKind.HealthThreshold, DcTriggerKind.OnKill, DcTriggerKind.OnShieldBreak,
            };
            foreach (DcHostArchetype archetype in Enum.GetValues(typeof(DcHostArchetype)))
            foreach (var trigger in nonAttack)
                Assert.IsTrue(DcApplicability.HasEventPoint(trigger, archetype),
                    $"{trigger} 는 공격 경로와 무관한 사건인데 {archetype} 에서 막혔다");
        }

        [Test]
        public void SelfPayloads_ApplyOnEveryArchetype()
        {
            var selfKinds = new[]
            {
                DcPayloadKind.SelfTileAoe, DcPayloadKind.SelfStatBuff,
                DcPayloadKind.SelfBlink, DcPayloadKind.AreaSleep,
                DcPayloadKind.PlacementAura, DcPayloadKind.AllyMoveSpeedAura,
            };
            foreach (DcHostArchetype archetype in Enum.GetValues(typeof(DcHostArchetype)))
            foreach (var kind in selfKinds)
                Assert.AreEqual(DcRejectReason.None,
                    DcApplicability.EvaluateMechanic(kind, DcTriggerKind.OnDeath, Host(archetype)),
                    $"{kind} 는 host 공격 모델과 무관해야 한다 ({archetype})");
        }
    }
}
