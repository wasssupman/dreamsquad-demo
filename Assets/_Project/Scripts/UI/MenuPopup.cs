using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.TimeControl;

namespace Wassup.UI
{
    // battle-hud-score-timer-menu Unit 2 — pause-style menu popup. The MENU button no
    // longer exits straight to the outgame scene; it opens this popup, which pauses the
    // battle (TimeManager, Battle domain) and offers [재개] resume / [나가기] exit.
    // The incoming-wave (attack pattern) strip is driven from here in Unit 3.
    public class MenuPopup : MonoBehaviour
    {
        [Header("Korean-capable font (Jua SDF). Null -> TMP default.")]
        [SerializeField] private TMP_FontAsset buttonFont;

        [Header("Attack-pattern strip (drives FadeIn/Roll + sorting boost)")]
        [SerializeField] private Wassup.UI.Draft.WavePatternStripView wavePatternStrip;

        // Buttons sit above the boosted strip; the strip's own dim overlay is the
        // backdrop, so this popup adds no dim of its own (avoids double-dimming).
        private const int StripSortingOrder = 950;
        private const int PopupSortingOrder = 960;

        private GameObject _root;
        private Button _resumeButton;
        private Button _exitButton;
        private bool _built;
        private bool _open;
        private TimeLease _pauseLease;

        public bool IsOpen => _open;

        private void Awake()
        {
            BuildCanvas();
            if (_root != null) _root.SetActive(false);
        }

        private void OnDisable()
        {
            // Safety: never leave the battle paused if this object is torn down while open.
            if (_open) { _pauseLease.Dispose(); _open = false; }
        }

        public void Open()
        {
            if (_open) return;               // idempotent
            if (!_built) BuildCanvas();
            _open = true;
            _pauseLease = TimeManager.Instance.Request(TimeDomain.Battle, 0f, priority: 100);
            if (_root != null) _root.SetActive(true);
            if (wavePatternStrip != null)
            {
                // Lift the strip above this popup so its own dim overlay reads as the
                // backdrop (no double-dim), then reveal it. Buttons sit on a higher
                // canvas (see BuildCanvas) so they stay above the strip.
                wavePatternStrip.SetSortingOverride(true, StripSortingOrder);
                wavePatternStrip.FadeIn();
            }
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            _pauseLease.Dispose();           // idempotent no-op on double dispose
            if (wavePatternStrip != null)
            {
                wavePatternStrip.Roll();
                wavePatternStrip.SetSortingOverride(false);
            }
            if (_root != null) _root.SetActive(false);
        }

        private void OnResume() => Close();

        private void OnExit()
        {
            // Release the pause before leaving; the scene teardown + TimeManager.ResetAll
            // at the match boundary also clears it, so double-release is harmless.
            if (_open) { _pauseLease.Dispose(); _open = false; }
            SceneTransition.Go(SceneNames.Outgame);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = PopupSortingOrder;  // above the boosted strip, below MENU button (1000)
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _root = new GameObject("PopupRoot", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var rrt = (RectTransform)_root.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;

            // No dim backdrop here — the strip's own full-screen overlay (boosted above
            // this canvas' gameplay layers) provides the dim and blocks gameplay input.
            // Buttons live at the bottom, clear of the upper-third strip (no overlap).
            _resumeButton = MakeButton("ResumeButton", "재개", new Vector2(-150f, 120f),
                new Color(0.16f, 0.5f, 0.28f, 0.96f), OnResume);
            _exitButton = MakeButton("ExitButton", "나가기", new Vector2(150f, 120f),
                new Color(0.6f, 0.2f, 0.2f, 0.96f), OnExit);

            UiLayer.Apply(gameObject);
        }

        private Button MakeButton(string name, string label, Vector2 bottomAnchoredPos, Color color, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_root.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = bottomAnchoredPos;
            rt.sizeDelta = new Vector2(260f, 96f);
            go.GetComponent<Image>().color = color;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            if (buttonFont != null) tmp.font = buttonFont;
            tmp.text = label;
            tmp.fontSize = 40;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return btn;
        }
    }
}
