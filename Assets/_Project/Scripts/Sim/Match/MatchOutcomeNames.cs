using Wassup.Core.Session;

namespace Wassup.Sim.Match
{
    /// <summary>
    /// battle-sim-extraction unit 14 — <see cref="MatchOutcome"/> 의 로그·트레이스 문자열.
    ///
    /// 종료 사유 enum 자체는 **신설하지 않는다** — unit 12 가 세션 계약(`Wassup.Core.Session`)에
    /// 이미 정의했고, 규칙이 그 어휘로 결과를 내야 세션 이벤트·커맨드로그·리플레이가 한 어휘를 쓴다.
    /// 여기 있는 것은 그 enum 을 **기존 문자열로 되돌리는 표**뿐이다.
    ///
    /// **한 글자도 달라선 안 된다**: `BattleLogger.SetResult` 와 골든 트레이스의
    /// `CaptureLegacyTraceResult` 가 이 문자열을 그대로 실어서, 바뀌면 골든 byte diff 가 난다.
    /// </summary>
    public static class MatchOutcomeNames
    {
        public static string Of(MatchOutcome outcome) => outcome switch
        {
            MatchOutcome.Victory => "victory",
            MatchOutcome.VictoryTimeout => "victory_timeout",
            MatchOutcome.Defeat => "defeat",
            MatchOutcome.Aborted => "aborted",
            _ => "none",
        };
    }
}
