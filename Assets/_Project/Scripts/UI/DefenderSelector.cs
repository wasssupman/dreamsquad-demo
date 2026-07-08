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

        // ui-tweak 2026-07-08 — 포트레이트 슬롯은 상시 테두리/딤 없이, 선택된 슬롯만
        // 골드 프레임으로 표시한다.
        private static readonly Color SelectionFrameColor = new Color(1f, 0.82f, 0.28f, 1f);

        private struct SlotView
        {
            public DefenderUnitData data;
            public Image background;
            public Image portrait;
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
                if (s.portrait != null && s.portrait.enabled)
                {
                    // 딤/상시 테두리 제거: 포트레이트는 항상 풀 밝기, 선택 슬롯만 골드 프레임.
                    s.background.color = isSelected ? SelectionFrameColor : Color.clear;
                }
                else
                {
                    // 폴백(포트레이트 없음): 기존 단색 배경 + 선택 시 밝게.
                    var matColor = s.data.visualMaterial != null ? s.data.visualMaterial.GetColor("_BaseColor") : Color.gray;
                    s.background.color = isSelected ? Color.Lerp(matColor, Color.white, 0.45f) : matColor;
                }
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
            // ui-tweak 2026-07-08 — 유닛 슬롯 20% 확대(760x100 → 912x120). 슬롯은
            // childForceExpand 로 패널을 채우므로 패널 크기만 키우면 균등 확대된다.
            prt.sizeDelta = new Vector2(912f, 120f);

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
                // 포트레이트가 있으면 상시 테두리 없음(투명). 선택 시에만 Update 가 프레임을 칠한다.
                // 폴백(포트레이트 없음)만 단색 배경 유지.
                bg.color = data.portrait != null
                    ? Color.clear
                    : (data.visualMaterial != null ? data.visualMaterial.GetColor("_BaseColor") : Color.gray);
                var btn = go.GetComponent<Button>();
                int idx = i;
                btn.onClick.AddListener(() => OnSlotClicked(idx));
                var dragSlot = go.AddComponent<DefenderDragSlot>();
                dragSlot.Bind(data, dragPlacementController);

                // defender-portraits 3 — 포트레이트 채움(약간의 패딩으로 background 가
                // 선택 테두리로 보이게). raycastTarget=false 라 드래그 입력은 슬롯 루트가 받는다.
                var portraitGO = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                portraitGO.transform.SetParent(go.transform, false);
                var prt = (RectTransform)portraitGO.transform;
                prt.anchorMin = Vector2.zero;
                prt.anchorMax = Vector2.one;
                prt.offsetMin = new Vector2(4f, 4f);
                prt.offsetMax = new Vector2(-4f, -4f);
                var portraitImg = portraitGO.GetComponent<Image>();
                portraitImg.preserveAspect = true;
                portraitImg.raycastTarget = false;
                portraitImg.sprite = data.portrait;
                portraitImg.enabled = data.portrait != null;

                var nameGO = new GameObject("Name", typeof(RectTransform));
                nameGO.transform.SetParent(go.transform, false);
                var nrt = (RectTransform)nameGO.transform;
                if (data.portrait != null)
                {
                    // 포트레이트가 있으면 이름은 하단 소형 오버레이.
                    nrt.anchorMin = new Vector2(0f, 0f);
                    nrt.anchorMax = new Vector2(1f, 0f);
                    nrt.pivot = new Vector2(0.5f, 0f);
                    nrt.sizeDelta = new Vector2(0f, 22f);
                    nrt.anchoredPosition = new Vector2(0f, 2f);
                }
                else
                {
                    // 폴백: 기존 중앙 텍스트.
                    nrt.anchorMin = Vector2.zero;
                    nrt.anchorMax = Vector2.one;
                    nrt.offsetMin = new Vector2(4f, 4f);
                    nrt.offsetMax = new Vector2(-4f, -4f);
                }
                var tmp = nameGO.AddComponent<TextMeshProUGUI>();
                tmp.text = data.displayName;
                tmp.fontSize = data.portrait != null ? 14 : 18;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;

                _slots[i] = new SlotView { data = data, background = bg, portrait = portraitImg, nameLabel = tmp, button = btn };
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
