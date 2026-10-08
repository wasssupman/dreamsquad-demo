using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 1 완료 기준 ④ — id 발급 순번과 `None` 의 자리.
    [TestFixture]
    public class SimEntityIdTests
    {
        [Test]
        public void 센티널이_unit0_결정과_같다()
        {
            Assert.AreEqual(0, SimEntityId.Match.Value, "판 호스트 = 0");
            Assert.AreEqual(-1, SimEntityId.None.Value, "부재 = -1");
            Assert.AreEqual(1, SimEntityId.FirstSpawnValue, "스폰은 1부터");

            Assert.IsTrue(SimEntityId.None.IsNone);
            Assert.IsFalse(SimEntityId.None.IsEntity);
            Assert.IsFalse(SimEntityId.Match.IsEntity, "판 호스트는 개체가 아니다");
        }

        [Test]
        public void None_은_오름차순에서_범위_밖이다()
        {
            // -1 을 고른 이유: 미발급이 정렬에서 «맨 앞»이 아니라 «범위 밖»이 되어,
            // 목록에 섞이면 0번 host 를 밀어내는 대신 곧바로 눈에 띈다.
            Assert.Less(SimEntityId.None.CompareTo(SimEntityId.Match), 0);
            Assert.Less(SimEntityId.None.CompareTo(new SimEntityId(1)), 0);
            Assert.Greater(new SimEntityId(1).CompareTo(SimEntityId.Match), 0);
        }

        [Test]
        public void 스폰_id_는_1부터_단조_증가한다()
        {
            var match = new BattleMatch(CoreGoldenCorpus.Fixture(1));
            var spawned = new List<CoreEvent>();
            match.Bus.Subscribe(CoreEventKind.UnitSpawned, 0, spawned.Add);
            match.Begin();

            for (int i = 0; i < 5; i++)
                match.Apply(Command.DebugSpawnEnemy(0, new int2(i, 0)));

            Assert.AreEqual(5, spawned.Count);
            for (int i = 0; i < 5; i++)
                Assert.AreEqual(i + 1, spawned[i].A.Value, $"{i}번째 스폰의 id");
        }

        [Test]
        public void 소멸한_id_는_재사용되지_않는다()
        {
            var match = new BattleMatch(CoreGoldenCorpus.Fixture(1));
            match.Begin();

            match.Apply(Command.DebugSpawnEnemy(0, new int2(0, 0)));   // id 1
            match.Apply(Command.DebugDestroy(new SimEntityId(1)));
            match.Apply(Command.DebugSpawnEnemy(0, new int2(1, 0)));   // id 2 — 1 이 아니다

            Assert.IsFalse(match.World.IsAlive(new SimEntityId(1)));
            Assert.IsTrue(match.World.IsAlive(new SimEntityId(2)));
        }

        [Test]
        public void 개체_목록은_언제나_id_오름차순이다()
        {
            var match = new BattleMatch(CoreGoldenCorpus.Fixture(1));
            match.Begin();

            for (int i = 0; i < 8; i++) match.Apply(Command.DebugSpawnEnemy(0, new int2(i, 0)));
            // 가운데를 지운다 — 끝만 지우면 잘못된 구현도 통과한다.
            match.Apply(Command.DebugDestroy(new SimEntityId(3)));
            match.Apply(Command.DebugDestroy(new SimEntityId(6)));
            match.Apply(Command.DebugSpawnEnemy(0, new int2(9, 0)));

            var units = match.World.Units;
            for (int i = 1; i < units.Count; i++)
                Assert.Less(units[i - 1].Id.Value, units[i].Id.Value,
                    "순회 순서가 결정론의 축이다(계약 5)");
        }
    }
}
