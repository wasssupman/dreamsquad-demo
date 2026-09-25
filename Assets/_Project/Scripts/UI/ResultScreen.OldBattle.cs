using Wassup.Core;

namespace Wassup.UI
{
    // battle-core-rebuild unit 8c — **옛 씬 전용 입력**만 이 부분 파일에 떼어 뒀다(옛 씬은 `MatchTally` 로 부르고, 새 씬은
    // `MatchOutcome` 을 직접 준다). `ledgers/retire-set.md` 에 올라 있어 unit 9 는 이 파일을 **지우기만** 하면 된다 — 부르는 쪽은
    // 옛 `GameManager` 뿐이다. 거동 무변(파일 분할만).
    public partial class ResultScreen
    {
        public void Show(MatchTally tally)
            => Render(tally.Total, tally.Stability, tally.StabilityMax, tally.WaveReached);
    }
}
