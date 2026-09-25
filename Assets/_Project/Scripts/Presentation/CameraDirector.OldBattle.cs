namespace Wassup.Presentation
{
    // battle-core-rebuild unit 8c — **옛 씬 전용 입력**만 이 부분 파일에 떼어 뒀다(옛 씬은 `GameManager` 를 두고, 새 씬은
    // push 로 민다). `ledgers/retire-set.md` 에 올라 있어 unit 9 는 이 파일을 **지우기만** 하면 된다 — 본 파일의
    // `partial void` 선언은 구현이 사라지면 호출째 컴파일에서 빠진다. 거동 무변(파일 분할만).
    public partial class CameraDirector
    {
        private Wassup.Core.GameManager _gm; // 구독 대상 캐시 — 언구독이 teardown 순서 무관하도록

        partial void SubscribeOldBattlePhase()
        {
            // 구독은 Start — GameManager(-100)와 Awake 순서가 동률이라 Instance 보장 지점 사용.
            // 이미 지나간 페이즈(GameManager.Start 가 먼저 돈 경우)는 CurrentPhase 스냅으로 커버.
            _gm = Wassup.Core.GameManager.Instance;
            if (_gm == null) return; // BattleScene 외 씬 — 비행 채널 비활성
            _gm.PhaseChanged += OnPhaseChanged;
            OnPhaseChanged(_gm.CurrentPhase);
        }

        partial void UnsubscribeOldBattlePhase()
        {
            if (_gm != null) _gm.PhaseChanged -= OnPhaseChanged;
        }
    }
}
