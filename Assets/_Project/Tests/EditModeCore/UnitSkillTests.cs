using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7a — **유닛이 저작으로 든 규칙**이 실제 concrete 로 끝까지 도는가
    // (적 악몽 · 배치 스킬 · 실드 · 브레스 · 감속 오라 · 출혈). 카드는 7b.
    [TestFixture]
    public class UnitSkillTests
    {
        [Test]
        public void 드래곤_브레스는_N타마다_콘_안의_적만_태우고_발동_사건이_콘을_싣는다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, enemyDamage: 1f, enemyRange: 3f, enemyHealth: 9999f);
            var breath = Rule(TriggerKind.AttackN, EffectKind.AreaBreath);
            breath.Rule.Period = 1; breath.Effect.Magnitude = 50f; breath.Effect.TileRange = 3;
            breath.Effect.ConeHalfAngleDeg = 50f;
            breath.Effect.ConeSinHalf = math.sin(math.radians(50f)); breath.Effect.ConeCosHalf = math.cos(math.radians(50f));
            GiveEnemy(def, 0, breath);
            var m = CoreMatchFixtures.BeginBattle(def);
            var front = SpawnDefender(m, new int2(4, 2));
            var behind = SpawnDefender(m, new int2(8, 2));   // 등 뒤
            var dragon = SpawnEnemy(m, new int2(6, 2));
            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);
            var hits = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);
            CoreCombatFixtures.Tick(m, 3);
            Assert.IsNotEmpty(fired);
            var f = fired[0];
            Assert.AreEqual(AttackShapeBaked.SectorKind, f.AttackShape.kind, "브레스의 그림 = 이 스킬의 콘(6c 후속 3)");
            Assert.AreEqual(3f, f.AttackRange);
            Assert.AreEqual(math.cos(math.radians(50f)), f.AttackShape.cosHalf, 1e-4f);
            int frontBurn = hits.FindAll(h => h.B == front.Id && h.Amount >= 50f).Count;
            int backBurn = hits.FindAll(h => h.B == behind.Id && h.Amount >= 50f).Count;
            Assert.Greater(frontBurn, 0);
            Assert.AreEqual(0, backBurn, "등 뒤는 콘 밖");
            Assert.AreEqual(dragon.Id, hits.Find(h => h.B == front.Id && h.Amount >= 50f).A, "킬 귀속 = 드래곤");
        }

        [Test]
        public void 마메모_자장가는_주기마다_가까운_상대를_재운다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f);
            var lullaby = Rule(TriggerKind.PeriodicTimer, EffectKind.AreaSleep);
            lullaby.Rule.PeriodSeconds = 0.5f; lullaby.Effect.Magnitude = 1f; lullaby.Effect.TileRange = 4; lullaby.Effect.Duration = 2f;
            GiveEnemy(def, 0, lullaby);
            var m = CoreMatchFixtures.BeginBattle(def);
            var near = SpawnDefender(m, new int2(4, 2));
            var far = SpawnDefender(m, new int2(1, 2));
            SpawnEnemy(m, new int2(6, 2));
            CoreCombatFixtures.Tick(m, 32);
            Assert.IsTrue(near.Cc.IsActive(CcSlotKind.Sleep), "가까운 1기");
            Assert.IsFalse(far.Cc.IsActive(CcSlotKind.Sleep), "인원 1");
        }

        [Test]
        public void 실드셔틀_배치_보호막은_주변_아군에게_건다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            var onPlace = Rule(TriggerKind.OnPlace, EffectKind.GrantShield);
            onPlace.Effect.Magnitude = 40f; onPlace.Effect.TileRange = 2;
            GiveUnit(def, 0, onPlace);
            var m = CoreMatchFixtures.BeginBattle(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(5, 1)));
            var ally = m.World.Units[m.World.Units.Count - 1];
            var granted = CoreCombatFixtures.Listen(m, CoreEventKind.ShieldGranted);
            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(0, new int2(4, 1))).Reason);
            CoreCombatFixtures.Tick(m, 3);
            Assert.IsTrue(granted.Exists(g => g.B == ally.Id), "배치 순간 주변 아군에 실드");
            Assert.AreEqual(40f, Wassup.BattleCore.Combat.ShieldMath.Sum(ally.Shield.Slots), 1e-4f, "한 틱 늦게 드레인(C17) 뒤 슬롯에 있다");
        }

        [Test]
        public void 궁수_감속_오라와_난도질꾼_출혈은_상대에게_건다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            var slow = Rule(TriggerKind.OnPlace, EffectKind.OpponentStatAura);
            slow.Effect.Magnitude = -40f; slow.Effect.TileRange = 3; slow.Effect.Duration = 5f; slow.Effect.StatKind = (int)SkillStatKind.MoveSpeedMul;
            var bleed = Rule(TriggerKind.OnPlace, EffectKind.AreaApplyStack);
            bleed.Effect.Magnitude = 2f; bleed.Effect.TileRange = 3; bleed.Effect.Duration = 4f; bleed.Effect.StackKind = (int)SkillStackKind.Bleed;
            GiveUnit(def, 0, slow, bleed);
            var m = CoreMatchFixtures.BeginBattle(def);
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 1)));
            var enemy = m.World.Units[m.World.Units.Count - 1];
            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(0, new int2(4, 1))).Reason);
            m.Tick();
            Assert.AreEqual(0.6f, enemy.Modifiers.Effective.MoveSpeedMul, 1e-4f, "−40% = 배율 0.6(곱셈 버킷)");
            Assert.AreEqual(2, enemy.Stacks.CountOf(StackKind.Bleed));
        }

        [Test]
        public void 짱쎈_경계_자폭은_층을_안_가리고_시전자_몸만큼_넓다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, enemyHealth: 100f);
            int blast = AddBlastProjectile(def);
            def.Enemies[0].BodyRadius = 0.5f;
            var quake = Rule(TriggerKind.HealthThreshold, EffectKind.SelfTileAoe);
            quake.Rule.Fraction = 0.2f; quake.Effect.Magnitude = 60f; quake.Effect.TileRange = 2; quake.Effect.DataIndex = blast;
            GiveEnemy(def, 0, quake);
            var m = CoreMatchFixtures.BeginBattle(def);
            var e = SpawnEnemy(m, new int2(6, 2));
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            e.Health = 70f;
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(1, spawned.Count);
            Assert.AreEqual(0.5f, spawned[0].SiteFired.OriginBody, 1e-5f, "몸에서 나오는 것(제약 13)");
            Assert.AreEqual(0, m.World.Projectiles.Count == 0 ? 0 : m.World.Projectiles[0].TargetLayers, "경계 발동은 층 0 = 무제한");
        }
    }
}
