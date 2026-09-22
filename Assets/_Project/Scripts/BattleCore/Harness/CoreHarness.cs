using System.Collections.Generic;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 엔진 없이 판을 돌리는 러너.
    //
    // 옛 하네스와 **병존**한다(계약 3). 둘이 동시에 있는 동안만 A/B 비교가 가능하고,
    // 옛 러너는 unit 9 에서 은퇴한다. 비교 축은 id 가 아니라 **순서**다 — 센티널이
    // 다르기 때문이다(unit 0 항목 9).
    //
    // 옛 `SimHarnessClock` 의 `Time.captureDeltaTime` 고정은 옮기지 않았다.
    // 코어는 프레임을 모르고, 시간은 틱 수로 만든다.
    public static class CoreHarness
    {
        public sealed class Result
        {
            public CoreTrace Trace;
            public BattleMatch Match;
            public List<Receipt> Receipts;
        }

        /// <summary>
        /// 시나리오 하나를 끝까지 돌린다.
        ///
        /// 한 틱의 차례: **이 틱의 커맨드 → `match.Tick()`**. 커맨드를 뒤에 놓으면
        /// 「틱 t 에 배치」가 실제로는 t+1 부터 효력이 생겨 골든이 한 틱씩 밀린다.
        ///
        /// `ticks` 를 다 돌기 전에 판이 끝나면(제출·만료) 남은 틱은 no-op 이다 —
        /// 일부러 멈추지 않는다. 「끝난 판에 틱을 더 줘도 아무 일도 없다」가 계약 5 이고,
        /// 그것을 골든이 증언해야 한다.
        /// </summary>
        public static Result Run(MatchDefinition def, CommandSchedule schedule, int ticks, string scenario)
        {
            var match = new BattleMatch(def);
            var trace = new CoreTrace
            {
                scenario = scenario ?? "",
                configHash = def.ConfigHash ?? "",
                matchSeed = def.Seed,
                stepDt = BattleMatch.Dt,
                tickCount = ticks,
            };

            // 기록은 **버스 구독**으로 한다 — 틱 루프가 「무엇을 기록할지」를 알면
            // 기록이 규칙의 일부가 된다. 순서 0 = 누구보다 먼저(담당자가 사건을 보고
            // 상태를 바꾸기 전의 값을 남긴다).
            match.Bus.Subscribe(CoreEventKind.MatchStarted, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.UnitSpawned, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.UnitDestroyed, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.MatchEnded, 0, trace.Record);
            // unit 2 — 맵·이동의 사건. **새 채널을 열면 여기 구독도 같이 연다** — 안 열면
            // 골든이 그 규칙에 대해 아무 말도 하지 않은 채 초록이 된다(조용한 무증언).
            match.Bus.Subscribe(CoreEventKind.GoalReached, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.Detected, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.AggroAcquired, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.Blinked, 0, trace.Record);
            // unit 3 — 전투 판정의 사건. 새 채널을 열면 여기 구독도 같이 연다.
            match.Bus.Subscribe(CoreEventKind.AttackResolved, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.ProjectileSpawned, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.ProjectileDespawned, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.ProjectileHit, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.DamageApplied, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.HealApplied, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.ShieldBroken, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.UnitSlain, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.Knockup, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.LeapAscend, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.LeapDescend, 0, trace.Record);
            // unit 4 — 매치 담당자의 사건. 새 채널을 열면 여기 구독도 같이 연다.
            match.Bus.Subscribe(CoreEventKind.WaveQueued, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.WaveStarted, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.BonusOffered, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.BonusPulled, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.CostChanged, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.Placed, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.Retired, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.PlacementRejected, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.DefenderActivated, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.HeartChanged, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.HeartCollapsed, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.GimmickAssigned, 0, trace.Record);
            match.Bus.Subscribe(CoreEventKind.PlacementPhaseChanged, 0, trace.Record);

            int kills = 0;
            match.Bus.Subscribe(CoreEventKind.UnitSlain, 1, _ => kills++);

            // 구독 **뒤에** 시작한다 — `Begin` 이 `MatchStarted` 를 그 자리에서 배달한다.
            match.Begin();

            var receipts = new List<Receipt>(schedule?.Count ?? 0);
            schedule?.Rewind();

            for (int t = 0; t < ticks; t++)
            {
                schedule?.ApplyDue(match, t, receipts);
                match.Tick();
                match.ClearEvents();   // outbox 는 Unity 층의 것 — 하네스는 구독으로 받는다
            }

            trace.finalStateHash = match.World.StateHash();
            // 처치 수는 사건을 세어 만든다 — **피해로 죽은 것**의 수이고 진영을 안 가린다.
            trace.finalKills = kills;
            // unit 4 — 점수·유출은 담당자가 말한다. 처치 수와 점수가 **다를 수 있다**:
            // 점수는 적을 잡은 것만 세고(방어유닛의 죽음은 처치가 아니다), 유출은 처치가
            // 아니라 「돌격형이 마음을 치고 산화한 수」다(Y9).
            trace.finalScore = match.Score.SubmissionScore;
            trace.finalLeaks = match.Heart.Leaks;
            return new Result { Trace = trace, Match = match, Receipts = receipts };
        }
    }
}
