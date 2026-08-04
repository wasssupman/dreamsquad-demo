using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.Session;
using Wassup.Data;
using Wassup.DepthParallax;
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
        // defender-deploy-cutscene unit 3 — 드래그 시작 시 좌상단 컷신 재생기. 씬에 배치해
        // 할당하면 인스펙터 튜닝(hold/scale/margin)이 반영된다. 미할당이면 AddComponent 폴백.
        [SerializeField] private DeployCutscenePlayer deployCutscenePlayer;
        // 드래그 프리뷰 sway 튜닝값(SO). 컨트롤러가 런타임 부착이라 여기서 할당해 주입한다.
        // 미할당이면 컨트롤러가 클래스 기본값으로 폴백. 에셋 편집이 런타임에 반영된다.
        [SerializeField] private DragSwaySettings swaySettings;
        // depth-parallax u9 — 배치 컷신 틸트 패럴랙스 튜닝 SO(선택). 플레이어가 런타임 부착이라
        // 여기서 할당해 주입한다. 미할당이면 플레이어가 클래스 기본값으로 폴백(라이브 튜닝 불가).
        [SerializeField] private DepthParallaxSettings depthParallaxSettings;
        // ui-tweak 2026-07-09 — 슬롯 이름은 한글(음차). 기본 TMP 폰트는 한글 글리프가
        // 없어 네모로 깨지므로 한글 SDF(Jua)를 주입한다. 미할당이면 라틴 폴백.
        [SerializeField] private TMP_FontAsset nameFont;
        [SerializeField] private BattleHudTrayConfig trayConfig;
        // action-tray unit 4 — 비용 부족 차단 시 rail pulse 피드백 대상. 슬롯이
        // 런타임 생성이라 전역 이벤트 대신 Bind 로 직접 참조를 넘긴다.
        [SerializeField] private CostDisplay costDisplay;
        // battle-hud-layout 2 — 페이즈별 스트립 크기. Battle 은 관전이 주 활동이라
        // 슬림 축소로 중앙 하단 보드 가림을 상쇄한다. 슬롯은 childForceExpand 라
        // 패널 크기만 바꾸면 균등 축소된다.
        [SerializeField] private Vector2 placementSize = new Vector2(912f, 120f);
        [SerializeField] private Vector2 battleSize = new Vector2(912f, 88f);

        private GameObject _panel;
        private Transform _slotContainer;
        private bool _built;
        // tray-cost-well unit 1 — 트레이 폭 산출 입력. 페이즈 전환은 슬롯 수를
        // 모르므로 마지막으로 빌드된 값을 재사용한다.
        private int _lastSlotCount;

        // action-tray unit 1 — 슬롯별 시각 참조 캐시. RebuildSlots 에서만 재구성하고
        // Update 는 ReadModel.CostCurrentInt 가 바뀐 프레임에만 순회한다(매 프레임
        // 할당/계층 검색 금지 계약). 슬롯 GO Destroy 와 함께 리스트도 클리어라
        // 재빌드/재진입 시 stale 참조 없음.
        private struct SlotVisual
        {
            public DefenderUnitData data;
            public RectTransform rect;
            public int cost;
            public Image portrait;   // 포트레이트 없으면 null (폴백 bg 틴트)
            public Image slotBg;
            public Color slotBgBase;
            public TextMeshProUGUI costText;
            public GameObject warnGlyph; // 구매 불가 시에만 활성 (색 단독 금지 계약)
            // defender-placement-cooldown 2 — 쿨타임 오버레이(placementCooldown>0 슬롯만 생성,
            // 아니면 전부 null). shown 상태는 여기 두지 않고 cooldownRoot.activeSelf 로 읽는다
            // (struct value-copy 라 필드에 쓰면 소실 — critic M1).
            public GameObject cooldownRoot;
            public Image cooldownFill;
            public TextMeshProUGUI cooldownText;
            public Material cooldownMat; // 셰이더 인스턴스(수명 소유). 폴백(머티리얼 미할당)이면 null.
            public Image cooldownRim;    // 테두리 림 글로우(호흡 펄스). 색 alpha 0 이면 미생성.
        }

        private readonly List<SlotVisual> _slotVisuals = new();
        private int _lastCostSeen = int.MinValue;
        // defender-placement-cooldown 2 — 표시 중 오버레이 유무(만료 프레임에 AnyActive 가 이미
        // false 여도 hide/pop 을 마치도록 순회를 한 프레임 더 돌리는 가드).
        private bool _anyCooldownShown;
        // 쿨타임 액체는 코스트 물통 셰이더(Wassup/UI/CostWell)를 재사용한다 — 같은 uniform.
        private static readonly int CdFillId = Shader.PropertyToID("_Fill");
        private static readonly int CdAspectId = Shader.PropertyToID("_Aspect");
        private static readonly int CdRadiusId = Shader.PropertyToID("_Radius");
        private static readonly int CdLiquidBottomId = Shader.PropertyToID("_LiquidBottom");
        private static readonly int CdLiquidTopId = Shader.PropertyToID("_LiquidTop");
        private static readonly int CdSurfaceId = Shader.PropertyToID("_SurfaceColor");
        private const float CooldownCornerRadius = 12f;

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
                draftController.DraftStarted += OnDraftStarted;
            // gift-phase unit 3 — 트레이 노출은 PhaseChanged(Placement) 로 이관. 예전엔
            // DraftConfirmed/PlacementRequested 로 노출했으나 그 신호는 이제 선물 페이즈
            // 시작점이라 선물 도중 트레이가 튀어나온다. DraftStarted 는 숨김용으로 유지.
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
            // defender-placement-cooldown 1 — 배치 성공 시 쿨타임 시작. 컨트롤러는 Awake 의
            // EnsureDragController 로 확정돼 있어 여기서 구독(OnDisable 해제와 대칭). 멱등.
            if (dragPlacementController != null)
            {
                dragPlacementController.PlacementCommitted -= OnDefenderPlaced;
                dragPlacementController.PlacementCommitted += OnDefenderPlaced;
            }
        }

        private void OnDisable()
        {
            if (draftController != null)
                draftController.DraftStarted -= OnDraftStarted;
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            if (dragPlacementController != null)
                dragPlacementController.PlacementCommitted -= OnDefenderPlaced;
        }

        // defender-placement-cooldown 1 — 배치가 성공 확정된 유닛 타입을 쿨타임에 넣는다.
        // placementCooldown==0 이면 StartCooldown 이 no-op(등록 안 함) → "0 = inert".
        private void OnDefenderPlaced(DefenderUnitData unit)
        {
            var rt = GameManager.Instance != null ? GameManager.Instance.CooldownRuntime : null;
            if (rt != null && unit != null) rt.StartCooldown(unit, unit.placementCooldown);
        }

        // battle-hud-layout 2 — Placement 풀 / Battle 슬림. 그 외 페이즈는 패널이
        // 자체 이벤트로 숨으므로 크기를 건드리지 않는다.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (_panel == null) return;
            if (phase == GamePhase.Battle)
                ApplyTraySize(0); // 마지막 슬롯 수 유지
            else if (phase == GamePhase.Placement)
            {
                // gift-phase unit 3 — 배치 진입(선물 종료 후)에서 트레이 리사이즈 + 노출·구성.
                // 실제 폭은 RebuildSlots 가 풀 길이로 다시 확정한다.
                ApplyTraySize(0);
                OnDraftConfirmed();
            }
            else
                // gift-phase (review m1) — None/Draft/Gift/Result 에선 숨김(선물 도중 트레이
                // 노출 방지, AwakeningGaugeView 와 일관). Battle/Placement 는 위에서 처리.
                _panel.SetActive(false);
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

            _panel = new GameObject("DefenderPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            // battle-hud-layout 0 — bottom-center: 드림캐쳐 핸드(0,32)와 동일 축/y 로
            // 맞춰 스트립↔핸드 플립이 좌표 점프 없는 제자리 플립이 되게 한다.
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, AnchoredY);
            // ui-tweak 2026-07-08 — 유닛 슬롯 20% 확대(760x100 → 912x120). 슬롯은
            // childForceExpand 로 패널을 채우므로 패널 크기만 키우면 균등 확대된다.
            prt.sizeDelta = PlacementSize;

            var panelImage = _panel.GetComponent<Image>();
            if (trayConfig != null && trayConfig.trayFrame != null)
            {
                panelImage.sprite = trayConfig.trayFrame;
                panelImage.type = Image.Type.Sliced;
                panelImage.color = Color.white;
            }
            else
            {
                var fill = trayConfig != null ? trayConfig.fallbackFill : new Color(0.05f, 0.11f, 0.20f, 0.96f);
                var border = trayConfig != null ? trayConfig.fallbackBorder : new Color(0.94f, 0.72f, 0.24f, 1f);
                panelImage.sprite = UiRoundedSprite.Make(22f, 2f, fill, border);
                panelImage.type = Image.Type.Sliced;
            }
            panelImage.raycastTarget = false;

            float spacing = trayConfig != null ? trayConfig.slotSpacing : 8f;
            int horizontalPadding = trayConfig != null ? trayConfig.horizontalPadding : 18;
            int verticalPadding = trayConfig != null ? trayConfig.verticalPadding : 12;

            // tray-cost-well unit 1 — 바깥 그룹은 [코스트 셀][슬롯 컨테이너] 2 자식.
            // childForceExpandWidth 를 켜면 uGUI 가 per-child flexibleWidth 를
            // Max(flexible, 1) 로 덮어써(HorizontalOrVerticalLayoutGroup.cs:237-238)
            // 셀의 고정폭 계약이 무효화된다 — 반드시 false.
            var hlg = _panel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = spacing;
            hlg.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // 슬롯 전용 컨테이너. RebuildSlots 가 자식을 전부 Destroy 하므로
            // 코스트 셀과 같은 부모에 둘 수 없다(예전엔 _slotContainer == _panel 이라
            // 셀이 첫 배치 진입에서 조용히 파괴됐다).
            var slotsGO = new GameObject("SlotContainer",
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            slotsGO.transform.SetParent(_panel.transform, false);
            var slotHlg = slotsGO.GetComponent<HorizontalLayoutGroup>();
            slotHlg.spacing = spacing;
            slotHlg.childControlWidth = true;
            slotHlg.childControlHeight = true;
            slotHlg.childForceExpandWidth = true;  // 슬롯끼리는 동질 — 균등 분할
            slotHlg.childForceExpandHeight = true;
            var slotsLayout = slotsGO.GetComponent<LayoutElement>();
            slotsLayout.flexibleWidth = 1f;        // 남은 폭 전부
            _slotContainer = slotsGO.transform;

            // 셀은 자신을 sibling 0 으로 넣는다(좌측). 폭은 슬롯 수가 확정되는
            // RebuildSlots 에서 SetCellWidth 로 주입된다.
            if (costDisplay != null) costDisplay.AttachToTray(_panel.transform);

            // drag-cancel-affordance unit 0 — 드래그 취소 존을 컨트롤러에 넘긴다. 패널은 여기서
            // 한 번만 생성되고 RebuildSlots 는 SlotContainer 자식만 지우므로 참조가 stale 되지 않는다.
            if (dragPlacementController != null)
                dragPlacementController.SetCancelZone((RectTransform)_panel.transform);

            UiLayer.Apply(gameObject);
        }

        private Vector2 PlacementSize => trayConfig != null ? trayConfig.placementSize : placementSize;
        private Vector2 BattleSize => trayConfig != null ? trayConfig.battleSize : battleSize;
        private float AnchoredY => trayConfig != null ? trayConfig.anchoredY : 32f;

        // tray-cost-well unit 1 — 트레이 폭은 상수가 아니라 슬롯 수에서 유도한 뒤
        // 하단 코너 위젯(전투시작 버튼·각성 게이지·NextWaveDock) 예약폭으로 클램프한
        // 값이다. 코너 위젯은 전부 canvas order 7 > 트레이 4 라, 겹치면 그 구간의
        // 슬롯은 드래그가 아예 안 된다(각성 패널은 alpha 0.001 불가시 히트영역).
        private void ApplyTraySize(int slotCount)
        {
            if (_panel == null) return;
            if (slotCount > 0) _lastSlotCount = slotCount;
            var rect = (RectTransform)_panel.transform;
            rect.sizeDelta = ComputeTraySize(_lastSlotCount, out float cellWidth);
            if (costDisplay != null && cellWidth > 0f) costDisplay.SetCellWidth(cellWidth);
        }

        private Vector2 ComputeTraySize(int slotCount, out float cellWidth)
        {
            cellWidth = 0f;
            if (trayConfig == null || slotCount <= 0) return PlacementSize;

            float cell = trayConfig.costCellWidth;
            float slot = trayConfig.slotWidth;
            float spacing = trayConfig.slotSpacing;
            float hPad = trayConfig.horizontalPadding * 2f;
            float vPad = trayConfig.verticalPadding * 2f;

            // [pad] cell [gap] slot×n (gap×(n-1)) [pad] → gap 총 n개
            float fixedPart = hPad + spacing * slotCount;
            float flexPart = cell + slot * slotCount;
            float want = fixedPart + flexPart;

            float available = SafeAreaWidth() - trayConfig.cornerReservedWidth;
            if (available > fixedPart && want > available)
            {
                // 축소분은 셀과 슬롯이 같은 비율로 나눈다 — 셀만 고정폭을 지키면
                // 셀이 슬롯보다 넓어져 위계가 뒤집힌다(자원 표시 > 행동 대상).
                float scale = Mathf.Max(0.1f, (available - fixedPart) / flexPart);
                cell *= scale;
                want = available;
            }

            cellWidth = cell;
            return new Vector2(want, trayConfig.slotHeight + vPad);
        }

        // 트레이는 캔버스 직속이 아니라 SafeAreaRoot 자식이다 — 노치/컷아웃이
        // 있는 기기에서 가용폭은 "화면비 × 1080" 이 아니다.
        private float SafeAreaWidth()
        {
            var parent = _panel != null ? _panel.transform.parent as RectTransform : null;
            float w = parent != null ? parent.rect.width : 0f;
            return w > 1f ? w : UiCanvasSetup.ReferenceResolution.x;
        }

        private void RebuildSlots(DefenderUnitData[] pool)
        {
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotContainer.GetChild(i).gameObject);
            // defender-placement-cooldown 2 — 슬롯 GO 는 위에서 Destroy 되지만 셰이더 머티리얼
            // 인스턴스는 GC 되지 않는다(누수) → 명시적으로 정리.
            for (int i = 0; i < _slotVisuals.Count; i++)
                DestroyCooldownMat(_slotVisuals[i].cooldownMat);
            _slotVisuals.Clear();
            _anyCooldownShown = false;
            _lastCostSeen = int.MinValue; // 재빌드 후 첫 Update 에서 강제 갱신

            if (pool == null || pool.Length == 0) return;

            // 트레이 폭은 슬롯 수에서 유도된다 — 풀이 확정된 지금이 유일한 산출 시점.
            ApplyTraySize(pool.Length);

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
                dragSlot.Bind(data, dragPlacementController, costDisplay);

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

                // tray-cost-well unit 3 — 이름 하단 밴드. 포트레이트 유무와 무관하게
                // 항상 깔고 텍스트도 항상 하단 오버레이로 통일한다(예전엔 폴백만 중앙
                // 검정 텍스트라, 아트가 빠지면 반대 색이 되어 안 읽혔다).
                float bandH = trayConfig != null ? trayConfig.nameBandHeight : 36f;
                var bandGO = new GameObject("NameBand", typeof(RectTransform), typeof(Image));
                bandGO.transform.SetParent(go.transform, false);
                var brt = (RectTransform)bandGO.transform;
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 0f);
                brt.pivot = new Vector2(0.5f, 0f);
                brt.sizeDelta = new Vector2(-8f, bandH);
                brt.anchoredPosition = new Vector2(0f, 4f);
                var bandImg = bandGO.GetComponent<Image>();
                bandImg.sprite = UiRoundedSprite.Make(10f, 0f, Color.white, Color.clear);
                bandImg.type = Image.Type.Sliced;
                bandImg.color = NameBandColorFor(data.role);
                bandImg.raycastTarget = false;

                var nameGO = new GameObject("Name", typeof(RectTransform));
                nameGO.transform.SetParent(go.transform, false);
                var nrt = (RectTransform)nameGO.transform;
                nrt.anchorMin = new Vector2(0f, 0f);
                nrt.anchorMax = new Vector2(1f, 0f);
                nrt.pivot = new Vector2(0.5f, 0f);
                // 텍스트 폭을 밴드(-8)보다 안쪽으로 가둔다. 슬롯 폭 전체를 주면 긴 이름
                // ("파이어캐스터" 등)이 밴드 밖으로 삐져나와 흰 글자가 배경 없이 뜬다.
                nrt.sizeDelta = new Vector2(-14f, trayConfig != null ? trayConfig.nameTextHeight : 38f);
                nrt.anchoredPosition = new Vector2(0f, 2f);
                var tmp = nameGO.AddComponent<TextMeshProUGUI>();
                if (nameFont != null) tmp.font = nameFont;
                tmp.text = data.displayName;
                // action-tray unit 1 — 한 줄 auto-size (긴 이름 겹침/잘림 방지, config 범위).
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = trayConfig != null ? trayConfig.nameFontMin : 16f;
                tmp.fontSizeMax = trayConfig != null ? trayConfig.nameFontMax : 26f;
                // tray-cost-well unit 3 — 어두운 밴드 위 흰 텍스트. 예전 검정 볼드 +
                // 흰 아웃라인은 실기에서 거의 안 읽혔다. 밴드가 대비를 만드니
                // 아웃라인은 글자를 뒤덮는 손해만 남는다 — 제거하면 per-instance
                // fontMaterial 복사본도 안 생긴다(슬롯당 머티리얼 1개 절약).
                tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;

                // action-tray unit 1 — 좌상단 비용 chip + 시각 캐시.
                // role 배지는 unit 3 에서 제거됐다 — 클래스 정보는 밴드 틴트가 옮겨 받는다.
                var costText = BuildCostChip(go.transform, data.cost, out var warnGlyph);

                // defender-placement-cooldown 2 — 쿨다운>0 유닛만 오버레이 생성("0 = inert",
                // 머티리얼 인스턴스도 절약). 0 유닛은 전 필드 null → 리페인트가 건너뛴다.
                GameObject cdRoot = null; Image cdFill = null; TextMeshProUGUI cdText = null; Material cdMat = null; Image cdRim = null;
                if (data.placementCooldown > 0f)
                    BuildCooldownOverlay(go.transform, out cdRoot, out cdFill, out cdText, out cdMat, out cdRim);

                _slotVisuals.Add(new SlotVisual
                {
                    data = data,
                    rect = (RectTransform)go.transform,
                    cost = data.cost,
                    portrait = data.portrait != null ? portraitImg : null,
                    slotBg = bg,
                    slotBgBase = bg.color,
                    costText = costText,
                    warnGlyph = warnGlyph,
                    cooldownRoot = cdRoot,
                    cooldownFill = cdFill,
                    cooldownText = cdText,
                    cooldownMat = cdMat,
                    cooldownRim = cdRim,
                });
            }

            UiLayer.Apply(gameObject);
        }

        // first-session-tutorial unit 2 — soft recommendation only. Input stays
        // available on every slot; this never changes affordability or selection.
        public bool TryGetAffordableTutorialSlot(out RectTransform target)
        {
            target = null;
            // unit 13-A3 — 미지원이면 int.MinValue 로 전 슬롯을 탈락시킨다(구 null 폴백과 동일).
            int available = MatchSession.IsActive && MatchSession.Current.ReadModel.SupportedCost
                ? MatchSession.Current.ReadModel.CostCurrentInt
                : int.MinValue;
            int bestCost = int.MaxValue;
            bool foundNonDirectional = false;

            for (int i = 0; i < _slotVisuals.Count; i++)
            {
                var slot = _slotVisuals[i];
                if (slot.data == null || slot.rect == null || slot.cost > available) continue;
                bool nonDirectional = !slot.data.RequiresFacing;
                if (foundNonDirectional && !nonDirectional) continue;
                if (nonDirectional && !foundNonDirectional)
                {
                    foundNonDirectional = true;
                    bestCost = int.MaxValue;
                    target = null;
                }
                if (slot.cost >= bestCost) continue;
                bestCost = slot.cost;
                target = slot.rect;
            }
            return target != null;
        }

        // 구매 가능한 슬롯 가격의 색 = 코스트 셀 물통의 밝은 쪽 색(사용자 결정
        // 2026-07-21). 두 숫자가 같은 자원을 가리킨다는 신호를 색으로 준다.
        private Color AffordableCostColor =>
            trayConfig != null ? trayConfig.wellLiquidFullColor : Color.white;

        // tray-cost-well unit 3 — 이름 밴드 클래스 틴트. role 배지가 사라지면서
        // 클래스를 가리키는 화면 앵커가 여기로 옮겨왔다(first-session-tutorial unit 11
        // 이 첫 배치 직후 5개 클래스 설명을 강제로 읽히는데, 대응물이 없으면 순수
        // 텍스트가 된다). 어두운 base 에 낮은 비율로 섞어 흰 텍스트 대비를 지킨다.
        private Color NameBandColorFor(DefenderClass role)
        {
            var baseColor = trayConfig != null
                ? trayConfig.nameBandColor
                : new Color(0.02f, 0.04f, 0.09f, 0.88f);
            if (trayConfig == null || !trayConfig.TryGetRole(role, out var entry))
                return baseColor;
            var tinted = Color.Lerp(new Color(baseColor.r, baseColor.g, baseColor.b, 1f), entry.color, 0.45f);
            return new Color(tinted.r, tinted.g, tinted.b, baseColor.a);
        }

        // action-tray unit 1 — 좌상단 비용 플레이트. tray-cost-well unit 3 에서
        // ⚡볼트를 뺐다 — 에너지 기호는 코스트 셀에 1개만 두고(HUD 전체 앵커) 나머지
        // 숫자가 의미를 상속한다. 부족 glyph 는 기본 비활성.
        private TextMeshProUGUI BuildCostChip(Transform slot, int cost, out GameObject warnGlyph)
        {
            var plateSize = trayConfig != null ? trayConfig.costPlateSize : new Vector2(56f, 26f);
            var plateColor = trayConfig != null ? trayConfig.costPlateColor : new Color(0.03f, 0.06f, 0.12f, 0.88f);
            var chipGO = new GameObject("CostChip", typeof(RectTransform), typeof(Image));
            chipGO.transform.SetParent(slot, false);
            var crt = (RectTransform)chipGO.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(0f, 1f);
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = new Vector2(3f, -3f);
            crt.sizeDelta = plateSize;
            var chipImg = chipGO.GetComponent<Image>();
            chipImg.sprite = UiRoundedSprite.Make(8f, 0f, Color.white, Color.clear);
            chipImg.type = Image.Type.Sliced;
            chipImg.color = plateColor;
            chipImg.raycastTarget = false;

            var numGO = new GameObject("Cost", typeof(RectTransform));
            numGO.transform.SetParent(chipGO.transform, false);
            var numRt = (RectTransform)numGO.transform;
            numRt.anchorMin = Vector2.zero;
            numRt.anchorMax = Vector2.one;
            numRt.offsetMin = new Vector2(3f, 0f);
            numRt.offsetMax = new Vector2(-3f, 0f);
            var numTmp = numGO.AddComponent<TextMeshProUGUI>();
            if (nameFont != null) numTmp.font = nameFont;
            numTmp.text = cost.ToString();
            numTmp.fontSize = trayConfig != null ? trayConfig.costFontSize : 18f;
            numTmp.fontStyle = FontStyles.Bold;
            // 코스트 셀 물통과 같은 색 — "이 숫자는 그 자원이다"를 색으로 잇는다.
            numTmp.color = AffordableCostColor;
            numTmp.alignment = TextAlignmentOptions.Center; // 볼트가 빠져 좌측 여백이 불필요
            numTmp.textWrappingMode = TextWrappingModes.NoWrap;
            numTmp.raycastTarget = false;

            // 부족 glyph — chip 우하단 "✕" (색 판별 불가 사용자용, 기본 꺼짐).
            var warnGO = new GameObject("Warn", typeof(RectTransform));
            warnGO.transform.SetParent(chipGO.transform, false);
            var wrt = (RectTransform)warnGO.transform;
            wrt.anchorMin = new Vector2(1f, 0f);
            wrt.anchorMax = new Vector2(1f, 0f);
            wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.anchoredPosition = new Vector2(-2f, 4f);
            wrt.sizeDelta = new Vector2(16f, 16f);
            var warnTmp = warnGO.AddComponent<TextMeshProUGUI>();
            if (nameFont != null) warnTmp.font = nameFont;
            warnTmp.text = "X"; // ASCII — 주입 한글 SDF(Jua)에 특수 ✕ 글리프가 없어도 안전
            warnTmp.fontSize = 13f;
            warnTmp.fontStyle = FontStyles.Bold;
            warnTmp.color = trayConfig != null ? trayConfig.costWarnColor : new Color(1f, 0.34f, 0.28f, 1f);
            warnTmp.alignment = TextAlignmentOptions.Center;
            warnTmp.raycastTarget = false;
            warnGO.SetActive(false);
            warnGlyph = warnGO;

            return numTmp;
        }

        // tray-cost-well unit 3 — 우상단 role 배지 제거. 자리 값이 커서 포트레이트를
        // 가리는데 정보량이 그만 못했다. 클래스 정보는 NameBandColorFor 의 밴드
        // 틴트로 옮겼다(신규 픽셀 0). config 의 roles 데이터는 그 틴트 입력이라 유지.

        // action-tray unit 1 — affordability 갱신. ReadModel.CostCurrentInt 가 바뀐
        // 프레임에만 슬롯 순회(값 diff — 매 프레임 할당/검색 없음). 런타임 미초기화는
        // false-negative 를 피해 전부 available 로 유지(계약).
        private void Update()
        {
            if (_panel == null || !_panel.activeInHierarchy || _slotVisuals.Count == 0) return;
            // defender-placement-cooldown 2 — 쿨타임 오버레이는 매 프레임(코스트 diff-gate 위)에서
            // 리페인트한다. fillAmount 가 연속 변하므로 diff-gate 에 삼켜지면 안 된다.
            UpdateCooldownOverlays();
            // unit 13-A3 — 미지원 폴백은 `int.MaxValue`(위 주석의 계약: false-negative 를 피해 전부
            // available). 위 TryGetAffordableTutorialSlot 의 MinValue 와 **방향이 반대**이며 의도된 것이다.
            int current = MatchSession.IsActive && MatchSession.Current.ReadModel.SupportedCost
                ? MatchSession.Current.ReadModel.CostCurrentInt
                : int.MaxValue;
            if (current == _lastCostSeen) return;
            _lastCostSeen = current;

            var dim = trayConfig != null ? trayConfig.unaffordableDim : new Color(0.45f, 0.45f, 0.52f, 1f);
            var warn = trayConfig != null ? trayConfig.costWarnColor : new Color(1f, 0.34f, 0.28f, 1f);
            for (int i = 0; i < _slotVisuals.Count; i++)
            {
                var v = _slotVisuals[i];
                bool affordable = current >= v.cost;
                if (v.portrait != null)
                    v.portrait.color = affordable ? Color.white : dim;
                else if (v.slotBg != null)
                    v.slotBg.color = affordable ? v.slotBgBase : dim;
                if (v.costText != null)
                    v.costText.color = affordable ? AffordableCostColor : warn;
                if (v.warnGlyph != null && v.warnGlyph.activeSelf != !affordable)
                    v.warnGlyph.SetActive(!affordable);
            }
        }

        // defender-placement-cooldown 2 — 매 프레임 쿨타임 오버레이 리페인트. 런타임이 Battle
        // 도메인으로 tick 하므로 여기서는 RemainingFor/Fraction 을 읽어 그리기만 한다(슬로모
        // 감속·정지 동결 자동). shown 상태는 cooldownRoot.activeSelf(참조)로 읽어 struct
        // value-copy 함정을 피한다(critic M1).
        private void UpdateCooldownOverlays()
        {
            // unit 13-A3 — 쿨타임은 세션이 서빙한다. 유닛별 잔여는 키 조회(unitDefId)이고
            // "하나라도 활성인가"는 읽기 모델 스칼라다.
            if (!MatchSession.IsActive)
            {
                if (_anyCooldownShown) { HideAllCooldownOverlays(); _anyCooldownShown = false; }
                return;
            }
            var session = MatchSession.Current;
            // 활성 쿨타임도 표시 중 오버레이도 없으면 순회 스킵(전 유닛 0 이면 O(1)).
            if (!session.ReadModel.AnyPlacementCooldown && !_anyCooldownShown) return;

            bool anyShown = false;
            for (int i = 0; i < _slotVisuals.Count; i++)
            {
                var v = _slotVisuals[i];
                if (v.cooldownRoot == null) continue; // 쿨다운 없는 유닛(오버레이 미생성)
                // false = 쿨타임 아님(구 `RemainingFor(...) > 0f` 판정과 같은 경계).
                // out 변수를 미리 초기화한다 — `v.data != null` 단축 평가로 호출이 생략될 수 있어
                // 컴파일러가 할당을 증명하지 못한다(CS0165).
                float rem = 0f, cdFraction = 0f;
                bool onCooldown = v.data != null
                    && session.TryGetPlacementCooldown(v.data.id, out rem, out cdFraction);
                bool shown = v.cooldownRoot.activeSelf;
                if (onCooldown)
                {
                    if (!shown)
                    {
                        v.cooldownRoot.SetActive(true);
                        // 재표시 시 직전 쿨타임의 잔여 팝 스케일 초기화(안전).
                        if (v.cooldownText != null) v.cooldownText.rectTransform.localScale = Vector3.one;
                    }
                    float frac = cdFraction; // 남은비율 = 수위 → 아래로 빠짐 (조회 1회에 함께 왔다)
                    if (v.cooldownMat != null) { v.cooldownMat.SetFloat(CdFillId, frac); PushCooldownGeometry(v); }
                    else if (v.cooldownFill != null) v.cooldownFill.fillAmount = frac;
                    if (v.cooldownText != null)
                    {
                        // (juice 3) 정수 카운트다운이 바뀌는 프레임에만 스쿼시 팝. text 자체가
                        // 직전 값 저장소라 struct write-back 없이 변화 감지(critic M1 회피).
                        string s = Mathf.CeilToInt(rem).ToString();
                        if (v.cooldownText.text != s)
                        {
                            v.cooldownText.text = s;
                            StartCoroutine(NumberTickPopRoutine(v.cooldownText.rectTransform));
                        }
                    }
                    UpdateCooldownRim(v); // 림 글로우 호흡 펄스(시간 기반 stateless)
                    anyShown = true;
                }
                else if (shown)
                {
                    v.cooldownRoot.SetActive(false);
                    StartCoroutine(ReadyFlourishRoutine(v.rect)); // (juice 4) 종료 플러리시(전이 1회)
                }
            }
            _anyCooldownShown = anyShown;
        }

        private void HideAllCooldownOverlays()
        {
            for (int i = 0; i < _slotVisuals.Count; i++)
                if (_slotVisuals[i].cooldownRoot != null) _slotVisuals[i].cooldownRoot.SetActive(false);
        }

        // 셰이더 라운드코너는 종횡비를 알아야 타원으로 안 찌그러진다(CostDisplay.PushWellGeometry 미러).
        private void PushCooldownGeometry(SlotVisual v)
        {
            if (v.cooldownMat == null || v.cooldownFill == null) return;
            var rt = v.cooldownFill.rectTransform; // SDF 는 액체 quad(포트레이트 영역) 기준
            float w = rt.rect.width, h = rt.rect.height;
            if (w <= 1f || h <= 1f) return;
            v.cooldownMat.SetFloat(CdAspectId, w / h);
            v.cooldownMat.SetFloat(CdRadiusId, CooldownCornerRadius / h);
        }

        // 림 글로우 호흡 펄스. 상태 저장 없이 Time.unscaledTime 으로 알파만 변조(struct value-copy
        // 회피). 시각 플레어라 슬로모 무관 실시간(팝/플러리시와 일관). 기포는 CostDisplay 로 이관.
        private void UpdateCooldownRim(SlotVisual v)
        {
            if (v.cooldownRim == null) return;
            float baseA = trayConfig != null ? trayConfig.cooldownRimColor.a : 0.5f;
            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f)); // 느린 호흡
            var c = v.cooldownRim.color; c.a = baseA * pulse; v.cooldownRim.color = c;
        }

        // 오버슛(back-out) 이징 — 1 을 살짝 넘겼다 정착하는 "보잉". 말랑 연출의 뼈대.
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        // (juice 3) 카운트다운 틱 스쿼시&스트레치 팝. 부피 보존 느낌으로 가로↔세로 반대로 눌렀다
        // EaseOutBack 로 1,1 정착. UI 라 unscaled. rect 파괴 시 즉시 중단.
        private System.Collections.IEnumerator NumberTickPopRoutine(RectTransform rect)
        {
            if (rect == null) yield break;
            float pop = trayConfig != null ? trayConfig.cooldownTickPopScale : 1.35f;
            const float dur = 0.22f;
            float t = 0f;
            while (t < dur)
            {
                if (rect == null) yield break;
                t += Time.unscaledDeltaTime;
                float e = EaseOutBack(Mathf.Clamp01(t / dur));
                float sx = Mathf.LerpUnclamped(pop, 1f, e);
                float sy = Mathf.LerpUnclamped(2f - pop, 1f, e); // 반대 축
                rect.localScale = new Vector3(sx, sy, 1f);
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        // (juice 4) 종료 플러리시 — 유닛이 탱 튀어나오는 탄성 팝 + 잔물결 링 + 섬광(병렬).
        // UI 연출이라 unscaled. slotRect 파괴(RebuildSlots) 시 즉시 물러남(critic m2).
        private System.Collections.IEnumerator ReadyFlourishRoutine(RectTransform slotRect)
        {
            if (slotRect == null) yield break;
            StartCoroutine(ReadyRippleRoutine(slotRect));
            StartCoroutine(ReadyFlashRoutine(slotRect));

            float pop = trayConfig != null ? trayConfig.cooldownReadyPopScale : 1.16f;
            float dur = trayConfig != null ? Mathf.Max(0.01f, trayConfig.cooldownReadyPopDuration) : 0.22f;
            float t = 0f;
            while (t < dur)
            {
                if (slotRect == null) yield break;
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                // 살짝 눌렀다(0.9) 팝(pop)까지 튄 뒤 EaseOutBack 로 1 에 정착(오버슛) = 스프링 아웃.
                float s = k < 0.30f
                    ? Mathf.Lerp(0.90f, pop, k / 0.30f)
                    : pop + (1f - pop) * EaseOutBack((k - 0.30f) / 0.70f);
                slotRect.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            if (slotRect != null) slotRect.localScale = Vector3.one;
        }

        // 확장·페이드하는 잔물결 링(테두리만). 슬롯 자식 임시 GO — 끝나면 Destroy.
        private System.Collections.IEnumerator ReadyRippleRoutine(RectTransform slotRect)
        {
            if (slotRect == null) yield break;
            float w = slotRect.rect.width, h = slotRect.rect.height;
            if (w <= 1f || h <= 1f) yield break;

            var go = new GameObject("CooldownRipple", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slotRect, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);
            var img = go.GetComponent<Image>();
            img.sprite = UiRoundedSprite.Make(Mathf.Min(w, h) * 0.5f, 6f, Color.clear, Color.white); // 링(테두리만)
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            var baseCol = trayConfig != null ? trayConfig.cooldownReadyRippleColor : new Color(0.60f, 0.90f, 1f, 0.9f);
            float maxScale = trayConfig != null ? Mathf.Max(1f, trayConfig.cooldownReadyRippleScale) : 1.7f;
            const float dur = 0.40f;
            float t = 0f;
            while (t < dur)
            {
                if (go == null) yield break;
                if (slotRect == null) { Destroy(go); yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = Mathf.Lerp(0.6f, maxScale, k);
                rt.localScale = new Vector3(s, s, 1f);
                var c = baseCol; c.a = baseCol.a * (1f - k); img.color = c;
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // 셀 전체 짧은 밝은 섬광(페이드). 슬롯 자식 임시 GO — 끝나면 Destroy.
        private System.Collections.IEnumerator ReadyFlashRoutine(RectTransform slotRect)
        {
            if (slotRect == null) yield break;
            var go = new GameObject("CooldownFlash", typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(slotRect, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = UiRoundedSprite.Make(12f, 0f, Color.white, Color.clear);
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            var baseCol = trayConfig != null ? trayConfig.cooldownReadyFlashColor : new Color(1f, 1f, 1f, 0.5f);
            const float dur = 0.18f;
            float t = 0f;
            while (t < dur)
            {
                if (go == null) yield break;
                if (slotRect == null) { Destroy(go); yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                var c = baseCol; c.a = baseCol.a * (1f - k); img.color = c;
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // defender-placement-cooldown 2 — 쿨타임 오버레이. 루트는 셀 전체를 덮고 3층으로 구성:
        //  (1) 딤 스크림 — 셀 전체 상시 어둡게(이름밴드/칩 포함, 액체가 빠져도 유지)
        //  (2) 액체 — 포트레이트 영역만, 코스트 물통 셰이더 재사용, _Fill=남은비율로 아래로 빠짐
        //  (3) 카운트다운 숫자 — 포트레이트 영역 중앙
        private void BuildCooldownOverlay(Transform slot, out GameObject root, out Image fill,
            out TextMeshProUGUI text, out Material mat, out Image rim)
        {
            float bandH = trayConfig != null ? trayConfig.nameBandHeight : 36f;
            rim = null;

            root = new GameObject("CooldownOverlay", typeof(RectTransform));
            var rrt = (RectTransform)root.transform;
            rrt.SetParent(slot, false);
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(2f, 2f); // 셀 전체(이름밴드까지)
            rrt.offsetMax = new Vector2(-2f, -2f);
            rrt.SetAsLastSibling();

            // (1) 셀 전체 딤 스크림 — 액체 아래(먼저 생성=아래 레이어). 상시 어둡게.
            var dimGO = new GameObject("CooldownCellDim", typeof(RectTransform), typeof(Image));
            dimGO.transform.SetParent(root.transform, false);
            var drt = (RectTransform)dimGO.transform;
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            var dimImg = dimGO.GetComponent<Image>();
            dimImg.sprite = UiRoundedSprite.Make(CooldownCornerRadius, 0f, Color.white, Color.clear);
            dimImg.type = Image.Type.Sliced;
            dimImg.color = trayConfig != null ? trayConfig.cooldownCellDim : new Color(0.02f, 0.03f, 0.06f, 0.55f);
            dimImg.raycastTarget = false;

            // (2) 액체 — 포트레이트 영역(이름밴드 위)만.
            var fillGO = new GameObject("CooldownLiquid", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(root.transform, false);
            var frt = (RectTransform)fillGO.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(0f, bandH); frt.offsetMax = Vector2.zero; // 이름밴드 위
            fill = fillGO.GetComponent<Image>();
            fill.sprite = UiRoundedSprite.Make(0f, 0f, Color.white, Color.clear);
            fill.raycastTarget = false;

            float opacity = trayConfig != null ? trayConfig.cooldownLiquidOpacity : 0.85f;
            var liquidMat = trayConfig != null ? trayConfig.wellLiquidMaterial : null;
            mat = null;
            if (liquidMat != null)
            {
                // 셰이더 모드: Type.Simple + _Fill 로 수위를 프래그먼트가 자른다. 셰이더는 액체색
                // alpha 를 안 보므로 반투명은 Image.color.a 로 준다(포트레이트가 흐릿하게 비침).
                fill.type = Image.Type.Simple;
                fill.color = new Color(1f, 1f, 1f, opacity);
                mat = new Material(liquidMat) { name = "CooldownLiquid (Instance)", hideFlags = HideFlags.HideAndDontSave };
                if (trayConfig != null)
                {
                    mat.SetColor(CdLiquidBottomId, trayConfig.cooldownLiquidBottom);
                    mat.SetColor(CdLiquidTopId, trayConfig.cooldownLiquidTop);
                    mat.SetColor(CdSurfaceId, trayConfig.cooldownLiquidSurface);
                }
                mat.SetFloat(CdFillId, 0f);
                fill.material = mat;
            }
            else
            {
                // 폴백(셰이더 미할당): Filled 세로 채움. fillAmount=남은비율=수위(아래에서 위로).
                var top = trayConfig != null ? trayConfig.cooldownLiquidTop : new Color(0.24f, 0.27f, 0.35f, 1f);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                fill.fillAmount = 0f;
                fill.color = new Color(top.r, top.g, top.b, opacity);
            }

            var numGO = new GameObject("CooldownNumber", typeof(RectTransform));
            numGO.transform.SetParent(root.transform, false);
            var nrt = (RectTransform)numGO.transform;
            nrt.anchorMin = Vector2.zero; nrt.anchorMax = Vector2.one;
            nrt.offsetMin = new Vector2(0f, bandH); nrt.offsetMax = Vector2.zero; // 포트레이트 영역 중앙
            text = numGO.AddComponent<TextMeshProUGUI>();
            if (nameFont != null) text.font = nameFont;
            text.text = "";
            text.fontSize = trayConfig != null ? trayConfig.cooldownFontSize : 30f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = trayConfig != null ? trayConfig.cooldownTextColor : Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            // 액체 위 가독성 — 아웃라인 + 언더레이(드롭섀도). CostDisplay value 의 검증된 레시피
            // (어두운 물통↔밝은 액체 극단에서도 흰 숫자가 읽히게). 쿨타임 액체가 어두워도
            // 파형/하이라이트 위에서 또렷하게.
            var tmat = text.fontMaterial;
            tmat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            tmat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            tmat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.02f, 0.03f, 0.05f, 1f));
            tmat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            tmat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.4f);
            tmat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.4f);
            tmat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.35f);
            tmat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.95f));

            // (림 글로우) 테두리 안쪽 은은한 빛 — 색 alpha>0 일 때만. root 최상위 자식(테두리만이라
            // 중앙 숫자와 겹치지 않는다). 알파 호흡 펄스는 UpdateCooldownRim.
            // 기포는 코스트 물통(CostDisplay)으로 이관 — 유닛 셀은 림만(사용자 결정 2026-07-22).
            var rimColor = trayConfig != null ? trayConfig.cooldownRimColor : new Color(0.55f, 0.85f, 1f, 0.5f);
            if (rimColor.a > 0f)
            {
                var rimGO = new GameObject("CooldownRim", typeof(RectTransform), typeof(Image));
                rimGO.transform.SetParent(root.transform, false);
                var rimRt = (RectTransform)rimGO.transform;
                rimRt.anchorMin = Vector2.zero; rimRt.anchorMax = Vector2.one;
                rimRt.offsetMin = Vector2.zero; rimRt.offsetMax = Vector2.zero;
                rim = rimGO.GetComponent<Image>();
                rim.sprite = UiRoundedSprite.Make(CooldownCornerRadius, 3f, Color.clear, Color.white); // 테두리만
                rim.type = Image.Type.Sliced;
                rim.color = rimColor;
                rim.raycastTarget = false;
            }

            root.SetActive(false);
        }

        private void DestroyCooldownMat(Material m)
        {
            if (m == null) return;
            if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _slotVisuals.Count; i++)
                DestroyCooldownMat(_slotVisuals[i].cooldownMat);
        }

        // unit-dreamcatcher-inspect unit 0 — 드래그 컨트롤러 도달 경로. 컨트롤러는
        // EnsureDragController 가 런타임에 AddComponent 하므로 씬에서 배선할 수 없다
        // (이 SerializeField 도 씬에선 비어 있다). 수명 소유자인 여기서 노출한다.
        // 아직 생성 전이면 null — 호출측은 null == "드래그 안 함" 으로 읽으면 된다.
        public DefenderDragPlacementController DragController => dragPlacementController;

        private void EnsureDragController()
        {
            if (dragPlacementController == null)
                dragPlacementController = GetComponent<DefenderDragPlacementController>();
            if (dragPlacementController == null)
                dragPlacementController = gameObject.AddComponent<DefenderDragPlacementController>();
            if (deployCutscenePlayer == null)
                deployCutscenePlayer = GetComponent<DeployCutscenePlayer>();
            if (deployCutscenePlayer == null)
                deployCutscenePlayer = gameObject.AddComponent<DeployCutscenePlayer>();
            if (depthParallaxSettings != null)
                deployCutscenePlayer.SetSettings(depthParallaxSettings);
            if (bridge != null)
                dragPlacementController.Configure(bridge, Camera.main, bridge.PlacementInput,
                    swaySettings, nameFont, deployCutscenePlayer);
            // drag-cancel-affordance unit 0 — 취소 존 = 트레이 패널. Awake 는 이 메서드를
            // BuildCanvas 보다 먼저 부르므로 여기선 아직 null 일 수 있다(BuildCanvas 말미가 다시 준다).
            if (_panel != null)
                dragPlacementController.SetCancelZone((RectTransform)_panel.transform);
        }
    }
}
