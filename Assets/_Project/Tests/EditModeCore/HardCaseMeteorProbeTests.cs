using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using Wassup.Skills.Concrete;
using static Wassup.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // 하드 케이스 2 탐침(2026-09-26) — 「운석 = 자리에 떨어지는 광역 탄」 로직이 하나이고
    // 트리거(플레이어 시전 vs 타격 사건) · 자리 원천(지정 칸 vs 맞은 적의 위치) · 피해 값만 다른가.
    //
    //   액티브 「운석 소환」 = 지정 칸에 운석 1개 · N 범위 피해 100(`TileMeteorSkill`).
    //   드림캐쳐 「타격 운석」 = 부착 유닛이 적을 때릴 때마다 **그 적의 위치**에 운석 1개 · N 범위 피해 30
    //   (`AttackN(1) × ProjectileToTarget` → `TargetProjectileSkill` · 탄 줄 = 같은 운석).
    //
    // 정의표는 코어 API 로 손조립한다(빌더 없음). 저작 경로(`SkillData` · `DcMechanic` → 빌더)는 Unity 층이라
    // 헤드리스에서 돌지 않는다 — **빌더가 만들 수 있는 모양**은 `저작_가능한_타격_운석…` 테스트가 손으로 재현해 박제한다
    // (`SkyFallOnTarget` = 대상 낙하 · 단일 비산 — 자리 운석과 형이 다르다 · 비산은 같은 자리형 자 `SkillMath.ReachFromImpact`).
    //
    // ⚠ 초록 = 「현 구조가 그 문장을 만족한다」. `[Ignore]` = 「현 구조로는 그 문장이 성립하지 않는다」(사유에 파일:줄).
    // (탐침 당시의 `현행_` 박제와 짝 `[Ignore]` 는 unified-effect-layer 에서 풀리거나 지워졌다 — 남은 단언은 현 계약이다.)
    //
    // ⚠ 여기 수치는 게임 값이 아니라 픽스처다.
    //
    // `unified-effect-layer` 완료 기준 — 이 파일의 `[Ignore]` 해제가 그 spec 의 완료 기준이다(`docs/spec/unified-effect-layer/`).
    //   반경·예고 → unit 1(발사 요청 조립 한 갈래) · 낙하 그림 → unit 4(탄 그림은 사건만으로).
    [TestFixture]
    public class HardCaseMeteorProbeTests
    {
        public const int N = 1;
        private const float ActiveHit = 100f;
        public const float OnHitHit = 30f;
        private const float MeleeHit = 1f;       // D 의 평타(운석 피해와 금액으로 가른다)
        public const float WarningSec = 0.5f;   // 액티브 낙하 예고 = 비행 시간(30 틱)

        // 판 12×5(`CoreCombatFixtures.Definition`). 적은 속도 0 — 제자리에 선다.
        // 자리형 도달 = N + 칸 반폭 0.5 + 대상 몸 0.25 = 1.75 칸.
        private static readonly int2 DCell = new int2(3, 2);                               // 근접 D(사거리 1 → 1 + 0.5 + 0.25)
        private static readonly int2 ECell = new int2(4, 2);                               // 운석 자리(액티브 조준 칸 = 타격 대상 E)
        private static readonly int2[] NearCells = { new int2(5, 2), new int2(5, 3) };     // E 에서 1 · √2(안) — D 에서 2 · √5(D 사거리 밖)
        private static readonly int2 FarCell = new int2(6, 2);                             // E 에서 2(밖 — 1.75 < 2)

        private const int ThinEnemy = 0;   // 몸 0.25
        private const int FatEnemy = 1;    // 몸 0.6 — 경계 판정에 몸이 어디 붙는지 가른다

        // ── 정의표 ──────────────────────────────────────────────────────────

        /// <summary>운석 탄 한 줄(줄 0) + 근접 D + 적 두 줄(몸 0.25 · 0.6).</summary>
        private static MatchDefinition Definition(int defImpactTiles = N, MovementKind movement = MovementKind.SkyFall,
                                                  PayloadKind payload = PayloadKind.TileAoe, float splashRadius = 0f)
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: MeleeHit, defenderRange: 1f, enemyHealth: 100000f);
            var meteor = ProjectileDef.Default();
            meteor.Id = "probe_meteor";
            meteor.Movement = (int)movement;
            meteor.Payload = (int)payload;
            meteor.ImpactTileRange = defImpactTiles;
            meteor.SplashRadius = splashRadius;
            meteor.SplashDamageMul = 1f;
            def.Projectiles = new[] { meteor };

            var fat = def.Enemies[0];
            fat.Id = "probe_fat_enemy";
            fat.BodyRadius = 0.6f;
            def.Enemies = new[] { def.Enemies[0], fat };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        /// <summary>액티브 「운석 소환」 규칙(빌더 `BakeActive` 의 Meteor 갈래와 같은 칸들).</summary>
        private static RuleRow ActiveMeteorRule(int tiles = N)
        {
            Assert.IsTrue(SkillRouting.Registry.TryGet(TileMeteorSkill.Id, out var skill));
            var r = CoreCardFixtures.CardProbe(TriggerKind.None, skill);
            r.Rule.Label = "액티브 운석 소환";
            r.Rule.FireCap = 1;
            r.Rule.Lifetime = BindingLifetime.UntilFireCap;
            r.Effect.Magnitude = ActiveHit;
            r.Effect.TileRange = tiles;
            r.Effect.DataIndex = 0;              // 운석 탄 줄
            r.Effect.Duration = WarningSec;      // 메테오만 지속 = 낙하 예고
            r.Effect.VisualScale = 1f;
            return r;
        }

        /// <summary>드림캐쳐 「타격 운석」 규칙 — 매 타격 · 대상에게 탄(같은 운석 줄). 빌더 픽스처(`UnifiedEffectBakeFixtureTests`)가 굽힌 줄과 대조한다.</summary>
        public static RuleRow OnHitMeteorRule(int tiles = N, MovementKind movement = 0, PayloadKind payload = 0)
        {
            var r = CoreCardFixtures.CardRule(TriggerKind.AttackN, EffectKind.ProjectileToTarget);
            r.Rule.Label = "타격 운석";
            r.Rule.Subject = BindingSubject.Self;
            r.Rule.Period = 1;
            r.Effect.Magnitude = OnHitHit;
            r.Effect.TileRange = tiles;
            r.Effect.DataIndex = 0;              // 액티브와 **같은** 탄 줄
            r.Effect.VisualScale = 1f;
            r.Effect.ProjectileMovement = (int)movement;   // 0 = 탄 정의의 궤적을 쓴다
            r.Effect.ProjectilePayload = (int)payload;
            return r;
        }

        // ── 관측 ────────────────────────────────────────────────────────────

        private sealed class Obs
        {
            public BattleMatch M;
            public Unit D;
            public Unit E;
            public List<Unit> Near = new List<Unit>();
            public Unit Far;
            public List<CoreEvent> Spawned = new List<CoreEvent>();
            public List<CoreEvent> Despawned;   // 탄 뷰는 같은 배달 묶음에 소멸 사건이 온 탄을 안 세운다(unit 4)
            public List<CoreEvent> ProjHits;
            public List<CoreEvent> Damage;
            public List<CoreEvent> Attacks;

            public float MeteorDamageTo(Unit u, float amount)
            {
                float sum = 0f;
                foreach (var h in Damage) if (h.B == u.Id && math.abs(h.Amount - amount) < 1e-3f) sum += h.Amount;
                return sum;
            }

            public List<CoreEvent> MeteorHits(float amount) => Damage.FindAll(h => math.abs(h.Amount - amount) < 1e-3f);
        }

        private static Obs Arena(MatchDefinition def, int anchorEnemy = ThinEnemy, int farEnemy = ThinEnemy, bool withD = false)
        {
            var o = new Obs { M = CoreCardFixtures.CardBattle(def) };
            if (withD) o.D = SpawnDefender(o.M, DCell);
            o.E = Spawn(o.M, anchorEnemy, ECell);
            foreach (var c in NearCells) o.Near.Add(Spawn(o.M, ThinEnemy, c));
            o.Far = Spawn(o.M, farEnemy, FarCell);

            var m = o.M;
            m.Bus.Subscribe(CoreEventKind.ProjectileSpawned, 0, e => o.Spawned.Add(e));
            o.Despawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileDespawned);
            o.ProjHits = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileHit);
            o.Damage = CoreCombatFixtures.Listen(m, CoreEventKind.DamageApplied);
            o.Attacks = CoreCombatFixtures.Listen(m, CoreEventKind.AttackResolved);
            return o;
        }

        private static Unit Spawn(BattleMatch m, int enemyRow, int2 cell)
        {
            m.Apply(Command.DebugSpawnEnemy(enemyRow, cell));
            var units = m.World.Units;
            return units[units.Count - 1];
        }

        private static Obs RunActive(MatchDefinition def, int farEnemy = ThinEnemy, int tiles = N)
        {
            int card = CoreCardFixtures.AddActiveCard(def, "probe_meteor_active", 0, 0f, ActiveMeteorRule(tiles));
            var o = Arena(def, farEnemy: farEnemy);
            Assert.IsTrue(o.M.Apply(Command.CastActive(CoreCardFixtures.EntryOf(o.M, card), ECell)).Accepted, "시전 성사");
            CoreCombatFixtures.Tick(o.M, 60);
            return o;
        }

        private static Obs RunOnHit(MatchDefinition def, int anchorEnemy = ThinEnemy, int attacks = 3,
                                    RuleRow? rule = null)
        {
            int row = Add(def, rule ?? OnHitMeteorRule())[0];
            def.ConfigHash = def.ComputeConfigHash();
            var o = Arena(def, anchorEnemy: anchorEnemy, withD: true);
            Assert.IsNotNull(o.M.Bindings.Attach(o.D, in def.Bindings[row], row, o.M.Clock.Tick), "부착");
            for (int t = 0; t < 600 && CountBy(o.Attacks, o.D.Id) < attacks; t++) o.M.Tick();
            Assert.AreEqual(attacks, CountBy(o.Attacks, o.D.Id), "D 가 E 를 때린 횟수");
            CoreCombatFixtures.Tick(o.M, 5);   // 마지막 타격의 운석이 떨어질 여유(평타 주기 60 틱보다 짧다)
            return o;
        }

        private static int CountBy(List<CoreEvent> events, SimEntityId a) => events.FindAll(e => e.A == a).Count;

        // ── 1. 액티브 운석 ──────────────────────────────────────────────────

        [Test]
        public void 액티브_운석은_지정_칸_N_안_적에게_100_밖은_무피해_귀속은_시전자_없음()
        {
            var o = RunActive(Definition());
            Assert.AreEqual(1, o.Spawned.Count, "운석 1개");
            var s = o.Spawned[0];
            Assert.AreEqual(o.M.Map.CenterOf(ECell), s.SiteFired.Pos, "자리 = 지정 칸 중심");
            Assert.AreEqual(0f, s.SiteFired.OriginBody, "자리형 — 원점의 몸 0(칸 반폭 폴백)");
            Assert.AreEqual(MovementKind.SkyFall, (MovementKind)s.Arg);
            Assert.IsTrue(s.B.IsNone, "⚠ 귀속 = 없음 — 액티브는 시전자가 없다(`TileMeteorSkill.cs:25` · 사건 주어 = 판 `CardBindings.cs:128-135`)");
            Assert.AreEqual(Faction.DefenderUnit, s.Faction, "진영만 플레이어(방어유닛)로 접힌다");

            Assert.AreEqual(ActiveHit, o.MeteorDamageTo(o.E, ActiveHit), 1e-3f, "지정 칸의 적");
            foreach (var e in o.Near) Assert.AreEqual(ActiveHit, o.MeteorDamageTo(e, ActiveHit), 1e-3f, $"N 안 {e.Id}");
            Assert.AreEqual(0f, o.MeteorDamageTo(o.Far, ActiveHit), "N 밖(거리 2 > 1.75) 무피해");
            Assert.IsTrue(o.MeteorHits(ActiveHit).TrueForAll(h => h.A.IsNone), "피해 출처 = 없음(판)");
        }

        [Test]
        public void 액티브_운석은_예고_시간만큼_뒤에_떨어진다()
        {
            var def = Definition();
            int card = CoreCardFixtures.AddActiveCard(def, "probe_meteor_active", 0, 0f, ActiveMeteorRule());
            var o = Arena(def);
            o.M.Apply(Command.CastActive(CoreCardFixtures.EntryOf(o.M, card), ECell));
            CoreCombatFixtures.Tick(o.M, 20);
            Assert.AreEqual(1, o.Spawned.Count);
            Assert.AreEqual(0, o.MeteorHits(ActiveHit).Count, "예고 중(0.33초) — 아직 안 떨어졌다");
            CoreCombatFixtures.Tick(o.M, 20);
            Assert.AreEqual(3, o.MeteorHits(ActiveHit).Count, "0.5초 뒤 착탄");
        }

        // 제약 13 자리형: 도달 = |차| ≤ 범위 + 칸 반폭(0.5) + **대상의 몸**. 지정자(여기선 없음)의 몸은 안 붙는다.
        // ⚠ 요청문의 「적의 몸을 키워도 경계가 안 바뀐다」는 제약 13 셋째 항(대상의 몸)과 어긋난다 —
        // 대상의 몸은 **언제나** 붙는다. 안 붙는 것은 원점(지정 칸 / 지정한 유닛)의 몸이다. 아래가 그 두 문장을 가른다.
        [Test]
        public void 액티브_운석_자리형_도달은_범위_더하기_칸반폭_더하기_대상몸이다()
        {
            var thin = RunActive(Definition(), farEnemy: ThinEnemy);
            Assert.AreEqual(0f, thin.MeteorDamageTo(thin.Far, ActiveHit), "거리 2 · 몸 0.25 → 1 + 0.5 + 0.25 = 1.75 < 2 → 밖");

            var fat = RunActive(Definition(), farEnemy: FatEnemy);
            Assert.AreEqual(ActiveHit, fat.MeteorDamageTo(fat.Far, ActiveHit), 1e-3f,
                "거리 2 · 몸 0.6 → 1 + 0.5 + 0.6 = 2.1 ≥ 2 → 안(대상의 몸은 붙는다 — 제약 13)");
            Assert.AreEqual(0f, fat.Spawned[0].SiteFired.OriginBody, "원점 항 = 칸 반폭(몸 0 이 실린다)");
        }

        // ── 2. 타격 운석 ────────────────────────────────────────────────────

        [Test]
        public void 타격_운석은_매_타격마다_맞은_적_위치에_하나씩_떨어져_N_안에_30_밖은_무피해_귀속은_D()
        {
            var o = RunOnHit(Definition());
            var mine = o.Spawned.FindAll(e => e.B == o.D.Id);
            Assert.AreEqual(3, mine.Count, "타격 3회 → 운석 3개");
            foreach (var s in mine)
            {
                Assert.AreEqual(o.E.Position.x, s.SiteFired.Pos.x, 1e-4f, "자리 = 맞은 적 E(호스트 D 가 아니다)");
                Assert.AreEqual(o.E.Position.z, s.SiteFired.Pos.z, 1e-4f);
                Assert.AreEqual(0f, s.SiteFired.OriginBody, "자리형 — 원점의 몸 0");
                Assert.AreEqual(MovementKind.SkyFall, (MovementKind)s.Arg);
            }
            Assert.AreEqual(3 * OnHitHit, o.MeteorDamageTo(o.E, OnHitHit), 1e-3f, "E 자신");
            foreach (var e in o.Near) Assert.AreEqual(3 * OnHitHit, o.MeteorDamageTo(e, OnHitHit), 1e-3f, $"E 주변 N 안 {e.Id}");
            Assert.AreEqual(0f, o.MeteorDamageTo(o.Far, OnHitHit), "N 밖 무피해");
            Assert.IsTrue(o.MeteorHits(OnHitHit).TrueForAll(h => h.A == o.D.Id), "귀속 = D");
        }

        [Test]
        public void 타격_운석의_원점_항은_칸_반폭이다_맞은_적의_몸이_커져도_반경이_안_넓어진다()
        {
            // E 만 몸 0.6. 주변 적(몸 0.25) 경계가 E 의 몸만큼 넓어지면 「몸에서 나오는 것」으로 잘못 접힌 것이다.
            var o = RunOnHit(Definition(), anchorEnemy: FatEnemy);
            Assert.AreEqual(0f, o.MeteorDamageTo(o.Far, OnHitHit), "거리 2 — 원점 항이 E 의 몸(0.6)이면 2.35 로 안이 됐다");
            Assert.IsTrue(o.Spawned.TrueForAll(s => s.SiteFired.OriginBody == 0f));
            foreach (var e in o.Near) Assert.AreEqual(3 * OnHitHit, o.MeteorDamageTo(e, OnHitHit), 1e-3f);
        }

        // unified-effect-layer unit 1 해제 — 요청 조립이 궤적 결합 종류 하나로 갈린다. 칸 결합이면 대상이 있어도
        // 바인딩 반경이 **착탄 반경**이다(옛 대상 갈래는 그것을 재조준 반경으로 썼다 — 짝 `현행_…` 은 삭제).
        [Test]
        public void 타격_운석의_반경은_바인딩_TileRange_다()
        {
            var o = RunOnHit(Definition(defImpactTiles: 0), rule: OnHitMeteorRule(tiles: N));
            Assert.AreEqual(3 * OnHitHit, o.MeteorDamageTo(o.E, OnHitHit), 1e-3f, "E 자신");
            foreach (var e in o.Near) Assert.AreEqual(3 * OnHitHit, o.MeteorDamageTo(e, OnHitHit), 1e-3f, "바인딩 N = 1 이 착탄 반경");
            Assert.AreEqual(0f, o.MeteorDamageTo(o.Far, OnHitHit), "N 밖 무피해");
        }

        // ── 3. 로직 동치 ────────────────────────────────────────────────────

        [Test]
        public void 두_경로는_같은_탄_줄_같은_궤적_같은_페이로드_같은_원점항_같은_사건_사슬로_착탄한다()
        {
            var a = RunActive(Definition());
            var h = RunOnHit(Definition(), attacks: 1);
            var hs = h.Spawned.FindAll(e => e.B == h.D.Id);
            Assert.AreEqual(1, a.Spawned.Count);
            Assert.AreEqual(1, hs.Count);

            // 사건 사슬: ProjectileSpawned → ProjectileHit(TileAoe) → DamageApplied
            Assert.AreEqual(MovementKind.SkyFall, (MovementKind)a.Spawned[0].Arg);
            Assert.AreEqual((MovementKind)a.Spawned[0].Arg, (MovementKind)hs[0].Arg, "사건이 나르는 궤적(낙하 높이 키)");
            Assert.AreEqual(a.Spawned[0].SiteFired.OriginBody, hs[0].SiteFired.OriginBody, "원점 항(자리형 0)");

            var ah = a.ProjHits.Find(e => e.A == a.Spawned[0].A);
            var hh = h.ProjHits.Find(e => e.A == hs[0].A);
            Assert.AreEqual(PayloadKind.TileAoe, ah.Payload);
            Assert.AreEqual(ah.Payload, hh.Payload, "페이로드 = 같은 착탄 해석(`ResolveTileAoe`)");
            Assert.AreEqual(ah.DefIndex, hh.DefIndex, "착탄 VFX 키");
            Assert.AreEqual(ah.AreaTiles, hh.AreaTiles, "광역 반경(탄 정의 N = 바인딩 N 일 때)");
            Assert.AreEqual(3, ah.Arg);
            Assert.AreEqual(ah.Arg, hh.Arg, "피해자 수(E + 주변 2)");

            // 다른 것은 피해 값뿐 — **바인딩 Magnitude** 에서 온다(탄 정의에는 피해 칸이 없다 → 탄 줄 하나로 100/30).
            Assert.AreEqual(ActiveHit, a.Spawned[0].Amount, 1e-3f);
            Assert.AreEqual(OnHitHit, hs[0].Amount, 1e-3f);
        }

        [Test]
        public void 두_경로의_예고와_비행시간은_효과_파라미터가_정한다_저작_0_이면_즉발()
        {
            // unified-effect-layer unit 1 — 요청 조립은 한 갈래(궤적 결합 종류)다. 예고·비행 시간의 차이는 **갈래가 아니라
            // 효과 파라미터**에서 온다: 액티브는 Duration(예고) + Telegraph 를 싣고, 이 픽스처의 타격 운석은 둘 다 0/꺼짐이다.
            var a = RunActive(Definition());
            var h = RunOnHit(Definition(), attacks: 1);
            var hs = h.Spawned.Find(e => e.B == h.D.Id);
            Assert.AreEqual(N, a.Spawned[0].AreaTiles, "액티브 = 착탄 예고 링(`TileMeteorSkill` 의 Telegraph)");
            Assert.AreEqual(0, hs.AreaTiles, "타격 운석 = 예고 꺼짐(U1 기본값)");

            // 비행 시간: 액티브 = 바인딩 Duration(예고), 타격 = 바인딩 Duration 0 → SkyFall 0 = 첫 틱 착탄.
            int spawnTick = hs.Tick;
            var hit = h.ProjHits.Find(e => e.A == hs.A);
            Assert.LessOrEqual(hit.Tick - spawnTick, 1, "저작 0 — 타격 운석은 즉시 떨어진다");
            int aSpawn = a.Spawned[0].Tick;
            var aHit = a.ProjHits.Find(e => e.A == a.Spawned[0].A);
            Assert.GreaterOrEqual(aHit.Tick - aSpawn, 29, "액티브는 0.5초 예고 뒤");
        }

        [Test]
        public void 타격_운석의_낙하_시간은_바인딩_Duration_이다()
        {
            // unit 1 — 칸 결합 탄의 `FlightTime` = 의도 Duration(`TargetProjectileSkill` 이 SkillParams.Duration 을 싣는다).
            var rule = OnHitMeteorRule();
            rule.Effect.Duration = WarningSec;
            var h = RunOnHit(Definition(), rule: rule, attacks: 1);
            var hs = h.Spawned.Find(e => e.B == h.D.Id);
            CoreCombatFixtures.Tick(h.M, 40);
            var hit = h.ProjHits.Find(e => e.A == hs.A);
            Assert.GreaterOrEqual(hit.Tick - hs.Tick, 29, "0.5초 낙하 뒤 착탄");
        }

        // ── 4. 비주얼 공유 ──────────────────────────────────────────────────

        [Test]
        public void 타격_운석에도_착탄_예고가_뜬다()
        {
            // unified-effect-layer unit 2 에서 해제 — 예고는 효과 파라미터다(U1 · 기본 꺼짐). 이 픽스처가 켠다:
            // `BindingDef.Telegraph` → `SkillParams.Telegraph` → `TargetProjectileSkill` 이 의도에 싣고 → 칸 결합 갈래가
            // `TelegraphTileRange` 를 채운다. 저작 칸(빌더)은 unit 5 — 그 전 라이브는 늘 꺼짐(`두_경로의_예고와_…` 가 박제).
            var rule = OnHitMeteorRule();
            rule.Effect.Telegraph = true;
            var h = RunOnHit(Definition(), rule: rule, attacks: 1);
            Assert.AreEqual(N, h.Spawned.Find(e => e.B == h.D.Id).AreaTiles);
        }

        [Test]
        public void 착탄_그림_키는_두_경로에서_같다_탄_줄과_운석_폭발()
        {
            // 착탄 VFX = `ProjectileHit.DefIndex`(`CoreProjectileViewPool.PlayHitFromEvent`) · 운석 폭발 = TileAoe && AreaTiles > 0
            // (`CoreVfxSpawner.cs:285`). 둘 다 사건 값이라 탄이 이미 사라졌어도 선다.
            var a = RunActive(Definition());
            var h = RunOnHit(Definition(), attacks: 1);
            var hs = h.Spawned.Find(e => e.B == h.D.Id);
            var ah = a.ProjHits[0];
            var hh = h.ProjHits.Find(e => e.A == hs.A);
            Assert.AreEqual(0, ah.DefIndex);
            Assert.AreEqual(ah.DefIndex, hh.DefIndex, "같은 탄 줄 → 같은 착탄 프리팹");
            Assert.IsTrue(ah.Payload == PayloadKind.TileAoe && ah.AreaTiles > 0, "액티브 — 운석 폭발 그림 조건");
            Assert.IsTrue(hh.Payload == PayloadKind.TileAoe && hh.AreaTiles > 0, "타격 — 운석 폭발 그림 조건");
        }

        // unified-effect-layer unit 4 에서 해제 — 탄 뷰는 **사건만으로** 그린다: 비행 프리팹 키 = `ProjectileSpawned.DefIndex`,
        // 그리는 조건 = 「같은 배달 묶음(드라이버 한 프레임 = 틱 여럿)에 소멸 사건이 안 왔다」(`CoreProjectileViewPool` 보류 생성).
        // 월드 탄을 되찾지 않는다. 타격 운석은 unit 1 뒤 낙하 시간 = 바인딩 Duration 이라 예고를 저작하면 그림이 선다.
        [Test]
        public void 타격_운석도_떨어지는_운석_그림이_뜬다()
        {
            var rule = OnHitMeteorRule();
            rule.Effect.Duration = WarningSec;
            var h = RunOnHit(Definition(), rule: rule, attacks: 1);
            var hs = h.Spawned.Find(e => e.B == h.D.Id);
            Assert.AreEqual(0, hs.DefIndex, "사건이 탄 정의 줄(운석)을 나른다 — 뷰가 비행 프리팹을 고르는 키");
            Assert.IsFalse(h.Despawned.Exists(e => e.A == hs.A && e.Tick == hs.Tick),
                "생성 틱에 소멸하지 않는다 → 배달 묶음 끝에 살아 있다 → 낙하 그림");
        }

        [Test]
        public void 즉발_운석은_사건이_탄_줄을_나르지만_같은_틱에_소멸해_낙하_그림_없이_착탄_연출만_액티브는_산다()
        {
            // 라이브 그림 무변(unit 4) — 비행 0 × 프리팹 있는 탄(`census.md` 표 1 ★ · 자리 폭발 카드 6장)과 같은 모양.
            // 키는 사건에 있어도 소멸이 같은 묶음에 오므로 뷰는 비행 그림을 안 세운다(오늘과 같다) · 착탄 연출은 `ProjectileHit`.
            var a = RunActive(Definition());
            var h = RunOnHit(Definition(), attacks: 1);
            var aS = a.Spawned[0];
            var hs = h.Spawned.Find(e => e.B == h.D.Id);
            Assert.AreEqual(0, aS.DefIndex);
            Assert.AreEqual(0, hs.DefIndex);
            Assert.IsFalse(a.Despawned.Exists(e => e.A == aS.A && e.Tick == aS.Tick), "액티브 — 예고 0.5초 동안 산다 → 낙하 그림");
            Assert.IsTrue(h.Despawned.Exists(e => e.A == hs.A && e.Tick == hs.Tick),
                "타격 운석(Duration 0) — 생성 틱에 소멸 → 낙하 그림 없음");
            Assert.IsTrue(h.ProjHits.Exists(e => e.A == hs.A && e.Tick == hs.Tick), "착탄 연출은 같은 틱 착탄 사건이 나른다");
        }

        // ── 5. 저작 경로 — 빌더가 만들 수 있는 모양의 재현 ────────────────

        [Test]
        public void 액티브는_탄_정의의_궤적을_무시하고_언제나_하늘_낙하_칸_광역이다_같은_SO_공유_가능()
        {
            // 빌더가 받아 주는 타격 탄은 엔티티 바인딩뿐이다(아래). 그 SO 를 액티브가 가리켜도 `TileMeteorSkill` 이 의도에
            // SkyFall × TileAoe 를 **명시**해 탄 정의를 덮는다(unit 1 — 궤적 = 의도 명시 > 탄 정의 · 옛 applier 강제는 은퇴)
            // — 두 저작이 **같은 ProjectileData SO** 를 가리키는 것 자체는 된다.
            var def = Definition(movement: MovementKind.SkyFallOnEntity, payload: PayloadKind.SingleSplash, splashRadius: 1.5f);
            var a = RunActive(def);
            Assert.AreEqual(MovementKind.SkyFall, (MovementKind)a.Spawned[0].Arg);
            Assert.AreEqual(PayloadKind.TileAoe, a.ProjHits[0].Payload);
            Assert.AreEqual(3, a.ProjHits[0].Arg, "E + 주변 2");
        }

        [Test]
        public void 저작_가능한_타격_운석은_대상_낙하_단일_비산이라_자리_운석과_형이_다르고_비산_자는_같다()
        {
            // (탐침 당시) 빌더는 `ProjectileToTarget` 의 셀 바인딩 탄(SkyFall)을 거절했다 — unit 5 가 풀었다(`EffectComboRule`).
            // 받아 주는 운석형 궤적은 `SkyFallOnTarget` 하나 → (SkyFallOnEntity, SingleSplash)(`CombatDefinitionBuilder.cs:417-418`)
            // 이고 빌더는 그 축을 규칙에 싣는다(`CardDefinitionBuilder.cs:321-322`). 그 모양을 손으로 재현한다.
            var def = Definition(movement: MovementKind.SkyFallOnEntity, payload: PayloadKind.SingleSplash, splashRadius: N);
            var rule = OnHitMeteorRule(movement: MovementKind.SkyFallOnEntity, payload: PayloadKind.SingleSplash);
            var h = RunOnHit(def, rule: rule, attacks: 1);
            int hi = h.Spawned.FindIndex(e => e.B == h.D.Id);
            var hh = h.ProjHits.Find(e => e.A == h.Spawned[hi].A);

            Assert.AreEqual(MovementKind.SkyFallOnEntity, (MovementKind)h.Spawned[hi].Arg, "⚠ 궤적이 액티브(SkyFall)와 다르다");
            Assert.AreEqual(PayloadKind.SingleSplash, hh.Payload, "⚠ 페이로드가 다르다 — 직격 + 비산(`ResolveSingleSplash`)");
            Assert.AreEqual(0, hh.AreaTiles, "⚠ 광역 반경 0 → 운석 폭발 그림 조건(`CoreVfxSpawner.cs:285`) 불성립");
            // 반경은 **같은 자**다(unified-effect-layer unit 5) — 비산도 자리형 정본(`SkillMath.ReachFromImpact`)을 지나
            // 반경 N + 칸 반폭 0.5 + 대상 몸 = 1.75. (탐침 당시엔 월드 반경 + 대상 몸 인라인이라 1.5 로 손으로 맞춰야 했다.)
            Assert.AreEqual(OnHitHit, h.MeteorDamageTo(h.E, OnHitHit), 1e-3f, "직격");
            foreach (var e in h.Near) Assert.AreEqual(OnHitHit, h.MeteorDamageTo(e, OnHitHit), 1e-3f, "비산 N + 0.5 + 0.25");
            Assert.AreEqual(0f, h.MeteorDamageTo(h.Far, OnHitHit));
        }
    }
}
