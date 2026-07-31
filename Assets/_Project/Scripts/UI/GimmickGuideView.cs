using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // gimmick-match-integration unit 2 — 배치 페이즈 기믹 안내 카드.
    // GameManager.PhaseChanged 구독: Placement 진입 시 배정 기믹(제목+설명) 표시, 다른 페이즈엔 숨김.
    // 좌상단 메뉴버튼(order 1000) 회피 위해 상단 중앙(카운트다운 배너 아래) 배치 + sortingOrder 8.
    // AssignedGimmick==null(기믹 비활성) 이면 Placement 라도 표시 안 함.
    //
    // unit 5 — 카드는 우상단 도킹(오른쪽 화면 밖 → 슬라이드-인 등장). 첫 배치 상호작용
    // (DragBegan/Armed), 하단 "가즈아" 버튼 탭, 또는 autoCollapseSeconds 경과 시 같은 코너의
    // 칩으로 접혀 맵 시야를 확보한다. 칩 탭 = 재확장(같은 슬라이드-인) + 트리거 재무장.
    // 전환은 PrimeTween Sequence(useUnscaledTime) — 드래그 슬로우모(TimeManager Battle 도메인)
    // 영향 배제. 스타일 선례: BossWarningView.
    public class GimmickGuideView : MonoBehaviour
    {
        [SerializeField] private float cardWidth = 640f;
        // 카드/칩 공용 코너. 폭 640 카드의 왼쪽 끝이 상단 중앙 배치 배너(±280)와 겹치지 않는다.
        [SerializeField] private Vector2 cardCornerOffset = new Vector2(-40f, -40f);
        [SerializeField] private float introAnimSeconds = 0.3f;   // 화면 밖 → 코너 슬라이드-인
        [SerializeField] private float introOffscreenMargin = 60f; // 시작점: 오른쪽 화면 끝 밖 여분
        [SerializeField] private float autoCollapseSeconds = 3f;   // 상호작용 없이도 이 시간 뒤 접힘 (0 이하 = 타이머 접힘 없음)
        [SerializeField] private float collapseAnimSeconds = 0.18f;
        [SerializeField] private Vector2 chipCornerOffset = new Vector2(-40f, -40f); // 우상단 앵커 기준 (Battle 스코어 HUD 자리 — 이 뷰는 Placement 전용이라 충돌 없음)

        private GameObject _card;
        private GameObject _chip;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _subtitleLabel;
        private TextMeshProUGUI _summaryLabel;
        private TextMeshProUGUI _bodyLabel;
        private TextMeshProUGUI _chipLabel;
        private Image _cardIcon;
        private Image _chipIcon;
        private CanvasGroup _cardGroup;
        private CanvasGroup _chipGroup;
        private RectTransform _cardRt;
        private RectTransform _chipRt;
        private bool _built;
        private bool _subscribed;
        private bool _suppressed;
        private GamePhase _phase;
        private bool _showing;     // 표시 조건 충족(placement && 기믹 && 비억제) — 접힘 상태와 별개
        private bool _collapsed;
        private float _expandedAt; // unscaled 기준 — 자동 접힘 타이머
        private Sequence _seq;
        private DefenderDragPlacementController _controller;
        private bool _activityBound;
        // 라벨 공통 다크 아웃라인 — 라벨마다 fontMaterial 인스턴스를 만들지 않고 1개를 공유한다.
        private Material _labelOutlineMat;

        private static readonly Color AccentGold = new Color(1f, 0.82f, 0.35f, 1f);
        private static readonly Color PlateDark = new Color(0.03f, 0.04f, 0.09f, 0.97f);

        private void Awake()
        {
            BuildCanvas();
            if (_card != null) _card.SetActive(false);
            if (_chip != null) _chip.SetActive(false);
        }

        private void OnEnable()
        {
            TrySubscribe();
            SubscribeActivity();
            // 재활성 시 stale 타이머 방어 — 확장 카드가 비활성 기간을 타이머 경과로 오인해
            // 첫 프레임에 즉시 접히지 않게 재무장(리뷰 fix).
            if (_showing && !_collapsed) _expandedAt = Time.unscaledTime;
        }

        // Instance 가 Awake 시점에 없었으면(실행 순서) 여기서 재시도.
        private void Start() => TrySubscribe();

        private void OnDisable()
        {
            if (_subscribed && GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
            UnsubscribeActivity();
            // 진행 중 시퀀스를 끊고(behaviour-only disable 에도 안전 — 리뷰 fix) 접힘 상태의
            // 최종 모습으로 스냅.
            StopAnim();
            if (_showing)
            {
                if (_card != null) _card.SetActive(!_collapsed);
                if (_chip != null) _chip.SetActive(_collapsed);
            }
        }

        private void OnDestroy()
        {
            if (_labelOutlineMat != null) Destroy(_labelOutlineMat);
        }

        // unit 5 — DefenderSelector 가 컨트롤러 준비 후 호출(컨트롤러는 런타임 부착이라 씬 참조 불가).
        // 미호출이면 이벤트 트리거 없이 autoCollapseSeconds 타이머만으로 동작한다.
        public void BindPlacementActivity(DefenderDragPlacementController controller)
        {
            if (controller == null) return;
            if (_controller == controller && _activityBound) return;
            UnsubscribeActivity();
            _controller = controller;
            if (isActiveAndEnabled) SubscribeActivity();
        }

        private void SubscribeActivity()
        {
            if (_activityBound || _controller == null) return;
            _controller.DragBegan += CollapseIfExpanded;
            _controller.Armed += OnArmedActivity;
            _activityBound = true;
        }

        private void UnsubscribeActivity()
        {
            if (!_activityBound) return;
            if (_controller != null)
            {
                _controller.DragBegan -= CollapseIfExpanded;
                _controller.Armed -= OnArmedActivity;
            }
            _activityBound = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
            OnPhaseChanged(gm.CurrentPhase); // 현재 페이즈 동기(재시작/late-enable 대비)
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            _phase = phase;
            RefreshVisibility();
        }

        public void SetTutorialSuppressed(bool suppressed)
        {
            _suppressed = suppressed;
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            var gm = GameManager.Instance;
            bool show = !_suppressed && _phase == GamePhase.Placement && gm != null && gm.AssignedGimmick != null;
            if (show) Populate(gm.AssignedGimmick);
            if (show == _showing) return; // 표시 조건 유지 중엔 접힘 상태 보존(내용만 갱신)
            _showing = show;
            StopAnim();
            if (show)
            {
                ShowExpanded();
            }
            else
            {
                if (_card != null) _card.SetActive(false);
                if (_chip != null) _chip.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_showing || _collapsed) return;
            if (autoCollapseSeconds > 0f && Time.unscaledTime - _expandedAt >= autoCollapseSeconds)
                Collapse();
        }

        // 접힘 트리거 공통 진입점: 드래그 시작 / arm / "가즈아" 버튼이 전부 이 하나를 쓴다(리뷰 fix — 중복 제거).
        private void CollapseIfExpanded()
        {
            if (_showing && !_collapsed) Collapse();
        }

        private void OnArmedActivity(DefenderUnitData _) => CollapseIfExpanded();

        private void OnChipTapped()
        {
            if (!_showing) return;
            StopAnim();
            if (_chip != null) _chip.SetActive(false);
            ShowExpanded(); // 타이머 리셋 + 트리거 재무장 — 다음 드래그/arm 에 다시 접힘
        }

        private void ShowExpanded()
        {
            _collapsed = false;
            _expandedAt = Time.unscaledTime;
            ResetCardVisual();
            if (_card != null) _card.SetActive(true);
            if (_chip != null) _chip.SetActive(false);
            // 등장 연출: 오른쪽 화면 밖 → 코너 슬라이드-인. pivot(1,1) 기준 x=+cardWidth 면
            // 카드 왼쪽 끝이 화면 오른쪽 끝 — 여분을 더해 완전히 가린 뒤 들어온다.
            if (_seq.isAlive) _seq.Stop();
            _cardRt.anchoredPosition = new Vector2(cardWidth + introOffscreenMargin, cardCornerOffset.y);
            _seq = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.UIAnchoredPosition(_cardRt, cardCornerOffset, introAnimSeconds, Ease.OutCubic));
        }

        private void Collapse()
        {
            if (_collapsed) return;
            _collapsed = true;
            if (_seq.isAlive) _seq.Stop();
            // 카드 페이드+스케일 아웃 → 완료 콜백에서 칩 팝인(별도 시퀀스 — 비활성 칩에 미리
            // 트윈을 만들지 않는다). 자기 콜백 안에서 Stop 금지(BossWarningView 교훈).
            _seq = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Alpha(_cardGroup, 0f, collapseAnimSeconds, Ease.OutQuad))
                .Group(Tween.Scale(_cardRt, Vector3.one * 0.92f, collapseAnimSeconds, Ease.OutQuad))
                .ChainCallback(ShowChipPop);
        }

        private void ShowChipPop()
        {
            _card.SetActive(false);
            ResetCardVisual();
            _chip.SetActive(true);
            _chipGroup.alpha = 0f;
            _chipRt.localScale = Vector3.one * 0.85f;
            _seq = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Alpha(_chipGroup, 1f, collapseAnimSeconds, Ease.OutQuad))
                .Group(Tween.Scale(_chipRt, Vector3.one, collapseAnimSeconds, Ease.OutBack));
        }

        // teardown 공통: 진행 중 시퀀스 중단 + 시각 상태 원복. active 스냅은 호출자가 결정.
        private void StopAnim()
        {
            if (_seq.isAlive) _seq.Stop();
            ResetCardVisual();
            ResetChipVisual();
        }

        // BuildCanvas 이후 비-null 불변 — 널가드 없이 원복만 한다.
        private void ResetCardVisual()
        {
            _cardGroup.alpha = 1f;
            _cardRt.localScale = Vector3.one;
            _cardRt.anchoredPosition = cardCornerOffset; // 슬라이드-인 중단 대비 코너 원복
        }

        private void ResetChipVisual()
        {
            _chipGroup.alpha = 1f;
            _chipRt.localScale = Vector3.one;
        }

        // gimmick-recognition-upgrade unit 0 — 문구 4단 소비.
        // 주제목 = ruleLabel(룰), 부제 = displayName(정서 카피), 본문 = summary 한 줄 + description 상세.
        // 폴백 체인: ruleLabel → displayName → gimmickId. 아이콘 미할당이면 라벨만 뜬다.
        private void Populate(Wassup.Data.GimmickData g)
        {
            string rule = FirstNonEmpty(g.ruleLabel, g.displayName, g.gimmickId);
            // 룰 라벨이 비어 displayName 으로 폴백했으면 같은 문구를 부제로 중복 노출하지 않는다.
            string subtitle = string.IsNullOrEmpty(g.ruleLabel) ? "" : g.displayName;

            if (_titleLabel != null) _titleLabel.text = rule;
            SetLabel(_subtitleLabel, subtitle);
            SetLabel(_summaryLabel, g.summary);
            SetLabel(_bodyLabel, g.description);
            if (_chipLabel != null) _chipLabel.text = $"<color=#FFD159>특수룰</color> · {rule}";
            SetIcon(_cardIcon, g.icon);
            SetIcon(_chipIcon, g.icon);
        }

        private static string FirstNonEmpty(string a, string b, string c) =>
            !string.IsNullOrEmpty(a) ? a : (!string.IsNullOrEmpty(b) ? b : c);

        // 빈 문구는 GameObject 를 꺼서 레이아웃에서 빠지게 한다(빈 줄 간격 방지).
        private static void SetLabel(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            bool has = !string.IsNullOrEmpty(text);
            label.text = has ? text : "";
            if (label.gameObject.activeSelf != has) label.gameObject.SetActive(has);
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            bool has = sprite != null;
            if (image.gameObject.activeSelf != has) image.gameObject.SetActive(has);
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 8);

            _card = new GameObject("GimmickGuideCard",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(CanvasGroup));
            _card.transform.SetParent(roots.SafeAreaRoot, false);
            _cardRt = (RectTransform)_card.transform;
            // 우상단 도킹 — 칩과 같은 코너(pivot 1,1). 높이는 아래로 자란다.
            _cardRt.anchorMin = new Vector2(1f, 1f);
            _cardRt.anchorMax = new Vector2(1f, 1f);
            _cardRt.pivot = new Vector2(1f, 1f);
            _cardRt.anchoredPosition = cardCornerOffset;
            _cardRt.sizeDelta = new Vector2(cardWidth, 0f); // 높이는 ContentSizeFitter 가 결정
            _cardGroup = _card.GetComponent<CanvasGroup>();
            // 카드 자체는 per-Graphic raycastTarget=false 로 입력을 통과시킨다. 그룹 차원으로 끄면
            // "가즈아" 버튼까지 죽으므로 blocksRaycasts 는 기본(true) 유지.

            var bg = _card.GetComponent<Image>();
            // 시인성: 뒷 배경(맵)이 비쳐 텍스트가 안 읽히던 문제 → 거의 불투명한 다크 플레이트로.
            bg.sprite = UiRoundedSprite.Make(18f, 3f, PlateDark, AccentGold);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false; // 배치 입력 비차단

            var vlg = _card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 16, 20);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = _card.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 머리말(UI 문구 — 데이터 아님) / 아이콘 / 제목(ruleLabel) / 부제(displayName)
            // / 한 줄(summary) / 상세(description). unit 0 문구 4단.
            MakeLabel(_card.transform, "Header", "이번 판 특수 룰", 22f, AccentGold, FontStyles.Bold);
            _cardIcon = MakeIcon(_card.transform, "Icon", 96f);
            _titleLabel = MakeLabel(_card.transform, "Title", "", 40f, Color.white, FontStyles.Bold);
            _subtitleLabel = MakeLabel(_card.transform, "Subtitle", "", 22f, new Color(0.72f, 0.76f, 0.85f, 1f), FontStyles.Italic);
            _summaryLabel = MakeLabel(_card.transform, "Summary", "", 28f, AccentGold, FontStyles.Bold);
            _bodyLabel = MakeLabel(_card.transform, "Body", "", 24f, new Color(0.9f, 0.92f, 0.96f, 1f), FontStyles.Normal);
            BuildGoButton();

            BuildChip(roots.SafeAreaRoot);

            UiLayer.Apply(gameObject);
        }

        // unit 5 rev — 카드 하단 "가즈아" 확인 버튼. 탭 = 즉시 접힘(칩 전환). 버튼 배경만
        // raycast 대상이라 카드의 나머지 영역은 여전히 배치 입력을 통과시킨다.
        private void BuildGoButton()
        {
            var row = new GameObject("GoButtonRow", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(_card.transform, false);
            row.GetComponent<LayoutElement>().preferredHeight = 64f;

            var btnGo = new GameObject("GoButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(row.transform, false);
            var brt = (RectTransform)btnGo.transform;
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(220f, 56f);

            var bg = btnGo.GetComponent<Image>();
            bg.sprite = UiRoundedSprite.Make(14f, 3f, AccentGold, new Color(0.35f, 0.24f, 0.05f, 1f));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var label = MakeLabel(btnGo.transform, "Label", "가즈아", 28f, Color.white, FontStyles.Bold);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.sizeDelta = Vector2.zero;

            var button = btnGo.GetComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(CollapseIfExpanded);
        }

        // unit 5 — 접힘 상태 칩. 우상단 앵커, 내용 폭에 맞춰 왼쪽으로 자라는 한 줄 요약. 탭 = 재확장.
        private void BuildChip(Transform parent)
        {
            _chip = new GameObject("GimmickGuideChip",
                typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter),
                typeof(CanvasGroup), typeof(Button));
            _chip.transform.SetParent(parent, false);
            _chipRt = (RectTransform)_chip.transform;
            _chipRt.anchorMin = new Vector2(1f, 1f);
            _chipRt.anchorMax = new Vector2(1f, 1f);
            _chipRt.pivot = new Vector2(1f, 1f);
            _chipRt.anchoredPosition = chipCornerOffset;
            _chipGroup = _chip.GetComponent<CanvasGroup>();

            var bg = _chip.GetComponent<Image>();
            bg.sprite = UiRoundedSprite.Make(14f, 3f, PlateDark, AccentGold);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true; // 칩만 탭 대상 — 코너라 배치 입력과 겹치지 않음

            var hlg = _chip.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(22, 22, 12, 12);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var fitter = _chip.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _chipIcon = MakeIcon(_chip.transform, "Icon", 32f);
            _chipLabel = MakeLabel(_chip.transform, "Label", "", 24f, Color.white, FontStyles.Bold);

            var button = _chip.GetComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(OnChipTapped);
        }

        // unit 0 — 아이콘 슬롯. 스프라이트 미할당이면 Populate 가 GameObject 를 꺼서
        // 레이아웃에서 통째로 빠진다(에셋↔코드 디커플링, StackIconRegistry null 폴백과 같은 형태).
        private static Image MakeIcon(Transform parent, string iconName, float size)
        {
            var go = new GameObject(iconName, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            var image = go.AddComponent<Image>();
            image.preserveAspect = true; // 부모가 폭을 늘려도 원본 비율 유지
            image.raycastTarget = false;
            go.SetActive(false); // 기본 숨김 — 스프라이트가 들어올 때만 켠다
            return image;
        }

        private TextMeshProUGUI MakeLabel(Transform parent, string labelName, string text, float size, Color color, FontStyles style)
        {
            var go = new GameObject(labelName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            // 시인성: 다크 아웃라인 — 라벨 5개가 같은 값이므로 인스턴스 1개를 공유(리뷰 fix,
            // OnDestroy 에서 해제). fontMaterial(라벨당 인스턴스) 대신 fontSharedMaterial 할당.
            if (_labelOutlineMat == null && tmp.font != null && tmp.font.material != null)
            {
                _labelOutlineMat = new Material(tmp.font.material);
                _labelOutlineMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                _labelOutlineMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
                _labelOutlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.18f);
            }
            if (_labelOutlineMat != null) tmp.fontSharedMaterial = _labelOutlineMat;
            return tmp;
        }
    }
}
