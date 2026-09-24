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
    }
}
