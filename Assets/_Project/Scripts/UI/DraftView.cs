using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // Runtime-built draft panel. Creates its own Canvas + grid + cards + confirm
    // button so Phase 1 adds zero prefab assets. Listens to DraftController events
    // for Start/Confirm and rebuilds the card grid from the current session pool.
    public class DraftView : MonoBehaviour
    {
        [SerializeField] private DraftController controller;

        private GameObject _panel;
        private RectTransform _cardContainer;
        private TextMeshProUGUI _counterLabel;
        private TextMeshProUGUI _titleLabel;
        private Button _confirmButton;
        private readonly List<DraftCardView> _cards = new();
        private bool _built;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.DraftStarted += OnDraftStarted;
            controller.DraftConfirmed += OnDraftConfirmed;
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.DraftStarted -= OnDraftStarted;
            controller.DraftConfirmed -= OnDraftConfirmed;
        }

        public bool IsOpen => _panel != null && _panel.activeSelf;

        // Build a self-contained Canvas hierarchy. Phase 1 keeps this programmatic to
        // avoid adding prefabs — UI scale mode matches ResultScreen (Screen Space Overlay).
        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            _panel = new GameObject("DraftPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            Stretch(prt);
            _panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

            _titleLabel = CreateLabel(_panel.transform, "Title", "Draft 7 of 10",
                fontSize: 54, alignment: TextAlignmentOptions.Center);
            var titleRt = (RectTransform)_titleLabel.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -40f);
            titleRt.sizeDelta = new Vector2(0f, 80f);

            var gridGO = new GameObject("CardGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGO.transform.SetParent(_panel.transform, false);
            _cardContainer = (RectTransform)gridGO.transform;
            _cardContainer.anchorMin = new Vector2(0.5f, 0.5f);
            _cardContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _cardContainer.pivot = new Vector2(0.5f, 0.5f);
            _cardContainer.sizeDelta = new Vector2(1500f, 560f);
            _cardContainer.anchoredPosition = new Vector2(0f, 10f);
            var grid = gridGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(280f, 260f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            _counterLabel = CreateLabel(_panel.transform, "Counter", "0/7",
                fontSize: 40, alignment: TextAlignmentOptions.Center);
            var counterRt = (RectTransform)_counterLabel.transform;
            counterRt.anchorMin = new Vector2(0f, 0f);
            counterRt.anchorMax = new Vector2(1f, 0f);
            counterRt.pivot = new Vector2(0.5f, 0f);
            counterRt.anchoredPosition = new Vector2(0f, 180f);
            counterRt.sizeDelta = new Vector2(0f, 60f);

            _confirmButton = CreateButton(_panel.transform, "Confirm", "CONFIRM");
            var btnRt = (RectTransform)_confirmButton.transform;
            btnRt.anchorMin = new Vector2(0.5f, 0f);
            btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 80f);
            btnRt.sizeDelta = new Vector2(320f, 80f);
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void OnDraftStarted()
        {
            if (!_built) BuildCanvas();
            _panel.SetActive(true);
            RebuildCards();
            RefreshStateIndicators();
        }

        private void OnDraftConfirmed()
        {
            _panel.SetActive(false);
        }

        private void RebuildCards()
        {
            foreach (var c in _cards)
            {
                if (c != null) Destroy(c.gameObject);
            }
            _cards.Clear();

            var pool = controller.Session.Pool;
            for (int i = 0; i < pool.Count; i++)
            {
                var unit = pool[i];
                if (unit == null) continue;
                var card = CreateCard(unit);
                _cards.Add(card);
            }
        }

        private DraftCardView CreateCard(DefenderUnitData unit)
        {
            var go = new GameObject($"Card_{unit.displayName}",
                typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_cardContainer, false);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            var button = go.GetComponent<Button>();
            var cs = button.colors;
            cs.highlightedColor = new Color(1f, 1f, 1f, 1f);
            button.colors = cs;

            var swatchGO = new GameObject("Swatch", typeof(RectTransform), typeof(Image));
            swatchGO.transform.SetParent(go.transform, false);
            var swatchRt = (RectTransform)swatchGO.transform;
            swatchRt.anchorMin = new Vector2(0f, 1f);
            swatchRt.anchorMax = new Vector2(1f, 1f);
            swatchRt.pivot = new Vector2(0.5f, 1f);
            swatchRt.anchoredPosition = new Vector2(0f, -8f);
            swatchRt.sizeDelta = new Vector2(-16f, 40f);
            var swatch = swatchGO.GetComponent<Image>();
            swatch.color = unit.visualMaterial != null ? unit.visualMaterial.GetColor("_BaseColor") : Color.white;

            var stats = $"{unit.displayName}\n\nHP  {unit.health:0}\nRNG {unit.attackRange:0.##}\nDMG {unit.attackDamage:0}\nCD  {unit.attackCooldown:0.##}s";
            var label = CreateLabel(go.transform, "Label", stats, fontSize: 22,
                alignment: TextAlignmentOptions.Top);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(10f, 10f);
            lrt.offsetMax = new Vector2(-10f, -56f);

            var view = go.AddComponent<DraftCardView>();
            view.Bind(button, label, bg, swatch, unit);
            button.onClick.AddListener(() => OnCardClicked(view));
            return view;
        }

        private void OnCardClicked(DraftCardView card)
        {
            if (!controller.TogglePick(card.Unit)) return;
            RefreshStateIndicators();
        }

        private void OnConfirmClicked()
        {
            controller.TryConfirm();
        }

        private void RefreshStateIndicators()
        {
            foreach (var c in _cards)
            {
                c.SetPicked(controller.Session.IsPicked(c.Unit));
            }
            int picked = controller.Session.PickedCount;
            _counterLabel.text = $"{picked}/{controller.PickCount}";
            _confirmButton.interactable = controller.Session.IsFull;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
            int fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string name, string text)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.2f, 0.5f, 0.9f, 1f);
            var button = go.GetComponent<Button>();
            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            Stretch(lrt);
            var tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 36;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
