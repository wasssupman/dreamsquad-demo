using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7d — **분열.** 계기 = 피해로 죽음(`OnSlain` — 킬러는 안 본다, 옛 피해 사망 분기 그대로) ·
    // 자리 = 부모 **칸 중심** · 배치각 = 인덱스 결정론 · 자식은 전멸 판정 **앞**에 태어난다(X2 ①).
    [TestFixture]
    public class SplitTests
    {
        private static MatchDefinition Def(int count, int child = 1)
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f);
            var kid = def.Enemies[0];
            kid.Id = "fixture_slime_child";
            kid.Health = 20f;
            def.Enemies = new[] { def.Enemies[0], kid };
            def.Enemies[0].SplitCount = count;
            def.Enemies[0].SplitChildDefIndex = child;
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        private static List<Unit> Children(BattleMatch m)
        {
            var got = new List<Unit>();
            foreach (var u in m.World.Units) if (u.Kind == UnitKind.Enemy && u.DefIndex == 1) got.Add(u);
            return got;
        }

        private static void Hurt(Unit u, float amount, SimEntityId source)
            => u.Inbox.Damage.Add(new DamageEntry { Amount = amount, Source = source });

        [Test]
        public void 고정구가_상한보다_많이_넣어도_상한만큼만_선다()
        {
            // M2 — 상한은 빌더에도 있지만 고정구·헤드리스는 빌더를 안 지난다. 코어가 정의표 상한으로 한 번 더 자른다.
            var def = Def(count: 20);
            int cap = def.Movement.SplitMaxChildren;
            Assert.Less(cap, 20, "상한이 20 보다 작아야 이 테스트가 뜻을 갖는다");
            var m = CoreMatchFixtures.BeginBattle(def);
            var said = new List<string>();
            m.Report = said.Add;
            var parent = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            var killer = CoreTriggerFixtures.SpawnDefender(m, new int2(1, 1));
            Hurt(parent, 1e6f, killer.Id);
            m.Tick();
            Assert.AreEqual(cap, Children(m).Count);
            Assert.IsTrue(said.Exists(s => s.Contains("[Split]")), "자른 사실을 말한다(조용한 절단 금지)");
        }

        [Test]
        public void 증상_슬라임을_잡으면_그_칸에서_자식이_퍼진다()
        {
            var m = CoreMatchFixtures.BeginBattle(Def(count: 3));
            var parent = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            // 부모를 칸 중심에서 비켜 세운다 — 연속 좌표에 더하면 자식이 옆 칸에 태어난다(E1).
            parent.Position += new float3(0.4f, 0f, -0.3f);
            var killer = CoreTriggerFixtures.SpawnDefender(m, new int2(1, 1));
            Hurt(parent, 1e6f, killer.Id);
            m.Tick();

            var kids = Children(m);
            Assert.AreEqual(3, kids.Count);
            var center = m.Map.CenterOf(new int2(5, 2));
            float radius = m.Definition.Movement.SplitSpreadFraction * m.Map.TileSize;
            for (int c = 0; c < kids.Count; c++)
            {
                Assert.AreEqual(new int2(5, 2), m.Map.CellOf(kids[c].Position), "부모와 같은 칸");
                float angle = math.PI * 2f * c / 3f;
                Assert.AreEqual(center.x + math.cos(angle) * radius, kids[c].Position.x, 1e-4f, "배치각 = 2π·c/count(인덱스 결정론)");
                Assert.AreEqual(center.z + math.sin(angle) * radius, kids[c].Position.z, 1e-4f);
            }
        }

        [Test]
        public void 출처_없는_피해로_죽어도_갈라진다_옛_처치_분기는_킬러를_안_봤다()
        {
            var m = CoreMatchFixtures.BeginBattle(Def(count: 2));
            Hurt(CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2)), 1e6f, SimEntityId.None);
            m.Tick();
            Assert.AreEqual(2, Children(m).Count, "지속 피해·운석처럼 출처 없는 피해도 피해 사망이다");
        }

        [Test]
        public void 죽음이_아닌_제거와_치명_타이머로는_안_갈라진다()
        {
            var m = CoreMatchFixtures.BeginBattle(Def(count: 2));
            var a = CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2));
            m.Apply(Command.DebugDestroy(a.Id));
            var b = CoreTriggerFixtures.SpawnEnemy(m, new int2(6, 2));
            b.Progressive = new ProgressiveStates { LethalActive = true, LethalRemaining = BattleMatch.Dt };
            CoreCombatFixtures.Tick(m, 4);
            Assert.IsNull(m.World.Find(b.Id), "치명 타이머로 죽었다");
            Assert.AreEqual(0, Children(m).Count, "OnDeath 로 넓히면 여기서 갈라진다(H7)");
        }

        [Test]
        public void 부모만_죽은_틱에도_필드는_비지_않는다_자식이_전멸_판정_앞에_태어난다()
        {
            var m = CoreMatchFixtures.BeginBattle(Def(count: 2));
            Hurt(CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2)), 1e6f, SimEntityId.None);
            bool clearAtWaveStep = true;
            // 웨이브 담당자 단계가 도는 그 틱에 필드가 비어 있으면 다음 웨이브가 당겨진다(X2 ①).
            m.Tick();
            clearAtWaveStep = m.Waves.FieldClear;
            Assert.IsFalse(clearAtWaveStep);
        }

        [Test]
        public void 자기_자신을_가리키는_분열은_건너뛰고_말한다()
        {
            var m = CoreMatchFixtures.BeginBattle(Def(count: 2, child: 0));
            var said = new List<string>();
            m.Report = said.Add;
            Hurt(CoreTriggerFixtures.SpawnEnemy(m, new int2(5, 2)), 1e6f, SimEntityId.None);
            CoreCombatFixtures.Tick(m, 2);
            Assert.AreEqual(0, CoreCombatFixtures.First(m, UnitKind.Enemy) == null ? 0 : 1, "무한 분열 방지");
            Assert.IsTrue(said.Exists(s => s.Contains("자기 자신")));
        }
    }
}
