using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Wassup.Battle.Combat;

namespace Wassup.Tests.EditMode
{
    // nightmare-catcher unit 1 — pure threat math invariants: find-or-append
    // accumulation, max-damage leader, simId tie determinism(unit 1 이 Entity.Index
    // 축 교체), dead attackers excluded via the caller-resolved alive mask, empty → Null.
    public class ThreatTableTests
    {
        private static Entity E(int index) => new Entity { Index = index, Version = 1 };

        private static Entity Leader(ThreatEntry[] entries, bool[] alive, int[] simIds = null)
        {
            using var entriesNa = new NativeArray<ThreatEntry>(entries, Allocator.Temp);
            using var aliveNa = new NativeArray<bool>(alive, Allocator.Temp);
            // 기본값: Entity.Index 를 simId 로 재사용 — 기존 케이스의 기대값 보존.
            simIds ??= System.Array.ConvertAll(entries, e => e.attacker.Index);
            using var simNa = new NativeArray<int>(simIds, Allocator.Temp);
            return ThreatTable.Leader(entriesNa, aliveNa, simNa);
        }

        [Test]
        public void Leader_EmptyTable_ReturnsNull()
        {
            Assert.AreEqual(Entity.Null, Leader(new ThreatEntry[0], new bool[0]));
        }

        [Test]
        public void Leader_PicksHighestCumulativeDamage()
        {
            var leader = Leader(new[]
            {
                new ThreatEntry { attacker = E(1), cumulativeDamage = 10f },
                new ThreatEntry { attacker = E(2), cumulativeDamage = 55f },
                new ThreatEntry { attacker = E(3), cumulativeDamage = 30f },
            }, new[] { true, true, true });
            Assert.AreEqual(E(2), leader);
        }

        [Test]
        public void Leader_Tie_PrefersLowerSimId_RegardlessOfOrder()
        {
            var a = new ThreatEntry { attacker = E(7), cumulativeDamage = 40f };
            var b = new ThreatEntry { attacker = E(3), cumulativeDamage = 40f };
            Assert.AreEqual(E(3), Leader(new[] { a, b }, new[] { true, true }));
            Assert.AreEqual(E(3), Leader(new[] { b, a }, new[] { true, true }));
        }

        [Test]
        public void Leader_Tie_UsesSimId_NotEntityIndex()
        {
            // battle-sim-extraction unit 1 — Entity.Index 가 커도 simId(스폰 순서)가
            // 낮으면 이긴다. 축이 교체됐음을 고정하는 테스트.
            var a = new ThreatEntry { attacker = E(3), cumulativeDamage = 40f };
            var b = new ThreatEntry { attacker = E(7), cumulativeDamage = 40f };
            Assert.AreEqual(E(7), Leader(new[] { a, b }, new[] { true, true }, new[] { 5, 1 }));
        }

        [Test]
        public void Leader_DeadAttackerExcluded_FallsToNextAlive()
        {
            var leader = Leader(new[]
            {
                new ThreatEntry { attacker = E(1), cumulativeDamage = 100f },
                new ThreatEntry { attacker = E(2), cumulativeDamage = 60f },
            }, new[] { false, true });
            Assert.AreEqual(E(2), leader);
        }

        [Test]
        public void Leader_AllDead_ReturnsNull()
        {
            var leader = Leader(new[]
            {
                new ThreatEntry { attacker = E(1), cumulativeDamage = 100f },
            }, new[] { false });
            Assert.AreEqual(Entity.Null, leader);
        }

        [Test]
        public void Accumulate_SameAttacker_SumsIntoOneEntry()
        {
            using var world = new World("ThreatTableTests");
            var em = world.EntityManager;
            var boss = em.CreateEntity();
            var table = em.AddBuffer<ThreatEntry>(boss);

            ThreatTable.Accumulate(table, E(1), 10f);
            ThreatTable.Accumulate(table, E(1), 15f);

            Assert.AreEqual(1, table.Length);
            Assert.AreEqual(E(1), table[0].attacker);
            Assert.AreEqual(25f, table[0].cumulativeDamage);
        }

        [Test]
        public void Accumulate_DistinctAttackers_IndependentEntries()
        {
            using var world = new World("ThreatTableTests");
            var em = world.EntityManager;
            var boss = em.CreateEntity();
            var table = em.AddBuffer<ThreatEntry>(boss);

            ThreatTable.Accumulate(table, E(1), 10f);
            ThreatTable.Accumulate(table, E(2), 20f);
            ThreatTable.Accumulate(table, E(1), 5f);

            Assert.AreEqual(2, table.Length);
            Assert.AreEqual(15f, table[0].cumulativeDamage);
            Assert.AreEqual(20f, table[1].cumulativeDamage);
        }
    }
}
