using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;

namespace Wassup.UI
{
    // runtime-stat-refresh Unit 2 — dev/QA-only lobby button: pulls the latest
    // sheet balance into the in-memory catalog SOs (next battle uses the new
    // numbers). Hidden outside development builds; the release APK never shows it.
    public class StatRefreshButtonView : MonoBehaviour
    {
        // menu font assets (LiberationSans/Anton/Bangers) carry no Korean glyphs —
        // all lobby labels are English all-caps; keep these matching.
        private const string IdleLabel = "REFRESH STATS";

        [SerializeField] private UnitStatRuntimeRefresher refresher;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text resultLabel;

        private void Awake()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                gameObject.SetActive(false);
                return;
            }
            if (resultLabel != null) resultLabel.text = "";
            button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        private void OnClick()
        {
            if (refresher == null || refresher.RequestInFlight) return;
            button.interactable = false;
            if (label != null) label.text = "REFRESHING...";

            refresher.Refresh(log =>
            {
                // the scene may have unloaded while the request was in flight
                if (this == null || button == null) return;
                button.interactable = true;
                if (label != null) label.text = IdleLabel;
                if (resultLabel != null) resultLabel.text = FirstLine(log);
                Debug.Log($"[StatRefresh]\n{log}");
            });
        }

        private static string FirstLine(string log)
        {
            if (string.IsNullOrEmpty(log)) return "";
            int newline = log.IndexOf('\n');
            return newline < 0 ? log : log.Substring(0, newline);
        }
    }
}
