using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6b — **장 캐리어**(포탈 · 당김 · 아군 버프). 수명과 아군 버프 재발행.
    [TestFixture]
    public class FieldCarrierTests
    {
        private static FieldCarrier AllyBuff(float3 center, float mul, float range = 1f, float duration = 0f)
            => new FieldCarrier
            {
                Kind = FieldKind.AllyBuff,
                Center = center,
                Range = range,
                Stat = StatKind.DamageMul,
                Magnitude = mul,
                Duration = duration,
            };

        private static (BattleMatch m, Unit defender) Board()
        {
            var m = CoreMatchFixtures.BeginBattle(CoreMatchFixtures.Definition());
            m.Apply(Command.DebugSpawnDefender(0, new int2(5, 2)));
            return (m, CoreCombatFixtures.First(m, UnitKind.Defender));
        }

        [Test]
        public void 겹친_아군_장은_깐_순서와_무관하게_가장_강한_값이다()
        {
            foreach (bool strongFirst in new[] { true, false })
            {
                var (m, d) = Board();
                var c = d.Position;
                m.World.SpawnField(AllyBuff(c, strongFirst ? 2f : 1.5f), 0);
                m.World.SpawnField(AllyBuff(c, strongFirst ? 1.5f : 2f), 0);
                m.Tick();
                Assert.AreEqual(2f, d.Modifiers.Effective.DamageMul, 1e-5f, $"strongFirst={strongFirst}");
                Assert.AreEqual(1, d.Modifiers.Count, "한 슬롯을 나눠 쓴다");
            }
        }

        [Test]
        public void 재발행_지속은_틱_델타보다_길고_나가면_곧_풀린다()
        {
            // F36 — 규칙은 「재발행 지속 > 최대 틱 델타」다. 고정 틱이라 델타는 1/60 하나뿐이다.
            float refresh = FieldRefresh.Seconds(BattleMatch.Dt);
            Assert.Greater(refresh, BattleMatch.Dt);

            var (m, d) = Board();
            var f = m.World.SpawnField(AllyBuff(d.Position, 2f), 0);
            CoreCombatFixtures.Tick(m, 30);
            Assert.AreEqual(2f, d.Modifiers.Effective.DamageMul, 1e-5f, "안에 있는 동안은 매 틱 유지된다");

            f.Center += new float3(5f, 0f, 0f);   // 장이 멀어진다 = 대상이 밖
            CoreCombatFixtures.Tick(m, FieldRefresh.TickMargin + 1);
            Assert.AreEqual(1f, d.Modifiers.Effective.DamageMul, 1e-5f, "재발행이 멈추면 여유 뒤 풀린다");
        }

        [Test]
        public void 수명은_이동_뒤에_깎이고_소멸_사건을_낸다()
        {
            // 옛 `EffectTickSystem`(27) 은 이동 뒤였다 — 사라지는 틱에도 이동은 그 장을 한 번 더 본다.
            var map = CoreMapFixtures.Open(9, 5, new int2(8, 2), new int2(0, 2));
            var m = new BattleMatch(CoreMapFixtures.Definition(map));
            m.Begin();
            var spawned = CoreCombatFixtures.Listen(m, CoreEventKind.FieldSpawned);
            var gone = CoreCombatFixtures.Listen(m, CoreEventKind.FieldDespawned);
            m.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            var enemy = m.World.Units[m.World.Units.Count - 1];
            m.Tick();
            float z0 = enemy.Position.z;

            m.World.SpawnField(new FieldCarrier
            {
                Kind = FieldKind.Pull,
                Center = new float3(1f, 0f, 4f),
                Range = 5f,
                Speed = 4f,
                Duration = BattleMatch.Dt,   // 한 틱
            }, 0);
            m.Tick();   // 틱 밖에서 낸 사건은 다음 배달(틱 끝)에 간다
            Assert.AreEqual(1, spawned.Count);
            Assert.AreEqual(5, spawned[0].AreaTiles, "unit 7d — 소용돌이 그림의 반경(칸)이 사건에 값으로 실린다");
            Assert.AreEqual(0f, spawned[0].SiteFired.OriginBody, "자리형 — 원점 항은 칸 반폭(뷰가 CoreDrawRadius 로)");
            Assert.Greater(enemy.Position.z, z0, "사라지는 틱에도 이동은 당김을 받는다");
            Assert.AreEqual(1, gone.Count, "계약 7");
            Assert.AreEqual(spawned[0].A, gone[0].A);
            Assert.AreEqual(0, m.World.Fields.Count);
        }

        [Test]
        public void 수명_0은_무기한이다()
        {
            var (m, d) = Board();
            m.World.SpawnField(AllyBuff(d.Position, 1.5f), 0);
            CoreCombatFixtures.Tick(m, 300);
            Assert.AreEqual(1, m.World.Fields.Count);
        }
    }
}
