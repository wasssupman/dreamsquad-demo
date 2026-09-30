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
    // unified-effect-layer unit 1 — 발사 요청 조립 한 갈래(H2)의 **라이브 행 무변**(`docs/spec/unified-effect-layer/census.md` 표 1).
    //
    // `IntentApplier.SpawnProjectile` 을 지나는 라이브 행마다 **실제 concrete**(또는 `ResignationBarrage`)를 돌려 나온
    // `ProjectileRequest` 를, 옛 두 갈래 조립(`이전_조립` — unit 1 직전 코드의 사본)이 **같은 의도**로 만들었을 요청과
    // 필드 전부(struct 동치)로 대조한다. 옛 자리 갈래는 의도의 궤적 칸을 안 읽었고(강제) 옛 대상 갈래는 Duration 을 안 읽었으므로,
    // unit 1 이 의도에 더한 칸(자리형의 SkyFall × TileAoe 명시 · 비수·부메랑의 Duration)은 옛 조립의 결과를 바꾸지 않는다 —
    // 그래서 「새 의도 → 새 조립」 = 「새 의도 → 옛 조립」 = 「옛 의도 → 옛 조립」이다.
    //
    // ⚠ 탄 줄 0 은 **Homing × SingleSplash** 로 저작한다 — 라이브 `Projectile_Meteor` · `_BruiserShock` · `_JjangssenQuake` 가
    // 그렇다(flightMode 0). 자리형이 궤적을 명시하지 않으면 여기서 대상 결합으로 새어 요청이 사라진다(= 빨강).
    //
    // ⚠ 여기 수치는 게임 값이 아니라 픽스처다.
    [TestFixture]
    public class SpawnAssemblyEquivalenceTests
    {
        private const int MeteorRow = 0;
        private const int NeedleRow = 1;
        private const int BoomerangRow = 2;

        private BattleMatch _m;
        private Unit _d, _e;
        private Recorder _ctx;
        private List<string> _said;

        private static SkillEntityId S(Unit u) => CoreSkillContext.ToSkill(u.Id);

        private static ProjectileDef Row(string id, MovementKind movement, PayloadKind payload)
        {
            var p = ProjectileDef.Default();
            p.Id = id;
            p.Movement = (int)movement;
            p.Payload = (int)payload;
            return p;
        }

        [SetUp]
        public void SetUp()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, enemyHealth: 500f);
            def.Projectiles = new[]
            {
                Row("eq_meteor_homing", MovementKind.HomingToEntity, PayloadKind.SingleSplash),
                Row("eq_needle", MovementKind.HomingToEntity, PayloadKind.SingleSplash),
                Row("eq_boomerang", MovementKind.BoomerangReturn, PayloadKind.PathHit),
            };
            def.ConfigHash = def.ComputeConfigHash();
            _m = CoreMatchFixtures.BeginBattle(def);
            _said = new List<string>();
            _m.Report = _said.Add;
            _d = SpawnDefender(_m, new int2(3, 2));
            _e = SpawnEnemy(_m, new int2(6, 2));
            _ctx = new Recorder(new CoreSkillContext(_m.World, _m.Map, _m.Intents));
        }

        // ── 대조 ────────────────────────────────────────────────────────────

        private void Run(ISkill skill, CasterRef caster, in SkillTarget target, in SkillParams p)
        {
            _ctx.Emitted.Clear();
            int before = _m.World.ProjectileRequests.Count;
            // 디스패처처럼 발동 문맥을 연다(시전 진영 = 시전자의 진영 — 주체 없는 시전의 편은 문맥만 안다).
            _m.Intents.Begin(null, caster.Faction, null);
            try { skill.Execute(caster, target, p, _ctx); }
            finally { _m.Intents.End(); }
            Assert.AreEqual(1, _ctx.Emitted.Count, "의도 하나");
            Assert.AreEqual(before + 1, _m.World.ProjectileRequests.Count, $"요청 하나(버려지지 않는다) — {string.Join(" / ", _said)}");
            AssertSame(_ctx.Emitted[0], _m.World.ProjectileRequests[before], Faction.DefenderUnit);
        }

        private void AssertSame(in SimIntent i, in ProjectileRequest actual, Faction casterFaction)
        {
            Assert.IsTrue(이전_조립(i, casterFaction, out var expected), "옛 조립도 요청을 냈다");
            Assert.AreEqual(expected, actual, "요청 필드 전부 동치(struct)");
        }

        // unit 1 직전 `IntentApplier.SpawnProjectile` 의 두 갈래 — **사본**이다(비교 기준 · 고치지 않는다).
        private bool 이전_조립(in SimIntent i, Faction casterFaction, out ProjectileRequest req)
        {
            req = ProjectileRequest.Empty;
            var def = _m.Intents.Definition;
            if (i.DataIndex < 0 || i.DataIndex >= def.Projectiles.Length) return false;
            var pd = def.Projectiles[i.DataIndex];
            var owner = i.Source.IsValid ? _m.World.Find(new SimEntityId(i.Source.Value)) : null;
            var ownerFaction = owner != null ? owner.Faction : casterFaction;
            float tileSize = _m.Map != null ? _m.Map.TileSize : 1f;

            req.DefIndex = i.DataIndex;
            req.Owner = owner != null ? owner.Id : SimEntityId.None;
            req.OwnerFaction = ownerFaction;
            req.TargetMask = (int)FactionRelation.OpponentUnitsOf(ownerFaction != Faction.None ? ownerFaction : casterFaction);
            req.TargetLayers = i.TargetTraversalLayers;
            req.Damage = i.Amount;

            if (i.Target.IsValid)
            {
                var victim = _m.World.Find(new SimEntityId(i.Target.Value));
                if (victim == null) return false;
                req.Movement = i.ProjectileMovement != 0 ? (MovementKind)i.ProjectileMovement : (MovementKind)pd.Movement;
                req.Payload = i.ProjectilePayload != 0 ? (PayloadKind)i.ProjectilePayload : (PayloadKind)pd.Payload;
                bool directional = MovementBinding.Of(req.Movement) == BindingClass.Direction;
                req.Target = directional ? SimEntityId.None : victim.Id;
                req.Origin = i.Position;
                req.Impact = victim.Position;
                req.Direction = directional ? i.DirectionXZ : math.normalizesafe((victim.Position - i.Position).xz);
                req.DistanceOverride = directional ? i.TileRange * tileSize : 0f;
                req.RetargetTileRange = directional ? 0 : i.TileRange;
            }
            else
            {
                req.Movement = MovementKind.SkyFall;
                req.Payload = PayloadKind.TileAoe;
                req.Origin = i.Position;
                req.Impact = i.Position;
                req.ImpactTileRange = i.TileRange;
                req.OriginBodyRadius = i.OriginBodyRadius;
                req.FlightTime = i.Duration;
                req.TelegraphTileRange = i.Telegraph ? i.TileRange : 0;
            }
            return true;
        }

        private static SkillParams P(float magnitude, int tileRange, int dataIndex, float duration = 0f,
                                     int movement = 0, int payload = 0, byte layers = 0,
                                     float speed = 0f, float hitThreshold = 0f)
            => new SkillParams(magnitude, duration, tileRange, 0, dataIndex, 0, speed, hitThreshold, 0f, 0, 0,
                               visualScale: 1f, projectileMovement: movement, projectilePayload: payload,
                               targetTraversalLayers: layers);

        private CasterRef Caster(Unit u) => new CasterRef(S(u), u.Faction, u.HitRadius);

        // unit 2 — 드레인이 채우는 원점(`TriggerDispatcher.Execute`)의 손조립. 자기 사건 = ① = ② = 주인의 자리·몸.
        private static SkillOrigin Self(Unit u) => SkillOrigin.OfSubject(u.Position, u.HitRadius);

        // ── 표 1 행 ─────────────────────────────────────────────────────────

        [Test]
        public void 행1_찌르기_바늘_대상_추적_탄은_무변()
        {
            // Card_PokeNeedle — 재조준 반경 4 · Duration 은 대상 결합이 안 읽는다(0 이 아니어도 무변).
            var p = P(5f, 4, NeedleRow, duration: 0.7f, speed: 8f, hitThreshold: 0.3f, layers: 1);
            Run(new TargetProjectileSkill(), Caster(_d), SkillTarget.OfUnit(S(_e), Self(_d)), p);
            var r = _m.World.ProjectileRequests[0];
            Assert.AreEqual(_e.Id, r.Target);
            Assert.AreEqual(4, r.RetargetTileRange);
            Assert.AreEqual(0f, r.FlightTime, "대상 결합은 Duration 을 안 읽는다");
        }

        [Test]
        public void 행2_부메랑_방향_탄은_무변()
        {
            var p = P(5f, 3, BoomerangRow, movement: (int)MovementKind.BoomerangReturn);
            Run(new TargetProjectileSkill(), Caster(_d),
                SkillTarget.OfUnit(S(_e), Self(_d), new float2(1f, 0f)), p);
            var r = _m.World.ProjectileRequests[0];
            Assert.IsTrue(r.Target.IsNone, "방향 결합 — 임자 없음");
            Assert.AreEqual(3f * _m.Map.TileSize, r.DistanceOverride, 1e-6f);
        }

        [Test]
        public void 행17_19_44_54_자기_자리_폭발은_무변()
        {
            // Card_CorneredBurst · Card_ShieldBurst · Card_TremorPlate · 브루저 · 짱쎈(적 시전자) — 몸에서 나오는 것.
            Run(new SelfAreaBlastSkill(), Caster(_d), SkillTarget.At(Self(_d)), P(20f, 1, MeteorRow, layers: 1));
            Run(new SelfAreaBlastSkill(), Caster(_e), SkillTarget.At(Self(_e)), P(20f, 2, MeteorRow));
            var r = _m.World.ProjectileRequests;
            Assert.AreEqual(MovementKind.SkyFall, r[0].Movement, "Homing 저작인데도 하늘 낙하");
            Assert.AreEqual(PayloadKind.TileAoe, r[0].Payload);
            Assert.AreEqual(_d.HitRadius, r[0].OriginBodyRadius, 1e-6f, "제약 13 — 시전자의 몸");
            Assert.AreEqual(_e.Id, r[1].Owner);
        }

        [Test]
        public void 행13_15_죽은_자리_폭발은_무변()
        {
            // Card_CalamityHeart · Card_Farewell(OnDeath — 시전자 없음) · Card_CorpseBurst(OnKill — 자리·몸 = 죽은 적).
            var site = _e.Position;
            Run(new DeathSiteBlastSkill(), Caster(_d),
                SkillTarget.At(new SkillOrigin(_d.Position, _d.HitRadius, site, 0.6f, default)), P(15f, 1, MeteorRow));
            Run(new DeathSiteBlastSkill(), CasterRef.Player(Faction.DefenderUnit),
                SkillTarget.At(SkillOrigin.OfSubject(_d.Position, 1f)), P(15f, 1, MeteorRow));
            Assert.AreEqual(0.6f, _m.World.ProjectileRequests[0].OriginBodyRadius, 1e-6f, "죽은 적의 몸(감지자 스냅샷)");
            Assert.AreEqual(0f, _m.World.ProjectileRequests[0].FlightTime, "즉발");
        }

        [Test]
        public void 행16_퇴근_운석은_무변()
        {
            // Card_SeveranceMeteor — 비워진 칸 · 몸 0 · 비행 0.8 · 예고 끔.
            var vacated = _m.Map.CenterOf(new int2(4, 2));
            Run(new DeathSiteBlastSkill(), CasterRef.Player(Faction.DefenderUnit),
                SkillTarget.At(new SkillOrigin(vacated, 0f, vacated, 0f, default)), P(15f, 1, MeteorRow, duration: 0.8f));
            var r = _m.World.ProjectileRequests[0];
            Assert.AreEqual(0.8f, r.FlightTime, 1e-6f);
            Assert.AreEqual(0, r.TelegraphTileRange, "예고 끔(U1)");
        }

        [Test]
        public void 행35_액티브_운석은_무변()
        {
            // Active_Meteor — 지정 칸 · 예고 켬 · 비행 = warningSec.
            var cell = new int2(5, 2);
            Run(new TileMeteorSkill(), CasterRef.Player(Faction.DefenderUnit),
                SkillTarget.At(SkillOrigin.AtCell(cell, _m.Map.CenterOf(cell))), P(100f, 1, MeteorRow, duration: 1.5f));
            var r = _m.World.ProjectileRequests[0];
            Assert.AreEqual(1, r.TelegraphTileRange, "예고 켬");
            Assert.AreEqual(1.5f, r.FlightTime, 1e-6f);
            Assert.AreEqual(_m.Map.CenterOf(new int2(5, 2)), r.Impact);
        }

        [Test]
        public void 행68_퇴근_기믹_임계_운석은_무변()
        {
            var def = CoreCombatFixtures.Definition();
            int proj = AddBlastProjectile(def);
            def.Projectiles[proj].Movement = (int)MovementKind.HomingToEntity;   // 라이브 `Projectile_Meteor` 저작
            def.Projectiles[proj].Payload = (int)PayloadKind.SingleSplash;
            var gimmick = CoreGimmickFixtures.ClockOut(threshold: 3, meteorCount: 4, meteorProjectile: proj);
            CoreGimmickFixtures.With(def, gimmick);
            _m = CoreMatchFixtures.BeginBattle(def);
            for (int k = 0; k < 3; k++) _m.Apply(Command.DebugDropResignation(new int2(k, 0)));
            _m.Tick();   // 임계 → 사건(틱 끝 배달) → 요청

            var reqs = _m.World.ProjectileRequests;
            Assert.AreEqual(4, reqs.Count);
            var co = gimmick.ClockOut;
            for (int k = 0; k < reqs.Count; k++)
            {
                // 옛 의도(궤적 칸 없음)를 요청의 자리로 되짚어 옛 조립에 넣는다 — 칸 선택(난수)은 이 unit 과 무관하다.
                var old = new SimIntent
                {
                    Kind = SimIntentKind.SpawnProjectile,
                    Source = SkillEntityId.None,
                    Target = SkillEntityId.None,   // ⚠ default 는 «무효»가 아니다 — 명시한다
                    Position = reqs[k].Impact,
                    Amount = co.MeteorDamage,
                    TileRange = co.MeteorTileRange,
                    DataIndex = proj,
                    Duration = co.MeteorWarningSec + k * co.MeteorStaggerSec,
                    Telegraph = false,
                };
                AssertSame(old, reqs[k], Faction.DefenderUnit);
            }
        }

        [Test]
        public void 자리형이_궤적을_명시하지_않으면_대상_결합으로_새어_버리고_말한다()
        {
            // 옛 applier 강제가 걷힌 뒤의 그물 — Homing 저작 탄 × 대상 없음 = 조용한 오발사가 아니라 경고.
            // skill-data-table 감사 — 주인 없는 탄은 발동 문맥이 편을 밝힌다(문맥 없이 적용하면 쓰기 표면이 말하고 버린다).
            _m.Intents.Begin(null, BattleMatch.PlayerFaction, null);
            try
            {
                _m.Intents.Apply(new SimIntent { Kind = SimIntentKind.SpawnProjectile, Position = _e.Position,
                                                 Source = SkillEntityId.None, Target = SkillEntityId.None,
                                                 Amount = 1f, TileRange = 1, DataIndex = MeteorRow });
            }
            finally { _m.Intents.End(); }
            Assert.AreEqual(0, _m.World.ProjectileRequests.Count);
            Assert.IsTrue(_said.Exists(s => s.Contains("조준 대상이 없다")));
        }

        // ── 의도 기록기 — 질의는 코어 문맥에 넘기고 `Emit` 만 적는다 ─────────────────────

        private sealed class Recorder : ISkillContext
        {
            private readonly CoreSkillContext _inner;
            public readonly List<SimIntent> Emitted = new List<SimIntent>();
            public Recorder(CoreSkillContext inner) => _inner = inner;

            public float3 Position(SkillEntityId id) => _inner.Position(id);
            public int2 CellOf(SkillEntityId id) => _inner.CellOf(id);
            public int2 CellOfPosition(float3 world) => _inner.CellOfPosition(world);
            public float3 CellCenter(int2 cell) => _inner.CellCenter(cell);
            public float TileSize => _inner.TileSize;
            public bool TryFacing(SkillEntityId id, out float2 dirXZ) => _inner.TryFacing(id, out dirXZ);
            public Faction FactionOf(SkillEntityId id) => _inner.FactionOf(id);
            public float Health(SkillEntityId id) => _inner.Health(id);
            public float MaxHealth(SkillEntityId id) => _inner.MaxHealth(id);
            public float Stat(SkillEntityId id, UnitStat stat) => _inner.Stat(id, stat);
            public bool Has(SkillEntityId id, UnitPredicate pred) => _inner.Has(id, pred);
            public byte TraversalLayers(SkillEntityId id) => _inner.TraversalLayers(id);
            public float ShieldValueFrom(SkillEntityId target, SkillEntityId source) => _inner.ShieldValueFrom(target, source);
            public int Opponents(CasterRef caster, float3 center, int tileRange, CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
                => _inner.Opponents(caster, center, tileRange, filter, metric, into);
            public int Allies(CasterRef caster, float3 center, int tileRange, CandidateFilter filter, RangeMetric metric, SkillEntityId[] into)
                => _inner.Allies(caster, center, tileRange, filter, metric, into);
            public bool TryDensestOpponentCluster(CasterRef caster, int densityRadius, out int2 cell, out int count)
                => _inner.TryDensestOpponentCluster(caster, densityRadius, out cell, out count);
            public bool TryLandingCellNear(int2 desired, int maxRing, out int2 cell) => _inner.TryLandingCellNear(desired, maxRing, out cell);
            public PatternAimNeed AimNeedOfPattern(SkillEntityId host, int patternIndex) => _inner.AimNeedOfPattern(host, patternIndex);
            public void Emit(in SimIntent intent) { Emitted.Add(intent); _inner.Emit(in intent); }
            public void Emit(in MetaIntent intent) => _inner.Emit(in intent);
        }
    }
}
