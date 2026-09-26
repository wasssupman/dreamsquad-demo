using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Emission;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // 하드 케이스 탐침(2026-09-26) — 「효과는 하나의 정의, 트리거만 다르게」가 현 코어에서 서나.
    //
    //   A  = 유닛 배치 스킬: 배치 시 자기 자리 N 칸 안 **모든 적**에게 **각각** 호밍 탄 1발 · 100 피해.
    //   AA = 드림캐쳐: 호스트 H 가 살아 있는 동안 **새로 배치되는 아군마다** A 를 그 유닛 자리에서 시전
    //        (피해 귀속 = H — 결정 「스킬 피해 출처 = 시전자」).
    //
    // 정의표는 코어 API 로 손조립한다(빌더 없음). 저작 경로(`DcMechanic` → 빌더)는 Unity 층이라 헤드리스에서
    // 돌지 않는다 — 판정은 탐침 보고에 정적으로 남긴다.
    //
    // ⚠ 초록 = 「현 구조가 그 문장을 만족한다」. `[Ignore]` = 「현 구조로는 그 문장이 성립하지 않는다」 — 사유에
    // 막힌 지점(파일:줄)을 적었다. `현행_` 으로 시작하는 테스트는 **지금의 동작을 박제한 증언**이다 — 코어가 고쳐지면
    // 빨개지는 것이 정상이고, 그때 짝이 되는 `[Ignore]` 를 풀면 된다.
    //
    // ⚠ 여기 수치는 게임 값이 아니라 픽스처다.
    //
    // `unified-effect-layer` 완료 기준 — 이 파일의 `[Ignore]` 해제가 그 spec 의 완료 기준이다(`docs/spec/unified-effect-layer/`).
    //   AA 두 건 → unit 3(버스트 슬롯이 발동 주체를 든다) 에서 해제. U3(킬은 탄이 나간 유닛 몫)로 귀속은 H 가 아니라 U 다 —
    //   A 와 AA 는 탄 수 · 대상 집합 · 피해 합 · 귀속(= 쏜 유닛)까지 같고, 다른 것은 수명(호스트 H)뿐이다.
    [TestFixture]
    public class HardCaseUnifiedSkillProbeTests
    {
        private const int N = 2;
        private const float Hit = 100f;
        private const int Ticks = 90;

        // 판 12×5(`CoreMatchFixtures.Board`). 스폰은 x=0 — 웨이브 적은 거기 멈춰 선다(속도 0) → 어느 스코프에도 안 든다.
        private static readonly int2 PlaceCell = new int2(6, 2);
        private static readonly int2[] InCells = { new int2(7, 2), new int2(6, 4) };   // 거리 1 · 2 (≤ 2 + 몸 0.5 + 0.25)
        private static readonly int2 OutCell = new int2(10, 2);                         // 거리 4
        private static readonly int2 HostCell = new int2(10, 4);                        // H — OutCell 과 거리 2(H 스코프 안)
        private static readonly int2 SecondPlaceCell = new int2(5, 3);

        // ── 정의표 ──────────────────────────────────────────────────────────

        /// <summary>효과 한 벌 = 탄 줄 0 + 발사 명세 줄 0. 바인딩은 이 둘을 **index 로** 가리킨다.</summary>
        private static MatchDefinition Definition(bool fanOut = true, int shots = 1, float interval = 0f,
                                                  MovementKind movement = MovementKind.HomingToEntity,
                                                  float enemyHealth = 1000f)
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            def.Units[0].MaxOnBoard = 8;
            def.Units[0].DeathCooldown = 0f;   // H 가 죽은 뒤 같은 줄(U2)을 곧바로 놓는다
            for (int i = 0; i < def.Enemies.Length; i++)
            {
                def.Enemies[i].MoveSpeed = 0f;
                def.Enemies[i].Health = enemyHealth;
            }

            var missile = ProjectileDef.Default();
            missile.Id = "probe_homing_missile";
            missile.Movement = (int)movement;   // 대상 바인딩 = 융단폭격(FanOut)의 전제
            missile.Payload = (int)PayloadKind.SingleSplash;
            missile.SplashDamageMul = 0f;                           // 직격만 — 피해 합이 발수 × 100 이 되게
            missile.SplashRadius = 0f;
            missile.Speed = 10f;
            missile.MinFlightTime = 0.05f;
            def.Projectiles = new[] { missile };

            var shotRows = new PatternShotDef[shots];
            for (int i = 0; i < shots; i++)
                shotRows[i] = new PatternShotDef { DirectionT = 0.5f, IntervalAfterPreviousSec = i > 0 ? interval : 0f };
            def.Patterns = new[]
            {
                new PatternDef
                {
                    Id = "probe_barrage", BarrelProjectileDefIndex = 0, Damage = Hit,
                    Selection = (int)PatternSelectionRule.RoundRobin,
                    Shots = shotRows,
                    ReselectPerShot = true, TelegraphSec = 0f, ScopeTileRange = N,
                    FanOutToAllCandidates = fanOut,
                },
            };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>A·AA 가 공유하는 효과 부분(트리거 = 배치). 다른 것은 주어·수명·출처뿐이다.</summary>
        private static BindingDef Effect()
        {
            var r = Rule(TriggerKind.OnPlace, TriggerPayload.EmitProjectilePattern);
            r.PatternDefIndex = 0;
            r.TileRange = N;          // 조준 후보 반경 = 탄 최대 거리(`EmitPatternParams.Range`)
            r.Magnitude = Hit;        // ⚠ 패턴 경로는 이 값을 읽지 않는다 — `패턴_경로의_피해는_…` 참조
            return r;
        }

        private static BindingDef UnitSkillRow()
        {
            var r = Effect();
            r.Label = "A 배치 융단폭격";
            r.Subject = BindingSubject.Self;
            r.Origin = BindingOrigin.UnitAuthored;
            return r;
        }

        private static BindingDef CardRow()
        {
            var r = Effect();
            r.Label = "AA 배치 상속 융단폭격";
            r.Subject = BindingSubject.Any;
            r.SubjectFilter = BindingSubjectFilter.PlacedDefender;
            r.Lifetime = BindingLifetime.Owner;
            r.Origin = BindingOrigin.Card;
            return r;
        }

        // ── 관측 ────────────────────────────────────────────────────────────

        private sealed class Obs
        {
            public BattleMatch M;
            public Unit Caster;        // 배치된 유닛(A 자신 · AA 의 U)
            public Unit Host;          // AA 의 H(A 에서는 null)
            public List<Unit> In = new List<Unit>();
            public Unit Out;
            public List<CoreEvent> Spawned;
            public List<CoreEvent> Hits;

            public List<CoreEvent> SpawnedBy(SimEntityId owner) => Spawned.FindAll(e => e.B == owner);

            public HashSet<int> Victims()
            {
                var s = new HashSet<int>();
                foreach (var h in Hits) s.Add(h.B.Value);
                return s;
            }

            public float DamageTo(Unit u)
            {
                float sum = 0f;
                foreach (var h in Hits) if (h.B == u.Id) sum += h.Amount;
                return sum;
            }

            public float Total()
            {
                float sum = 0f;
                foreach (var h in Hits) sum += h.Amount;
                return sum;
            }
        }

        private static Unit Place(BattleMatch m, int2 cell)
        {
            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(0, cell)).Reason, $"배치 {cell}");
            var units = m.World.Units;
            return units[units.Count - 1];
        }

        private static Obs Arena(MatchDefinition def, int2[] inCells)
        {
            var o = new Obs { M = CoreMatchFixtures.BeginBattle(def) };
            foreach (var c in inCells) o.In.Add(SpawnEnemy(o.M, c));
            o.Out = SpawnEnemy(o.M, OutCell);
            return o;
        }

        private static Obs RunA(int2[] inCells = null, bool fanOut = true, int shots = 1)
        {
            var def = Definition(fanOut, shots);
            GiveUnit(def, 0, UnitSkillRow());
            var o = Arena(def, inCells ?? InCells);
            o.Spawned = CoreCombatFixtures.Listen(o.M, CoreEventKind.ProjectileSpawned);
            o.Hits = CoreCombatFixtures.Listen(o.M, CoreEventKind.DamageApplied);
            o.Caster = Place(o.M, PlaceCell);
            CoreCombatFixtures.Tick(o.M, Ticks);
            return o;
        }

        private static Obs RunAA(out int cardRow, MatchDefinition def = null, int ticks = Ticks)
        {
            def = def ?? Definition();
            cardRow = Add(def, CardRow())[0];
            def.ConfigHash = def.ComputeConfigHash();
            var o = Arena(def, InCells);
            o.Host = SpawnDefender(o.M, HostCell);    // 디버그 스폰 = 활성화 사건 없음(H 자신은 발화하지 않는다)
            Assert.IsNotNull(o.M.Bindings.Attach(o.Host, in def.Bindings[cardRow], cardRow, o.M.Clock.Tick), "부착");
            o.Spawned = CoreCombatFixtures.Listen(o.M, CoreEventKind.ProjectileSpawned);
            o.Hits = CoreCombatFixtures.Listen(o.M, CoreEventKind.DamageApplied);
            o.Caster = Place(o.M, PlaceCell);
            CoreCombatFixtures.Tick(o.M, ticks);
            return o;
        }

        // ── 1. A 단독 ───────────────────────────────────────────────────────

        [Test]
        public void A_배치하면_N칸_안_적_각각에게_호밍탄_한_발씩_100_피해_밖은_무피해_귀속은_A()
        {
            var o = RunA();
            var mine = o.SpawnedBy(o.Caster.Id);
            Assert.AreEqual(InCells.Length, mine.Count, "N 안 적 수 = 탄 수");
            foreach (var s in mine)
                Assert.AreEqual(o.Caster.Position.x, s.SiteFired.Pos.x, 1e-4f, "탄은 A 자리에서 난다");
            foreach (var e in o.In) Assert.AreEqual(Hit, o.DamageTo(e), 1e-3f, $"N 안 적 {e.Id} 가 정확히 100");
            Assert.AreEqual(0f, o.DamageTo(o.Out), "N 밖 적은 무피해");
            Assert.IsTrue(o.Hits.TrueForAll(h => h.A == o.Caster.Id), "귀속 = A");
            Assert.AreEqual(Hit * InCells.Length, o.Total(), 1e-3f);
        }

        // ── 2. AA 단독 ──────────────────────────────────────────────────────

        [Test]
        public void AA_새로_배치된_U_자리에서_N칸_안_적_각각에게_100_귀속은_U()
        {
            var o = RunAA(out _);
            var fromU = o.SpawnedBy(o.Caster.Id);
            Assert.AreEqual(InCells.Length, fromU.Count, "U 자리 N 안 적 수 = 탄 수(귀속 U)");
            Assert.AreEqual(0, o.SpawnedBy(o.Host.Id).Count, "H 가 쏜 탄은 0 — H 는 수명만 잇는다");
            foreach (var s in fromU)
                Assert.AreEqual(o.Caster.Position.x, s.SiteFired.Pos.x, 1e-4f, "탄은 U 자리에서 난다");
            foreach (var e in o.In) Assert.AreEqual(Hit, o.DamageTo(e), 1e-3f);
            Assert.AreEqual(0f, o.DamageTo(o.Out), "H 옆(U 에서 4칸) 적은 무피해");
            Assert.IsTrue(o.Hits.TrueForAll(h => h.A == o.Caster.Id), "귀속 = U(U3 — 탄이 나간 유닛 몫)");
        }

        [Test]
        public void AA_호스트가_죽으면_다음_배치에는_발화하지_않는다()
        {
            var o = RunAA(out _);
            var killer = o.Out;
            o.M.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.DealDamage, Target = CoreSkillContext.ToSkill(o.Host.Id),
                Source = CoreSkillContext.ToSkill(killer.Id), Amount = 99999f,
            });
            CoreCombatFixtures.Tick(o.M, 4);
            Assert.IsNull(o.M.World.Find(o.Host.Id), "H 가 판에서 사라졌다");

            var fired = CoreCombatFixtures.Listen(o.M, CoreEventKind.TriggerFired);
            int spawnedBefore = o.Spawned.Count;
            var u2 = Place(o.M, SecondPlaceCell);
            CoreCombatFixtures.Tick(o.M, Ticks);
            Assert.AreEqual(0, fired.Count, "수명 Owner — H 와 함께 떨어졌다");
            Assert.AreEqual(spawnedBefore, o.Spawned.Count, "U2 배치에 탄 0");
            Assert.IsNotNull(u2);
        }

        // ── 3. 동치 증언 ────────────────────────────────────────────────────

        [Test]
        public void A_와_AA_는_탄_수_대상_집합_피해_합_귀속이_같다()
        {
            var a = RunA();
            var aa = RunAA(out _);
            Assert.AreEqual(a.SpawnedBy(a.Caster.Id).Count, aa.SpawnedBy(aa.Caster.Id).Count, "탄 수");
            Assert.AreEqual(a.Spawned.Count, aa.Spawned.Count, "탄 총수(다른 주인의 탄 없음)");
            CollectionAssert.AreEquivalent(Cells(a, a.Victims()), Cells(aa, aa.Victims()), "대상 집합(칸)");
            Assert.AreEqual(a.Total(), aa.Total(), 1e-3f, "피해 합");
            Assert.IsTrue(a.Hits.TrueForAll(h => h.A == a.Caster.Id), "A 귀속 = 쏜 유닛");
            Assert.IsTrue(aa.Hits.TrueForAll(h => h.A == aa.Caster.Id), "AA 귀속 = 쏜 유닛(U) — H 가 아니다");
        }

        // 두 판의 SimEntityId 는 발급 순서가 달라(H 가 하나 더 있다) 칸으로 비교한다.
        private static List<int2> Cells(Obs o, HashSet<int> ids)
        {
            var list = new List<int2>();
            foreach (var e in o.In) if (ids.Contains(e.Id.Value)) list.Add(e.Footprint != null ? e.Footprint.Anchor : Cell(o, e));
            if (ids.Contains(o.Out.Id.Value)) list.Add(Cell(o, o.Out));
            return list;
        }

        private static int2 Cell(Obs o, Unit u) => new int2((int)math.round(u.Position.x), (int)math.round(u.Position.z));

        // ── 4. 「범위 안 모든 적」 표현 가능성 ────────────────────────────────

        [Test]
        public void 전원_손잡이를_켜면_탄_수가_적_수를_따라간다_5기와_1기()
        {
            var five = new[] { new int2(7, 2), new int2(6, 4), new int2(5, 2), new int2(6, 1), new int2(7, 3) };
            var a5 = RunA(five);
            Assert.AreEqual(5, a5.SpawnedBy(a5.Caster.Id).Count, "적 5기 → 5발(발수 1 저작이어도)");
            foreach (var e in a5.In) Assert.AreEqual(Hit, a5.DamageTo(e), 1e-3f);

            var a1 = RunA(new[] { new int2(7, 2) });
            Assert.AreEqual(1, a1.SpawnedBy(a1.Caster.Id).Count, "적 1기 → 1발");
            Assert.AreEqual(Hit, a1.DamageTo(a1.In[0]), 1e-3f);
        }

        [Test]
        public void 현행_발수_고정_어휘는_적_수와_어긋난다_손잡이_없이는_범위_안_전원을_못_쓴다()
        {
            // 발수 3 · 순회 선정 · 손잡이 끔 — 「범위 안 전원에게 각 1발」을 발수로 흉내 내면:
            var five = new[] { new int2(7, 2), new int2(6, 4), new int2(5, 2), new int2(6, 1), new int2(7, 3) };
            var a5 = RunA(five, fanOut: false, shots: 3);
            Assert.AreEqual(3, a5.SpawnedBy(a5.Caster.Id).Count, "적 5기인데 3발 — 둘은 무피해");
            int untouched = 0;
            foreach (var e in a5.In) if (a5.DamageTo(e) == 0f) untouched++;
            Assert.AreEqual(2, untouched);

            var a1 = RunA(new[] { new int2(7, 2) }, fanOut: false, shots: 3);
            Assert.AreEqual(3, a1.SpawnedBy(a1.Caster.Id).Count, "적 1기인데 3발");
            Assert.AreEqual(3 * Hit, a1.DamageTo(a1.In[0]), 1e-3f, "한 적에게 300");
        }

        // ── 5. 효과 한 번 정의 — 코어 정의표에서 참조인가 복사인가 ───────────

        [Test]
        public void 패턴_경로의_피해는_바인딩_Magnitude_가_아니라_발사_명세의_Damage_다()
        {
            // 두 바인딩 줄(A · AA)은 `BindingDef` **값 복사**이고, 공유하는 것은 index 둘(탄 0 · 명세 0)뿐이다.
            // 피해는 그 공유 줄(`PatternDef.Damage`)에서 나온다(`IntentApplier.cs:311`) — 바인딩 Magnitude 를 0 으로 해도 100.
            var def = Definition();
            var unit = UnitSkillRow();
            unit.Magnitude = 0f;
            var card = CardRow();
            var rows = Add(def, unit, card);
            Assert.AreEqual(def.Bindings[rows[0]].PatternDefIndex, def.Bindings[rows[1]].PatternDefIndex, "같은 명세 줄을 가리킨다");
            Assert.AreSame(def.Bindings[rows[0]].Effect, def.Bindings[rows[1]].Effect, "실행자는 무상태 한 벌(라우팅 표)");

            def.Units[0].Bindings = new[] { rows[0] };
            def.ConfigHash = def.ComputeConfigHash();
            var o = Arena(def, InCells);
            o.Hits = CoreCombatFixtures.Listen(o.M, CoreEventKind.DamageApplied);
            o.Caster = Place(o.M, PlaceCell);
            CoreCombatFixtures.Tick(o.M, Ticks);
            foreach (var e in o.In) Assert.AreEqual(Hit, o.DamageTo(e), 1e-3f, "Magnitude 0 이어도 100 — 피해의 정본은 PatternDef");
        }

        // ── 6. 버스트 수명 = 발동 주체 ∧ 바인딩을 든 자(U2) · 킬 귀속(U3) · 캐논 회귀 ─────────

        private const int BurstShots = 4;
        private const float BurstInterval = 0.5f;   // 30 틱 — 첫 발 뒤 개입할 여유

        private static MatchDefinition BurstDefinition() => Definition(fanOut: false, shots: BurstShots, interval: BurstInterval);

        /// <summary>첫 탄이 나갈 때까지만 돌린다(배치 모션 길이를 가정하지 않는다).</summary>
        private static void TickUntilFirstShot(Obs o)
        {
            for (int t = 0; t < Ticks && o.Spawned.Count == 0; t++) o.M.Tick();
            Assert.AreEqual(1, o.Spawned.Count, "버스트 첫 발");
        }

        [Test]
        public void AA_버스트는_끝까지_가면_발수_전부가_나간다_대조군()
        {
            var o = RunAA(out _, BurstDefinition(), ticks: Ticks * 3);
            Assert.AreEqual(BurstShots, o.SpawnedBy(o.Caster.Id).Count, "개입 없으면 저작 발수 전부");
        }

        [Test]
        public void AA_버스트_도중_발동_주체가_퇴근하면_남은_발이_없다()
        {
            var o = RunAA(out _, BurstDefinition(), ticks: 0);
            TickUntilFirstShot(o);
            // ⚠ `Unit` 은 풀링된다 — 소멸 뒤 참조는 다른 개체일 수 있어 id 를 먼저 잡는다.
            SimEntityId u = o.Caster.Id, h = o.Host.Id;
            Assert.AreEqual(RejectReason.None, o.M.Apply(Command.Retire(u)).Reason, "퇴근");
            CoreCombatFixtures.Tick(o.M, Ticks * 3);
            Assert.IsNull(o.M.World.Find(u), "U 가 판에서 사라졌다");
            Assert.IsNotNull(o.M.World.Find(h), "H(바인딩을 든 자)는 살아 있다");
            Assert.AreEqual(1, o.Spawned.Count, "남은 발 없음(U2) — H 가 대신 쏘지도 않는다");
        }

        [Test]
        public void AA_버스트_도중_호스트가_죽으면_남은_발이_없다()
        {
            var o = RunAA(out _, BurstDefinition(), ticks: 0);
            TickUntilFirstShot(o);
            SimEntityId u = o.Caster.Id, h = o.Host.Id;   // 풀링 — id 를 먼저 잡는다
            o.M.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.DealDamage, Target = CoreSkillContext.ToSkill(h),
                Source = CoreSkillContext.ToSkill(o.Out.Id), Amount = 99999f,
            });
            CoreCombatFixtures.Tick(o.M, Ticks * 3);
            Assert.IsNull(o.M.World.Find(h), "H 가 판에서 사라졌다");
            Assert.IsNotNull(o.M.World.Find(u), "U(발동 주체)는 살아 있다");
            Assert.AreEqual(1, o.Spawned.Count, "남은 발 없음 — 바인딩째 멈춘다(오늘 그대로)");
        }

        [Test]
        public void AA_탄_킬은_발동_주체의_OnKill_을_울리고_호스트의_OnKill_은_울리지_않는다()
        {
            var def = Definition(enemyHealth: Hit * 0.5f);
            var onKill = new ProbeSkill();
            var casters = new List<int>();
            onKill.OnExecute = (c, t, p, ctx) => casters.Add(c.Unit.Value);
            GiveUnit(def, 0, Probe(TriggerKind.OnKill, onKill));   // U · H 가 같은 유닛 줄 — 둘 다 OnKill 을 든다
            var o = RunAA(out _, def, ticks: 0);
            // ⚠ `Unit` 은 풀링된다 — 처치 뒤 참조는 다른 개체일 수 있어 id 를 먼저 잡는다.
            var inIds = o.In.ConvertAll(e => e.Id);
            int u = CoreSkillContext.ToSkill(o.Caster.Id).Value;
            int h = CoreSkillContext.ToSkill(o.Host.Id).Value;
            CoreCombatFixtures.Tick(o.M, Ticks);
            foreach (var id in inIds) Assert.IsNull(o.M.World.Find(id), $"N 안 적 {id} 처치");
            Assert.AreEqual(InCells.Length, casters.FindAll(c => c == u).Count, "킬마다 U 의 OnKill(U3)");
            Assert.AreEqual(0, casters.FindAll(c => c == h).Count, "H 의 OnKill 은 무발화");
        }

        [Test]
        public void 캐논_폭격_대상_낙하_전원_손잡이_탄_수는_반경_안_적_수다()
        {
            // 회귀 고정 — 라이브 캐논(배치 스킬 · 자기 사건)의 1:1 융단폭격(FanOut · SkyFallOnEntity = 대상 결합).
            var five = new[] { new int2(7, 2), new int2(6, 4), new int2(5, 2), new int2(6, 1), new int2(7, 3) };
            var def = Definition(movement: MovementKind.SkyFallOnEntity);
            GiveUnit(def, 0, UnitSkillRow());
            var o = Arena(def, five);
            o.Spawned = CoreCombatFixtures.Listen(o.M, CoreEventKind.ProjectileSpawned);
            o.Hits = CoreCombatFixtures.Listen(o.M, CoreEventKind.DamageApplied);
            o.Caster = Place(o.M, PlaceCell);
            CoreCombatFixtures.Tick(o.M, Ticks);
            Assert.AreEqual(five.Length, o.SpawnedBy(o.Caster.Id).Count, "탄 수 = 반경 안 적 수");
            Assert.AreEqual(o.Spawned.Count, o.SpawnedBy(o.Caster.Id).Count, "다른 주인의 탄 없음");
            foreach (var e in o.In) Assert.AreEqual(Hit, o.DamageTo(e), 1e-3f, $"반경 안 적 {e.Id}");
            Assert.AreEqual(0f, o.DamageTo(o.Out), "반경 밖 무피해");
        }
    }
}
