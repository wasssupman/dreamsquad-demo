using NUnit.Framework;
using Wassup.Skills;
using Wassup.BattleCore;
using Wassup.BattleCore.Goals;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 4 — 목표 concrete 3 과 성적.
    [TestFixture]
    public class MatchGoalTests
    {
        private static BattleMatch Run(string scenario, out CoreTrace trace)
        {
            var sc = CoreGoldenCorpus.ByName(scenario);
            var result = CoreHarness.Run(sc.BuildDefinition(), sc.BuildSchedule(), sc.Ticks, sc.Name);
            trace = result.Trace;
            return result.Match;
        }

        [Test]
        public void 시간_점수_목표는_만료로_끝나고_점수는_킬_생값이다()
        {
            var match = Run("kill_race_3min", out var trace);

            Assert.IsTrue(match.Clock.Ended);
            Assert.AreEqual(MatchEndReason.Complete, match.Clock.EndReason);
            Assert.AreEqual(180f, match.Clock.BattleTime, 0.02f, "배치 창은 전투 시계에 안 들어간다");
            Assert.Greater(trace.finalKills, 0);

            var outcome = match.Outcome;
            Assert.AreEqual(OutcomeKind.Complete, outcome.Kind);
            Assert.AreEqual(match.Score.SubmissionScore, outcome.Score, "가공이 없다");
            Assert.AreEqual(SortDirection.Descending, outcome.Sort);
            Assert.IsTrue(outcome.SubmitsReport, "v1 제출은 이 모드만");
        }

        [Test]
        public void 웨이브_클리어_목표는_마지막_웨이브_전멸로_끝난다()
        {
            var match = Run("wave_clear_8", out _);

            Assert.IsTrue(match.Clock.Ended);
            Assert.AreEqual(MatchEndReason.Complete, match.Clock.EndReason);
            Assert.AreEqual(8, match.Waves.WaveReached);
            Assert.IsTrue(match.Waves.LastWaveDispatchedAndFieldClear);

            var outcome = match.Outcome;
            Assert.AreEqual(OutcomeKind.Complete, outcome.Kind);
            Assert.AreEqual(8, outcome.Score, "점수는 도달 웨이브 — 부분 달성을 줄 세우는 축이다");
            Assert.AreEqual(SortDirection.Descending, outcome.Sort);
            Assert.IsFalse(outcome.SubmitsReport);
        }

        [Test]
        public void 타임어택_목표는_같은_신호에_반대_정렬이다()
        {
            var match = Run("time_attack_8", out _);

            Assert.IsTrue(match.Clock.Ended);
            var outcome = match.Outcome;
            Assert.AreEqual(OutcomeKind.Complete, outcome.Kind);
            Assert.AreEqual(SortDirection.Ascending, outcome.Sort, "작을수록 좋다");
            Assert.AreEqual((int)System.Math.Round(match.Clock.BattleTime * 1000.0), outcome.Score,
                "밀리초 — 초로 접으면 같은 초 안의 차이가 동점이 되고 그 동점을 아무도 안 정했다");
            Assert.Greater(outcome.Score, 0);
        }

        [Test]
        public void 웨이브_클리어에서_마음이_부서지면_패배다()
        {
            var match = Run("heart_collapse", out _);

            Assert.IsTrue(match.Clock.Ended);
            Assert.AreEqual(MatchEndReason.StressFull, match.Clock.EndReason,
                "넷째 통로를 만들지 않는다 — 라벨은 목표가 붙인다");

            var outcome = match.Outcome;
            Assert.AreEqual(OutcomeKind.Defeat, outcome.Kind,
                "「지지 않는다」는 이제 `KillScoreTimed` 의 성질이지 게임의 성질이 아니다");
            Assert.AreEqual(MatchEndReason.StressFull, outcome.Reason);
            Assert.Greater(outcome.Leaks, 0);
        }

        [Test]
        public void 시간_점수_목표는_붕괴해도_같은_잣대다()
        {
            var def = CoreGoldenCorpus.CollapseFixture(5150);
            def.Mode.Goal = GoalKind.KillScoreTimed;
            def.Mode.Clock = ClockKind.FixedLimit;
            def.Mode.TargetWaves = 0;
            def.ConfigHash = def.ComputeConfigHash();

            var match = new BattleMatch(def);
            match.Begin();
            match.Apply(Command.FinishPlacement());
            for (int t = 0; t < 6000 && !match.Clock.Ended; t++) match.Tick();

            Assert.AreEqual(MatchEndReason.StressFull, match.Clock.EndReason);
            Assert.AreEqual(OutcomeKind.Complete, match.Outcome.Kind,
                "남은 시간을 몰수당할 뿐, 그때까지의 처치 수가 같은 잣대로 줄 세워진다");
        }

        [Test]
        public void 제출은_모드가_열어_준_경우에만_된다()
        {
            var def = CoreMatchFixtures.Definition();
            def.Mode.AllowSubmit = false;
            def.Mode.SubmitUnlockSeconds = 0f;
            def.ConfigHash = def.ComputeConfigHash();
            var match = CoreMatchFixtures.BeginBattle(def);

            Assert.AreEqual(RejectReason.SubmitLocked, match.Apply(Command.Submit()).Reason,
                "「제출」 어휘는 그 모드에만 있다");

            var def2 = CoreMatchFixtures.Definition();
            def2.Mode.SubmitUnlockSeconds = 0f;
            def2.ConfigHash = def2.ComputeConfigHash();
            var match2 = CoreMatchFixtures.BeginBattle(def2);
            Assert.IsTrue(match2.Apply(Command.Submit()).Accepted);
            Assert.AreEqual(OutcomeKind.Submitted, match2.Outcome.Kind);
        }

        [Test]
        public void 성적의_조립_지점은_하나다()
        {
            var match = Run("kill_race_3min", out _);
            var a = match.Outcome;
            var b = match.Outcome;

            Assert.AreEqual(a.Score, b.Score);
            Assert.AreEqual(a.Kind, b.Kind);
            Assert.AreEqual(a.ModeId, b.ModeId);
            Assert.AreEqual(a.ModeId, a.LeaderboardId, "리더보드 키가 비면 modeId 가 그 자리다");
        }

        [Test]
        public void 읽기_모델은_목표마다_뜻이_다르다()
        {
            var timed = Run("kill_race_3min", out _);
            Assert.AreEqual(timed.Score.Kills, timed.GoalRead.Current);
            Assert.AreEqual(0, timed.GoalRead.Target, "끝내는 것은 시계다");

            var clear = Run("wave_clear_8", out _);
            Assert.AreEqual(clear.Waves.WaveReached, clear.GoalRead.Current);
            Assert.AreEqual(8, clear.GoalRead.Target);
        }

        [Test]
        public void 세는_시계는_만료로_끝나지_않는다()
        {
            var def = CoreGoldenCorpus.WaveFixture(1717, GoalKind.WaveClear, ClockKind.CountUp, 8);
            def.Mode.MatchSeconds = 1f;   // 만료가 있었다면 1초에 끝났을 것이다
            def.ConfigHash = def.ComputeConfigHash();

            var match = new BattleMatch(def);
            match.Begin();
            match.Apply(Command.FinishPlacement());
            for (int t = 0; t < 600; t++) match.Tick();

            Assert.IsFalse(match.Clock.Ended, "끝내는 것은 목표다 — 시계는 세기만 한다");
            Assert.Greater(match.Clock.BattleTime, 1f);
        }
    }
}
