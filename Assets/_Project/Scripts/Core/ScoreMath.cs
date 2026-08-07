namespace Wassup.Core
{
    /// <summary>
    /// three-minute-survival unit 3 — 최종 결과 점수의 유일한 계산 지점.
    ///
    /// **점수는 처치로만 번다.** 처치한 적의 killScore 합이 곧 총점이다.
    /// 구 예산 소모 모델(시간·스트레스 예산에서 소모분을 깎던 3축 산식)은 폐기했다 —
    /// 3분은 이제 지갑이 아니라 판의 길이이고, 스트레스는 패배도 점수도 만들지 않는다.
    /// 패배해도 점수는 깎이지 않으므로 산식에 분기가 하나도 없다.
    ///
    /// **동점은 남은 골 안정도로 가른다.** 서버는 int 점수 하나만 받으므로(TournamentApi)
    /// 제출값에 안정도를 실어 보낸다 — 아래 EncodeSubmission/DecodeKillScore.
    ///
    /// 아키텍처 무참조 순수 함수 — UnityEngine/Entities 를 참조하지 않는다(MatchSeed 선례).
    /// </summary>
    public static class ScoreMath
    {
        public readonly struct BattleScore
        {
            /// <summary>처치한 적의 killScore 합.</summary>
            public readonly int Kill;
            /// <summary>총점. 처치 축이 유일하므로 Kill 과 같다 — 호출부가 "총점" 을 읽는
            /// 자리를 남겨 둔다(점수 축이 다시 늘어나면 여기만 바뀐다).</summary>
            public readonly int Total;

            public BattleScore(int kill)
            {
                Kill = kill;
                Total = kill;
            }
        }

        /// <param name="killScoreTotal">실제 처치분 누적. 유출당한 적은 포함되지 않는다.</param>
        public static BattleScore Evaluate(int killScoreTotal)
            => new BattleScore(killScoreTotal > 0 ? killScoreTotal : 0);

        // ── 제출값 인코딩 ────────────────────────────────────────────────────────
        //
        // submitted = BASE + killScore × 1000 + 안정도permille
        //
        // BASE 오프셋이 있는 이유: 구 산식의 총점은 현실적으로 1~3만이라 `v / 1000` 으로
        // 디코딩하면 **10~30 이라는 그럴듯한 가짜 점수**가 되어 신규 기록과 구분할 수 없다.
        // 오프셋 미만은 구 포맷으로 판정해 디코딩하지 않는다(화면이 원값을 그대로 보여준다).
        // 신규 기록이 구 기록보다 항상 위로 정렬되는 것은 의도다 — 룰이 다른 기록이다.
        //
        // permille(0~999)을 쓰는 이유: 맵별 안정도 최대치가 달라도 인코딩이 깨지지 않고
        // tie-break 가 공정하다(비율 비교).
        public const int SubmissionBase = 1_000_000_000;
        public const int KillScoreScale = 1000;
        /// <summary>인코딩이 int 를 넘지 않는 killScore 상한.</summary>
        public const int MaxEncodableKillScore = (int.MaxValue - SubmissionBase - 999) / KillScoreScale;

        public static int EncodeSubmission(int killScoreTotal, int stability, int stabilityMax)
        {
            int kill = killScoreTotal > 0 ? killScoreTotal : 0;
            if (kill > MaxEncodableKillScore) kill = MaxEncodableKillScore;
            return SubmissionBase + kill * KillScoreScale + StabilityPermille(stability, stabilityMax);
        }

        /// <summary>안정도 비율을 0~999 로. max ≤ 0 이면 0(정보 없음).</summary>
        public static int StabilityPermille(int stability, int stabilityMax)
        {
            if (stabilityMax <= 0 || stability <= 0) return 0;
            if (stability >= stabilityMax) return 999;
            // 정수 산술로 반올림 — float 왕복이 경계에서 999/1000 을 오가는 것을 막는다.
            int permille = (stability * 999 * 2 / stabilityMax + 1) / 2;
            return permille > 999 ? 999 : permille;
        }

        public static bool IsEncodedSubmission(int submitted) => submitted >= SubmissionBase;

        /// <summary>제출값에서 처치 점수를 되꺼낸다. 구 포맷(오프셋 미만)이면 -1.</summary>
        public static int DecodeKillScore(int submitted)
            => IsEncodedSubmission(submitted) ? (submitted - SubmissionBase) / KillScoreScale : -1;

        /// <summary>제출값에서 안정도 permille 을 되꺼낸다. 구 포맷이면 -1.</summary>
        public static int DecodeStabilityPermille(int submitted)
            => IsEncodedSubmission(submitted) ? (submitted - SubmissionBase) % KillScoreScale : -1;

        /// <summary>
        /// 리더보드·히스토리의 표시 문자열. 신규 기록은 처치 점수만, 구 기록은 원값 그대로.
        /// 표시 지점 3곳(결과 화면·리더보드·히스토리)이 같은 규칙을 쓰게 한 단일 창구다.
        /// </summary>
        public static int DisplayScore(int submitted)
        {
            int kill = DecodeKillScore(submitted);
            return kill >= 0 ? kill : submitted;
        }
    }
}
