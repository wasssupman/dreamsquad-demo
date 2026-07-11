using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // 배치 스트립: picked defender 타입을 하단에 표시하는 드래그-드롭 소스.
    // ui-tweak 2026-07-08 — 선택(click-to-select) 개념 제거. 배치는 슬롯을 끌어다 놓는
    // 드래그-드롭 전용(DefenderDragSlot/DefenderDragPlacementController)이며, 클릭 배치는
    // 비활성화되어 GameManager.SelectedDefender 를 채우지 않는다(선택 하이라이트 없음).
    public class DefenderSelector : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        [SerializeField] private DraftController draftController;
        [SerializeField] private DefenderDragPlacementController dragPlacementController;
        // 드래그 프리뷰 sway 튜닝값(SO). 컨트롤러가 런타임 부착이라 여기서 할당해 주입한다.
        // 미할당이면 컨트롤러가 클래스 기본값으로 폴백. 에셋 편집이 런타임에 반영된다.
        [SerializeField] private DragSwaySettings swaySettings;
        // ui-tweak 2026-07-09 — 슬롯 이름은 한글(음차). 기본 TMP 폰트는 한글 글리프가
        // 없어 네모로 깨지므로 한글 SDF(Jua)를 주입한다. 미할당이면 라틴 폴백.
        [SerializeField] private TMP_FontAsset nameFont;
        // battle-hud-layout 2 — 페이즈별 스트립 크기. Battle 은 관전이 주 활동이라
        // 슬림 축소로 중앙 하단 보드 가림을 상쇄한다. 슬롯은 childForceExpand 라
        // 패널 크기만 바꾸면 균등 축소된다.
        [SerializeField] private Vector2 placementSize = new Vector2(912f, 120f);
        [SerializeField] private Vector2 battleSize = new Vector2(912f, 88f);

        private GameObject _panel;
        private Transform _slotContainer;
        private bool _built;

        // dreamcatcher-awakening-hand unit 6 — flip-transition hook. The hand
        // view (DreamcatcherHandView) is the single owner of the strip↔hand
        // state and animates/toggles this panel; the selector's own show/hide
        // events (draft/placement) stay untouched.
        public GameObject PanelGO => _panel;

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
            {
                GameManager.Instance.PlacementRequested += OnDraftConfirmed;
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (draftController != null)
            {
                draftController.DraftConfirmed -= OnDraftConfirmed;
                draftController.DraftStarted -= OnDraftStarted;
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PlacementRequested -= OnDraftConfirmed;
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            }
        }

        // battle-hud-layout 2 — Placement 풀 / Battle 슬림. 그 외 페이즈는 패널이
        // 자체 이벤트로 숨으므로 크기를 건드리지 않는다.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (_panel == null) return;
            if (phase == GamePhase.Battle)
                ((RectTransform)_panel.transform).sizeDelta = battleSize;
            else if (phase == GamePhase.Placement)
                ((RectTransform)_panel.transform).sizeDelta = placementSize;
        }

        private void OnDraftConfirmed()
        {
            EnsureDragController();
            if (bridge == null || bridge.DefenderPool == null) return;
            RebuildSlots(bridge.DefenderPool);
            _panel.SetActive(true);
        }

        private void OnDraftStarted()
        {
            _panel.SetActive(false);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 4);

            _panel = new GameObject("DefenderPanel", typeof(RectTransform));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            // battle-hud-layout 0 — bottom-center: 드림캐쳐 핸드(0,32)와 동일 축/y 로
            // 맞춰 스트립↔핸드 플립이 좌표 점프 없는 제자리 플립이 되게 한다.
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 32f);
            // ui-tweak 2026-07-08 — 유닛 슬롯 20% 확대(760x100 → 912x120). 슬롯은
            // childForceExpand 로 패널을 채우므로 패널 크기만 키우면 균등 확대된다.
            prt.sizeDelta = placementSize;

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

            if (pool == null || pool.Length == 0) return;

            for (int i = 0; i < pool.Length; i++)
            {
                var data = pool[i];
                if (data == null) continue;
                var go = new GameObject($"Slot_{data.displayName}",
                    typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_slotContainer, false);
                var bg = go.GetComponent<Image>();
                // 상시 테두리 없음: 포트레이트 슬롯은 투명 배경(드래그 레이캐스트 타겟 역할만).
                // 폴백(포트레이트 없음)만 단색 배경 유지.
                bg.color = data.portrait != null
                    ? Color.clear
                    : (data.visualMaterial != null ? data.visualMaterial.GetColor("_BaseColor") : Color.gray);
                var dragSlot = go.AddComponent<DefenderDragSlot>();
                dragSlot.Bind(data, dragPlacementController);

                // defender-portraits 3 — 포트레이트 채움(4px 패딩). raycastTarget=false 라
                // 드래그 입력은 슬롯 루트(bg Image)가 받는다.
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
                    nrt.sizeDelta = new Vector2(0f, 32f);
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
                if (nameFont != null) tmp.font = nameFont;
                tmp.text = data.displayName;
                tmp.fontSize = data.portrait != null ? 26 : 30;
                // ui-tweak 2026-07-09 — 흰색은 밝은 크림/골드 포트레이트 위에서 안 읽힌다.
                // 아웃라인은 글자를 뒤덮어 오히려 가독성을 해쳐 제거. 검정 볼드로 단순 대비.
                tmp.color = Color.black;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                // ui-tweak 2026-07-09 — 검정 글자에 얇은 흰색 아웃라인(할로). 두꺼우면
                // 글자를 뒤덮으므로 0.12 로 얇게 — 밝은/어두운 포트레이트 양쪽에서 테두리 확보.
                var nameMat = tmp.fontMaterial; // per-instance 복사본(슬롯 Destroy 시 정리됨)
                nameMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                nameMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(1f, 1f, 1f, 1f));
                nameMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.12f);
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
