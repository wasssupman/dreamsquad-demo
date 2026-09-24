using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Map;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6b — **길막 설치물.** 길을 막다가 부서진다. 문은 「부서짐」 하나다.
    [TestFixture]
    public class BlockingHazardTests
    {
        private static MatchDefinition Def(float hp, float decay, HazardShapeKind shape = HazardShapeKind.SingleCell)
        {
            var def = CoreMatchFixtures.Definition();
            var row = BlockingHazardDef.Default();
            row.Id = "fixture_blocker";
            row.MaxHealth = hp;
            row.DecayPerSec = decay;
            row.Shape = (int)shape;
            def.BlockingHazards = new[] { row };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        private static bool Blocked(BattleMatch m, int2 c)
            => m.Map.Obstacles.Blocked[GridMath.CellIndex(c, m.Map.GridSize)];

        [Test]
        public void 체력_나누기_초당_감소가_아무도_안_때렸을_때의_수명이다()
        {
            // F11 — 100 ÷ 50 = 2초. 두 값을 따로 굴리면 수명이 통째로 달라진다.
            var m = CoreMatchFixtures.BeginBattle(Def(hp: 100f, decay: 50f));
            var gone = CoreCombatFixtures.Listen(m, CoreEventKind.UnitDestroyed);
            Assert.IsTrue(m.Apply(Command.DebugSpawnBlocker(0, new int2(5, 0))).Accepted);

            CoreCombatFixtures.Tick(m, 110);
            Assert.AreEqual(0, gone.Count, "2초 전에는 서 있다");
            CoreCombatFixtures.Tick(m, 20);
            Assert.AreEqual(1, gone.Count, "무간섭 수명 ≈ 체력 ÷ 초당 감소");
        }

        [Test]
        public void 문은_부서짐_하나다_노후화가_없으면_시간으로_안_사라진다()
        {
            var m = CoreMatchFixtures.BeginBattle(Def(hp: 100f, decay: 0f));
            m.Apply(Command.DebugSpawnBlocker(0, new int2(5, 0)));
            CoreCombatFixtures.Tick(m, 600);
            Assert.NotNull(CoreCombatFixtures.First(m, UnitKind.BlockingHazard), "시한 만료 경로가 없다");
        }

        [Test]
        public void 막는_칸은_저작_모양이고_부서지면_다음_틱에_길이_열린다()
        {
            var m = CoreMatchFixtures.BeginBattle(Def(hp: 100f, decay: 0f, HazardShapeKind.Square3x3));
            m.Apply(Command.DebugSpawnBlocker(0, new int2(5, 2)));
            m.Tick();
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                Assert.IsTrue(Blocked(m, new int2(5 + dx, 2 + dy)), $"({dx},{dy}) 3×3 은 아홉 칸을 막는다");
            Assert.IsFalse(Blocked(m, new int2(7, 2)));

            var blocker = CoreCombatFixtures.First(m, UnitKind.BlockingHazard);
            Assert.AreEqual(1.5f, blocker.HitRadius, 1e-6f, "몸 = 막는 칸의 내접원");
            m.Apply(Command.DebugDestroy(blocker.Id));
            m.Tick();
            Assert.IsFalse(Blocked(m, new int2(5, 2)), "unit 2 경로 — 사라지면 재수집이 푼다");
        }

        [Test]
        public void 골_칸과_이미_막힌_칸에는_못_세운다()
        {
            var m = CoreMatchFixtures.BeginBattle(Def(hp: 100f, decay: 0f));
            Assert.IsFalse(m.Apply(Command.DebugSpawnBlocker(0, new int2(11, 2))).Accepted, "골 칸");
            Assert.IsTrue(m.Apply(Command.DebugSpawnBlocker(0, new int2(5, 0))).Accepted);
            m.Tick();
            Assert.IsFalse(m.Apply(Command.DebugSpawnBlocker(0, new int2(5, 0))).Accepted, "이미 막힌 칸");
        }

        [Test]
        public void 탄이_세우는_길막은_그_탄의_줄을_지난다()
        {
            var def = Def(hp: 80f, decay: 10f);
            CoreCombatFixtures.GiveProjectile(def, MovementKind.BallisticArcToPoint, PayloadKind.SpawnBlocker);
            def.BlockingHazards[0].SpawnedByProjectile = 0;
            def.ConfigHash = def.ComputeConfigHash();
            Assert.AreEqual(0, def.BlockerOfProjectile(0));
            Assert.AreEqual(-1, def.BlockerOfProjectile(1));

            var m = CoreMatchFixtures.BeginBattle(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 0)));
            var caster = CoreCombatFixtures.First(m, UnitKind.Defender);
            Assert.IsTrue(m.Apply(Command.DebugFireProjectile(0, caster.Id, new int2(6, 0))).Accepted);
            CoreCombatFixtures.Tick(m, 120);

            var blocker = CoreCombatFixtures.First(m, UnitKind.BlockingHazard);
            Assert.NotNull(blocker, "착탄이 길막을 세운다");
            Assert.AreEqual(0, blocker.DefIndex, "정의 줄을 가리킨다 — 노후화·모양이 따라온다");
            Assert.Less(blocker.Health, 80f, "노후화가 돈다");
        }
    
        // ── unit 7d — 부서지면 터진다(옛 `BarrelExplosionSystem`) ──────────────

        private static BattleMatch Barrel(float explode, out int blastDef)
        {
            var def = Def(hp: 10f, decay: 0f);
            blastDef = CoreTriggerFixtures.AddBlastProjectile(def);
            def.BlockingHazards[0].ExplodeDamage = explode;
            def.BlockingHazards[0].ExplodeTileRange = 1;
            def.BlockingHazards[0].ExplodeProjectileDefIndex = blastDef;
            def.ConfigHash = def.ComputeConfigHash();
            return CoreMatchFixtures.BeginBattle(def);
        }

        [Test]
        public void 폭발_저작이_있는_길막은_부서지는_틱에_그_칸에서_적만_때리는_즉발_광역을_낸다()
        {
            var m = Barrel(explode: 40f, out _);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            m.Apply(Command.DebugSpawnBlocker(0, new int2(5, 2)));
            var barrel = CoreCombatFixtures.First(m, UnitKind.BlockingHazard);
            var barrelId = barrel.Id;   // 개체는 풀로 돌아가면 비워진다 — id 를 먼저 쥔다
            barrel.Inbox.Damage.Add(new DamageEntry { Amount = 100f, Source = SimEntityId.None });
            CoreCombatFixtures.Tick(m, 2);

            Assert.AreEqual(1, spawned.Count);
            var e = spawned[0];
            Assert.AreEqual(40f, e.Amount, 1e-4f);
            Assert.AreEqual(0f, e.SiteFired.OriginBody, 1e-6f, "자리에 떨어지는 것 — 몸 0");
            Assert.AreEqual(new int2(5, 2), m.Map.CellOf(e.SiteTarget.Pos), "그 칸 중심");
            Assert.AreEqual(barrelId, e.B, "처치 귀속 = 그 설치물(옛 처치 점수는 킬러를 안 봤다 — 출처가 없으면 점수가 사라진다)");
        }

        [Test]
        public void 폭발_저작이_없는_길막은_부서져도_안_터진다()
        {
            var m = Barrel(explode: 0f, out _);
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.ProjectileSpawned);
            m.Apply(Command.DebugSpawnBlocker(0, new int2(5, 2)));
            CoreCombatFixtures.First(m, UnitKind.BlockingHazard).Inbox.Damage.Add(new DamageEntry { Amount = 100f });
            CoreCombatFixtures.Tick(m, 2);
            Assert.IsEmpty(spawned, "기존 길막 무회귀");
        }
    }
}
