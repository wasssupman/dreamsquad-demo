using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7a — **증상 단언**(규칙이 화면에서 보이는 형태로) + 캐논 1:1 융단폭격.
    //
    // 「내 함수가 옳은 값을 내나」가 아니라 **플레이어가 보는 문장**을 단언한다(CLAUDE.md 버그 절차 2).
    [TestFixture]
    public class TriggerSymptomTests
    {
        [Test]
        public void 적을_죽인_자리에서_시체_폭발이_터지고_그_시체의_몸만큼_넓다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 10f, enemyHealth: 10f);
            def.Enemies[0].BodyRadius = 1f;                 // 큰 시체
            int blast = AddBlastProjectile(def);
            var rule = Rule(TriggerKind.OnKill, TriggerPayload.SelfTileAoe);
            rule.Magnitude = 5f;
            rule.TileRange = 1;
            rule.DataIndex = blast;
            GiveUnit(def, 0, rule);

            var m = CoreMatchFixtures.BeginBattle(def);
            var d = SpawnDefender(m, new int2(2, 2));
            var victim = SpawnEnemy(m, new int2(4, 2));
            var bystander = SpawnEnemy(m, new int2(7, 2));   // 시체에서 3칸 — 칸 반폭(0.5)이면 2.5 라 못 닿는다
            float3 deathSite = victim.Position;
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            var hits = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);

            CoreCombatFixtures.Tick(m, 10);

            var boom = spawned.Find(e => e.B == d.Id);
            Assert.AreEqual(d.Id, boom.B, "폭발이 났다(킬 귀속 = 킬러)");
            Assert.AreEqual(1f, boom.SiteFired.OriginBody, 1e-5f, "원점 항 = **시체의 몸**(킬러의 몸이 아니다)");
            Assert.AreEqual(deathSite.x, boom.SiteFired.Pos.x, 1e-4f, "죽인 자리에서");
            Assert.IsTrue(hits.Exists(e => e.B == bystander.Id && System.Math.Abs(e.Amount - 5f) < 1e-4f),
                "3칸 떨어진 적이 맞았다 = 반경 1 + 시체 몸 1 + 대상 몸 1");
        }

        [Test]
        public void 배치하면_배치_스킬이_다음_틱의_주기_seam_에서_곧바로_난다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            var rule = Rule(TriggerKind.OnPlace, TriggerPayload.SelfStatBuff);
            rule.Magnitude = 1.5f;
            rule.StatKind = (int)SkillStatKind.DamageMul;
            GiveUnit(def, 0, rule);

            var m = CoreMatchFixtures.BeginBattle(def);
            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);
            var activated = CoreCombatFixtures.Listen(m, CoreEventKind.DefenderActivated);
            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(0, new int2(3, 1))).Reason);
            Assert.AreEqual(1, activated.Count, "배치 페이즈 없음 = 놓는 순간 활성화");
            var u = m.World.Find(CoreMatchFixtures.PlacedDefender(m));
            Assert.AreEqual(1f, u.Modifiers.Effective.DamageMul, 1e-5f, "아직(사건은 틱 끝에 배달된다)");

            int tick = m.Clock.Tick;
            m.Tick();
            Assert.AreEqual(1, fired.Count, "활성화 뒤 **첫 틱**에 — 주기(초)를 기다리지 않는다");
            Assert.AreEqual(tick, fired[0].Tick);
            Assert.AreEqual(1.5f, u.Modifiers.Effective.DamageMul, 1e-5f, "그 틱의 전투 앞에 걸렸다(주기 seam = 장 준비 끝)");
            CoreCombatFixtures.Tick(m, 120);
            Assert.AreEqual(1, fired.Count, "배치 엣지는 1회");
        }

        [Test]
        public void 부착한_규칙이_붙은_유닛이_죽으면_작별_선물이_그_자리에서_터진다_시전자가_없어도()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, enemyHealth: 100f);
            int blast = AddBlastProjectile(def);
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            var d = SpawnDefender(m, new int2(5, 2));
            var e = SpawnEnemy(m, new int2(6, 2));
            var gift = Rule(TriggerKind.OnDeath, TriggerPayload.SelfTileAoe);
            gift.Magnitude = 7f;
            gift.TileRange = 1;
            gift.DataIndex = blast;
            m.Bindings.Attach(d, in gift, -1, 0);          // 부착(카드의 자리 — 7b 가 이 입구를 쓴다)
            float3 site = d.Position;
            float body = d.HitRadius;

            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            var hits = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);
            m.Intents.Apply(new SimIntent { Kind = SimIntentKind.DealDamage, Target = CoreSkillContext.ToSkill(d.Id),
                                            Source = CoreSkillContext.ToSkill(e.Id), Amount = 99999f });
            CoreCombatFixtures.Tick(m, 4);

            Assert.IsNull(m.World.Find(d.Id), "죽었고 사라졌다");
            Assert.AreEqual(1, spawned.Count);
            Assert.IsTrue(spawned[0].B.IsNone, "시전자가 없다 — 귀속할 유닛이 없다");
            Assert.AreEqual(site.x, spawned[0].SiteFired.Pos.x, 1e-4f, "쓰러진 그 자리에서");
            Assert.AreEqual(body, spawned[0].SiteFired.OriginBody, 1e-5f, "자기 몸만큼(발화 시점 스냅샷 — 0 으로 새지 않는다)");
            Assert.AreEqual(Wassup.Battle.Units.Faction.DefenderUnit, spawned[0].Faction, "진영도 값이다 — 적을 때린다");
            Assert.IsTrue(hits.Exists(h => h.B == e.Id && System.Math.Abs(h.Amount - 7f) < 1e-4f), "옆의 적이 맞았다");
        }

        private static (BattleMatch, Unit) Cannon(bool fanOut, int enemiesInScope)
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            var barrel = ProjectileDef.Default();
            barrel.Id = "fixture_missile";
            barrel.Movement = (int)MovementKind.SkyFallOnEntity;
            barrel.Payload = (int)PayloadKind.SingleSplash;
            def.Projectiles = new[] { barrel };
            def.Patterns = new[]
            {
                new PatternDef
                {
                    Id = "fixture_cannon_strike", BarrelProjectileDefIndex = 0, Damage = 200f,
                    Selection = (int)Wassup.BattleCore.Combat.Emission.PatternSelectionRule.RoundRobin,
                    Shots = new[] { new PatternShotDef { DirectionT = 0.5f } },
                    ReselectPerShot = true, TelegraphSec = 0.4f, ScopeTileRange = 3,
                    FanOutToAllCandidates = fanOut, FanOutStaggerSec = 0.08f,
                },
            };
            var rule = Rule(TriggerKind.OnPlace, TriggerPayload.EmitProjectilePattern);
            rule.PatternDefIndex = 0;
            GiveUnit(def, 0, rule);
            var m = CoreMatchFixtures.BeginBattle(def);
            var cells = new[] { new int2(4, 1), new int2(5, 1), new int2(4, 2), new int2(5, 3), new int2(6, 2) };
            for (int i = 0; i < enemiesInScope; i++) m.Apply(Command.DebugSpawnEnemy(0, cells[i]));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(11, 4)));   // 스코프 밖
            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(0, new int2(3, 1))).Reason);
            return (m, m.World.Find(CoreMatchFixtures.PlacedDefender(m)));
        }

        [Test]
        public void 캐논_융단폭격은_미사일_수가_반경_안_적_수다()
        {
            foreach (int n in new[] { 1, 3, 5 })
            {
                var (m, cannon) = Cannon(fanOut: true, enemiesInScope: n);
                var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
                CoreCombatFixtures.Tick(m, 2);
                int missiles = spawned.FindAll(e => e.B == cannon.Id).Count;
                Assert.AreEqual(n, missiles, $"1:1 — 적 {n}기면 미사일 {n}발(안 배선하면 조용히 한 발이 된다)");
            }
        }

        [Test]
        public void 손잡이가_꺼진_명세는_한_발이다()
        {
            var (m, cannon) = Cannon(fanOut: false, enemiesInScope: 4);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(1, spawned.FindAll(e => e.B == cannon.Id).Count);
        }

        [Test]
        public void 융단폭격의_시차는_칸마다_예고_시간에_얹힌다()
        {
            var (m, cannon) = Cannon(fanOut: true, enemiesInScope: 3);
            CoreCombatFixtures.Tick(m, 2);   // 틱 1 = 주기 seam 발동 → 발사 요청, 틱 2 = 탄이 선다
            var mine = new List<Projectile>();
            foreach (var p in m.World.Projectiles) if (p.Owner == cannon.Id) mine.Add(p);
            Assert.AreEqual(3, mine.Count);
            var times = mine.ConvertAll(p => p.FlightTime);
            times.Sort();
            Assert.AreEqual(0.4f, times[0], 1e-4f, "첫 칸 = 저작 예고");
            Assert.AreEqual(0.48f, times[1], 1e-4f, "다음 칸 = + 시차");
        }
    }
}
