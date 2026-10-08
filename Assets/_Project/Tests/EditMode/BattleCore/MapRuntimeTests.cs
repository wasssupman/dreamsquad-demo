using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 2 — 맵 런타임의 계약: 슬롯·장애물·점유·효과 타일.
    public class FlowFieldSetTests
    {
        [Test]
        public void 슬롯이_없으면_시끄럽게_실패한다()
        {
            // M2 이식 제외 — 옛 `SlotFor` 는 조용히 primary 를 돌려줬다("현행 안전망").
            // 그러면 미저작 목적지가 엉뚱한 길로 가는 것을 덮는다.
            var map = CoreMapFixtures.Open(5, 5, new int2(4, 4), new int2(0, 0));
            var runtime = new MapRuntime(map, new[] { LayerBits.Path });

            Assert.DoesNotThrow(() => runtime.Flow.GoalSlot(LayerBits.Path));
            Assert.Throws<System.InvalidOperationException>(
                () => runtime.Flow.Slot(new int2(2, 2), LayerBits.Path),
                "저작하지 않은 목적지는 폴백 없이 던진다");
        }

        [Test]
        public void 경유점과_거점이_슬롯이_된다()
        {
            var map = CoreMapFixtures.Open(6, 6, new int2(5, 5), new int2(0, 0));
            map.WaypointCells = new[] { new int2(2, 2) };
            map.WaypointRanges = new[] { new int2(0, 1) };
            map.Structures = new[]
            {
                new StructureSpot { Cell = new int2(4, 1), Faction = 1 << 4, Footprint = 3 },
            };
            var runtime = new MapRuntime(map, new[] { LayerBits.Path });

            Assert.IsTrue(runtime.Flow.HasSlot(new int2(2, 2), LayerBits.Path));
            Assert.IsTrue(runtime.Flow.HasSlot(new int2(4, 1), LayerBits.Path));
            Assert.IsTrue(runtime.Flow.Slot(new int2(4, 1), LayerBits.Path).Reaches(new int2(0, 0)));
        }

        [Test]
        public void 장애물이_바뀐_틱에만_다시_굽는다()
        {
            var map = CoreMapFixtures.Open(6, 6, new int2(5, 5), new int2(0, 0));
            var runtime = new MapRuntime(map, new[] { LayerBits.Path });

            runtime.Obstacles.BeginRebuild();
            runtime.Obstacles.EndRebuild();
            Assert.IsFalse(runtime.Flow.Rebuild(runtime.Obstacles), "안 바뀌면 안 굽는다");

            runtime.Obstacles.BeginRebuild();
            runtime.Obstacles.Block(new int2(3, 3));
            Assert.IsTrue(runtime.Obstacles.EndRebuild());
            Assert.IsTrue(runtime.Flow.Rebuild(runtime.Obstacles));
        }

        [Test]
        public void 막으면_돌아간다()
        {
            // 폭 1 복도를 막으면 그 슬롯은 도달 불가가 된다 — 「막으면 돌아간다」의 최소 사례.
            var map = CoreMapFixtures.Corridor(6, 3, 1);
            var runtime = new MapRuntime(map, new[] { LayerBits.Path });
            Assert.IsTrue(runtime.Flow.GoalSlot(LayerBits.Path).Reaches(new int2(0, 1)));

            runtime.Obstacles.BeginRebuild();
            runtime.Obstacles.Block(new int2(3, 1));
            runtime.Obstacles.EndRebuild();
            runtime.Flow.Rebuild(runtime.Obstacles);

            Assert.IsFalse(runtime.Flow.GoalSlot(LayerBits.Path).Reaches(new int2(0, 1)));
        }
    }

    public class ObstacleSetTests
    {
        [Test]
        public void 같은_집합이면_같은_시그니처다()
        {
            var a = new ObstacleSet(new int2(5, 5));
            a.BeginRebuild(); a.Block(new int2(1, 2)); a.Block(new int2(3, 4)); a.EndRebuild();
            uint first = a.Signature;

            a.BeginRebuild(); a.Block(new int2(3, 4)); a.Block(new int2(1, 2));
            Assert.IsFalse(a.EndRebuild(), "순서가 달라도 같은 집합이면 재빌드 신호가 없다");
            Assert.AreEqual(first, a.Signature);
        }

        [Test]
        public void 개수가_다르면_반드시_다른_시그니처다()
        {
            // 해시 충돌 시 변경을 **영영** 놓치므로 개수를 함께 섞는다.
            var a = new ObstacleSet(new int2(5, 5));
            a.BeginRebuild(); a.Block(new int2(1, 1)); a.EndRebuild();
            uint one = a.Signature;

            a.BeginRebuild(); a.Block(new int2(1, 1)); a.Block(new int2(2, 2)); a.EndRebuild();
            Assert.AreNotEqual(one, a.Signature);
        }

        [Test]
        public void 고정_칸은_재수집을_살아남는다()
        {
            var a = new ObstacleSet(new int2(5, 5));
            a.SetManual(new int2(2, 2), true);
            a.BeginRebuild(); a.EndRebuild();
            Assert.IsTrue(a.Blocked[2 * 5 + 2]);

            a.SetManual(new int2(2, 2), false);
            a.BeginRebuild(); a.EndRebuild();
            Assert.IsFalse(a.Blocked[2 * 5 + 2]);
        }

        [Test]
        public void 다칸_점유를_통째로_막는다()
        {
            var a = new ObstacleSet(new int2(5, 5));
            a.BeginRebuild();
            a.BlockRect(new int2(1, 1), 2, 3);
            a.EndRebuild();
            Assert.AreEqual(6, a.Count);
            Assert.IsTrue(a.Blocked[3 * 5 + 2]);
        }
    }

    public class PlacementOccupancyTests
    {
        [Test]
        public void 다칸_점유와_주인은_항상_짝이다()
        {
            // M29 — 다칸 저작이 라이브다(방어유닛 2×2 · 캐논 2×3).
            var occ = new PlacementOccupancy();
            var id = new SimEntityId(7);
            occ.Occupy(id, new int2(1, 1), 2, 3);

            Assert.IsTrue(occ.IsOccupied(new int2(2, 3)));
            Assert.AreEqual(7, occ.OwnerAt(new int2(1, 1)));
            Assert.IsFalse(occ.IsFree(new int2(2, 2), 1, 1));

            Assert.IsTrue(occ.Release(id));
            Assert.IsFalse(occ.IsOccupied(new int2(2, 3)), "해제가 소멸과 짝이 아니면 칸이 영영 물린다");
            Assert.AreEqual(SimEntityId.NoneValue, occ.OwnerAt(new int2(1, 1)));
        }

        [Test]
        public void 한_칸만_겹쳐도_비어_있지_않다()
        {
            var occ = new PlacementOccupancy();
            occ.Occupy(new SimEntityId(1), new int2(0, 0), 2, 2);
            Assert.IsFalse(occ.IsFree(new int2(1, 1), 2, 2), "모서리 하나만 겹쳐도 거절");
            Assert.IsTrue(occ.IsFree(new int2(2, 2), 2, 2));
        }
    }

    public class EffectTileSelectTests
    {
        [Test]
        public void 같은_시드면_같은_칸이다()
        {
            // M16 — 소금 XOR · 0 시드 가드 · row-major 수집 셋이 함께 있어야 성립한다.
            var map = CoreMapFixtures.Open(6, 6, new int2(5, 5));
            var scratch = new int2[36];
            var a = new int2[4];
            var b = new int2[4];

            int na = EffectTileSelect.SelectCells(map, 1234, 4, scratch, a);
            int nb = EffectTileSelect.SelectCells(map, 1234, 4, scratch, b);

            Assert.AreEqual(na, nb);
            for (int i = 0; i < na; i++) Assert.AreEqual(a[i], b[i]);
        }

        [Test]
        public void 시드_0_도_판을_만든다()
        {
            // `| 1u` 0-시드 가드 — 없으면 생성기가 별도 폴백을 타서 같은 시드가 다른 판을 만든다.
            // ⚠ 그 가드의 부작용으로 **짝수 시드와 그 다음 홀수 시드가 같은 열을 만든다**
            // (0↔1, 2↔3 …). 옛 전투에서 그대로 옮겨 온 성질이고, 맵 시드는 믹서를 지나 오므로
            // 라이브에서는 인접 정수쌍이 들어오지 않는다. 값을 바꾸려면 여기부터 읽을 것.
            var map = CoreMapFixtures.Open(6, 6, new int2(5, 5));
            var scratch = new int2[36];
            var a = new int2[4];
            var b = new int2[4];

            int n = EffectTileSelect.SelectCells(map, 0, 4, scratch, a);
            Assert.AreEqual(4, n, "0 시드에서도 뽑힌다");

            EffectTileSelect.SelectCells(map, 8, 4, scratch, b);
            bool same = true;
            for (int i = 0; i < 4; i++) if (!a[i].Equals(b[i])) { same = false; break; }
            Assert.IsFalse(same, "다른 시드는 다른 칸을 준다");
        }

        [Test]
        public void 배치_가능_칸만_뽑는다()
        {
            var map = CoreMapFixtures.Open(4, 4, new int2(3, 3));
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                if (x != 0) CoreMapFixtures.Block(map, new int2(x, y));

            var scratch = new int2[16];
            var outCells = new int2[8];
            int n = EffectTileSelect.SelectCells(map, 7, 8, scratch, outCells);

            Assert.AreEqual(4, n, "열린 칸은 x = 0 열 넷뿐이다");
            for (int i = 0; i < n; i++) Assert.AreEqual(0, outCells[i].x);
        }
    }
}
