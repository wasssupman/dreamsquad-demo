using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Trigger;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7d — **시즌 기믹이 저절로 일어난다.** 6b2 는 셈판을 세우고 「무엇이 언제 놓나」를 남겼다.
    // 여기서 묻는 것은 그 답의 모양이다: ⑴ 레드불만 **판**의 주기 ⑵ 온천·번아웃은 **유닛마다** 부착 시점이 위상
    // ⑶ 필터가 기믹마다 다르다(온천 = 활성화부터 · 번아웃 = 배치 순간부터) ⑷ 상한에 막힌 주기는 접혔다가 곧바로 선다.
    [TestFixture]
    public class GimmickBindingTests
    {
        private static List<CoreEvent> Fired(BattleMatch m, GimmickKind kind)
        {
            var got = new List<CoreEvent>();
            m.Bus.Subscribe(CoreEventKind.GimmickTriggered, 0, e => { if (e.Arg == (int)kind) got.Add(e); });
            return got;
        }

        private static bool HasGimmickRule(Unit u)
        {
            for (int i = 0; i < u.Bindings.Count; i++)
                if (u.Bindings[i].Def.Origin == BindingOrigin.Gimmick) return true;
            return false;
        }

        private static int MatchGimmickRules(BattleMatch m)
        {
            int n = 0;
            var list = m.Bindings.MatchBindings;
            for (int i = 0; i < list.Count; i++) if (list[i].Def.Origin == BindingOrigin.Gimmick) n++;
            return n;
        }

        // ── 레드불 — 판의 주기 ─────────────────────────────────────────────────

        [Test]
        public void 레드불_주기는_판이_소유하고_주기마다_픽업이_선다()
        {
            var def = CoreGimmickFixtures.With(CoreCombatFixtures.Definition(),
                                               CoreGimmickFixtures.RedBull(lifetime: 30f));   // 주기 3초
            var m = CoreMatchFixtures.BeginBattle(def);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.PickupSpawned);

            Assert.AreEqual(1, MatchGimmickRules(m), "판 호스트 규칙 하나");
            CoreCombatFixtures.Tick(m, 170);
            Assert.AreEqual(0, spawned.Count, "주기 전");
            CoreCombatFixtures.Tick(m, 20);
            Assert.AreEqual(1, spawned.Count, "첫 주기");
            CoreCombatFixtures.Tick(m, 180);
            Assert.AreEqual(2, spawned.Count, "둘째 주기");
        }

        [Test]
        public void 레드불이_동시_상한에_막히면_밀린_주기를_접었다가_자리가_비는_다음_틱에_선다()
        {
            // 상한 1 · 수명 4초 · 주기 3초 — 둘째 주기(≈6초)는 막히고, 첫 캔이 만료(≈7초)되는 **다음 틱**에 선다.
            // 접지 않으면(주기를 버리면) 셋째 주기(≈9초)까지 기다린다 — 옛 `elapsed = min(elapsed, interval)` 이 막은 것.
            var def = CoreGimmickFixtures.With(CoreCombatFixtures.Definition(),
                                               CoreGimmickFixtures.RedBull(lifetime: 4f, maxActive: 1));
            var m = CoreMatchFixtures.BeginBattle(def);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.PickupSpawned);
            var expired = CoreCombatFixtures.Listen(m, CoreEventKind.PickupExpired);

            CoreCombatFixtures.Tick(m, 60 * 8);

            Assert.AreEqual(1, expired.Count);
            Assert.GreaterOrEqual(spawned.Count, 2);
            Assert.AreEqual(expired[0].Tick + 1, spawned[1].Tick, "슬롯이 빈 다음 틱");
        }

        [Test]
        public void 증상_레드불을_먹은_방어유닛이_빨라지고_시간이_지나면_쓰러진다()
        {
            // 먹을 수 있는 칸이 **그 방어유닛의 칸 하나**뿐인 판 — 주기가 놓은 캔을 반드시 그 유닛이 먹는다.
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f);
            var map = def.Map;
            var home = new int2(1, 1);
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var c = new int2(x, y);
                var t = map.Tiles[map.Index(c)];
                if (!c.Equals(home) && (t == MapTile.Walk || t == MapTile.Place)) CoreMapFixtures.Block(map, c);
            }
            CoreGimmickFixtures.With(def, CoreGimmickFixtures.RedBull(lifetime: 30f, mul: 1.5f, duration: 1f, fraction: 1f));
            var m = CoreMatchFixtures.BeginBattle(def);
            var u = CoreTriggerFixtures.SpawnDefender(m, home);

            CoreCombatFixtures.Tick(m, 185);   // 첫 주기(3초) — 같은 틱에 먹는다
            Assert.AreEqual(1.5f, u.Modifiers.Effective.AttackSpeedMul, 1e-4f, "라스트런 — 빨라졌다");
            Assert.IsFalse(u.Dead);
            CoreCombatFixtures.Tick(m, 62);    // 라스트런 1초 뒤 crash(최대 체력 × 1)
            Assert.IsNull(m.World.Find(u.Id), "쓰러졌다(crash 로 사망 → 소멸)");
        }

        // ── 온천·번아웃 — 유닛마다의 주기 ──────────────────────────────────────

        [Test]
        public void 온천_열기는_유닛이_들고_부착_시점이_위상이다()
        {
            var def = CoreGimmickFixtures.With(CoreCombatFixtures.Definition(defenderDamage: 0f),
                                               CoreGimmickFixtures.Onsen());   // 주기 1초
            var m = CoreMatchFixtures.BeginBattle(def);
            var heat = Fired(m, GimmickKind.Onsen);

            var a = CoreTriggerFixtures.SpawnDefender(m, new int2(2, 1));
            CoreCombatFixtures.Tick(m, 30);
            var b = CoreTriggerFixtures.SpawnDefender(m, new int2(4, 1));
            CoreCombatFixtures.Tick(m, 120);

            Assert.AreEqual(0, MatchGimmickRules(m), "판 호스트가 아니다(정정 2)");
            Assert.IsTrue(HasGimmickRule(a) && HasGimmickRule(b), "유닛마다 하나");
            int firstA = heat.Find(e => e.A == a.Id).Tick;
            int firstB = heat.Find(e => e.A == b.Id).Tick;
            Assert.AreEqual(30, firstB - firstA, "위상 = 부착 시점 — 판 주기 하나로 접으면 둘이 같은 틱에 쌓인다");
            Assert.AreEqual(59, firstA, "부착 틱도 센다 — 1초 = 60틱째(옛 lazy-attach 가 붙인 프레임에 dt 를 쌓았다)");
        }

        [Test]
        public void 온천은_적과_순찰에도_붙고_배치_중인_방어유닛은_활성화부터_붙는다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].DeployMotionSeconds = 0.5f;
            CoreGimmickFixtures.With(def, CoreGimmickFixtures.Onsen());
            var m = CoreMatchFixtures.BeginBattle(def);

            m.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var placed = m.World.Find(CoreMatchFixtures.PlacedDefender(m));
            m.Apply(Command.LandDefender(placed.Id));
            Assert.IsTrue(placed.Deploying);
            Assert.IsFalse(HasGimmickRule(placed), "배치 중에는 안 붙는다(옛 `WithNone<PendingDeployment>`)");

            m.Apply(Command.DebugSpawnEnemy(0, new int2(0, 1)));
            var enemy = CoreCombatFixtures.First(m, UnitKind.Enemy);
            Assert.IsTrue(HasGimmickRule(enemy), "전 유닛 — 적에게도 붙는다");

            CoreCombatFixtures.Tick(m, 40);
            Assert.IsFalse(placed.Deploying);
            Assert.IsTrue(HasGimmickRule(placed), "활성화 사건에서 붙는다");
            Assert.AreEqual(1, CountGimmickRules(placed), "두 경로(즉시·모션 뒤)가 겹쳐도 하나");
        }

        [Test]
        public void 번아웃_피로는_방어유닛만_들고_배치_순간부터_쌓인다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].DeployMotionSeconds = 0.5f;
            CoreGimmickFixtures.With(def, CoreGimmickFixtures.Burnout());
            var m = CoreMatchFixtures.BeginBattle(def);
            var fatigue = Fired(m, GimmickKind.Burnout);

            m.Apply(Command.PlaceDefender(0, new int2(3, 1)));
            var placed = m.World.Find(CoreMatchFixtures.PlacedDefender(m));
            Assert.IsTrue(placed.Deploying);
            Assert.IsTrue(HasGimmickRule(placed), "배치 순간부터(옛 `FatigueAccrualSystem` 은 배치 중을 거르지 않았다)");

            m.Apply(Command.DebugSpawnEnemy(0, new int2(0, 1)));
            Assert.IsFalse(HasGimmickRule(CoreCombatFixtures.First(m, UnitKind.Enemy)), "방어유닛 전용");

            CoreCombatFixtures.Tick(m, 61);
            Assert.AreEqual(1, fatigue.Count);
            Assert.AreEqual(placed.Id, fatigue[0].A);
        }

        [Test]
        public void 기믹이_안_뽑힌_판에는_기믹_규칙이_한_줄도_없다()
        {
            var m = CoreMatchFixtures.BeginBattle(CoreCombatFixtures.Definition());
            var u = CoreTriggerFixtures.SpawnDefender(m, new int2(2, 1));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(0, 1)));
            CoreCombatFixtures.Tick(m, 200);

            Assert.AreEqual(0, MatchGimmickRules(m));
            Assert.IsFalse(HasGimmickRule(u));
            Assert.IsEmpty(m.World.Pickups);
        }

        private static int CountGimmickRules(Unit u)
        {
            int n = 0;
            for (int i = 0; i < u.Bindings.Count; i++) if (u.Bindings[i].Def.Origin == BindingOrigin.Gimmick) n++;
            return n;
        }
    }
}
