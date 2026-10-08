using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Effects;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6b — **효과 타일.** 칸 하나가 그 위에 놓인 유닛에게 주는 것.
    // 적용은 활성화 엣지, 끝은 **회수**(퇴근) — 옛 전투가 못 하던 것(F33).
    [TestFixture]
    public class EffectTileTests
    {
        private static MatchDefinition Def(float deployMotion = 0f, int footprintWidth = 1)
        {
            var def = CoreMatchFixtures.Definition();
            def.EffectTileCount = 3;
            def.EffectTiles = new[]
            {
                new EffectTileDef
                {
                    Id = "fixture_tile",
                    Entries = new[]
                    {
                        new EffectTileEntryDef { Stat = (int)StatKind.DamageMul, Op = (int)CombineOp.Additive, Magnitude = 0.5f },
                    },
                },
            };
            def.Units[0].DeployMotionSeconds = deployMotion;
            def.Units[0].FootprintWidth = footprintWidth;
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        private static int2 FreeCell(BattleMatch m)
        {
            for (int y = 0; y < 5; y++)
            for (int x = 1; x < 10; x++)
            {
                var c = new int2(x, y);
                if (m.Placement.EffectTileKindAt(c) >= 0) continue;
                if (m.Placement.Judge(0, c) == RejectReason.None) return c;
            }
            Assert.Fail("빈 칸이 없다");
            return default;
        }

        [Test]
        public void 증상_효과_타일에_놓으면_공격력이_오르고_퇴근시키면_회수된다()
        {
            var m = CoreMatchFixtures.BeginBattle(Def());
            var revoked = CoreCombatFixtures.Listen(m, CoreEventKind.ModifierRevoked);
            var cell = m.Placement.ArmedEffectTiles[0];
            Assert.AreEqual(0, m.Placement.EffectTileKindAt(cell));

            Assert.IsTrue(m.Apply(Command.PlaceDefender(0, cell)).Accepted);
            var id = CoreMatchFixtures.PlacedDefender(m);
            var u = m.World.Find(id);
            Assert.AreEqual(1.5f, u.Modifiers.Effective.DamageMul, 1e-5f, "공격력이 오른다");
            Assert.AreEqual(ModifierOrigin.Tile, u.Modifiers.Slots[0].Origin);
            Assert.AreEqual(SlotKind.Tile, u.Modifiers.Slots[0].Key.Tag.Kind);

            Assert.IsTrue(m.Apply(Command.Retire(id)).Accepted);
            Assert.AreEqual(1, revoked.Count, "퇴근은 타일이 준 슬롯을 거둔다(F33)");
            Assert.AreEqual(id, revoked[0].B);
            Assert.AreEqual((int)StatKind.DamageMul, revoked[0].Arg);

            // 다른(타일 없는) 칸에 다시 놓으면 원래 공격력이다 — 옛 칸의 효과가 따라오지 않는다.
            CoreCombatFixtures.Tick(m, 600);
            Assert.IsTrue(m.Apply(Command.PlaceDefender(0, FreeCell(m))).Accepted);
            var again = m.World.Find(CoreMatchFixtures.PlacedDefender(m));
            Assert.AreEqual(1f, again.Modifiers.Effective.DamageMul, 1e-5f, "공격력이 돌아온다");
        }

        [Test]
        public void 효과는_활성화_엣지에_걸린다()
        {
            // 옛 `ApplyEffectTileOnce` 는 배치 스킬 seam(활성화) 안에 있었다.
            var m = CoreMatchFixtures.BeginBattle(Def(deployMotion: 0.5f));
            var cell = m.Placement.ArmedEffectTiles[0];
            m.Apply(Command.PlaceDefender(0, cell));
            var u = m.World.Find(CoreMatchFixtures.PlacedDefender(m));
            Assert.IsTrue(u.Deploying);
            Assert.IsFalse(u.Modifiers.Any, "배치 중에는 아직 안 걸린다");
            CoreCombatFixtures.Tick(m, 40);
            Assert.IsFalse(u.Deploying);
            Assert.AreEqual(1.5f, u.Modifiers.Effective.DamageMul, 1e-5f);
        }

        [Test]
        public void 효과_타일_마킹은_배치_스킬_엣지와_공유하지_않는다()
        {
            // F19 — 타일 없는 칸에 놓아도 활성화(배치 스킬 엣지)는 나고, 타일 효과는 안 걸린다.
            // 타일 칸에 놓으면 활성화는 **한 번**, 타일 효과도 **한 번**이다.
            var m = CoreMatchFixtures.BeginBattle(Def());
            var activated = CoreCombatFixtures.Listen(m, CoreEventKind.DefenderActivated);
            var applied = CoreCombatFixtures.Listen(m, CoreEventKind.ModifierApplied);

            m.Apply(Command.PlaceDefender(0, FreeCell(m)));
            Assert.AreEqual(1, activated.Count);
            Assert.AreEqual(0, applied.Count);

            m.Apply(Command.PlaceDefender(0, m.Placement.ArmedEffectTiles[0]));
            CoreCombatFixtures.Tick(m, 60);
            Assert.AreEqual(2, activated.Count);
            Assert.AreEqual(1, applied.Count, "무한 지속 슬롯 — 갱신 사건이 나지 않는다");
        }

        [Test]
        public void 종류_배정은_시드_결정론이다()
        {
            var a = CoreMatchFixtures.BeginBattle(Def());
            var b = CoreMatchFixtures.BeginBattle(Def());
            Assert.AreEqual(a.Placement.ArmedEffectTiles.Count, b.Placement.ArmedEffectTiles.Count);
            for (int i = 0; i < a.Placement.ArmedEffectTiles.Count; i++)
            {
                var c = a.Placement.ArmedEffectTiles[i];
                Assert.AreEqual(c, b.Placement.ArmedEffectTiles[i]);
                Assert.AreEqual(a.Placement.EffectTileKindAt(c), b.Placement.EffectTileKindAt(c));
            }

            var kinds = new int[16];
            Somnia.Battle.BattleCore.Map.EffectTileSelect.AssignKinds(123, 3, kinds, kinds.Length);
            var seen = new HashSet<int>(kinds);
            Assert.IsTrue(seen.IsSubsetOf(new[] { 0, 1, 2 }));
            Assert.Greater(seen.Count, 1, "칸마다 난수 — round-robin 이 아니다");
        }

        [Test]
        public void 증상_퇴근_뒤_같은_칸에_다시_놓으면_다시_받는다()
        {
            // 칸은 판 내내 남고(옛 규칙) 효과는 개체에 걸렸다가 퇴근 때 거둔다(F33) —
            // 그래서 다음 유닛이 같은 타일을 다시 받는다. 옛 전투가 못 하던 것(6b 계약 11).
            var m = CoreMatchFixtures.BeginBattle(Def());
            var cell = m.Placement.ArmedEffectTiles[0];
            m.Apply(Command.PlaceDefender(0, cell));
            var first = CoreMatchFixtures.PlacedDefender(m);
            Assert.AreEqual(1.5f, m.World.Find(first).Modifiers.Effective.DamageMul, 1e-5f);

            m.Apply(Command.Retire(first));
            CoreCombatFixtures.Tick(m, 600);
            Assert.IsTrue(m.Apply(Command.PlaceDefender(0, cell)).Accepted);
            var second = m.World.Find(CoreMatchFixtures.PlacedDefender(m));
            Assert.AreNotEqual(first, second.Id);
            Assert.AreEqual(1.5f, second.Modifiers.Effective.DamageMul, 1e-5f, "같은 칸 = 같은 타일을 다시 받는다");
        }

        [Test]
        public void 증상_앵커가_아닌_칸의_타일은_안_받는다()
        {
            // 판정 칸은 대표 칸(앵커) 하나다(옛 `:7867`, defender-footprint unit 1).
            var m = CoreMatchFixtures.BeginBattle(Def(footprintWidth: 2));
            int2 tile = default, anchor = default;
            bool found = false;
            foreach (var c in m.Placement.ArmedEffectTiles)
            {
                var a = new int2(c.x - 1, c.y);   // 타일이 **오른쪽 칸**에 오게
                if (m.Placement.EffectTileKindAt(a) >= 0) continue;
                if (m.Placement.Judge(0, a) != RejectReason.None) continue;
                tile = c; anchor = a; found = true;
                break;
            }
            Assert.IsTrue(found, "앵커 왼쪽이 비고 타일이 오른쪽인 자리가 없다");
            Assert.IsTrue(m.Apply(Command.PlaceDefender(0, anchor)).Accepted);
            var u = m.World.Find(CoreMatchFixtures.PlacedDefender(m));
            Assert.AreEqual(1f, u.Modifiers.Effective.DamageMul, 1e-5f, $"타일 {tile} 은 앵커 {anchor} 가 아니다");
        }
    }
}
