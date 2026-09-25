namespace Wassup.Core
{
    // battle-core-rebuild unit 8c — **옛 씬 전용 입력**만 이 부분 파일에 떼어 뒀다(옛 씬은 `GameManager` 를 두고, 새 씬은
    // push 로 민다). `ledgers/retire-set.md` 에 올라 있어 unit 9 는 이 파일을 **지우기만** 하면 된다 — 본 파일의
    // `partial void` 선언은 구현이 사라지면 호출째 컴파일에서 빠진다. 거동 무변(파일 분할만).
    public partial class SoundManager
    {
        private bool _subscribed;

        partial void EnsureSubscribed()
        {
            if (_subscribed || !bgmOnlyInBattle) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
            OnPhaseChanged(GameManager.Instance.CurrentPhase);
        }

        partial void Unsubscribe()
        {
            if (_subscribed && GameManager.Instance != null) GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
        }
    }
}
