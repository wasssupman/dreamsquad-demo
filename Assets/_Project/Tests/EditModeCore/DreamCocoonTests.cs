using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using Wassup.Skills;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7d 구현 13 — **호접몽**: 끝까지 자면 영구 버프, 중간에 맞으면 파탄. 개시는 잠 + 감시 **원자**(S19 ·
    // `IntentApplier.BeginDreamCocoon`). 파탄은 새 중단 사유가 아니라 **피격 기상이 고치를 같이 걷는** 것이다 — 두 문이 갈리면
    // 「깼는데 고치가 남는」 상태가 난다(6a F25 — 피격 기상은 피해가 든 직후 같은 틱).
    [TestFixture]
    public class DreamCocoonTests
    {
        private static Unit Cocooned(out BattleMatch m, float seconds)
        {
            m = CoreMatchFixtures.BeginBattle(CoreCombatFixtures.Definition(defenderDamage: 0f));
            var u = CoreTriggerFixtures.SpawnDefender(m, new int2(3, 1));
            m.Intents.Begin(null, u.Faction, null);
            try
            {
                m.Intents.Apply(new SimIntent
                {
                    Kind = SimIntentKind.BeginDreamCocoon,
                    Target = new SkillEntityId(u.Id.Value), Source = new SkillEntityId(u.Id.Value),
                    Duration = seconds, Amount = 1.5f, Selector = (int)SkillStatKind.DamageMul, StackId = 77,
                });
            }
            finally { m.Intents.End(); }
            return u;
        }

        [Test]
        public void 중간에_맞으면_잠이_깨는_그_틱에_고치도_깨지고_보상이_없다()
        {
            var u = Cocooned(out var m, seconds: 1f);
            CoreCombatFixtures.Tick(m, 20);
            Assert.IsTrue(u.Progressive.CocoonActive);
            u.Inbox.Damage.Add(new DamageEntry { Amount = 1f, Source = SimEntityId.None });
            m.Tick();
            Assert.IsFalse(u.Cc.IsActive(CcSlotKind.Sleep), "피격 기상");
            Assert.IsFalse(u.Progressive.CocoonActive, "같은 틱에 고치가 깨졌다 — 「깼는데 고치가 남는」 상태가 없다");
            CoreCombatFixtures.Tick(m, 80);
            Assert.AreEqual(1f, u.Modifiers.Effective.DamageMul, 1e-5f, "파탄 — 보상 없음");
        }

        [Test]
        public void 끝까지_자면_영구_보상이다()
        {
            var u = Cocooned(out var m, seconds: 1f);
            CoreCombatFixtures.Tick(m, 70);
            Assert.IsFalse(u.Progressive.CocoonActive);
            Assert.AreEqual(1.5f, u.Modifiers.Effective.DamageMul, 1e-5f, "완주");
        }
    }
}
