using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // Phase 4 explicit placement UI. Shows the 7 picked defender types in a
    // bottom-left strip; clicking a slot promotes that type via
    // GameManager.SelectedDefender so PlacementInput places the specific type
    // rather than a random draw. First slot auto-selects on draft confirm.
    public class DefenderSelector : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private DraftController draftController;
        [SerializeField] private DefenderDragPlacementController dragPlacementController;
        // 드래그 프리뷰 sway 튜닝값(SO). 컨트롤러가 런타임 부착이라 여기서 할당해 주입한다.
        // 미할당이면 컨트롤러가 클래스 기본값으로 폴백. 에셋 편집이 런타임에 반영된다.
        [SerializeField] private DragSwaySettings swaySettings;

        private GameObject _panel;
        private Transform _slotContainer;
        private SlotView[] _slots;
        private bool _built;

        private struct SlotView
        {
            public DefenderUnitData data;
            public Image background;
            public TextMeshProUGUI nameLabel;
            public Button button;
        }

        private void Awake()
        {
            EnsureDragController();
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (draftController != null)
            {
                draftController.DraftConfirmed += OnDraftConfirmed;
                draftController.DraftStarted += OnDraftStarted;
            }
            // squad-loadout regression fix — squad mode has no draft, so the
            // placement entry comes via GameManager.PlacementRequested. Show the
            // pick strip there too. GameManager (-100 exec order) has set Instance
            // before this OnEnable.
            if (GameManager.Instance != null)
                GameManager.Instance.PlacementRequested += OnDraftConfirmed;
        }

        private void OnDisable()
        {
            if (draftController != null)
            {
                draftController.DraftConfirmed -= OnDraftConfirmed;
                draftController.DraftStarted -= OnDraftStarted;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.PlacementRequested -= OnDraftConfirmed;
        }

        private void OnDraftConfirmed()
        {
            EnsureDragController();
            if (bridge == null || bridge.DefenderPool == null) return;
            RebuildSlots(bridge.DefenderPool);
            _panel.SetActive(true);
            if (_slots != null && _slots.Length > 0) Select(_slots[0].data);
        }

        private void OnDraftStarted()
        {
            _panel.SetActive(false);
            Select(null);
        }

        private void Update()
        {
            if (_slots == null) return;
            // Single source of truth: GameManager.SelectedDefender. External
            // clears (e.g. SkillBar entering aim mode) immediately remove the
            // highlight without any local shadow copy to keep in sync.
            var current = GameManager.Instance != null ? GameManager.Instance.SelectedDefender : null;
            for (int i = 0; i < _slots.Length; i++)
            {
                ref var s = ref _slots[i];
                bool isSelected = s.data == current;
                var matColor = s.data.visualMaterial != null ? s.data.visualMaterial.GetColor("_BaseColor") : Color.gray;
                s.background.color = isSelected ? Color.Lerp(matColor, Color.white, 0.45f) : matColor;
            }
        }

        private void Select(DefenderUnitData data)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.SelectedDefender = data;
            // Last-pressed-wins mutual exclusion: any pending skill aim cancels
            // as soon as the player decisively picks a placement target.
            if (data != null) gm.RaiseAimCanceled();
        }

        private void OnSlotClicked(int index)
        {
            if (_slots == null || index < 0 || index >= _slots.Length) return;
            Select(_slots[index].data);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4;
            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("DefenderPanel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 0f);
            prt.pivot = new Vector2(0f, 0f);
            prt.anchoredPosition = new Vector2(40f, 40f);
            prt.sizeDelta = new Vector2(760f, 100f);

            var hlg = _panel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            _slotContainer = _panel.transform;

            UiLayer.Apply(gameObject);
        }

        private void RebuildSlots(DefenderUnitData[] pool)
        {
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotContainer.GetChild(i).gameObject);

            if (pool == null || pool.Length == 0) { _slots = null; return; }
            _slots = new SlotView[pool.Length];

            for (int i = 0; i < pool.Length; i++)
            {
                var data = pool[i];
                if (data == null) continue;
                var go = new GameObject($"Slot_{data.displayName}",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_slotContainer, false);
                var bg = go.GetComponent<Image>();
                bg.color = data.visualMaterial != null ? data.visualMaterial.GetColor("_BaseColor") : Color.gray;
                var btn = go.GetComponent<Button>();
                int idx = i;
                btn.onClick.AddListener(() => OnSlotClicked(idx));
                var dragSlot = go.AddComponent<DefenderDragSlot>();
                dragSlot.Bind(data, dragPlacementController);

                var nameGO = new GameObject("Name", typeof(RectTransform));
                nameGO.transform.SetParent(go.transform, false);
                var nrt = (RectTransform)nameGO.transform;
                nrt.anchorMin = Vector2.zero;
                nrt.anchorMax = Vector2.one;
                nrt.offsetMin = new Vector2(4f, 4f);
                nrt.offsetMax = new Vector2(-4f, -4f);
                var tmp = nameGO.AddComponent<TextMeshProUGUI>();
                tmp.text = data.displayName;
                tmp.fontSize = 18;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;

                _slots[i] = new SlotView { data = data, background = bg, nameLabel = tmp, button = btn };
            }

            UiLayer.Apply(gameObject);
        }

        private void EnsureDragController()
        {
            if (dragPlacementController == null)
                dragPlacementController = GetComponent<DefenderDragPlacementController>();
            if (dragPlacementController == null)
                dragPlacementController = gameObject.AddComponent<DefenderDragPlacementController>();
            if (bridge != null)
                dragPlacementController.Configure(bridge, Camera.main, bridge.PlacementInput, swaySettings);
        }
    }
}
