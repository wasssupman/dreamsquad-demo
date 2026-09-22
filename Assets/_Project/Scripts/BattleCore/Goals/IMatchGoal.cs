namespace Wassup.BattleCore.Goals
{
    // battle-core-rebuild unit 4 — **모드 로직의 자리.**
    //
    // 담당자(`MatchClock`·`WaveScheduler`·`HeartMeter`·`ScoreLedger`·…)는 모드를 모른다.
    // 그들이 아는 것은 자기 파라미터뿐이고, 「이 판이 언제 끝나고 몇 점인가」라는 **두 판정**을
    // 목표가 소유한다. 그래서 담당자 안에 `if (mode == …)` 가 없다(계약 12 · 제약 5).
    //
    // **종료의 «사유»는 담당자가, «의미»는 목표가.** `HeartMeter` 는 모드 무관하게 붕괴 시
    // `stress_full` 을 연다(통로는 3 그대로). 그 판이 「같은 잣대로 줄 세워지는 판」인지
    // 「웨이브 7/12 에서 멈춘 판」인지는 `BuildOutcome` 이 정한다.
    //
    // ⚠ 목표는 **개체 상태를 들지 않는다.** 들면 그것이 아홉 번째 담당자가 되고, 다음 사람이
    // 「이 상태는 누구 것인가」를 물을 때 답이 둘이 된다.
    public interface IMatchGoal
    {
        /// <summary>판이 열릴 때 한 번. 목표가 자기 진행값을 세운다.</summary>
        void OnBegin(MatchGoalContext ctx);

        /// <summary>매 틱. **끝났나**만 판정한다 — 점수는 여기서 안 센다.</summary>
        void OnTick(MatchGoalContext ctx);

        /// <summary>판이 끝난 뒤 한 번. 점수·표기·정렬 방향을 정한다.</summary>
        MatchOutcome BuildOutcome(MatchGoalContext ctx);

        /// <summary>HUD·결과 화면이 읽는 것.</summary>
        GoalReadModel Read(MatchGoalContext ctx);
    }

    /// <summary>
    /// 목표가 보는 세계. **담당자 읽기 모델 묶음 + 쓰기 권한 하나**(`EndMatch`)다.
    ///
    /// 담당자 인스턴스를 그대로 노출하지 않는 이유: 노출하면 목표가 코스트를 깎거나 웨이브를
    /// 밀 수 있게 되고, 그 순간 목표가 아홉 번째 담당자가 된다. 여기 있는 것은 전부 읽기이고
    /// 쓰기는 한 문뿐이다.
    /// </summary>
    public sealed class MatchGoalContext
    {
        private readonly MatchClock _clock;
        private readonly ScoreLedger _score;
        private readonly WaveScheduler _waves;
        private readonly HeartMeter _heart;
        private readonly ModeDef _mode;

        public MatchGoalContext(MatchClock clock, ScoreLedger score, WaveScheduler waves,
                                HeartMeter heart, in ModeDef mode)
        {
            _clock = clock;
            _score = score;
            _waves = waves;
            _heart = heart;
            _mode = mode;
        }

        // ── 읽기 모델 ────────────────────────────────────────────────────────

        public bool Ended => _clock.Ended;
        public MatchEndReason EndReason => _clock.EndReason;
        public MatchPhase Phase => _clock.Phase;
        public float BattleTime => _clock.BattleTime;
        public float Remaining => _clock.Remaining;

        public int Kills => _score.Kills;
        public int SubmissionScore => _score.SubmissionScore;

        public int WaveReached => _waves.WaveReached;
        public int WaveCount => _waves.WaveCount;

        /// <summary>
        /// 「마지막 웨이브가 나갔고 필드가 비었다」. `WaveClear`·`TimeAttack` 이 공유하는 **한 줄**이고
        /// 그 술어의 주인은 `WaveScheduler` 다 — 목표가 자기 버전을 다시 만들면 두 답이 생긴다.
        /// </summary>
        public bool LastWaveCleared => _waves.LastWaveDispatchedAndFieldClear;

        public bool FieldClear => _waves.FieldClear;

        public float Heart => _heart.Health;
        public float HeartMax => _heart.MaxHealth;
        public float Stress => _heart.Stress;
        public bool HeartCollapsed => _heart.Collapsed;
        public int Leaks => _heart.Leaks;

        public ModeDef Mode => _mode;

        // ── 쓰기 권한(하나) ──────────────────────────────────────────────────

        /// <summary>
        /// 판을 끝낸다. **목표가 가진 유일한 쓰기**다. 사유는 언제나 `Complete` 이고
        /// (목표 달성은 정상 종료다) 붕괴·제출은 각자 자기 통로로 들어온다.
        /// </summary>
        public void Complete() => _clock.EndMatch(MatchEndReason.Complete);
    }

    public enum OutcomeKind : byte
    {
        /// <summary>같은 잣대로 줄 세워지는 판.</summary>
        Complete = 0,
        /// <summary>목표를 못 이루고 끝난 판. `WaveClear`·`TimeAttack` 의 마음 붕괴가 여기다.</summary>
        Defeat = 1,
        /// <summary>플레이어가 제출해 끝낸 판.</summary>
        Submitted = 2,
    }

    /// <summary>점수를 줄 세우는 방향. 「작을수록 좋은」 목표가 있어서 값이 두 개다.</summary>
    public enum SortDirection : byte { Descending = 0, Ascending = 1 }

    /// <summary>
    /// 판이 끝난 시점의 성적 **하나**. 옛 `MatchTally` 의 후계다.
    ///
    /// 조립 지점이 하나뿐인 것이 계약이다(Y10) — 예전엔 재료 다섯이 흩어져 있고 종료 경로
    /// 다섯이 각자 조립해 한 곳만 빠뜨려도 조용히 어긋났다. 여기서는 `BuildOutcome` 한 곳이
    /// 담당자들의 읽기 모델을 모은다.
    ///
    /// **승패를 담는 자리가 «있다»**는 것이 옛 것과의 차이다(사용자 판정 2). 다만 그것은
    /// 전역 규칙이 아니라 **목표의 성질**이다 — 「지지 않는다」는 이제 `KillScoreTimed` 의
    /// 성질이지 게임의 성질이 아니다.
    /// </summary>
    public readonly struct MatchOutcome
    {
        public readonly OutcomeKind Kind;

        /// <summary>서버에 올라가는 수. **가공이 없다**(Y5).</summary>
        public readonly int Score;

        public readonly SortDirection Sort;

        /// <summary>한 토너먼트 = 한 모드. 제출 payload 가 이 값을 동봉한다(사용자 판정 3).</summary>
        public readonly string ModeId;

        public readonly string LeaderboardId;

        /// <summary>종료 통로. 셋뿐이다 — `complete` · `submitted` · `stress_full`.</summary>
        public readonly MatchEndReason Reason;

        public readonly int WaveReached;
        public readonly float ElapsedSeconds;

        /// <summary>**돌격형이 마음을 치고 산화한 수**(Y9). 「유출」이 아니다.</summary>
        public readonly int Leaks;

        public readonly int Stability;
        public readonly int StabilityMax;

        /// <summary>이 판을 서버에 올리나. v1 은 `KillScoreTimed` 만 true.</summary>
        public readonly bool SubmitsReport;

        public MatchOutcome(OutcomeKind kind, int score, SortDirection sort, in ModeDef mode,
                            MatchEndReason reason, int waveReached, float elapsedSeconds,
                            int leaks, int stability, int stabilityMax)
        {
            Kind = kind;
            Score = score > 0 ? score : 0;
            Sort = sort;
            ModeId = mode.ModeId;
            LeaderboardId = mode.EffectiveLeaderboardId;
            Reason = reason;
            WaveReached = waveReached;
            ElapsedSeconds = elapsedSeconds;
            Leaks = leaks;
            Stability = stability;
            StabilityMax = stabilityMax;
            SubmitsReport = mode.SubmitsReport;
        }
    }

    /// <summary>
    /// HUD 가 읽는 진행값. 목표마다 「무엇이 진행인가」가 달라서 이 두 칸의 **뜻이 달라진다** —
    /// 그것이 목표가 자기 읽기 모델을 갖는 이유다.
    /// </summary>
    public readonly struct GoalReadModel
    {
        /// <summary>지금 값(킬 수 · 도달 웨이브 · 경과 초).</summary>
        public readonly int Current;

        /// <summary>목표 값. 0 = 목표가 수치로 표현되지 않는다(시간이 끝내는 판).</summary>
        public readonly int Target;

        /// <summary>남은 시간(초). `CountUp` 목표는 0.</summary>
        public readonly float SecondsRemaining;

        public readonly SortDirection Sort;

        public GoalReadModel(int current, int target, float secondsRemaining, SortDirection sort)
        {
            Current = current;
            Target = target;
            SecondsRemaining = secondsRemaining;
            Sort = sort;
        }
    }

    /// <summary>목표 concrete 를 고르는 **유일한 자리**. 새 `goalKind` 는 여기 한 줄로 붙는다.</summary>
    public static class MatchGoals
    {
        public static IMatchGoal Create(GoalKind kind)
        {
            switch (kind)
            {
                case GoalKind.WaveClear: return new WaveClearGoal();
                case GoalKind.TimeAttack: return new TimeAttackGoal();
                default: return new KillScoreTimedGoal();
            }
        }
    }
}
