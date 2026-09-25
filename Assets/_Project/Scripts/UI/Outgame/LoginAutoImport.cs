using UnityEngine;
using Wassup.Core;
using Wassup.Core.Api;

namespace Wassup.UI
{
    // runtime-stat-refresh unit 7 — pull every sheet tab once the player clears the
    // login gate, so a QA build always runs on current numbers without anyone
    // remembering to press a button. LoginPanelView.onSignedIn is the single seam
    // every entry path goes through (login success, skip/guest, returning
    // auto-login), so one subscription covers them all.
    //
    // Non-blocking by design: the lobby opens immediately and the import lands
    // underneath. Blocking would hold the lobby for up to SheetFetcher's 30s
    // timeout on a bad network; the cost is that a battle started in the first
    // seconds runs on built values.
    //
    // Lives in UI (not Core) because it depends on LoginPanelView — the existing
    // dependency direction is UI -> Core.
    public class LoginAutoImport : MonoBehaviour
    {
        [SerializeField] private LoginPanelView loginPanel;
        // Unity can't serialize interface refs — take a MonoBehaviour and cast,
        // same as StatRefreshButtonView.refresherSource.
        [SerializeField] private MonoBehaviour refresherSource;

        private bool _done;
        private bool _devEnabled;

        private void Awake()
        {
            // Same gate as the lobby dev buttons: a release build must never call
            // the dev API. Not subscribing is the whole opt-out.
            _devEnabled = Debug.isDebugBuild || Application.isEditor;
            if (!_devEnabled) return;
            if (loginPanel != null) loginPanel.onSignedIn += OnSignedIn;
        }

        // 이미 로그인된 채 로비에 들어온 경우를 잡는 두 번째 진입점. LoginPanelView.Start
        // 는 `if (UserSession.IsSignedIn) return;` 으로 게이트만 닫고 onSignedIn 을 쏘지
        // 않으므로, 구독만으로는 이 경로에서 영영 발화하지 않는다. 해당되는 상황:
        //  - 세션 중 로비 재방문(전투 → 로비 복귀)
        //  - 에디터의 Enter Play Mode = DisableDomainReload. UserSession 이 static 이라
        //    Play 를 껐다 켜도 살아남아, 두 번째 Play 부터 로그인 흐름을 통째로 건너뛴다.
        //    (실기기·빌드는 프로세스가 새로 떠서 해당 없음 — 에디터 전용 함정이었다.)
        // _done 은 인스턴스 필드라 씬을 다시 로드하면 리셋된다 → "로비에 들어올 때마다
        // 한 번" 이 되고, 같은 진입에서 onSignedIn 까지 울려도 중복 실행되지 않는다.
        private void Start()
        {
            if (!_devEnabled) return;
            if (UserSession.IsSignedIn) TriggerOnce(refresherSource as IRuntimeRefresher);
        }

        private void OnDestroy()
        {
            if (loginPanel != null) loginPanel.onSignedIn -= OnSignedIn;
        }

        private void OnSignedIn() => TriggerOnce(refresherSource as IRuntimeRefresher);

        // Interface-in core so EditMode tests can drive it with a fake and no
        // network (same technique as DcSheetRuntimeRefresher.ApplyBodies).
        internal void TriggerOnce(IRuntimeRefresher refresher)
        {
            // onSignedIn can fire more than once per app session (e.g. returning
            // auto-login, then SKIP taking the already-signed-in path).
            if (_done) return;
            if (refresher == null)
            {
                Debug.LogWarning("[LoginAutoImport] refresherSource is not an IRuntimeRefresher — auto import skipped.", this);
                return;
            }
            _done = true;
            refresher.Refresh(log => Debug.Log($"[LoginAutoImport]\n{log}"));
        }
    }
}
