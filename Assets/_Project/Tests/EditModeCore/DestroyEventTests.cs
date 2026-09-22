using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 1 완료 기준 ⑤ — 계약 7.
    //
    // **모든 소멸 경로는 소멸 이벤트를 낸다.** 이것이 옛 뷰 풀 3곳의 「매 프레임 생존
    // 폴링」을 은퇴시키는 근거다 — 폴링은 사라진 걸 나중에 눈치채고, 이벤트는 사라질 때
    // 말해 준다. 그래서 이 테스트는 「스폰된 것 − 소멸 이벤트 = 아직 살아 있는 것」이
    // **정확히** 성립하는지를 묻는다.
    [TestFixture]
    public class DestroyEventTests
    {
        private BattleMatch _match;
        private List<CoreEvent> _spawned;
        private List<CoreEvent> _destroyed;

        [SetUp]
        public void SetUp()
        {
            _match = new BattleMatch(CoreGoldenCorpus.Fixture(1));
            _spawned = new List<CoreEvent>();
            _destroyed = new List<CoreEvent>();
            _match.Bus.Subscribe(CoreEventKind.UnitSpawned, 0, _spawned.Add);
            _match.Bus.Subscribe(CoreEventKind.UnitDestroyed, 0, _destroyed.Add);
            _match.Begin();
        }

        [Test]
        public void 사라진_유닛은_전부_소멸_이벤트를_냈다()
        {
            for (int i = 0; i < 6; i++) _match.Apply(Command.DebugSpawnEnemy(0, new int2(i, 0)));
            _match.Apply(Command.PlaceDefender(0, new int2(0, 1)));   // id 7
            _match.Apply(Command.DebugDestroy(new SimEntityId(2)));
            _match.Apply(Command.DebugDestroy(new SimEntityId(5)));
            _match.Apply(Command.Retire(new SimEntityId(7)));

            var live = new HashSet<int>();
            for (int i = 0; i < _spawned.Count; i++) live.Add(_spawned[i].A.Value);
            for (int i = 0; i < _destroyed.Count; i++)
                Assert.IsTrue(live.Remove(_destroyed[i].A.Value),
                    $"id {_destroyed[i].A.Value} 가 두 번 소멸했거나 스폰된 적이 없다");

            Assert.AreEqual(live.Count, _match.World.Count,
                "스폰 − 소멸 이벤트 = 월드에 남은 개체. 어긋나면 이벤트 없이 사라진 유닛이 있다");
            foreach (int id in live)
                Assert.IsTrue(_match.World.IsAlive(new SimEntityId(id)));
        }

        [Test]
        public void 소멸_이벤트는_발화_시점의_값을_싣는다()
        {
            _match.Apply(Command.DebugSpawnEnemy(0, new int2(2, 3)));
            var unit = _match.World.Find(new SimEntityId(1));
            float radius = unit.HitRadius;
            float3 pos = unit.Position;

            _match.Apply(Command.DebugDestroy(new SimEntityId(1)));

            Assert.AreEqual(1, _destroyed.Count);
            var e = _destroyed[0];

            // 드레인 시점에 되물으면 0 이 나온다 — 개체가 이미 없기 때문이다.
            // 그래서 이벤트가 값을 **스냅샷**한다(제약 13 의 사망 폭발 반경과 같은 이유).
            Assert.AreEqual(radius, e.SiteFired.OriginBody, 1e-5f, "몸 반경이 스냅샷돼야 한다");
            Assert.AreEqual(pos.x, e.SiteFired.Pos.x, 1e-5f);
            Assert.AreEqual(pos.z, e.SiteFired.Pos.z, 1e-5f);
            Assert.AreEqual((int)UnitKind.Enemy, e.Arg);
            Assert.IsNull(_match.World.Find(new SimEntityId(1)));
        }

        [Test]
        public void 중복_소멸은_사건이_아니다()
        {
            _match.Apply(Command.DebugSpawnEnemy(0, new int2(0, 0)));
            Assert.IsTrue(_match.Apply(Command.DebugDestroy(new SimEntityId(1))).Accepted);

            var second = _match.Apply(Command.DebugDestroy(new SimEntityId(1)));
            Assert.IsFalse(second.Accepted);
            Assert.AreEqual(RejectReason.NoSuchEntity, second.Reason);
            Assert.AreEqual(1, _destroyed.Count, "소멸 이벤트가 두 번 나면 뷰가 두 번 지운다");
        }

        [Test]
        public void 퇴근한_칸은_다시_쓸_수_있다()
        {
            var cell = new int2(1, 1);
            Assert.IsTrue(_match.Apply(Command.PlaceDefender(0, cell)).Accepted);
            Assert.AreEqual(RejectReason.Occupied, _match.Apply(Command.PlaceDefender(0, cell)).Reason);

            Assert.IsTrue(_match.Apply(Command.Retire(new SimEntityId(1))).Accepted);

            // 점유 해제가 소멸과 짝이 아니면, 죽은 유닛이 칸을 영영 물고 있게 된다.
            Assert.IsTrue(_match.Apply(Command.PlaceDefender(0, cell)).Accepted);
        }
    }
}
