using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;

namespace Wassup.UI
{
    // game-start-loadout-gate unit 2 — the lobby's "you can't start yet" notice.
    // Lists only what is missing and offers a jump to the screen that fixes it.
    //
    // Dumb view: it never opens a panel itself, it invokes callbacks. Panel
    // visibility has one owner (OutgameMenuController) and this must not become a
    // second one.
    public class LoadoutGatePopup : MonoBehaviour
    {
        [Header("Korean-capable font (Jua SDF). Null -> TMP default + global fallback.")]
        [SerializeField] private TMP_FontAsset font;

        private static readonly Color PanelBg = new Color(0.11f, 0.13f, 0.23f, 1f);
        private static readonly Color FrameAccent = new Color(0.90f, 0.72f, 0.34f, 1f);
        private static readonly Color HeaderGold = new Color(0.96f, 0.83f, 0.48f, 1f);
        private static readonly Color SectionMuted = new Color(0.68f, 0.72f, 0.82f, 1f);
        private static readonly Color GoButton = new Color(0.20f, 0.42f, 0.62f, 1f);
        private static readonly Color CloseButton = new Color(0.28f, 0.30f, 0.36f, 1f);

        private GameObject _root;
        private RectTransform _panel;
        private RectTransform _lineBox;     // shortfall lines are rebuilt per Show
        private RectTransform _buttonRow;   // so are the jump buttons
        private bool _built;

        private void Awake()
        {
            BuildOnce();
            if (_root != null) _root.SetActive(false);
        }

        // `shortfalls` is the caller's reusable list — read it now, never store it.
        public void Show(IReadOnlyList<LoadoutShortfall> shortfalls, Action onGoSquad, Action onGoDeck)
        {
            BuildOnce();
            Rebuild(shortfalls, onGoSquad, onGoDeck);

            // Same-canvas last sibling wins render order AND raycast priority. A
            // nested canvas with overrideSorting would only win the former, and taps
            // would still resolve to the lobby buttons underneath (see
            // SquadBuilderView.OpenPicker for where that was found the hard way).
            _root.transform.SetAsLastSibling();
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        private void Rebuild(IReadOnlyList<LoadoutShortfall> shortfalls, Action onGoSquad, Action onGoDeck)
        {
            for (int i = _lineBox.childCount - 1; i >= 0; i--) Destroy(_lineBox.GetChild(i).gameObject);
            for (int i = _buttonRow.childCount - 1; i >= 0; i--) Destroy(_buttonRow.GetChild(i).gameObject);

            bool squadUnmet = false, deckUnmet = false;
            int n = shortfalls != null ? shortfalls.Count : 0;
            for (int i = 0; i < n; i++)
            {
                var s = shortfalls[i];
                if (s.target == LoadoutTarget.Squad) squadUnmet = true; else deckUnmet = true;
                MakeLine(Describe(s));
            }

            // Only offer a jump to what is actually unmet.
            if (squadUnmet) MakeButton("스쿼드 편성", GoButton, () => { Hide(); onGoSquad?.Invoke(); });
            if (deckUnmet) MakeButton("드림캐쳐 덱", GoButton, () => { Hide(); onGoDeck?.Invoke(); });
            MakeButton("닫기", CloseButton, Hide);

            float h = 232f + n * 46f;
            _panel.sizeDelta = new Vector2(760f, h);
        }

        // Counts explain the usual case; a reason covers what counts cannot — a
        // full-size deck holding an unknown id would otherwise read "8/8" and send
        // the player in circles. Over-size gets its own line: a bare "10/8" after a
        // deck-size shrink reads as "10 of 8 needed — fine" and hides that the fix
        // is to remove cards, not add them.
        private static string Describe(LoadoutShortfall s)
        {
            string label = s.target == LoadoutTarget.Squad ? "스쿼드" : "드림캐쳐 덱";
            string unit = s.target == LoadoutTarget.Squad ? "명" : "장";
            if (s.have > s.need) return $"{label}   {s.have}/{s.need} — {s.have - s.need}{unit} 줄여야 해요";
            if (s.have < s.need) return $"{label}   {s.have}/{s.need}";
            return string.IsNullOrEmpty(s.reason) ? label : $"{label}   {s.reason}";
        }

        private void BuildOnce()
        {
            if (_built) return;
            _built = true;

            // Parent to the root canvas, not this GameObject: sibling order only
            // decides anything among siblings of the same canvas.
            var host = GetComponentInParent<Canvas>();
            Transform parent = host != null ? host.rootCanvas.transform : transform.root;

            _root = new GameObject("LoadoutGateRoot", typeof(RectTransform), typeof(Image), typeof(Button));
            _root.transform.SetParent(parent, false);
            var rrt = (RectTransform)_root.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = UiOverlay.Dim;
            var scrim = _root.GetComponent<Button>();
            scrim.transition = Selectable.Transition.None;
            scrim.onClick.AddListener(Hide);   // tap outside to dismiss

            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(_root.transform, false);
            var brt = (RectTransform)border.transform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(768f, 348f);
            border.GetComponent<Image>().color = FrameAccent;

            // The panel carries a no-op Button so clicks inside it are consumed
            // here: uGUI routes a click to the nearest ancestor handler, which is
            // the scrim's Hide.
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Button));
            panelGo.transform.SetParent(_root.transform, false);
            _panel = (RectTransform)panelGo.transform;
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(760f, 340f);
            panelGo.GetComponent<Image>().color = PanelBg;
            panelGo.GetComponent<Button>().transition = Selectable.Transition.None;

            var title = MakeText(_panel, new Vector2(0f, -30f), new Vector2(700f, 48f), 36f, HeaderGold, FontStyles.Bold);
            title.text = "출전 준비가 덜 됐어요";

            var hint = MakeText(_panel, new Vector2(0f, -84f), new Vector2(700f, 32f), 24f, SectionMuted, FontStyles.Normal);
            hint.text = "아래 항목을 맞춰야 시작할 수 있어요.";

            var lineGo = new GameObject("Lines", typeof(RectTransform), typeof(VerticalLayoutGroup));
            lineGo.transform.SetParent(_panel, false);
            _lineBox = (RectTransform)lineGo.transform;
            _lineBox.anchorMin = new Vector2(0f, 1f); _lineBox.anchorMax = new Vector2(1f, 1f);
            _lineBox.pivot = new Vector2(0.5f, 1f);
            _lineBox.anchoredPosition = new Vector2(0f, -126f);
            _lineBox.sizeDelta = new Vector2(-80f, 100f);
            var vlg = lineGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 6f;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;

            var rowGo = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(_panel, false);
            _buttonRow = (RectTransform)rowGo.transform;
            _buttonRow.anchorMin = new Vector2(0f, 0f); _buttonRow.anchorMax = new Vector2(1f, 0f);
            _buttonRow.pivot = new Vector2(0.5f, 0f);
            _buttonRow.anchoredPosition = new Vector2(0f, 28f);
            _buttonRow.sizeDelta = new Vector2(-60f, 80f);
            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 14f;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            UiLayer.Apply(gameObject);
            UiLayer.Apply(_root);
        }

        private void MakeLine(string text)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(_lineBox, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 40f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 30f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        private void MakeButton(string label, Color color, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_buttonRow, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(220f, 72f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 220f; le.preferredHeight = 72f;
            go.GetComponent<Image>().color = color;
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;   // else ColorTint overrides image.color
            btn.onClick.AddListener(() => onClick());

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = label;
            tmp.fontSize = 26f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        private TMP_Text MakeText(Transform parent, Vector2 pos, Vector2 size, float fontSize, Color color, FontStyles style)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
