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
