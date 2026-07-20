namespace Wassup.Core
{
    /// <summary>
    /// battle-score-formula unit 1 — 최종 결과 점수의 유일한 계산 지점.
    ///
    /// 예산 소모 모델이다: 시간·스트레스 예산을 만점으로 두고 소모한 만큼 깎은 뒤,
    /// 실제 처치분을 더한다.
    ///
    ///   시간점수    = 남은시간ms × 초당점수 / 1000   (패배 시 0)
    ///   스트레스점수 = (한계 − 누적) × 점당점수
    ///   킬점수      = 실제 처치한 적의 killScore 합
    ///
    /// **승패 규칙도 여기 있다.** Bridge 에 흩으면 테스트로 고정되지 않는다(spec 계약 3).
    /// 아키텍처 무참조 순수 함수 — UnityEngine/Entities 를 참조하지 않는다(MatchSeed 선례).
    /// int 만 쓴다: 최악 180,000ms × 100 = 18,000,000 이고, int 오버플로 임계는
    /// 초당점수 11,930 이다(ScoreRulesData 의 Range 가 막는다).
    /// </summary>
    public static class ScoreMath
    {
        public readonly struct BattleScore
        {
            public readonly int Time;
            public readonly int Stress;
            public readonly int Kill;
            public readonly int Total;

            public BattleScore(int time, int stress, int kill)
            {
                Time = time;
                Stress = stress;
                Kill = kill;
                Total = time + stress + kill;
            }
        }

        /// <param name="remainingMs">남은 전투 시간(ms). 음수는 0 으로 본다.</param>
        /// <param name="stressAccrued">_goalReachedCount + _leakAllowancePenalty (유출 + 몽마의 계약 선불).</param>
        /// <param name="stressLimit">deck.defeatGoalReachedCount <b>원본값</b>. EffectiveLeakLimit 이 아니다.</param>
        /// <param name="killScoreTotal">실제 처치분 누적. 유출당한 적은 포함되지 않는다.</param>
        /// <param name="defeated">스트레스 한계 도달로 패배했는가.</param>
        public static BattleScore Evaluate(
            int remainingMs,
            int stressAccrued,
            int stressLimit,
            int killScoreTotal,
            bool defeated,
            int timeScorePerSecond,
            int stressScorePerPoint)
        {
            // 패배하면 시간 예산은 전부 소모한 것으로 본다. 이게 산식의 **유일한 분기**다 —
            // 버팀 승리(victory_timeout)는 남은 시간이 0 이라 자동으로 0 점이 되고,
            // 패배 시 스트레스점수도 아래 뺄셈이 알아서 0 을 만든다(누적 == 한계).
            int usableMs = defeated || remainingMs < 0 ? 0 : remainingMs;
            // ms × 초당점수 / 1000 과 결과가 같되(정수 나눗셈 포함), 곱하기 전에 초 단위를
            // 떼어내 오버플로 여지를 없앤다. 현행 180초 × Range 상한이면 직접 곱해도
            // int 에 들어가지만, 제한시간이 길어지면 그 여유가 사라진다.
            int time = usableMs / 1000 * timeScorePerSecond
                       + usableMs % 1000 * timeScorePerSecond / 1000;

            // 정상 경로에서는 음수가 될 수 없다: 패배 트리거 시점의 누적은 정확히 한계이고
            // (지불 게이트가 remaining ≥ 1 을 강제 + 유출은 1씩 증가), 비패배 종료면
            // 누적 ≤ 한계 − 1 이라 최소 1점분이 남는다.
            // 이 clamp 는 defeatGoalReachedCount ≤ 0 인 덱 오저작 방어다 — 지우지 말 것.
            int remainingStress = stressLimit - stressAccrued;
            if (remainingStress < 0) remainingStress = 0;
            int stress = remainingStress * stressScorePerPoint;

            int kill = killScoreTotal > 0 ? killScoreTotal : 0;

            return new BattleScore(time, stress, kill);
        }
    }
}
