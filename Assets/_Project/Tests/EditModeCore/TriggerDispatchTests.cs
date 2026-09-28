using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7a — 디스패처: 전순서 키 · 세대 BFS(직접 재진입만) · 깊이 4 · 잔여 큐 후속/지난 seam.
    [TestFixture]
    public class TriggerDispatchTests
    {
        private static BattleMatch Match()
        {
            var m = new BattleMatch(CoreCombatFixtures.Definition());
            m.Begin();
            return m;
        }

        [Test]
        public void seam_틱_순서는_번호가_아니라_파이프라인이_정한다()
        {
            var m = Match();
            var order = m.Triggers.Order;
            Assert.Less(order.IndexOf(Seam.Periodic), order.IndexOf(Seam.Attack),
                "주기는 장 준비 끝 — 공격보다 **앞**이다");
            Assert.Greater((int)Seam.Periodic, (int)Seam.Attack, "enum 값은 반대다(append-only 의 몫)");
            Assert.AreEqual(0, order.IndexOf(Seam.Immediate), "커맨드 = 틱의 맨 앞");
            Assert.Less(order.IndexOf(Seam.Attack), order.IndexOf(Seam.Death));
            Assert.Less(order.IndexOf(Seam.Death), order.IndexOf(Seam.Lifecycle));
            Assert.Less(order.IndexOf(Seam.Lifecycle), order.IndexOf(Seam.Threshold));
        }

        [Test]
        public void 후속_지난_판정에_seam_enum_값을_비교하는_코드가_없다()
        {
            // 5b 의 `CoreViewYardstickTests` 선례 — 막으려는 것이 값이 아니라 **형태**라 grep 을 테스트로 옮긴다.
            var dir = Path.Combine(CoreGoldenStore.RepoRoot, "Assets/_Project/Scripts/BattleCore");
            var hits = new List<string>();
            // `(int)Seam._Count` 는 배열 크기라 제외한다 — 종류 수이지 순서가 아니다.
            var bad = new Regex(@"\(int\)\s*[\w\.]*[Ss]eam(?!\._Count)\b[\w\.]*\s*[<>]|[<>]=?\s*\(int\)\s*[\w\.]*[Ss]eam(?!\._Count)\b|Seam\.(?!_Count)\w+\s*[<>]|[<>]=?\s*Seam\.(?!_Count)\w+");
            foreach (var path in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
                code = Regex.Replace(code, @"/\*.*?\*/", "", RegexOptions.Singleline);
                if (bad.IsMatch(code)) hits.Add(Path.GetFileName(path));
            }
            CollectionAssert.IsEmpty(hits, "seam 순서는 `SeamTickOrder` 로만 묻는다");
        }

        [Test]
        public void SeamHooks_Run_호출부는_여섯이다()
        {
            var dir = Path.Combine(CoreGoldenStore.RepoRoot, "Assets/_Project/Scripts/BattleCore");
            int n = 0;
            string immediateHost = null;
            foreach (var path in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string code = Regex.Replace(File.ReadAllText(path), @"//[^\n]*", "");
                foreach (Match hit in Regex.Matches(code, @"Seams\??\.Run\(\s*Seam\.(\w+)"))
                {
                    n++;
                    if (hit.Groups[1].Value == "Immediate") immediateHost = Path.GetFileName(path);
                }
            }
            Assert.AreEqual(6, n);
            Assert.AreEqual("CommandPhase.cs", immediateHost, "Immediate 는 커맨드 콜스택 안에서만 돈다");
        }

        [Test]
        public void 잔여_규칙_후속_seam_은_같은_틱_지난_seam_은_다음_틱()
        {
            var m = Match();
            var d = SpawnDefender(m, new int2(5, 2));
            int attackTick = -1, periodicTick = -1, raisedAt = -1;

            var onAttack = new ProbeSkill { OnExecute = (c, t, p, x) => attackTick = m.Clock.Tick };
            var onPeriodic = new ProbeSkill { OnExecute = (c, t, p, x) => periodicTick = m.Clock.Tick };
            var bAttack = CoreTriggerFixtures.AttachRuntime(m, d, Probe(TriggerKind.AttackN, onAttack), 0);
            var bPeriodic = CoreTriggerFixtures.AttachRuntime(m, d, Probe(TriggerKind.PeriodicTimer, onPeriodic), 0);

            // 주기 seam(앞)에서 공격 seam(뒤)으로 → 같은 틱.
            var starter = new ProbeSkill();
            starter.OnExecute = (c, t, p, x) =>
            {
                if (raisedAt >= 0) return;
                raisedAt = m.Clock.Tick;
                m.Triggers.RaiseFor(bAttack, EventAt(Seam.Attack, d));
            };
            var s = Probe(TriggerKind.PeriodicTimer, starter);
            s.Rule.PeriodSeconds = BattleMatch.Dt;
            CoreTriggerFixtures.AttachRuntime(m, d, s, 0);
            m.Tick();
            Assert.AreEqual(raisedAt, attackTick, "후속 seam — 같은 틱");

            // 공격 seam(뒤)에서 주기 seam(앞)으로 → 다음 틱.
            int second = -1;
            onAttack.OnExecute = (c, t, p, x) =>
            {
                if (second >= 0) return;
                second = m.Clock.Tick;
                m.Triggers.RaiseFor(bPeriodic, EventAt(Seam.Periodic, d));
            };
            m.Triggers.RaiseFor(bAttack, EventAt(Seam.Attack, d));   // 틱 밖 — 다음 틱의 공격 seam
            m.Tick();
            Assert.GreaterOrEqual(second, 0);
            Assert.AreEqual(-1, periodicTick, "지난 seam 은 이번 틱에 안 돈다");
            m.Tick();
            Assert.AreEqual(second + 1, periodicTick, "지난 seam — 다음 틱");
        }

        [Test]
        public void 전순서_한_사건_안은_소유자_id_다음_InstanceId_오름차순이다()
        {
            var m = Match();
            var a = SpawnDefender(m, new int2(4, 2));
            var b = SpawnDefender(m, new int2(6, 2));
            var log = new List<string>();
            ProbeSkill P(string tag) => new ProbeSkill { OnExecute = (c, t, p, x) => log.Add(tag) };

            // 부착 순서를 뒤섞어도 실행 순서는 (소유자, InstanceId) 다.
            var rb1 = Probe(TriggerKind.PeriodicTimer, P("b1")); rb1.Rule.PeriodSeconds = BattleMatch.Dt;
            var ra1 = Probe(TriggerKind.PeriodicTimer, P("a1")); ra1.Rule.PeriodSeconds = BattleMatch.Dt;
            var ra2 = Probe(TriggerKind.PeriodicTimer, P("a2")); ra2.Rule.PeriodSeconds = BattleMatch.Dt;
            CoreTriggerFixtures.AttachRuntime(m, b, rb1, 0);
            CoreTriggerFixtures.AttachRuntime(m, a, ra1, 0);
            CoreTriggerFixtures.AttachRuntime(m, a, ra2, 0);
            m.Tick();
            CollectionAssert.AreEqual(new[] { "a1", "a2", "b1" }, log, "유닛 순회 = SimEntityId 오름차순");
        }

        [Test]
        public void 세대_BFS_직접_재진입은_같은_드레인의_다음_세대로_간다()
        {
            var m = Match();
            var d = SpawnDefender(m, new int2(5, 2));
            var log = new List<string>();
            Binding child = null;
            var childSkill = new ProbeSkill { OnExecute = (c, t, p, x) => log.Add("child") };
            child = CoreTriggerFixtures.AttachRuntime(m, d, Probe(TriggerKind.None, childSkill), 0);
            var parent = new ProbeSkill { OnExecute = (c, t, p, x) => { log.Add("parent"); m.Triggers.RaiseFor(child, EventAt(Seam.Periodic, d)); } };
            var sibling = new ProbeSkill { OnExecute = (c, t, p, x) => log.Add("sibling") };
            var rp = Probe(TriggerKind.PeriodicTimer, parent); rp.Rule.PeriodSeconds = 99f;
            var rs = Probe(TriggerKind.PeriodicTimer, sibling); rs.Rule.PeriodSeconds = 99f;
            var bp = CoreTriggerFixtures.AttachRuntime(m, d, rp, 0);
            var bs = CoreTriggerFixtures.AttachRuntime(m, d, rs, 0);
            m.Triggers.RaiseFor(bp, EventAt(Seam.Periodic, d));
            m.Triggers.RaiseFor(bs, EventAt(Seam.Periodic, d));
            m.Tick();
            CollectionAssert.AreEqual(new[] { "parent", "sibling", "child" }, log,
                "세대 0 전부 → 세대 1 (한 틱 안에서 — intent 경유가 아니라 직접 재진입)");
        }

        [Test]
        public void 깊이_4_초과는_Report_로_말하고_버린다()
        {
            var m = Match();
            var said = new List<string>();
            m.Report = said.Add;
            var d = SpawnDefender(m, new int2(5, 2));
            Binding self = null;
            var loop = new ProbeSkill();
            loop.OnExecute = (c, t, p, x) => m.Triggers.RaiseFor(self, EventAt(Seam.Periodic, d));
            self = CoreTriggerFixtures.AttachRuntime(m, d, Probe(TriggerKind.None, loop), 0);
            m.Triggers.RaiseFor(self, EventAt(Seam.Periodic, d));
            m.Tick();
            Assert.AreEqual(1 + TriggerDispatcher.MaxDepth, loop.Count, "세대 0..4");
            Assert.IsTrue(said.Exists(s => s.Contains("깊이")), "조용한 폐기 금지");
            Assert.AreEqual(0, m.Triggers.PendingAt(Seam.Periodic));
        }

        [Test]
        public void 공격_N회는_대표_대상이_있는_RESOLVE_만_세고_게이트_실패는_카운트를_안_올린다()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 1f, enemyHealth: 1000f);
            var probe = new ProbeSkill();
            var rule = Probe(TriggerKind.AttackN, probe);
            rule.Rule.Period = 3;
            GiveUnit(def, 0, rule);
            var m = CoreMatchFixtures.BeginBattle(def);
            var shooter = SpawnDefender(m, new int2(5, 2));
            SpawnEnemy(m, new int2(6, 2));
            var resolved = CoreCombatFixtures.Listen(m, CoreEventKind.AttackResolved);
            CoreCombatFixtures.Tick(m, 60 * 7);
            int mine = resolved.FindAll(e => e.A == shooter.Id).Count;   // 적도 때린다 — 자기 규칙은 자기 공격만 센다
            Assert.GreaterOrEqual(mine, 6);
            Assert.AreEqual(mine / 3, probe.Count, "3타마다 1회");

            // 게이트(처형타) — 체력 30% 이하 대상에게만 센다: 멀쩡한 적이면 영원히 0.
            var def2 = CoreCombatFixtures.Definition(defenderDamage: 1f, enemyHealth: 1000f);
            var gated = new ProbeSkill();
            var g = Probe(TriggerKind.AttackN, gated);
            g.Rule.Period = 1; g.Rule.Gate = GateKind.HpBelow; g.Rule.GateSubject = GateSubject.EventTarget; g.Rule.GateValue = 0.3f;
            GiveUnit(def2, 0, g);
            var m2 = CoreMatchFixtures.BeginBattle(def2);
            var dd = SpawnDefender(m2, new int2(5, 2));
            SpawnEnemy(m2, new int2(6, 2));
            CoreCombatFixtures.Tick(m2, 60 * 3);
            Assert.AreEqual(0, gated.Count);
            Assert.AreEqual(0, dd.Bindings[0].Counter, "카운트 게이트 — 실패 사건은 카운터 무변");
        }

        [Test]
        public void 주기_규칙은_잠든_유닛도_쏘고_죽은_유닛은_안_쏜다()
        {
            // 사용자 결정 ③ 기본값 = 현행 박제(옛 주기 감지자는 행동 잠금을 안 읽었다).
            var m = Match();
            var d = SpawnDefender(m, new int2(5, 2));
            var probe = new ProbeSkill();
            var r = Probe(TriggerKind.PeriodicTimer, probe); r.Rule.PeriodSeconds = BattleMatch.Dt;
            CoreTriggerFixtures.AttachRuntime(m, d, r, 0);
            d.Cc.Apply(Wassup.BattleCore.Effects.CcSlotKind.Sleep, 99f, float3.zero, SimEntityId.None);
            CoreCombatFixtures.Tick(m, 3);
            Assert.AreEqual(3, probe.Count, "잠든 채로 쏜다");
            d.Dead = true;
            CoreCombatFixtures.Tick(m, 1);
            Assert.AreEqual(3, probe.Count, "시체는 새 발동을 시작하지 않는다");
        }

        [Test]
        public void 체력_경계는_한_방에_여러_경계를_뚫어도_1회다()
        {
            var def = CoreCombatFixtures.Definition();
            var probe = new ProbeSkill();
            var r = Probe(TriggerKind.HealthThreshold, probe);
            r.Rule.Fraction = 0.2f;
            GiveEnemy(def, 0, r);
            var m = Match();
            m = new BattleMatch(def); m.Begin();
            var e = SpawnEnemy(m, new int2(6, 2));
            e.Health = e.MaxHealth * 0.3f;   // 0.8 · 0.6 · 0.4 세 경계를 한 번에
            m.Tick();
            Assert.AreEqual(1, probe.Count);
            Assert.AreEqual(4, e.Bindings[0].NextBoundary, "k 는 가장 깊은 경계까지 나간다");
            e.Health = e.MaxHealth;
            m.Tick();
            Assert.AreEqual(1, probe.Count, "회복해도 되감기지 않는다(핑퐁 차단)");
        }
    }
}
