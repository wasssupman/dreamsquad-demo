using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7d — **보스 규칙.** ⑴ 일반 도약(짱쎈)은 밀집한 곳으로 순간이동하고, 비행 창이 끝나는 틱에
    // 그 자리에 착지 슬램(자리형 · 몸 0)이 떨어진다 — 옛 전투는 슬램을 뷰가 도착한 시각에 브리지가 쐈다.
    // ⑵ 「생존당 1회」는 `fireCap 1` 이고 **궁극기에만** 준다(빈사폭주류 경계 규칙은 다회). ⑶ 보스는 어그로에 안 끌려간다.
    [TestFixture]
    public class BossTests
    {
        private const float SlamDamage = 50f;

        private static BattleMatch LeapBoard(out Unit boss, out int slamDef, float bossHealth = 100f)
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, enemyHealth: bossHealth);
            slamDef = CoreTriggerFixtures.AddBlastProjectile(def);
            var leap = CoreTriggerFixtures.Rule(TriggerKind.HealthThreshold, EffectKind.SelfBlink);
            leap.Fraction = 0.3f;
            leap.Magnitude = 2f;     // 밀집 탐색 반경(칸) — `LeapParams` 가 이름을 붙인다
            leap.TileRange = 2;      // 착지 링 상한
            leap.SlamDamage = SlamDamage;
            leap.SlamTileRange = 1;
            leap.DataIndex = slamDef;
            CoreTriggerFixtures.GiveEnemy(def, 0, leap);
            var m = CoreMatchFixtures.BeginBattle(def);
            // 밀집 셋(오른쪽) · 외톨이 하나(왼쪽 위)
            CoreTriggerFixtures.SpawnDefender(m, new int2(9, 1));
            CoreTriggerFixtures.SpawnDefender(m, new int2(9, 2));
            CoreTriggerFixtures.SpawnDefender(m, new int2(10, 2));
            CoreTriggerFixtures.SpawnDefender(m, new int2(1, 4));
            boss = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            return m;
        }

        private static void Hurt(Unit u, float amount)
            => u.Inbox.Damage.Add(new DamageEntry { Amount = amount, Source = SimEntityId.None });

        [Test]
        public void 증상_보스가_밀집한_곳으로_도약하고_비행이_끝나는_틱에_그_자리에_슬램이_떨어진다()
        {
            var m = LeapBoard(out var boss, out _);
            var ascend = CoreCombatFixtures.Listen(m, CoreEventKind.LeapAscend);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            Hurt(boss, 35f);   // 경계 0.7 을 넘는다
            CoreCombatFixtures.Tick(m, 2);

            Assert.AreEqual(1, ascend.Count, "도약했다");
            Assert.AreEqual(0, ascend[0].Arg, "일반 도약(궁극기 아님)");
            var landing = ascend[0].SiteTarget.Pos;
            Assert.Greater(landing.x, 6.5f, "밀집한 쪽(오른쪽)으로");
            Assert.AreEqual(m.Definition.Movement.BossLeapFlightSeconds, ascend[0].Amount, 1e-5f,
                            "비행 창 길이는 판의 저작 — 뷰가 사건으로 받는다");
            Assert.AreEqual(landing.x, boss.Position.x, 1e-4f, "순간이동은 이미 끝났다(뷰만 아치로 난다)");
            Assert.IsEmpty(spawned, "슬램은 아직 — 뷰가 도착하기 전에 터지지 않는다");

            int flightTicks = MatchClock.TicksOf(m.Definition.Movement.BossLeapFlightSeconds, BattleMatch.Dt);
            CoreCombatFixtures.Tick(m, flightTicks + 1);

            Assert.AreEqual(1, spawned.Count, "창 끝에 한 발");
            Assert.AreEqual(0f, spawned[0].SiteFired.OriginBody, 1e-6f, "자리에 떨어지는 것 — 몸 0(제약 13)");
            Assert.AreEqual(landing.x, spawned[0].SiteTarget.Pos.x, 1e-4f, "착지 자리에");
            Assert.AreEqual(SlamDamage, spawned[0].Amount, 1e-4f);
        }

        [Test]
        public void 비행_창_동안_보스는_공격도_이동도_못_하지만_맞는다()
        {
            var m = LeapBoard(out var boss, out _);
            Hurt(boss, 35f);
            CoreCombatFixtures.Tick(m, 3);

            Assert.IsTrue(boss.ActionLocked, "공격 불가(옛 `LeapFlight`)");
            Assert.IsTrue(boss.MovementLocked, "자기주도 이동 불가");
            Assert.IsTrue(boss.IsTargetable(), "⚠ 맞는다 — 무적은 궁극기 전용");
            CoreCombatFixtures.Tick(m, 60);
            Assert.IsFalse(boss.ActionLocked, "창이 끝나면 풀린다");
        }

        [Test]
        public void 비행_중에_죽으면_슬램_없이_끝난다()
        {
            var m = LeapBoard(out var boss, out _);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            Hurt(boss, 35f);
            CoreCombatFixtures.Tick(m, 3);
            Hurt(boss, 1e6f);
            CoreCombatFixtures.Tick(m, 80);
            Assert.IsEmpty(spawned, "옛 `abandoned` — 끊긴 비행은 착지 처리를 안 한다");
        }

        // ── fireCap ───────────────────────────────────────────────────────────

        private static int FireCountOnThresholds(EffectKind payload, int fireCap)
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, enemyHealth: 100f);
            var probe = new CoreTriggerFixtures.ProbeSkill();
            var rule = CoreTriggerFixtures.Probe(TriggerKind.HealthThreshold, probe);
            rule.Payload = payload;
            rule.Fraction = 0.2f;   // 경계 0.8 · 0.6 · 0.4 · 0.2
            rule.FireCap = fireCap;
            CoreTriggerFixtures.GiveEnemy(def, 0, rule);
            var m = CoreMatchFixtures.BeginBattle(def);
            CoreTriggerFixtures.SpawnDefender(m, new int2(9, 2));
            var boss = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            for (int k = 0; k < 4; k++) { Hurt(boss, 21f); CoreCombatFixtures.Tick(m, 2); }
            return probe.Count;
        }

        [Test]
        public void 궁극기는_경계를_여러_번_넘어도_생존당_한_번이다_fireCap_1()
            => Assert.AreEqual(1, FireCountOnThresholds(EffectKind.UltimateLeap, fireCap: 1));

        [Test]
        public void 빈사폭주류_경계_규칙은_발동_상한이_없어_경계마다_난다()
            => Assert.AreEqual(4, FireCountOnThresholds(EffectKind.SelfBuffLethal, fireCap: 0));

        // ── 어그로 면역 ───────────────────────────────────────────────────────

        private static Unit AggroTarget(bool boss)
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, aggroCapacity: 2, enemyRange: 1f);
            def.Enemies[0].Attack.BossImmune = boss;
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            var guardian = CoreTriggerFixtures.SpawnDefender(m, new int2(6, 2));
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(3, 2));
            m.World.AggroRequests.Add(AggroRequest.Taunted(enemy.Id, guardian.Id, 5f));
            CoreCombatFixtures.Tick(m, 2);
            return enemy;
        }

        [Test]
        public void 보스는_도발에_안_끌려간다_일반_적은_끌려간다()
        {
            var normal = AggroTarget(boss: false);
            Assert.IsTrue(normal.Aggro != null && !normal.Aggro.Target.IsNone, "대조군 — 일반 적은 끌려간다");
            var boss = AggroTarget(boss: true);
            Assert.IsTrue(boss.Aggro == null || boss.Aggro.Target.IsNone, "보스 어그로 면역(옛 `AggroStateSystem.cs:175`)");
        }
    }
}
