using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.BattleCore;
using Somnia.Battle.UnitAi;
using static Somnia.Battle.Tests.EditMode.Core.CoreCombatFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 8a2 행 7 — **방어유닛 AI 전이 트레이스**(옛 `BattleBridge.TraceDefenderAiTransition`,
    // `BattleBridge.cs:4127-4136` · 옛 채널 22). 옛 규칙 두 줄:
    //   ⑴ 「변할 때만 한 줄」 — 같은 상태가 이어지면 사건이 없다.
    //   ⑵ 값 = 유닛 id · 바뀐 상태(옛 `i`). 새 사건은 **이전 상태**도 싣는다(값 스냅샷 — 드레인 시점에 되묻지 않는다).
    // 채널은 트레이스에 있고(`CoreTraceChannel.DefenderAiChanged`) 골든 하네스는 구독하지 않는다(`GimmickTriggered` 형).
    public class DefenderAiTraceTests
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

        [Test]
        public void 상태가_변할_때만_한_건이고_이전과_이후를_값으로_싣는다()
        {
            var m = SummonerBoard(out var summoner);
            var got = new List<CoreEvent>();
            m.Bus.Subscribe(CoreEventKind.DefenderAiChanged, 0, e => got.Add(e));

            Tick(m, 2);
            Assert.IsNotNull(First(m, UnitKind.Patrol), "순찰병이 나와야 유지중 전이가 생긴다");
            Tick(m, 2);
            Assert.AreEqual(DefenderAiState.Sustaining, summoner.Ai.Defender, "소환물 생존 = 유지중");

            var mine = got.FindAll(e => e.A == summoner.Id);
            Assert.IsNotEmpty(mine, "대기 → 유지중 전이가 사건으로 나와야 한다");
            var last = mine[mine.Count - 1];
            Assert.AreEqual((int)DefenderAiState.Sustaining, last.Arg, "Arg = 바뀐 뒤");
            Assert.AreNotEqual((float)DefenderAiState.Sustaining, last.Amount, "Amount = 바뀌기 전(다른 값)");
            Assert.AreEqual(summoner.DefIndex, last.DefIndex, "유닛 줄을 값으로 싣는다");

            // ⑴ 같은 상태가 이어지는 동안 사건이 없다.
            int before = got.Count;
            var patrol = First(m, UnitKind.Patrol);
            for (int t = 0; t < 30 && patrol != null && !patrol.Dead; t++) m.Tick();
            int sameStateEvents = got.FindAll(e => e.A == summoner.Id && e.Tick > last.Tick
                                                   && e.Arg == (int)DefenderAiState.Sustaining).Count;
            Assert.AreEqual(0, sameStateEvents, "같은 상태 재진입이 아닌 한 반복 사건이 없다");

            // 소환물이 사라지면 유지중에서 **빠지는** 전이가 한 건 나온다.
            m.Apply(Command.DebugDestroy(First(m, UnitKind.Patrol).Id));
            Tick(m, 1);
            var exit = got.FindLast(e => e.A == summoner.Id);
            Assert.AreEqual((float)DefenderAiState.Sustaining, exit.Amount, "이전 = 유지중");
            Assert.AreNotEqual((int)DefenderAiState.Sustaining, exit.Arg, "이후 = 유지중이 아니다");
            Assert.Greater(got.Count, before, "빠지는 전이가 기록됐다");
        }

        [Test]
        public void 트레이스_채널로_기록된다_골든_하네스는_구독하지_않는다()
        {
            var trace = new CoreTrace();
            var m = SummonerBoard(out var summoner);
            m.Bus.Subscribe(CoreEventKind.DefenderAiChanged, 0, trace.Record);
            Tick(m, 4);
            var rows = trace.events.FindAll(r => r.channel == CoreTraceChannel.DefenderAiChanged);
            Assert.IsNotEmpty(rows, "구독하면 채널 61 로 기록된다");
            Assert.AreEqual(summoner.Id.Value, rows[0].a, "a = 유닛 id");
            Assert.AreEqual(61, (int)CoreTraceChannel.DefenderAiChanged, "append-only 번호 핀");
        }
    }
}
