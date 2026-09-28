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
    // 하드 케이스 4 탐침(2026-09-28) — 「적 유닛이 드림캐쳐 규칙을 **가졌다고 치면** 효과가 발동하나」.
    //
    // 부착 가능 여부(카드 부착 게이트 · 빌더 조합 검증)는 **논외**다. 카드 규칙 줄(`BindingOrigin.Card`)을 코어 API
    // (`BindingRegistry.Attach`)로 적 숙주에 직접 붙이고, 그 뒤 사건·효과가 어떻게 흐르는지만 본다.
    //
    //   1 별똥 타격(AttackN 1 × ProjectileToTarget · 하늘 낙하 운석) — 적이 방어유닛을 때릴 때마다
    //   2 코스트 획득(PeriodicTimer × GainCost) — 누구의 코스트가 느나
    //   3 개사기(OnPlace · Any · PlacedDefender × EmitProjectilePattern 융단 호밍) — 남의 배치에 누가 누구를 쏘나
    //   4 대조군 — 적이 든 자기 배치 규칙(OnPlace · Self)은 영영 안 터진다
    //
    // ⚠ 초록 = 「현 구조가 그 문장을 만족한다」. `[Ignore]` = 「현 구조로는 그 문장이 성립하지 않는다」 — 사유에 막힌 지점
    // (파일:줄)을 적었다. 「사용자 결정 필요」가 붙은 것은 **규칙의 성질**이 정해지지 않아 구현이 답을 못 고르는 자리다.
    // `현행_` 으로 시작하는 테스트는 **지금의 동작을 박제한 증언**이다 — 코어가 고쳐지면 빨개지는 것이 정상이고,
    // 그때 짝이 되는 `[Ignore]` 를 풀면 된다.
    //
    // ⚠ 여기 수치는 게임 값이 아니라 픽스처다.
    [TestFixture]
    public class HardCaseDreamcatcherOnEnemyProbeTests
    {
        // ── 1. 별똥 타격 ────────────────────────────────────────────────────

        private const int MeteorTiles = 1;
        private const float MeteorHit = 30f;
        private const float MeteorFall = 0.5f;

        // 판 12×5 · 웨이브 스폰 x=0(웨이브 적은 속도 0 · 규칙 없음 — 아래 칸들은 그 사거리 밖).
        // 자리형 도달(운석) = 1 + 칸 반폭 0.5 + 대상 몸(방어유닛 0.5) = 2.0.
        private static readonly int2 ECell = new int2(4, 2);                              // 규칙을 든 적
        private static readonly int2 DCell = new int2(3, 2);                              // 맞는 방어유닛(E 에서 1)
        private static readonly int2[] NearDefCells = { new int2(2, 2), new int2(2, 3) }; // D 에서 1 · √2(안)
        private static readonly int2 FarDefCell = new int2(5, 4);                         // D 에서 2.83(밖) · E 사거리 밖

        private static MatchDefinition MeteorDefinition()
        {
            // 방어유닛은 안 때린다(피해 0) · 적은 근접 1(운석 피해와 금액으로 가른다).
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, enemyDamage: 1f, enemyRange: 1f, enemyHealth: 100000f);
            var meteor = ProjectileDef.Default();
            meteor.Id = "probe_meteor";
            meteor.Movement = (int)MovementKind.SkyFall;
            meteor.Payload = (int)PayloadKind.TileAoe;
            meteor.ImpactTileRange = MeteorTiles;
            meteor.SplashDamageMul = 1f;
            def.Projectiles = new[] { meteor };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>드림캐쳐 「별똥 타격」 카드 규칙 줄 — 하드 케이스 2 의 타격 운석과 같은 모양 + 낙하 0.5초.</summary>
        private static BindingDef StarfallRow()
        {
            var r = CoreCardFixtures.CardRule(TriggerKind.AttackN, EffectKind.ProjectileToTarget);
            r.Label = "별똥 타격(적 소유)";
            r.Subject = BindingSubject.Self;
            r.Period = 1;
            r.Magnitude = MeteorHit;
            r.TileRange = MeteorTiles;
            r.Duration = MeteorFall;
            r.DataIndex = 0;
            r.VisualScale = 1f;
            return r;
        }

        private static float DamageTo(List<CoreEvent> hits, SimEntityId id, float amount)
        {
            float s = 0f;
            foreach (var h in hits) if (h.B == id && math.abs(h.Amount - amount) < 1e-3f) s += h.Amount;
            return s;
        }

        [Test]
        public void 별똥_타격을_든_적이_방어유닛을_때리면_그_방어유닛_자리에_운석이_떨어져_반경_안_방어유닛만_맞는다()
        {
            var def = MeteorDefinition();
            int row = Add(def, StarfallRow())[0];
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreCardFixtures.CardBattle(def);
            var d = SpawnDefender(m, DCell);
            var near = new List<SimEntityId>();
            foreach (var c in NearDefCells) near.Add(SpawnDefender(m, c).Id);
            var far = SpawnDefender(m, FarDefCell).Id;
            var e = SpawnEnemy(m, ECell);
            SimEntityId dId = d.Id, eId = e.Id;
            Assert.IsNotNull(m.Bindings.Attach(e, in def.Bindings[row], row, m.Clock.Tick), "적 숙주에 부착(코어 API)");

            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            var hits = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);
            var attacks = CoreCombatFixtures.Listen(m, CoreEventKind.AttackResolved);
            const int Attacks = 3;
            for (int t = 0; t < 600 && attacks.FindAll(a => a.A == eId).Count < Attacks; t++) m.Tick();
            Assert.AreEqual(Attacks, attacks.FindAll(a => a.A == eId).Count, "E 가 D 를 때린 횟수");
            CoreCombatFixtures.Tick(m, 40);   // 마지막 운석의 낙하(0.5초) 여유 — 평타 주기(60 틱)보다 짧다

            var mine = spawned.FindAll(s => s.B == eId);
            Assert.AreEqual(Attacks, mine.Count, "타격마다 운석 하나(귀속 E)");
            foreach (var s in mine)
            {
                Assert.AreEqual(Faction.EnemyUnit, s.Faction, "탄 진영 = 숙주(적)");
                Assert.AreEqual(d.Position.x, s.SiteFired.Pos.x, 1e-4f, "자리 = 맞은 방어유닛 D");
                Assert.AreEqual(d.Position.z, s.SiteFired.Pos.z, 1e-4f);
                Assert.AreEqual(0f, s.SiteFired.OriginBody, "자리형 — 원점의 몸 0(제약 13)");
            }
            Assert.AreEqual(Attacks * MeteorHit, DamageTo(hits, dId, MeteorHit), 1e-3f, "D 자신");
            foreach (var id in near) Assert.AreEqual(Attacks * MeteorHit, DamageTo(hits, id, MeteorHit), 1e-3f, $"반경 안 방어유닛 {id}");
            Assert.AreEqual(0f, DamageTo(hits, far, MeteorHit), "반경 밖 방어유닛 무피해");
            Assert.AreEqual(0f, DamageTo(hits, eId, MeteorHit), "반경 안 적(E 자신)은 무피해 — 시전자 상대 진영만");
            Assert.IsTrue(hits.FindAll(h => math.abs(h.Amount - MeteorHit) < 1e-3f).TrueForAll(h => h.A == eId), "운석 피해 출처 = E");
        }

        // ── 2. 코스트 획득 ──────────────────────────────────────────────────

        private const float CostGain = 2f;

        private static BattleMatch CostArena(out List<CoreEvent> fired)
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.Cost = new CostDef { Start = 0f, Max = 100f, RegenPerSec = 0f };   // 재생 0 — 늘면 규칙 몫이다
            var r = CoreCardFixtures.CardRule(TriggerKind.PeriodicTimer, EffectKind.GainCost);
            r.Label = "코스트 획득(적 소유)";
            r.PeriodSeconds = 1f;
            r.Magnitude = CostGain;
            int row = Add(def, r)[0];
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            var host = SpawnEnemy(m, new int2(6, 2));
            Assert.IsNotNull(m.Bindings.Attach(host, in def.Bindings[row], row, m.Clock.Tick), "적 숙주에 부착(코어 API)");
            fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);
            Assert.AreEqual(0f, m.Cost.Current, 1e-6f, "시작 코스트 0");
            CoreCombatFixtures.Tick(m, 130);
            return m;
        }

        [Test]
        public void 현행_적이_든_코스트_획득은_플레이어_코스트를_늘린다()
        {
            // `GainCostSkill`(`MetaSkills.cs:26-30`)은 시전자를 안 싣는 `MetaIntent`(`SkillIntent.cs:216-220` — Kind · Amount 뿐)를
            // 내고, 적용(`IntentApplier.cs:558-559`)은 숙주 진영을 묻지 않고 판의 유일한 코스트 담당자(플레이어)에 더한다.
            var m = CostArena(out var fired);
            Assert.Greater(fired.Count, 0, "적 숙주에서도 주기 규칙이 발화한다");
            Assert.AreEqual(CostGain * fired.Count, m.Cost.Current, 1e-3f, "발화마다 **플레이어** 코스트 +2");
        }

        [Test]
        [Ignore("성립하지 않는다 · 사용자 결정 필요(설계 구멍 — 미결) — 코스트는 플레이어 자원이고 적(숙주)에게는 자원 개념이 없다. "
              + "`MetaIntent`(`SkillIntent.cs:216-220`)는 시전자·진영을 안 싣고 `IntentApplier.cs:558-559` 가 무조건 `CostLedger.Gain` "
              + "(`CostLedger.cs:98`)으로 보낸다. 적이 들면 «무효 / 플레이어 코스트를 뺏는다 / 적 전용 자원» 중 무엇인지 정해지지 않았다.")]
        public void 적이_든_코스트_획득은_플레이어_코스트를_늘리지_않는다()
        {
            var m = CostArena(out _);
            Assert.AreEqual(0f, m.Cost.Current, 1e-6f);
        }

        // ── 3. 개사기(남의 배치 상속 융단폭격) ─────────────────────────────

        private const int Scope = 3;
        private const float ShotHit = 100f;
        private static readonly int2 PlaceCell = new int2(6, 2);
        private static readonly int2[] InCells = { new int2(7, 2), new int2(6, 4) };   // 배치 칸에서 1 · 2(스코프 3 안)
        private static readonly int2 HostCell = new int2(10, 4);                        // 숙주 적 — 배치 칸에서 4.5(밖)

        private static MatchDefinition BarrageDefinition()
        {
            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            def.Units[0].MaxOnBoard = 8;
            for (int i = 0; i < def.Enemies.Length; i++)
            {
                def.Enemies[i].MoveSpeed = 0f;
                def.Enemies[i].Health = ShotHit * 0.5f;   // 한 발에 죽는다 — 킬 귀속을 본다
            }
            var missile = ProjectileDef.Default();
            missile.Id = "probe_homing_missile";
            missile.Movement = (int)MovementKind.HomingToEntity;
            missile.Payload = (int)PayloadKind.SingleSplash;
            missile.SplashDamageMul = 0f;
            missile.SplashRadius = 0f;
            missile.Speed = 10f;
            missile.MinFlightTime = 0.05f;
            def.Projectiles = new[] { missile };
            def.Patterns = new[]
            {
                new PatternDef
                {
                    Id = "probe_barrage", BarrelProjectileDefIndex = 0, Damage = ShotHit,
                    Selection = (int)PatternSelectionRule.RoundRobin,
                    Shots = new[] { new PatternShotDef { DirectionT = 0.5f } },
                    ReselectPerShot = true, TelegraphSec = 0f, ScopeTileRange = Scope,
                    FanOutToAllCandidates = true,
                },
            };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>개사기 카드 규칙 줄 — 하드 케이스 A/AA 의 `CardRow` 와 같은 모양(스코프 3).</summary>
        private static BindingDef GaesagiRow()
        {
            var r = CoreCardFixtures.CardRule(TriggerKind.OnPlace, EffectKind.EmitProjectilePattern);
            r.Label = "개사기(적 소유)";
            r.Subject = BindingSubject.Any;
            r.SubjectFilter = BindingSubjectFilter.PlacedDefender;
            r.Lifetime = BindingLifetime.Owner;
            r.PatternDefIndex = 0;
            r.TileRange = Scope;
            r.Magnitude = ShotHit;
            return r;
        }

        private sealed class BarrageObs
        {
            public BattleMatch M;
            public SimEntityId Host, Placed;
            public List<SimEntityId> In = new List<SimEntityId>();
            public List<CoreEvent> Spawned, Hits, Fired;
            public List<int> OnKillCasters = new List<int>();
        }

        private static BarrageObs RunBarrage()
        {
            var def = BarrageDefinition();
            var o = new BarrageObs();
            var onKill = new ProbeSkill { OnExecute = (c, t, p, ctx) => o.OnKillCasters.Add(c.Unit.Value) };
            GiveUnit(def, 0, Probe(TriggerKind.OnKill, onKill));
            GiveEnemy(def, 0, Probe(TriggerKind.OnKill, onKill));   // 숙주(적)도 OnKill 을 든다 — 킬이 숙주 몫이면 울린다
            int row = Add(def, GaesagiRow())[0];
            def.ConfigHash = def.ComputeConfigHash();
            o.M = CoreMatchFixtures.BeginBattle(def);
            foreach (var c in InCells) o.In.Add(SpawnEnemy(o.M, c).Id);
            var host = SpawnEnemy(o.M, HostCell);
            o.Host = host.Id;
            Assert.IsNotNull(o.M.Bindings.Attach(host, in def.Bindings[row], row, o.M.Clock.Tick), "적 숙주에 부착(코어 API)");
            o.Spawned = CoreCombatFixtures.Listen(o.M, CoreEventKind.ProjectileSpawned);
            o.Hits = CoreCombatFixtures.Listen(o.M, CoreEventKind.DamageApplied);
            o.Fired = CoreCombatFixtures.Listen(o.M, CoreEventKind.TriggerFired);
            Assert.AreEqual(RejectReason.None, o.M.Apply(Command.PlaceDefender(0, PlaceCell)).Reason, "배치");
            var units = o.M.World.Units;
            o.Placed = units[units.Count - 1].Id;
            CoreCombatFixtures.Tick(o.M, 90);
            return o;
        }

        [Test]
        public void 현행_적이_든_개사기는_배치된_방어유닛이_적에게_쏘고_킬도_방어유닛_몫이다()
        {
            // 「남의 배치」 규칙은 소유자 진영을 묻지 않고 판 위 모든 유닛의 `Any` 규칙을 모은다(`TriggerDispatcher.cs:254-270`).
            // 발동 주체 = 사건 주체(배치된 유닛 · U3) — 숙주가 적이어도 탄은 **배치된 방어유닛**에서 적에게 나간다.
            var o = RunBarrage();
            Assert.Greater(o.Fired.Count, 0, "적 숙주의 Any 규칙이 방어유닛 배치에 발화한다");
            Assert.AreEqual(InCells.Length, o.Spawned.FindAll(s => s.B == o.Placed).Count, "탄 주인 = 배치된 방어유닛 · 스코프 안 적 수만큼");
            Assert.AreEqual(0, o.Spawned.FindAll(s => s.B == o.Host).Count, "숙주(적)가 쏜 탄 0");
            Assert.IsTrue(o.Spawned.TrueForAll(s => s.Faction == Faction.DefenderUnit), "탄 진영 = 방어유닛");
            foreach (var id in o.In) Assert.IsNull(o.M.World.Find(id), $"스코프 안 적 {id} 처치");
            Assert.IsNotNull(o.M.World.Find(o.Host), "숙주(적)는 스코프 밖 — 무사");
            Assert.IsTrue(o.Hits.TrueForAll(h => h.A == o.Placed), "피해 출처 = 배치된 방어유닛");
            int placed = CoreSkillContext.ToSkill(o.Placed).Value, host = CoreSkillContext.ToSkill(o.Host).Value;
            Assert.AreEqual(InCells.Length, o.OnKillCasters.FindAll(c => c == placed).Count, "킬마다 방어유닛의 OnKill(U3)");
            Assert.AreEqual(0, o.OnKillCasters.FindAll(c => c == host).Count, "숙주(적)의 OnKill 은 무발화");
        }

        [Test]
        public void 현행_조합_검증은_적_숙주의_남의_배치_규칙을_영영_안_터진다고_답한다_코어는_터진다()
        {
            // 검증의 ①(`EffectComboRule.cs:52` → `SkillRouting.HasDetector` `SkillRouting.cs:196-198` — 적 × OnPlace = 감지자 없음)이
            // 주체 축(`Any`)을 보기 **전에** 거절한다. 그러나 `Any` 의 사건은 숙주가 아니라 남(방어유닛)의 배치라 코어는 발화한다(위).
            // 검증과 코어가 어긋난다 — 라이브 영향 0(적이 카드 규칙을 드는 경로가 없다).
            var c = new EffectCombo
            {
                Trigger = TriggerKind.OnPlace, Subject = BindingSubject.Any, Payload = EffectKind.EmitProjectilePattern,
                HasProjectile = true, Binding = BindingClass.Entity, FanOut = true, HostIsEnemy = true,
            };
            Assert.AreEqual(ComboVerdict.NeverFires, EffectComboRule.Check(in c), "검증 = 영영 안 터짐");
            var o = RunBarrage();
            Assert.Greater(o.Spawned.Count, 0, "코어 = 터진다");
        }

        [Test]
        [Ignore("사용자 결정 필요 — 적이 든 «남의 배치» 규칙의 효과가 누구 편인가. 현행 발동 주체 = 사건 주체(U3 — 배치된 방어유닛, "
              + "`TriggerDispatcher.cs:254-270` 이 소유자 진영을 안 본다)라 숙주(적) 편이 아니다: 탄은 방어유닛에서 적에게 나가고 킬도 "
              + "방어유닛 몫이다(위 `현행_`). «숙주 편»이면 발동 주체·진영·귀속 중 무엇이 숙주를 따르는지부터 정해야 한다.")]
        public void 적이_든_남의_배치_규칙의_효과는_숙주_편이다()
        {
            var o = RunBarrage();
            Assert.IsTrue(o.Spawned.TrueForAll(s => s.Faction == Faction.EnemyUnit), "탄 진영 = 숙주(적)");
            foreach (var id in o.In) Assert.IsNotNull(o.M.World.Find(id), "적은 맞지 않는다");
        }

        // ── 4. 대조군 — 적의 자기 배치 ─────────────────────────────────────

        [Test]
        public void 적이_든_자기_배치_규칙은_검증도_영영_안_터진다고_답하고_실제로도_안_터진다()
        {
            var c = new EffectCombo
            {
                Trigger = TriggerKind.OnPlace, Subject = BindingSubject.Self, Payload = EffectKind.EmitProjectilePattern,
                HostIsEnemy = true,
            };
            Assert.AreEqual(ComboVerdict.NeverFires, EffectComboRule.Check(in c), "적 × OnPlace Self = 감지자 없음");

            var def = CoreMatchFixtures.Definition();
            def.Units[0].Cost = 0;
            def.Units[0].MaxOnBoard = 8;
            var probe = new ProbeSkill();
            var r = CoreCardFixtures.CardProbe(TriggerKind.OnPlace, probe);
            r.Subject = BindingSubject.Self;
            int row = Add(def, r)[0];
            def.ConfigHash = def.ComputeConfigHash();
            var m = CoreMatchFixtures.BeginBattle(def);
            var fired = CoreCombatFixtures.Listen(m, CoreEventKind.TriggerFired);
            var host = SpawnEnemy(m, new int2(8, 2));
            Assert.IsNotNull(m.Bindings.Attach(host, in def.Bindings[row], row, m.Clock.Tick), "적 숙주에 부착(코어 API)");
            Assert.AreEqual(RejectReason.None, m.Apply(Command.PlaceDefender(0, new int2(6, 2))).Reason, "남의 배치");
            SpawnEnemy(m, new int2(8, 3));   // 적 스폰은 배치 사건이 아니다
            CoreCombatFixtures.Tick(m, 120);
            Assert.AreEqual(0, probe.Count, "실행 0");
            Assert.AreEqual(0, fired.Count, "발화 사건 0");
        }
    }
}
