namespace Wassup.Core
{
    /// <summary>
    /// three-minute-survival unit 7 — **판이 끝난 시점의 성적 하나.**
    ///
    /// 마감의 4단계가 이 값 하나를 통과한다: 취합(`BattleBridge.BuildTally`) → 기록(BattleLogger)
    /// → 통보(`TournamentMatchReporter`) → 표시(`ResultScreen`). 예전엔 재료 5개가 브리지
    /// 필드로 흩어져 있고 종료 경로 5곳이 각자 조립했다 — 한 곳만 빠뜨려도 조용히 어긋났다.
    ///
    /// **점수는 처치로만 번다**(unit 3). 총점 = 잡은 마리 수이고, 산식에 분기가
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
        /// <summary>배틀 로그에 남는 결과 라벨. 값은 셋이다 —
        /// `complete`(3분 완주) · `submitted`(유저 제출) · `stress_full`(스트레스 100).
        /// **승패를 담는 자리는 없다** — 그 개념이 사라졌고, 자리를 남기면 조용히 되살아난다.
        ///
        /// ⚠ heart-stress-axis — **라벨이 셋이라고 게임 종료가 셋인 것은 아니다.**
        /// 게임 규칙상의 통로는 2개(3분 만료 · 스트레스 100)이고 `submitted` 는 절차 밖
        /// 탈출구다(사용자 결정 2026-08-23). UI·문구에서 제출을 「게임을 끝내는 방법」으로
        /// 승격시키지 않는다.</summary>
        public readonly string Outcome;

        /// <summary>**잡은 마리 수 = 점수.** three-minute-kill-race unit 1 —
        /// 개체 1킬 = 1점이고 예외가 없다(보스도 분열체도 1). 티어로 가중하던
        /// `killScore` 축은 은퇴했고, 그래서 점수와 처치 수가 한 축이다.
        ///
        /// 유출당한 적은 포함되지 않는다 — 그쪽은 `EnemyKilledEvent` 를 발화하지 않는다.
        /// 그것이 「못 잡은 적 = 못 번 점수」라는 유일한 페널티의 실체다.</summary>
        public readonly int Kills;

        public readonly int Stability;
        public readonly int StabilityMax;
        /// <summary>도달 웨이브 = 마지막으로 큐잉된 웨이브 번호.</summary>
        public readonly int WaveReached;
        /// <summary>**돌격형이 마음을 치고 산화한 수** = 이 판의 「놓쳤다」.
        /// heart-stress-axis 에서 뜻이 바뀌었다 — 구 의미(「부서진 마음으로 적이 흘러듦」)는
        /// 첫 붕괴에 판이 끝나므로 구조적으로 발생 불가다. 공성형은 마음 앞에서 아직 잡을 수
        /// 있으므로 놓친 것이 아니다. ⚠ 화면 라벨을 「유출」로 쓰면 거짓말이다.
        /// 점수와 무관하며 로그·결과 화면 집계다.</summary>
        public readonly int Leaks;

        public MatchTally(string outcome, int kills,
            int stability, int stabilityMax, int waveReached, int leaks)
        {
            Outcome = outcome;
            Kills = kills > 0 ? kills : 0;
            Stability = stability;
            StabilityMax = stabilityMax;
            WaveReached = waveReached;
            Leaks = leaks;
        }

        /// <summary>총점. 점수원이 처치 하나뿐이라 <see cref="Kills"/> 와 같다 — 호출부가
        /// "총점" 을 읽는 자리를 남겨 둔다(점수 축이 다시 늘어나면 여기만 바뀐다).</summary>
        public int Total => Kills;

        /// <summary>**서버에 보내는 수.** 「무엇을 제출하나」의 유일한 답이다.
        /// 총점 그대로이며 가공이 없다 — 화면 숫자와 완전히 같은 값이 올라간다.</summary>
        public int SubmissionScore => Kills;
    }
}
