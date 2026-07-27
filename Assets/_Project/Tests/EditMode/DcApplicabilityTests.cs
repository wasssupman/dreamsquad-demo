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

        // 판정은 mechanic 전체를 받는다(게이트 축 + 반경 축). 기본 반경 4 = 폴백 있음.
        private static DcMechanic Mech(DcPayloadKind kind, DcTriggerKind trigger,
            int tileRange = 4, DcGateKind gate = DcGateKind.None,
            DcGateSubject subject = DcGateSubject.Self) =>
            new DcMechanic
            {
                trigger = new DcTriggerSpec { kind = trigger, gate = gate, gateSubject = subject },
                payload = new DcPayloadSpec { kind = kind, tileRange = tileRange },
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
                var reason = DcApplicability.EvaluateMechanic(Mech(payload, trigger), Host(archetype));
                // Enum.IsDefined 는 항상 참이라 아무것도 못 잡았다 — 배선 누락 전용
                // sentinel 로 바꿔야 "새 kind 를 추가하고 잊으면 실패한다"가 성립한다.
                Assert.AreNotEqual(DcRejectReason.Unclassified, reason,
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
                Assert.AreNotEqual(DcRejectReason.Unclassified, reason,
                    $"미분류 조합: {kind} × {route}");
            }
        }

        // ── 현행 동작 미러 ─────────────────────────────────────────────────
        [Test]
        public void PokeNeedle_RejectsAllyTargetingHost()
        {
            Assert.AreEqual(DcRejectReason.NeedsEnemyTargeting,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN), Healer()),
                "힐러는 아군을 겨눠 니들이 아군을 때린다");
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN), Archer()));
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN), Guardian()),
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
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.HeavyStrike, DcTriggerKind.AttackN), Archer()));
            Assert.AreEqual(DcRejectReason.NeedsDamageOutput,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.HeavyStrike, DcTriggerKind.AttackN),
                    Host(DcHostArchetype.Standard, hasDamageOutput: false)));
        }

        [Test]
        public void DuplicateState_Rejects()
        {
            Assert.AreEqual(DcRejectReason.DuplicateState,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.SelfBuffLethal, DcTriggerKind.None),
                    Host(DcHostArchetype.Standard, lethal: true)));
            Assert.AreEqual(DcRejectReason.DuplicateState,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.DreamCocoon, DcTriggerKind.None),
                    Host(DcHostArchetype.Standard, cocoon: true)));
        }

        // ── 사건 지점: unit 3·4 로 전 아키타입 개통 ────────────────────────
        [Test]
        public void AttackN_HasEventPointOnEveryArchetype()
        {
            foreach (DcHostArchetype archetype in Enum.GetValues(typeof(DcHostArchetype)))
                Assert.AreEqual(DcRejectReason.None,
                    DcApplicability.EvaluateMechanic(
                        Mech(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN), Host(archetype)),
                    $"{archetype} — 각 아키타입은 자기 '공격 성립' 지점에서 카운트한다");
        }

        // ── 게이트 축: 평가할 수단이 없는 host 에서는 부착을 거절한다 ──────
        // 폭탄맨·캐스터의 사건 지점은 RESOLVE 와 달리 bestTarget 을 들고 있지 않아
        // EventTarget 게이트를 평가할 수 없다. 거절하지 않으면 게이트가 실패가 아니라
        // **없는 것처럼** 무시되고 조건 없이 발동한다(사양 초과).
        [Test]
        public void EventTargetGate_RejectedWhereHostGivesNoTarget()
        {
            foreach (var host in new[] { BombMan(), Caster() })
                Assert.AreEqual(DcRejectReason.NeedsTargetContext,
                    DcApplicability.EvaluateMechanic(
                        Mech(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN,
                             gate: DcGateKind.HpBelow, subject: DcGateSubject.EventTarget), host),
                    "게이트를 평가할 수 없는 host — 무시하고 발동하느니 거절");

            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(
                    Mech(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN,
                         gate: DcGateKind.HpBelow, subject: DcGateSubject.EventTarget), Archer()),
                "RESOLVE host 는 bestTarget 을 주므로 게이트 평가가 가능하다");

            // Self 주어는 host 가 대상을 안 줘도 평가할 수 있다(주어가 host 자신).
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(
                    Mech(DcPayloadKind.SelfStatBuff, DcTriggerKind.OnDamagedN,
                         gate: DcGateKind.HpBelow, subject: DcGateSubject.Self), BombMan()));
        }

        // 강공은 RESOLVE 의 출력 데미지를 배율하므로 RESOLVE host 전용이다.
        // 예전엔 hasDamageOutput 만 봐서, 폭탄맨에 output 이 생기면 부착됐다가
        // 훅에서 조용히 소모되는 구멍이 열렸다(에셋 값의 우연에 의존).
        [Test]
        public void HeavyStrike_RequiresResolveHost()
        {
            Assert.AreEqual(DcRejectReason.NeedsTargetContext,
                DcApplicability.EvaluateMechanic(
                    Mech(DcPayloadKind.HeavyStrike, DcTriggerKind.AttackN),
                    Host(DcHostArchetype.BombThrow, DcProjectileRoute.Grenade)));
        }

        // host 가 대상을 안 주는 부류(폭탄맨·캐스터)는 폴백 반경이 유일한 수단이다.
        [Test]
        public void PokeNeedle_WithoutHostTarget_NeedsFallbackRange()
        {
            foreach (var host in new[] { BombMan(), Caster() })
                Assert.AreEqual(DcRejectReason.NeedsFallbackRange,
                    DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN, tileRange: 0), host),
                    "반경 0 이면 니들이 영영 안 나간다 — 붙이면 안 된다");
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.ProjectileToTarget, DcTriggerKind.AttackN, tileRange: 0), Archer()),
                "host 가 대상을 주는 부류는 반경 0 이어도 정상(B안)");
        }

        // 사건 지점이 열려도 "그 공격의 대상"이 필요한 페이로드는 여전히 거절(계약 9).
        [Test]
        public void ApplyCcToTarget_StillRejectedOnBombThrow()
        {
            Assert.AreEqual(DcRejectReason.NeedsTargetContext,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.ApplyCcToTarget, DcTriggerKind.AttackN), BombMan()));
        }

        [Test]
        public void ApplyCcToTarget_NeedsTargetContext_EvenAfterEventPointOpens()
        {
            // 사건 지점이 있어도(Standard/FacingVolley) 대상 문맥이 필요하다는 축은 별개다.
            // 폭탄맨/캐스터가 unit 3·4 로 열려도 이 페이로드는 영구 거절 대상(계약 9).
            Assert.AreEqual(DcRejectReason.None,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.ApplyCcToTarget, DcTriggerKind.AttackN), MachineGunner()));
            Assert.AreEqual(DcRejectReason.NeedsEnemyTargeting,
                DcApplicability.EvaluateMechanic(Mech(DcPayloadKind.ApplyStackToTarget, DcTriggerKind.AttackN), Healer()));
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
            foreach (var trigger in nonAttack)
                Assert.IsTrue(DcApplicability.IsTriggerWired(trigger), $"{trigger} 가 배선 밖으로 빠졌다");
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
                    DcApplicability.EvaluateMechanic(Mech(kind, DcTriggerKind.OnDeath), Host(archetype)),
                    $"{kind} 는 host 공격 모델과 무관해야 한다 ({archetype})");
        }
    }
}
