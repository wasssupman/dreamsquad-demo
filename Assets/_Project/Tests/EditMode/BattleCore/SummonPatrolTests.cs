using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using static Somnia.Battle.Tests.EditMode.Core.CoreCombatFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7d — **소환사의 순찰병**과 그 디버그 문(tools.md 10 `PatrolDebugMenu` 의 후계).
    //
    // ⑴ 소환물이 살아 있어도 쿨은 돌고 **스폰만** 건너뛴다(C2 — 안 돌리면 소환물이 죽는 즉시 재소환된다).
    // ⑵ 순찰병의 구역 = 소환사 칸 앵커 · 반경 = 사거리 칸 수(최소 1).
    // ⑶ 디버그 소환은 소환사와 **같은 조립 자리**(`CombatPhase.SpawnPatrol`)를 지난다 — 두 벌이면 「어떤 길로
    //    태어났나」가 구역·이동·공격을 바꾼다. 소환사가 없으니 연쇄 소멸도 없다.
    public class SummonPatrolTests
    {
        private static BattleMatch SummonerBoard(out Unit summoner)
        {
            var def = Definition(policy: AttackPolicy.Summon, defenderRange: 3f);
            def.Units[0].Attack.Policy = (int)AttackPolicy.Summon;
            def.Units[0].Attack.SummonPatrolDefIndex = 1;
            def.ConfigHash = def.ComputeConfigHash();
            var m = new BattleMatch(def);
            m.Begin();
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            summoner = First(m, UnitKind.Defender);
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            return m;
        }

        private static int CountPatrols(BattleMatch m)
        {
            int n = 0;
            var units = m.World.Units;
            for (int i = 0; i < units.Count; i++) if (units[i].Kind == UnitKind.Patrol && !units[i].Dead) n++;
            return n;
        }

        [Test]
        public void 소환물이_살아있는_동안_쿨은_돌고_스폰만_건너뛴다()
        {
            var m = SummonerBoard(out var summoner);
            Tick(m, 2);
            Assert.AreEqual(1, CountPatrols(m), "구역에 적이 있으면 첫 순찰병이 나온다");

            // 쿨 한 바퀴를 넘게 돌려도 둘째가 안 선다 — 그동안 쿨은 계속 움직인다.
            int changes = 0;
            float last = summoner.Attack.CooldownRemaining;
            for (int t = 0; t < 180; t++)
            {
                m.Tick();
                if (summoner.Attack.CooldownRemaining != last) changes++;
                last = summoner.Attack.CooldownRemaining;
            }
            Assert.AreEqual(1, CountPatrols(m), "소환물이 살아 있으면 스폰만 건너뛴다");
            Assert.Greater(changes, 60, "그동안에도 쿨은 돈다");
        }

        [Test]
        public void 소환물이_죽으면_즉시가_아니라_남은_쿨이_다_돈_뒤에_다시_나온다()
        {
            var m = SummonerBoard(out var summoner);
            Tick(m, 2);
            var patrol = First(m, UnitKind.Patrol);
            Assert.IsNotNull(patrol);

            // 쿨이 넉넉히 남은 순간을 고른다(0 근처면 「즉시」와 구분이 안 된다).
            for (int guard = 0; guard < 120 && summoner.Attack.CooldownRemaining < summoner.Attack.Interval * 0.5f; guard++)
                m.Tick();
            int waitTicks = (int)math.floor(summoner.Attack.CooldownRemaining * 60f) - 2;
            Assert.Greater(waitTicks, 5, "고른 순간에 쿨이 남아 있어야 한다");

            m.Apply(Command.DebugDestroy(patrol.Id));
            Tick(m, waitTicks);
            Assert.AreEqual(0, CountPatrols(m), "쿨이 남아 있는 동안은 재소환이 없다(C2)");
            Tick(m, 10);
            Assert.AreEqual(1, CountPatrols(m), "쿨이 다 돌면 다시 나온다");
        }

        [Test]
        public void 순찰병의_구역은_소환사_칸이_앵커고_반경은_사거리_칸수다()
        {
            var m = SummonerBoard(out var summoner);
            Tick(m, 2);
            var patrol = First(m, UnitKind.Patrol);
            Assert.IsNotNull(patrol);
            var anchor = m.Map.CellOf(summoner.Position);
            Assert.AreEqual(anchor, patrol.Patrol.Anchor);
            Assert.AreEqual(anchor, patrol.Patrol.Home);
            Assert.AreEqual(math.max(1, Somnia.Battle.Skills.SkillMath.RangeToTiles(summoner.Attack.Range)), patrol.Patrol.Radius);
            Assert.AreEqual(summoner.Id, patrol.Patrol.SummonedBy);
        }

        [Test]
        public void 디버그_소환은_소환사와_같은_조립을_지나고_소환사가_없다()
        {
            var summoned = SummonerBoard(out _);
            Tick(summoned, 2);
            var viaSummoner = First(summoned, UnitKind.Patrol);

            var def = Definition();
            var m = new BattleMatch(def);
            m.Begin();
            var r = m.Apply(Command.DebugSummonPatrol(1, new int2(4, 1), viaSummoner.Patrol.Radius));
            Assert.IsTrue(r.Accepted, r.Reason.ToString());
            var viaDebug = First(m, UnitKind.Patrol);
            Assert.IsNotNull(viaDebug);

            Assert.AreEqual(viaSummoner.Patrol.Anchor, viaDebug.Patrol.Anchor);
            Assert.AreEqual(viaSummoner.Patrol.Radius, viaDebug.Patrol.Radius);
            Assert.AreEqual(viaSummoner.Move.Speed, viaDebug.Move.Speed);
            Assert.AreEqual(viaSummoner.Move.TraversalLayers, viaDebug.Move.TraversalLayers);
            Assert.AreEqual(viaSummoner.MaxHealth, viaDebug.MaxHealth);
            Assert.AreEqual(m.Map.CenterOf(new int2(4, 1)), viaDebug.Position, "자리 = 앵커 칸 중심(소환사와 같은 자리 규칙)");
            Assert.IsNotNull(viaDebug.Attack, "공격 상태도 같은 조립에서 선다");
            Assert.IsTrue(viaDebug.Patrol.SummonedBy.IsNone, "소환사가 없다 — 연쇄 소멸의 주인이 없다");

            Tick(m, 30);
            Assert.IsFalse(viaDebug.Dead, "주인 없는 순찰병은 소환사 연쇄로 사라지지 않는다");
        }

        [Test]
        public void 디버그_소환은_없는_줄과_판_밖_칸을_거절한다()
        {
            var m = new BattleMatch(Definition());
            m.Begin();
            Assert.AreEqual(RejectReason.InvalidUnit, m.Apply(Command.DebugSummonPatrol(9, new int2(4, 1), 2)).Reason);
            Assert.AreEqual(RejectReason.OutOfBounds, m.Apply(Command.DebugSummonPatrol(1, new int2(99, 1), 2)).Reason);
            Assert.AreEqual(0, CountPatrols(m));
        }
    }
}
