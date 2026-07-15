using UnityEngine;
using Wassup.Core;

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

        private void Awake()
        {
            // Same gate as the lobby dev buttons: a release build must never call
            // the dev API. Not subscribing is the whole opt-out.
            if (!Debug.isDebugBuild && !Application.isEditor) return;
            if (loginPanel != null) loginPanel.onSignedIn += OnSignedIn;
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
