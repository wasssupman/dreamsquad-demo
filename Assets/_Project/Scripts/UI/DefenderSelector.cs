using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
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
        // gimmick-match-integration unit 5 — 기믹 안내 카드의 "첫 배치 상호작용" 접힘 트리거.
        // 컨트롤러가 런타임 부착이라 뷰가 직접 참조할 수 없어, 수명 소유자인 여기서 Bind 로
        // 넘긴다(costDisplay 패턴). 미할당이면 카드는 타이머 접힘만으로 동작한다.
        [SerializeField] private GimmickGuideView gimmickGuide;
        // battle-hud-layout 2 — 페이즈별 스트립 크기. Battle 은 관전이 주 활동이라
        // 슬림 축소로 중앙 하단 보드 가림을 상쇄한다. 슬롯은 childForceExpand 라
        // 패널 크기만 바꾸면 균등 축소된다.
        [SerializeField] private Vector2 placementSize = new Vector2(912f, 120f);
        [SerializeField] private Vector2 battleSize = new Vector2(912f, 88f);

        private GameObject _panel;
        private Transform _slotContainer;
        private bool _built;

        // action-tray unit 1 — 슬롯별 시각 참조 캐시. RebuildSlots 에서만 재구성하고
        // Update 는 CostRuntime.CurrentInt 가 바뀐 프레임에만 순회한다(매 프레임
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
        }

        private readonly List<SlotVisual> _slotVisuals = new();
        private int _lastCostSeen = int.MinValue;

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
        }

        private void OnDisable()
        {
            if (draftController != null)
                draftController.DraftStarted -= OnDraftStarted;
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
        }

        // battle-hud-layout 2 — Placement 풀 / Battle 슬림. 그 외 페이즈는 패널이
        // 자체 이벤트로 숨으므로 크기를 건드리지 않는다.
        private void OnPhaseChanged(GamePhase phase)
        {
            if (_panel == null) return;
            if (phase == GamePhase.Battle)
                ((RectTransform)_panel.transform).sizeDelta = BattleSize;
            else if (phase == GamePhase.Placement)
            {
                // gift-phase unit 3 — 배치 진입(선물 종료 후)에서 트레이 리사이즈 + 노출·구성.
                ((RectTransform)_panel.transform).sizeDelta = PlacementSize;
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

            var hlg = _panel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = trayConfig != null ? trayConfig.slotSpacing : 8f;
            int horizontalPadding = trayConfig != null ? trayConfig.horizontalPadding : 18;
            int verticalPadding = trayConfig != null ? trayConfig.verticalPadding : 12;
            hlg.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            _slotContainer = _panel.transform;

            UiLayer.Apply(gameObject);
        }

        private Vector2 PlacementSize => trayConfig != null ? trayConfig.placementSize : placementSize;
        private Vector2 BattleSize => trayConfig != null ? trayConfig.battleSize : battleSize;
        private float AnchoredY => trayConfig != null ? trayConfig.anchoredY : 32f;

        private void RebuildSlots(DefenderUnitData[] pool)
        {
            for (int i = _slotContainer.childCount - 1; i >= 0; i--)
                Destroy(_slotContainer.GetChild(i).gameObject);
            _slotVisuals.Clear();
            _lastCostSeen = int.MinValue; // 재빌드 후 첫 Update 에서 강제 갱신

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

                // action-tray unit 1 — 이름 하단 반투명 밴드 (텍스트보다 먼저 추가해
                // 아래에 깔림). 포트레이트 슬롯에만 — 폴백 중앙 텍스트는 기존 유지.
                if (data.portrait != null)
                {
                    var bandGO = new GameObject("NameBand", typeof(RectTransform), typeof(Image));
                    bandGO.transform.SetParent(go.transform, false);
                    var brt = (RectTransform)bandGO.transform;
                    brt.anchorMin = new Vector2(0f, 0f);
                    brt.anchorMax = new Vector2(1f, 0f);
                    brt.pivot = new Vector2(0.5f, 0f);
                    brt.sizeDelta = new Vector2(-8f, 30f);
                    brt.anchoredPosition = new Vector2(0f, 4f);
                    var bandImg = bandGO.GetComponent<Image>();
                    bandImg.sprite = UiRoundedSprite.Make(10f, 0f, Color.white, Color.clear);
                    bandImg.type = Image.Type.Sliced;
                    bandImg.color = trayConfig != null ? trayConfig.nameBandColor : new Color(0f, 0f, 0f, 0.35f);
                    bandImg.raycastTarget = false;
                }

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
                // action-tray unit 1 — 한 줄 auto-size (긴 이름 겹침/잘림 방지, config 범위).
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = trayConfig != null ? trayConfig.nameFontMin : 16f;
                tmp.fontSizeMax = trayConfig != null ? trayConfig.nameFontMax : 26f;
                if (data.portrait == null) tmp.fontSizeMax = 30f;
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

                // action-tray unit 1 — 좌상단 비용 chip + 우상단 role 배지 + 시각 캐시.
                var costText = BuildCostChip(go.transform, data.cost, out var warnGlyph);
                BuildRoleBadge(go.transform, data.role);
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
                });
            }

            UiLayer.Apply(gameObject);
        }

        // first-session-tutorial unit 2 — soft recommendation only. Input stays
        // available on every slot; this never changes affordability or selection.
        public bool TryGetAffordableTutorialSlot(out RectTransform target)
        {
            target = null;
            var runtime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            int available = runtime != null ? runtime.CurrentInt : int.MinValue;
            int bestCost = int.MaxValue;
            bool foundNonDirectional = false;

            for (int i = 0; i < _slotVisuals.Count; i++)
            {
                var slot = _slotVisuals[i];
                if (slot.data == null || slot.rect == null || slot.cost > available) continue;
                bool nonDirectional = !slot.data.directionalAttack;
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

        // action-tray unit 1 — 좌상단 비용 플레이트 (시안: 다크 코너 플레이트에
        // ⚡볼트 + 숫자). 볼트 스프라이트 누락 시 숫자만. 부족 glyph 는 기본 비활성.
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

            float numX = 6f;
            var bolt = trayConfig != null ? trayConfig.slotCostBolt : null;
            if (bolt != null)
            {
                float boltSize = plateSize.y - 8f;
                var boltGO = new GameObject("Bolt", typeof(RectTransform), typeof(Image));
                boltGO.transform.SetParent(chipGO.transform, false);
                var brt2 = (RectTransform)boltGO.transform;
                brt2.anchorMin = new Vector2(0f, 0.5f);
                brt2.anchorMax = new Vector2(0f, 0.5f);
                brt2.pivot = new Vector2(0f, 0.5f);
                brt2.anchoredPosition = new Vector2(5f, 0f);
                brt2.sizeDelta = new Vector2(boltSize, boltSize);
                var boltImg = boltGO.GetComponent<Image>();
                boltImg.sprite = bolt;
                boltImg.preserveAspect = true;
                boltImg.raycastTarget = false;
                numX = 5f + boltSize + 3f;
            }

            var numGO = new GameObject("Cost", typeof(RectTransform));
            numGO.transform.SetParent(chipGO.transform, false);
            var numRt = (RectTransform)numGO.transform;
            numRt.anchorMin = Vector2.zero;
            numRt.anchorMax = Vector2.one;
            numRt.offsetMin = new Vector2(numX, 0f);
            numRt.offsetMax = new Vector2(-4f, 0f);
            var numTmp = numGO.AddComponent<TextMeshProUGUI>();
            if (nameFont != null) numTmp.font = nameFont;
            numTmp.text = cost.ToString();
            numTmp.fontSize = trayConfig != null ? trayConfig.costFontSize : 18f;
            numTmp.fontStyle = FontStyles.Bold;
            numTmp.color = Color.white;
            numTmp.alignment = TextAlignmentOptions.MidlineLeft;
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

        // action-tray unit 1 — 우상단 role 배지. config entry 누락/미할당 시 neutral
        // 폴백(회색 "?") — NRE 없이 성립(unit 0 완료 기준 이월분).
        private void BuildRoleBadge(Transform slot, DefenderClass role)
        {
            string glyph = "?";
            Color color = new Color(0.45f, 0.45f, 0.5f, 0.95f);
            if (trayConfig != null && trayConfig.TryGetRole(role, out var entry) && !string.IsNullOrEmpty(entry.glyph))
            {
                glyph = entry.glyph;
                color = entry.color;
            }

            var badgeSize = trayConfig != null ? trayConfig.roleBadgeSize : new Vector2(26f, 26f);
            var badgeGO = new GameObject("RoleBadge", typeof(RectTransform), typeof(Image));
            badgeGO.transform.SetParent(slot, false);
            var brt = (RectTransform)badgeGO.transform;
            brt.anchorMin = new Vector2(1f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-2f, -2f);
            brt.sizeDelta = badgeSize;
            var badgeImg = badgeGO.GetComponent<Image>();
            badgeImg.sprite = UiRoundedSprite.MakeCircle(26, color, 1.5f, new Color(0f, 0f, 0f, 0.55f));
            badgeImg.raycastTarget = false;

            var glyphGO = new GameObject("Glyph", typeof(RectTransform));
            glyphGO.transform.SetParent(badgeGO.transform, false);
            var grt = (RectTransform)glyphGO.transform;
            grt.anchorMin = Vector2.zero;
            grt.anchorMax = Vector2.one;
            grt.offsetMin = Vector2.zero;
            grt.offsetMax = Vector2.zero;
            var glyphTmp = glyphGO.AddComponent<TextMeshProUGUI>();
            if (nameFont != null) glyphTmp.font = nameFont;
            glyphTmp.text = glyph;
            glyphTmp.fontSize = trayConfig != null ? trayConfig.roleFontSize : 14f;
            glyphTmp.fontStyle = FontStyles.Bold;
            glyphTmp.color = Color.white;
            glyphTmp.alignment = TextAlignmentOptions.Center;
            glyphTmp.textWrappingMode = TextWrappingModes.NoWrap;
            glyphTmp.raycastTarget = false;
        }

        // action-tray unit 1 — affordability 갱신. CostRuntime.CurrentInt 가 바뀐
        // 프레임에만 슬롯 순회(값 diff — 매 프레임 할당/검색 없음). 런타임 미초기화는
        // false-negative 를 피해 전부 available 로 유지(계약).
        private void Update()
        {
            if (_panel == null || !_panel.activeInHierarchy || _slotVisuals.Count == 0) return;
            var costRuntime = GameManager.Instance != null ? GameManager.Instance.CostRuntime : null;
            int current = costRuntime != null ? costRuntime.CurrentInt : int.MaxValue;
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
                    v.costText.color = affordable ? Color.white : warn;
                if (v.warnGlyph != null && v.warnGlyph.activeSelf != !affordable)
                    v.warnGlyph.SetActive(!affordable);
            }
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
            if (gimmickGuide != null)
                gimmickGuide.BindPlacementActivity(dragPlacementController);
        }
    }
}
