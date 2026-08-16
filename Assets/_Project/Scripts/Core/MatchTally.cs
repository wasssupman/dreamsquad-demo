namespace Wassup.Core
{
    /// <summary>
    /// three-minute-survival unit 7 — **판이 끝난 시점의 성적 하나.**
    ///
    /// 마감의 4단계가 이 값 하나를 통과한다: 취합(`BattleBridge.BuildTally`) → 기록(BattleLogger)
    /// → 통보(`TournamentMatchReporter`) → 표시(`ResultScreen`). 예전엔 재료 5개가 브리지
    /// 필드로 흩어져 있고 종료 경로 5곳이 각자 조립했다 — 한 곳만 빠뜨려도 조용히 어긋났다.
    ///
    /// **점수는 처치로만 번다**(unit 3). 총점 = 처치한 적의 killScore 합이고, 산식에 분기가
    /// 하나도 없다 — 시간·스트레스 축은 폐기됐고 패배해도 깎이지 않는다.
    ///
    /// **서버에 보내는 수는 <see cref="SubmissionScore"/> 하나다**(unit 6). 가공하지 않는다 —
    /// 남은 안정도를 값에 실어 동점을 가르던 인코딩은 폐기됐다.
    ///
    /// 아키텍처 무참조 순수 값 — UnityEngine/Entities 를 참조하지 않는다(MatchSeed 선례).
    /// 조립 지점이 하나뿐이라 인자 순서 실수는 그 한 곳에서만 가능하다.
    /// </summary>
    public readonly struct MatchTally
    {
        /// <summary>배틀 로그에 남는 결과 라벨. three-minute-kill-race unit 0 이후 값은
        /// `complete`(3분 완주) 하나이고, unit 3 이 `submitted`(유저 제출)를 더한다.
        /// **승패를 담는 자리는 없다** — 그 개념이 사라졌고, 자리를 남기면 조용히 되살아난다.</summary>
        public readonly string Outcome;

        /// <summary>처치한 적의 killScore 합(일반 1 / 엘리트 3 / 보스 10). 유출당한 적은
        /// 포함되지 않는다 — 그쪽은 `EnemyKilledEvent` 를 발화하지 않는다.</summary>
        public readonly int KillScore;
        /// <summary>처치 마리 수. 점수가 티어 가중이라 <see cref="KillScore"/> 와 다르다
        /// (잡몹 10기 + 보스 1기 = 20점 / 11기).</summary>
        public readonly int KillCount;

        public readonly int Stability;
        public readonly int StabilityMax;
        /// <summary>도달 웨이브 = 마지막으로 큐잉된 웨이브 번호.</summary>
        public readonly int WaveReached;
        /// <summary>골을 뚫린 횟수(스트레스). 점수와 무관하며 로그·배지용 집계다.</summary>
        public readonly int Leaks;

        public MatchTally(string outcome, int killScore, int killCount,
            int stability, int stabilityMax, int waveReached, int leaks)
        {
            Outcome = outcome;
            KillScore = killScore > 0 ? killScore : 0;
            KillCount = killCount > 0 ? killCount : 0;
            Stability = stability;
            StabilityMax = stabilityMax;
            WaveReached = waveReached;
            Leaks = leaks;
        }

        /// <summary>총점. 점수원이 처치 하나뿐이라 <see cref="KillScore"/> 와 같다 — 호출부가
        /// "총점" 을 읽는 자리를 남겨 둔다(점수 축이 다시 늘어나면 여기만 바뀐다).</summary>
        public int Total => KillScore;

        /// <summary>**서버에 보내는 수.** 「무엇을 제출하나」의 유일한 답이다.
        /// 총점 그대로이며 가공이 없다 — 화면 숫자와 완전히 같은 값이 올라간다.</summary>
        public int SubmissionScore => KillScore;
    }
}
