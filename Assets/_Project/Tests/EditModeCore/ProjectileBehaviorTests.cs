using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Skills;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using static Wassup.Tests.EditMode.Core.CoreCombatFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 3 — 궤적 × 페이로드가 **판 위에서** 도는지.
    //
    // 순수 수학은 `ProjectileMathTests` 가 고정한다. 여기서 묻는 것은 배선이다:
    // 요청 → 스폰 → 전진 → 착탄 → 소멸이 한 줄로 이어지나.
    public class ProjectileBehaviorTests
    {
        private static BattleMatch Match(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        private static MatchDefinition WithProjectile(MovementKind movement, PayloadKind payload,
                                                      float damage = 10f, float range = 4f,
                                                      int impactTileRange = 1, int pierce = 1,
                                                      float maxDistance = 0f, float speed = 10f)
        {
            var def = Definition(defenderDamage: damage, defenderRange: range, enemySpeed: 0f);
            GiveProjectile(def, movement, payload, speed: speed, impactTileRange: impactTileRange,
                           pierce: pierce, maxDistance: maxDistance);
            return def;
        }

        [Test]
        public void 요청은_한_틱_뒤에_탄이_된다()
        {
            // 공격 루프는 탄 단계 **뒤**에 돈다. 이 지연을 없애려고 단계를 뒤로 옮기면
            // 같은 틱에 쏜 탄이 같은 틱에 착탄해 선딜이 사라진다.
            var m = Match(WithProjectile(MovementKind.HomingToEntity, PayloadKind.SingleSplash));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 1)));

            Tick(m, 1);
            Assert.AreEqual(1, m.World.ProjectileRequests.Count, "요청만 서 있다");
            Assert.AreEqual(0, m.World.Projectiles.Count);

            Tick(m, 1);
            Assert.AreEqual(0, m.World.ProjectileRequests.Count);
            Assert.AreEqual(1, m.World.Projectiles.Count);
        }

        [Test]
        public void 유도탄은_맞히고_사라진다()
        {
            var m = Match(WithProjectile(MovementKind.HomingToEntity, PayloadKind.SingleSplash));
            var hits = Listen(m, CoreEventKind.ProjectileHit);
            var gone = Listen(m, CoreEventKind.ProjectileDespawned);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 1)));
            var e = First(m, UnitKind.Enemy);

            Tick(m, 30);
            Assert.Greater(hits.Count, 0);
            Assert.Greater(gone.Count, 0, "모든 소멸은 소멸 사건을 낸다");
            Assert.Less(e.Health, e.MaxHealth);
        }

        [Test]
        public void 임자가_사라지면_탄도_사라진다()
        {
            var m = Match(WithProjectile(MovementKind.HomingToEntity, PayloadKind.SingleSplash,
                                         damage: 1f, speed: 0.5f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 1)));
            var e = First(m, UnitKind.Enemy);

            Tick(m, 2);
            Assert.AreEqual(1, m.World.Projectiles.Count);
            m.Apply(Command.DebugDestroy(e.Id));
            Tick(m, 2);
            Assert.AreEqual(0, m.World.Projectiles.Count, "재조준 반경이 0 이면 소멸이 기본이다");
        }

        [Test]
        public void 칸_광역은_반경_안_전원을_때린다()
        {
            var m = Match(WithProjectile(MovementKind.SkyFall, PayloadKind.TileAoe,
                                         damage: 7f, range: 5f, impactTileRange: 1));
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 1)));   // 착탄 칸의 이웃
            foreach (var u in m.World.Units) if (u.Move != null) u.Move.Speed = 0f;

            Tick(m, 5);
            var units = m.World.Units;
            Assert.Less(units[1].Health, units[1].MaxHealth);
            Assert.Less(units[2].Health, units[2].MaxHealth, "이웃 칸도 반경 1 안이다");
        }

        // ── unit 9c — 길막(방벽)은 어느 쪽 광역에도 안 맞는다 ─────────────────
        //
        // 옛 GoalProjectileTests::TileAoe_BlockingHazard_IsVictimOfNeitherPool. 옛 착탄의 광역 풀은 진영
        // 파생 그룹(`AnyDefender`/`AnyEnemy` · 스플래시·스윕·튕김은 `OpponentUnitsOf`)이라 방벽 비트가
        // 어디에도 없었다. 코어는 적의 공격 마스크(방벽 포함)를 탄이 그대로 들고 가 적 광역이 방벽을 쳤다.
        // ⚠ **직격은 그대로 맞는다** — 적이 방벽을 겨눠 쏜 탄이 방벽을 못 부수면 길막이 무적이 된다.

        private const float BarrierHealth = 80f;

        private static (BattleMatch m, Unit enemy, Unit defender, Unit barrier) BarrierBoard()
        {
            var def = Definition(defenderDamage: 0f, defenderRange: 0.5f, enemySpeed: 0f);
            GiveProjectile(def, MovementKind.SkyFall, PayloadKind.TileAoe, impactTileRange: 1);
            var splash = def.Projectiles[0];
            splash.Id = "fixture_splash";
            splash.Movement = (int)MovementKind.HomingToEntity;
            splash.Payload = (int)PayloadKind.SingleSplash;
            splash.SplashRadius = 2f;
            splash.SplashDamageMul = 1f;
            var sweep = def.Projectiles[0];
            sweep.Id = "fixture_sweep";
            sweep.Movement = (int)MovementKind.DirectionalLinear;
            sweep.Payload = (int)PayloadKind.PathHit;
            sweep.PierceCount = 2;
            sweep.Speed = 8f;
            sweep.MaxDistance = 10f;
            def.Projectiles = new[] { def.Projectiles[0], splash, sweep };
            def.Units[0].Attack.ProjectileDefIndex = -1;   // 방어유닛은 스스로 쏘지 않는다 — 탄은 테스트가 요청한다
            def.ConfigHash = def.ComputeConfigHash();
            var m = Match(def);
            m.Apply(Command.DebugSpawnEnemy(0, new int2(8, 3)));
            var enemy = m.World.Units[m.World.Units.Count - 1];
            m.Apply(Command.DebugSpawnDefender(0, new int2(5, 2)));
            var defender = m.World.Units[m.World.Units.Count - 1];
            var barrier = m.World.Spawn(UnitKind.BlockingHazard, Faction.BlockingHazard, -1,
                                        new float3(5.5f, 0f, 1.5f), 0.5f, BarrierHealth, deploying: false, tick: 0);
            return (m, enemy, defender, barrier);
        }

        private static ProjectileRequest AreaShot(Unit owner, int defIndex, int mask, float3 at)
        {
            var req = ProjectileRequest.Empty;
            req.DefIndex = defIndex;
            req.Movement = defIndex == 0 ? MovementKind.SkyFall : MovementKind.HomingToEntity;
            req.Payload = defIndex == 0 ? PayloadKind.TileAoe : PayloadKind.SingleSplash;
            req.Owner = owner.Id;
            req.OwnerFaction = owner.Faction;
            req.TargetMask = mask;
            req.Origin = at;
            req.Impact = at;
            req.Damage = 5f;
            return req;
        }

        [Test]
        public void 적의_칸_광역은_길막을_치지_않고_옆의_방어유닛은_친다()
        {
            var (m, enemy, defender, barrier) = BarrierBoard();
            m.World.ProjectileRequests.Add(AreaShot(enemy, 0, Wassup.BattleCore.Combat.TargetDefaults.EnemyMask,
                                                    barrier.Position));
            Tick(m, 10);
            Assert.Less(defender.Health, defender.MaxHealth, "전제 — 광역이 터졌다(이웃 칸 방어유닛)");
            Assert.AreEqual(BarrierHealth, barrier.Health, 1e-4f, "적 광역이 길막을 쳤다");
        }

        [Test]
        public void 방어유닛의_칸_광역도_길막을_치지_않는다()
        {
            var (m, _, defender, barrier) = BarrierBoard();
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 1)));
            var near = m.World.Units[m.World.Units.Count - 1];
            m.World.ProjectileRequests.Add(AreaShot(defender, 0, Wassup.BattleCore.Combat.TargetDefaults.DefenderMask,
                                                    barrier.Position));
            Tick(m, 10);
            Assert.Less(near.Health, near.MaxHealth, "전제 — 광역이 터졌다(이웃 칸 적)");
            Assert.AreEqual(BarrierHealth, barrier.Health, 1e-4f, "방어 광역이 길막을 쳤다");
        }

        [Test]
        public void 적_탄의_스플래시는_길막을_치지_않지만_길막을_겨눈_직격은_맞는다()
        {
            var (m, enemy, defender, barrier) = BarrierBoard();
            var toDefender = AreaShot(enemy, 1, Wassup.BattleCore.Combat.TargetDefaults.EnemyMask, enemy.Position);
            toDefender.Target = defender.Id;
            m.World.ProjectileRequests.Add(toDefender);
            Tick(m, 60);
            Assert.Less(defender.Health, defender.MaxHealth, "전제 — 직격이 닿았다");
            Assert.AreEqual(BarrierHealth, barrier.Health, 1e-4f, "스플래시가 길막을 쳤다");

            var toBarrier = AreaShot(enemy, 1, Wassup.BattleCore.Combat.TargetDefaults.EnemyMask, enemy.Position);
            toBarrier.Target = barrier.Id;
            m.World.ProjectileRequests.Add(toBarrier);
            Tick(m, 60);
            Assert.Less(barrier.Health, BarrierHealth, "길막을 겨눈 직격까지 막으면 길막이 무적이 된다");
        }

        // ── unit 9c 행 5 — 스플래시·스윕·튕김·재조준은 «유닛»만 고른다 ─────────
        //
        // 옛 `ProjectileHitSystem` 의 부가 피해자 풀은 `OpponentUnitsOf`(유닛 진영 하나)였고
        // (:330 스플래시 · :384 튕김 · :504 스윕 · :651 방향탄 튕김), 재조준 풀은 적 유닛
        // (`ProjectileMoveSystem.cs:78`)이었다 — **거점(마음·본능)은 빠진다.** 칸 광역만 진영
        // 파생 그룹(`AnyDefender`/`AnyEnemy`)이라 거점을 포함한다(아래 대조 테스트).

        private const float StructureHealth = 200f;

        private static Unit SpawnStructure(BattleMatch m, Faction faction, float3 at)
            => m.World.Spawn(UnitKind.Structure, faction, -1, at, 0.5f, StructureHealth, deploying: false, tick: 0);

        [Test]
        public void 적_탄의_스플래시는_마음을_치지_않는다()
        {
            var (m, enemy, defender, _) = BarrierBoard();
            var heart = SpawnStructure(m, Faction.DefenderCore, new float3(5.5f, 0f, 3.5f));   // 방어유닛 바로 옆
            var shot = AreaShot(enemy, 1, Wassup.BattleCore.Combat.TargetDefaults.EnemyMask, enemy.Position);
            shot.Target = defender.Id;
            m.World.ProjectileRequests.Add(shot);
            Tick(m, 60);
            Assert.Less(defender.Health, defender.MaxHealth, "전제 — 직격이 닿았다");
            Assert.AreEqual(StructureHealth, heart.Health, 1e-4f, "적 스플래시가 마음을 쳤다");
        }

        [Test]
        public void 방어유닛_탄의_재조준은_거점을_고르지_않는다()
        {
            var (m, _, defender, _) = BarrierBoard();
            m.Apply(Command.DebugSpawnEnemy(0, new int2(2, 3)));
            var doomed = m.World.Units[m.World.Units.Count - 1];
            SpawnStructure(m, Faction.EnemyCore, new float3(3.5f, 0f, 3.5f));   // 더 가깝다
            m.Apply(Command.DebugSpawnEnemy(0, new int2(3, 4)));   // 거점보다 멀고 (8,3) 의 적보다 가깝다
            var farther = m.World.Units[m.World.Units.Count - 1];
            var shot = AreaShot(defender, 1, Wassup.BattleCore.Combat.TargetDefaults.DefenderMask, defender.Position);
            shot.Target = doomed.Id;
            shot.RetargetTileRange = 6;
            m.World.ProjectileRequests.Add(shot);
            Tick(m, 1);
            m.Apply(Command.DebugDestroy(doomed.Id));
            Tick(m, 1);
            Assert.AreEqual(1, m.World.Projectiles.Count, "전제 — 재조준으로 살아남았다");
            Assert.AreEqual(farther.Id, m.World.Projectiles[0].Target, "재조준이 거점을 골랐다");
        }

        [Test]
        public void 방어유닛_탄의_튕김은_거점을_고르지_않는다()
        {
            var (m, _, defender, _) = BarrierBoard();
            m.Apply(Command.DebugSpawnEnemy(0, new int2(2, 3)));
            var first = m.World.Units[m.World.Units.Count - 1];
            SpawnStructure(m, Faction.EnemyCore, new float3(3.5f, 0f, 3.5f));   // 더 가깝다
            m.Apply(Command.DebugSpawnEnemy(0, new int2(0, 3)));
            var farther = m.World.Units[m.World.Units.Count - 1];
            var shot = AreaShot(defender, 1, Wassup.BattleCore.Combat.TargetDefaults.DefenderMask, defender.Position);
            shot.Target = first.Id;
            shot.BounceCount = 1;
            shot.BounceTileRange = 6;
            shot.BounceDamageMul = 1f;
            m.World.ProjectileRequests.Add(shot);
            for (int i = 0; i < 60 && first.Health >= first.MaxHealth; i++) Tick(m, 1);
            Assert.Less(first.Health, first.MaxHealth, "전제 — 첫 적이 맞았다");
            Assert.AreEqual(1, m.World.Projectiles.Count, "전제 — 튕겨서 살아남았다");
            Assert.AreEqual(farther.Id, m.World.Projectiles[0].Target, "튕김이 거점을 골랐다");
        }

        [Test]
        public void 방어유닛의_경로_스윕은_적_거점을_지나친다()
        {
            var (m, _, defender, _) = BarrierBoard();
            var instinct = SpawnStructure(m, Faction.EnemyInstinct, new float3(7.5f, 0f, 2.5f));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(9, 2)));
            var behind = m.World.Units[m.World.Units.Count - 1];
            var shot = AreaShot(defender, 2, Wassup.BattleCore.Combat.TargetDefaults.DefenderMask, defender.Position);
            shot.Movement = MovementKind.DirectionalLinear;
            shot.Payload = PayloadKind.PathHit;
            shot.Direction = new float2(1f, 0f);
            m.World.ProjectileRequests.Add(shot);
            Tick(m, 60);
            Assert.Less(behind.Health, behind.MaxHealth, "전제 — 스윕이 뒤의 적까지 닿았다");
            Assert.AreEqual(StructureHealth, instinct.Health, 1e-4f, "스윕이 적 거점을 쳤다");
        }

        // 대조 — 칸 광역은 옛 풀(`AnyEnemy`)이 거점을 품었다(옛 TileAoe_EnemyFaction_IncludesEnemyStructures).
        [Test]
        public void 방어유닛의_칸_광역은_적_거점도_친다()
        {
            var (m, _, defender, _) = BarrierBoard();
            var heart = SpawnStructure(m, Faction.EnemyCore, new float3(2.5f, 0f, 3.5f));
            m.World.ProjectileRequests.Add(AreaShot(defender, 0, Wassup.BattleCore.Combat.TargetDefaults.DefenderMask,
                                                    heart.Position));
            Tick(m, 10);
            Assert.Less(heart.Health, StructureHealth, "칸 광역은 적 거점을 친다(옛 규칙)");
        }

        [Test]
        public void 경로_스윕은_관통_예산까지_때린다()
        {
            var m = Match(WithProjectile(MovementKind.DirectionalLinear, PayloadKind.PathHit,
                                         damage: 5f, range: 6f, pierce: 2, maxDistance: 8f, speed: 6f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(1, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(8, 1)));
            foreach (var u in m.World.Units) if (u.Move != null) u.Move.Speed = 0f;

            Tick(m, 60);
            var units = m.World.Units;
            int hurt = 0;
            for (int i = 1; i < units.Count; i++) if (units[i].Health < units[i].MaxHealth) hurt++;
            Assert.AreEqual(2, hurt, "관통 예산 2 를 넘지 않는다");
        }

        // unit 9 감사 A — 옛 `ProjectileHitSystem`(7f9b496e1 :536~545) 「a 1-pierce shot must stop at the
        // nearest enemy it crossed」. 한 틱에 둘을 가로지르면 **진행 방향으로 앞(가까운 쪽)**부터 관통을
        // 쓴다 — `SimEntityId` 순이 아니다. 먼 적을 먼저 소환해 작은 id 를 준다(순회 순서가 이기면 빨강).
        [Test]
        public void 관통_1_탄은_한_틱에_가로지른_적_중_가까운_쪽에서_멈춘다()
        {
            var m = Match(WithProjectile(MovementKind.DirectionalLinear, PayloadKind.PathHit,
                                         damage: 5f, range: 6f, pierce: 1, maxDistance: 8f, speed: 600f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(1, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var far = m.World.Units[m.World.Units.Count - 1];
            m.Apply(Command.DebugSpawnEnemy(0, new int2(3, 1)));
            var near = m.World.Units[m.World.Units.Count - 1];
            foreach (var u in m.World.Units) if (u.Move != null) u.Move.Speed = 0f;
            Assert.Less(far.Id.CompareTo(near.Id), 0, "전제 — 먼 적의 id 가 더 작다");

            Tick(m, 60);
            Assert.Less(near.Health + far.Health, near.MaxHealth + far.MaxHealth, "전제 — 탄이 누군가를 맞혔다");
            Assert.Less(near.Health, near.MaxHealth, "관통 1 은 가까운 적을 맞힌다");
            Assert.AreEqual(far.MaxHealth, far.Health, 1e-4f, "관통 1 이 먼 적까지 갔다(순회 순서가 기하 순서를 이겼다)");
        }

        [Test]
        public void 길막_페이로드는_설치물을_세운다()
        {
            var def = WithProjectile(MovementKind.BallisticArcToPoint, PayloadKind.SpawnBlocker,
                                     damage: 1f, range: 5f);
            def.Projectiles[0].BlockerHealth = 50f;
            def.Projectiles[0].BlockerBodyRadius = 0.5f;
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 2)));
            foreach (var u in m.World.Units) if (u.Move != null) u.Move.Speed = 0f;

            Tick(m, 60);
            Assert.IsNotNull(First(m, UnitKind.BlockingHazard), "착탄 칸에 물건이 선다");
        }

        [Test]
        public void 자리형_탄은_원점_몸을_안_싣는다()
        {
            // 일반 공격의 탄은 **자리에 떨어지는 것**이다 — 던져서 도달한 좌표이지
            // 누군가의 몸이 아니다. 0 이 그 형의 표현이다(제약 13).
            var m = Match(WithProjectile(MovementKind.SkyFall, PayloadKind.TileAoe, range: 5f));
            var spawned = Listen(m, CoreEventKind.ProjectileSpawned);
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 2)));

            Tick(m, 5);
            Assert.Greater(spawned.Count, 0);
            Assert.AreEqual(0f, spawned[0].SiteFired.OriginBody, 1e-5f);
        }

        [Test]
        public void 방향_바인딩은_재조준_반경을_0_으로_접는다()
        {
            // 같은 필드가 두 뜻을 겸하지 않게 스폰에서 한 번 접는다 — 방향탄에는 겨눌 임자가 없다.
            var def = WithProjectile(MovementKind.DirectionalLinear, PayloadKind.PathHit,
                                     range: 5f, maxDistance: 6f);
            def.ConfigHash = def.ComputeConfigHash();
            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));

            Tick(m, 2);
            Assert.AreEqual(1, m.World.Projectiles.Count);
            Assert.AreEqual(0, m.World.Projectiles[0].RetargetTileRange);
        }

        [Test]
        public void 궁극기_도약은_이탈_예고_강습_슬램이다()
        {
            var def = WithProjectile(MovementKind.SkyFall, PayloadKind.TileAoe,
                                     damage: 1f, range: 5f, impactTileRange: 1);
            def.ConfigHash = def.ComputeConfigHash();
            var m = Match(def);
            var descend = Listen(m, CoreEventKind.LeapDescend);
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(9, 2)));
            var d = First(m, UnitKind.Defender);
            var e = First(m, UnitKind.Enemy);
            e.Move.Speed = 0f;

            d.Progressive = new ProgressiveStates
            {
                LeapActive = true,
                LeapRemaining = 0.5f,
                LandingWorld = e.Position,
                SlamDamage = 30f,
                SlamTileRange = 1,
                SlamProjectileDefIndex = 0,
            };

            Assert.IsFalse(d.IsTargetable(), "이탈 중에는 판 밖이다");
            Tick(m, 40);
            Assert.AreEqual(1, descend.Count);
            Assert.AreEqual(0f, descend[0].SiteTarget.OriginBody, 1e-5f,
                "착지 슬램은 **자리에 떨어지는 것**이다 — 그 몸이 내리찍는 것이 아니다");
            Assert.Less(e.Health, e.MaxHealth, "착지 자리에 슬램이 떨어진다");
        }
    }
}
