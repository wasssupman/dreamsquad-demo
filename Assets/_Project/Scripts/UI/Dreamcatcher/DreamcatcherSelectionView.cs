using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Wassup.Data;

namespace Wassup.UI
{
    // ingame-dreamcatcher Unit 4 — in-game 3-card selection modal. Runtime-built
    // canvas (PlacementPhaseView pattern); Show presents three cards and invokes
    // the pick callback on tap.
    public class DreamcatcherSelectionView : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;

        private GameObject _panel;
        private RectTransform _row;
        private bool _built;
        private Action<DreamcatcherCard> _onPick;
        private readonly List<GameObject> _cards = new List<GameObject>();

        private void Awake()
        {
            BuildCanvas();
            Hide();
        }

        public void Show(IReadOnlyList<DreamcatcherCard> three, Action<DreamcatcherCard> onPick)
        {
            if (!_built) BuildCanvas();
            _onPick = onPick;

            foreach (var c in _cards) if (c != null) Destroy(c);
            _cards.Clear();

            for (int i = 0; i < three.Count; i++)
            {
                var card = three[i];
                var go = BuildCardButton(card);
                _cards.Add(go);
            }

            UiLayer.Apply(gameObject);
            _panel.SetActive(true);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnPickInternal(DreamcatcherCard card)
        {
            var cb = _onPick;
            _onPick = null;
            Hide();
            cb?.Invoke(card);
        }

        private GameObject BuildCardButton(DreamcatcherCard card)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_row, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 420);
            go.GetComponent<Image>().color = new Color(0.16f, 0.20f, 0.34f, 1f);
            go.GetComponent<Button>().onClick.AddListener(() => OnPickInternal(card));

            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(go.transform, false);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = new Vector2(12, 12); lrt.offsetMax = new Vector2(-12, -12);
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = Summary(card);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 30; tmp.color = Color.white;
            if (font != null) tmp.font = font;
            return go;
        }

        private static string Summary(DreamcatcherCard card)
        {
            string axis = card.axis == CardTargetAxis.ClassRanger ? "RANGER"
                        : card.axis == CardTargetAxis.ClassGuardian ? "GUARDIAN"
                        : "COST-1";
            var parts = new List<string>();
            if (card.effects != null)
            {
                foreach (var e in card.effects)
                {
                    string k = e.kind == CardBuffKind.AttackDamage ? "ATK"
                             : e.kind == CardBuffKind.AttackSpeed ? "AS"
                             : e.kind == CardBuffKind.EffectiveHealth ? "HP"
                             : "MOVE";
                    string sign = e.percent >= 0 ? "+" : "";
                    parts.Add($"{k} {sign}{e.percent:0}%");
                }
            }
            return axis + "\n\n" + string.Join("\n", parts);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one; prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f, 0.94f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(_panel.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(800, 120); trt.anchoredPosition = new Vector2(0, 340);
            var ttmp = titleGo.GetComponent<TextMeshProUGUI>();
            ttmp.text = "DREAMCATCHER"; ttmp.alignment = TextAlignmentOptions.Center; ttmp.fontSize = 48; ttmp.color = Color.white;
            if (font != null) ttmp.font = font;

            var rowGo = new GameObject("CardRow", typeof(RectTransform));
            rowGo.transform.SetParent(_panel.transform, false);
            _row = rowGo.GetComponent<RectTransform>();
            _row.sizeDelta = new Vector2(1000, 440); _row.anchoredPosition = new Vector2(0, 0);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 30; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false; hlg.childControlHeight = false; hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            UiLayer.Apply(gameObject);
        }
    }
}
