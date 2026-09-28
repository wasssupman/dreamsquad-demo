using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using static Wassup.Tests.EditMode.Core.CoreCardFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // 플레이 3차 결함 — 사용자의 문장을 그대로 단언한다(CLAUDE.md 버그 절차 2).
    [TestFixture]
    public class PlayThreeSymptomTests
    {
        [Test]
        public void 진동갑주를_단_유닛이_적에게_맞아_체력_30퍼센트_밑으로_가면_주변이_터진다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, enemyDamage: 40f, enemyRange: 1.5f);
            int blast = CoreTriggerFixtures.AddBlastProjectile(def);
            var rule = CardRule(TriggerKind.HealthThreshold, EffectKind.SelfTileAoe);
            rule.Fraction = 0.7f;      // 저작 = 「HP 30% 이하」
            rule.Magnitude = 15f;
            rule.TileRange = 1;
            rule.DataIndex = blast;
            int card = AddAttachCard(def, "tremor_plate", 0, rule);
            var m = CardBattle(def);
            var host = Defender(m, new int2(3, 2));
            var enemy = CoreTriggerFixtures.SpawnEnemy(m, new int2(4, 2));
            Assert.IsTrue(m.Apply(Command.AttachCard(EntryOf(m, card), host.Id)).Accepted, "부착");
            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);
            var hits = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);

            float max = host.MaxHealth;
            int crossTick = -1;
            for (int t = 0; t < 60 * 30 && !host.Dead; t++)
            {
                m.Tick();
                if (crossTick < 0 && host.Health < max * 0.3f) crossTick = t;
                if (crossTick >= 0 && t > crossTick + 5) break;
            }
            Assert.GreaterOrEqual(crossTick, 0, $"적이 숙주를 30% 밑으로 깎았다(hp {host.Health}/{max})");
            Assert.AreEqual(1, fired.Count, "경계를 넘자 진동갑주가 1회 발동했다");
            Assert.IsTrue(hits.Exists(e => e.B == enemy.Id && System.Math.Abs(e.Amount - 15f) < 1e-4f),
                "옆 칸의 적이 피해 15 를 받았다");
        }
    }
}
