using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // tray-cost-well unit 1 — 트레이 좌측 코스트 셀.
    //
    // 이전(action-tray unit 2)에는 트레이 상단에 겹쳐 얹힌 별도 캔버스 레일이었고
    // 정수마다 1칸인 세그먼트 바를 그렸다. 지금은 트레이 패널의 첫 자식으로 들어가
    // 상단 숫자 + 하단 물통 2단을 그린다. 물통은 CostRuntime.Current 의 소수부
    // (= 다음 1코스트까지의 진행률)이고, 산식은 CostWellMath 가 소유한다.
    //
    // 이 unit 은 구조/배치/값 추종까지다. 충전 연출(wrap 팝·max 버스트·소비
    // 플래시·획득 플로팅)과 리젠 정지 표현은 unit 2.
    //
    // 순수 프레젠테이션 — CostRuntime 을 읽기만 하고 절대 쓰지 않는다.
    public class CostDisplay : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Header("Cost art (미할당 시 절차 폴백)")]
        [Tooltip("에너지 아이콘(라이트닝 볼트). HUD 전체에서 유일한 에너지 기호 — unit 3 이 슬롯 볼트를 지운다")]
        [SerializeField] private Sprite costEnergyIcon;
        [Tooltip("큰 숫자용 폰트(Jua/Anton 권장). 미지정 시 TMP 기본 볼드")]
        [SerializeField] private TMP_FontAsset numberFont;
        [SerializeField] private BattleHudTrayConfig trayConfig;

        // 폴백 팔레트 (config 미할당 시에만 쓰인다)
        private static readonly Color WellBackFallback = new(0.04f, 0.06f, 0.11f, 0.92f);
        private static readonly Color WellLiquidFallback = new(0.95f, 0.62f, 0.15f, 1f);
        private static readonly Color WellSurfaceFallback = new(1f, 0.95f, 0.7f, 0.85f);
        private static readonly Color ValueColor = Color.white;
        private static readonly Color PulseWarn = new(1f, 0.34f, 0.28f, 1f);

        private Transform _trayPanel;
        private GameObject _cell;
        private CanvasGroup _cellGroup;
        private LayoutElement _cellLayout;
        private Image _cellBg;
        private TextMeshProUGUI _valueText;
        private RectTransform _wellRect;
        private Image _wellLiquid;
        private RectTransform _surfaceRect;
        private Image _surfaceImage;
        private TextMeshProUGUI _pulseLabel;
        private Coroutine _pulse;

        private bool _built;
        private bool _phaseVisible;
        private bool _suppressed;

        // ── 트레이 부착 ──────────────────────────────────────────────

        // DefenderSelector.BuildCanvas 말미에서 호출된다. 두 컴포넌트 모두 Awake 에서
        // 캔버스를 짓기 때문에 실행 순서가 보장되지 않는다 — 그래서 여기서 짓는다.
        public void AttachToTray(Transform trayPanel)
        {
            if (trayPanel == null) return;
            _trayPanel = trayPanel;
            BuildCell();
            RefreshVisible();
        }

        // 트레이 폭이 화면비에 따라 클램프되므로 셀 폭도 소유자(DefenderSelector)가 정한다.
        // 계약: 셀 폭 <= 슬롯 폭 (자원 표시가 행동 대상보다 넓으면 위계가 뒤집힌다).
        public void SetCellWidth(float width)
        {
            if (_cellLayout == null) return;
            _cellLayout.preferredWidth = width;
            _cellLayout.minWidth = width;
        }

        // ── 표시 상태 ────────────────────────────────────────────────

        private void OnEnable()
        {
            if (gameManager != null) gameManager.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            // 위치/크기 계산 없음 — 레이아웃 그룹과 DefenderSelector 가 소유한다.
            _phaseVisible = phase == GamePhase.Placement || phase == GamePhase.Battle;
            RefreshVisible();
        }

        // battle-hud-layout 1 rev — 드림캐쳐 핸드 오픈 중엔 "유닛 손패 + 유닛 재화"가
        // 한 세트로 퇴장한다. 셀이 트레이 자식이 된 뒤로는 트레이 플립이 가림을
        // 소유하므로 이 경로는 사실상 중복이지만, 호출측(DreamcatcherHandView) 무변경을
        // 위해 시그니처를 유지한다. alpha 로만 처리해 레이아웃은 건드리지 않는다.
        public void SetSuppressed(bool value)
        {
            _suppressed = value;
            RefreshVisible();
        }

        // SetActive 를 쓰지 않는 이유: 셀이 레이아웃 자식이라 비활성화하면 슬롯들이
        // 남은 폭 전체로 재확장된다(154 → 176). 손패를 열 때마다 슬롯이 튀는
        // 리플로우가 생긴다. alpha 로 숨기면 레이아웃이 유지되고, 부수적으로
        // Update 가 계속 돌아 억제 구간에도 값이 최신으로 유지된다.
        private void RefreshVisible()
        {
            if (_cellGroup == null) return;
            bool show = _phaseVisible && !_suppressed;
            _cellGroup.alpha = show ? 1f : 0f;
            _cellGroup.blocksRaycasts = show;
        }

        // ── 값 추종 ──────────────────────────────────────────────────

        private void Update()
        {
            // activeSelf 가 아니라 activeInHierarchy — 셀이 트레이 자식이라
            // 부모만 꺼진 상태를 activeSelf 는 못 본다.
            if (_cell == null || !_cell.activeInHierarchy) return;
            var runtime = gameManager != null ? gameManager.CostRuntime : null;
            if (runtime == null) return;

            float fill = CostWellMath.WellFill(runtime.Current, runtime.Max);
            if (_wellLiquid != null)
            {
                _wellLiquid.fillAmount = fill;
                _wellLiquid.color = LiquidColorFor(fill);
            }
            ApplySurface(fill);

            if (_valueText != null)
            {
                int shown = CostWellMath.DisplayInt(runtime.Current);
                _valueText.text = $"{shown}<size=52%>/{Mathf.RoundToInt(runtime.Max)}</size>";
            }
        }

        private Color LiquidColorFor(float fill)
        {
            var baseColor = trayConfig != null ? trayConfig.wellLiquidColor : WellLiquidFallback;
            var fullColor = trayConfig != null ? trayConfig.wellLiquidFullColor : WellLiquidFallback;
            return Color.Lerp(baseColor, fullColor, fill);
        }

        // 액체 표면은 fill 을 따라 오르내리고, 바닥/천장에 붙으면 숨긴다
        // (각성 게이지 LiquidSurface 와 같은 규칙).
        private void ApplySurface(float fill)
        {
            if (_surfaceRect == null || _wellRect == null) return;
            bool visible = fill > 0.01f && fill < 0.99f;
            if (_surfaceImage != null && _surfaceImage.enabled != visible)
                _surfaceImage.enabled = visible;
            if (!visible) return;

            float h = _wellRect.rect.height;
            _surfaceRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(-h * 0.5f, h * 0.5f, fill));
        }

        // ── 비용 부족 피드백 (action-tray unit 4) ────────────────────

        public void PulseInsufficient(int missing)
        {
            if (_cell == null || !_cell.activeInHierarchy) return;
            if (_pulse != null) { StopCoroutine(_pulse); ResetPulseVisual(); }
            _pulse = StartCoroutine(PulseRoutine(Mathf.Max(1, missing)));
        }

        private System.Collections.IEnumerator PulseRoutine(int missing)
        {
            EnsurePulseLabel();
            _pulseLabel.text = $"코스트 {missing} 부족";
            _pulseLabel.gameObject.SetActive(true);
            if (_cellBg != null) _cellBg.color = new Color(1f, 0.72f, 0.68f, 0.55f);
            if (_valueText != null) _valueText.color = PulseWarn;
            yield return new WaitForSecondsRealtime(0.6f); // UI 는 슬로모 무관 실시간
            ResetPulseVisual();
            _pulse = null;
        }

        // 셀 배경 base 는 투명이다. 색을 스프라이트에 굽고 Image.color 를 흰색으로
        // 되돌리는 예전 방식이면 pulse 1회 후 base 틴트가 파괴된다.
        private void ResetPulseVisual()
        {
            if (_pulseLabel != null) _pulseLabel.gameObject.SetActive(false);
            if (_cellBg != null) _cellBg.color = Color.clear;
            if (_valueText != null) _valueText.color = ValueColor;
        }

        // 라벨은 셀이 아니라 트레이 패널 기준으로 붙인다 — 260px 라벨을 154px 셀에
        // 붙이면 좌우로 삐져나와 인접 슬롯을 덮는다.
        private void EnsurePulseLabel()
        {
            if (_pulseLabel != null) return;
            var parent = _trayPanel != null ? _trayPanel : _cell.transform;
            var go = new GameObject("InsufficientLabel", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 6f);
            rt.sizeDelta = new Vector2(260f, 34f);
            _pulseLabel = go.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _pulseLabel.font = numberFont;
            _pulseLabel.fontSize = 24f;
            _pulseLabel.fontStyle = FontStyles.Bold;
            _pulseLabel.color = PulseWarn;
            _pulseLabel.alignment = TextAlignmentOptions.Center;
            _pulseLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _pulseLabel.raycastTarget = false;
            // 라벨은 레이아웃 그룹 자식이 아니어야 한다(트레이 폭 배분에 끼어들면 안 됨).
            var ignore = go.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;
            go.SetActive(false);
        }

        // ── 셀 구성 ──────────────────────────────────────────────────

        private void BuildCell()
        {
            if (_built) return;
            _built = true;

            float cellW = trayConfig != null ? trayConfig.costCellWidth : 154f;
            float numberH = trayConfig != null ? trayConfig.cellNumberHeight : 48f;
            float rowGap = trayConfig != null ? trayConfig.cellRowGap : 4f;
            Vector2 wellPad = trayConfig != null ? trayConfig.wellPadding : new Vector2(8f, 6f);
            float numberSize = trayConfig != null ? trayConfig.cellNumberFontSize : 52f;

            _cell = new GameObject("CostCell", typeof(RectTransform), typeof(Image),
                typeof(CanvasGroup), typeof(LayoutElement));
            _cell.transform.SetParent(_trayPanel, false);
            _cell.transform.SetSiblingIndex(0); // 트레이 첫 자식 = 좌측

            _cellBg = _cell.GetComponent<Image>();
            _cellBg.sprite = UiRoundedSprite.Make(10f, 0f, Color.white, Color.clear);
            _cellBg.type = Image.Type.Sliced;
            _cellBg.color = Color.clear; // pulse 대상. base 가 투명이라 복원이 자명하다.
            _cellBg.raycastTarget = false;

            _cellGroup = _cell.GetComponent<CanvasGroup>();
            _cellGroup.alpha = 0f;

            _cellLayout = _cell.GetComponent<LayoutElement>();
            _cellLayout.preferredWidth = cellW;
            _cellLayout.minWidth = cellW;
            _cellLayout.flexibleWidth = 0f; // 고정폭. 바깥 HLG 는 childForceExpandWidth=false 여야 유효.

            BuildValueRow(numberH, numberSize);
            BuildWell(numberH, rowGap, wellPad);

            UiLayer.Apply(_cell);
        }

        // 상단 행: ⚡ 아이콘 + "현재/최대".
        private void BuildValueRow(float numberH, float numberSize)
        {
            float iconSize = Mathf.Min(28f, numberH - 12f);

            var iconGO = new GameObject("EnergyIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(_cell.transform, false);
            var irt = (RectTransform)iconGO.transform;
            irt.anchorMin = new Vector2(0f, 1f);
            irt.anchorMax = new Vector2(0f, 1f);
            irt.pivot = new Vector2(0f, 1f);
            irt.anchoredPosition = new Vector2(8f, -(numberH - iconSize) * 0.5f);
            irt.sizeDelta = new Vector2(iconSize, iconSize);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = costEnergyIcon != null
                ? costEnergyIcon
                : UiRoundedSprite.Make(6f, 0f, WellLiquidFallback, WellLiquidFallback);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var valueGO = new GameObject("Value", typeof(RectTransform));
            valueGO.transform.SetParent(_cell.transform, false);
            var vrt = (RectTransform)valueGO.transform;
            vrt.anchorMin = new Vector2(0f, 1f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.pivot = new Vector2(0.5f, 1f);
            vrt.offsetMin = new Vector2(8f + iconSize + 4f, -numberH);
            vrt.offsetMax = new Vector2(-6f, 0f);
            _valueText = valueGO.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _valueText.font = numberFont;
            _valueText.richText = true;
            _valueText.text = "0";
            _valueText.fontSize = numberSize;
            _valueText.enableAutoSizing = true;
            _valueText.fontSizeMin = numberSize * 0.6f;
            _valueText.fontSizeMax = numberSize;
            _valueText.fontStyle = FontStyles.Bold;
            _valueText.color = ValueColor;
            _valueText.alignment = TextAlignmentOptions.MidlineLeft;
            _valueText.textWrappingMode = TextWrappingModes.NoWrap;
            _valueText.raycastTarget = false;
        }

        // 하단: Mask 용기 + 아래→위로 차오르는 액체 + 표면 하이라이트.
        // 각성 게이지(AwakeningGaugeView)의 ChargeWell 구성을 세로 직사각으로 옮긴 것.
        private void BuildWell(float numberH, float rowGap, Vector2 wellPad)
        {
            var wellGO = new GameObject("Well", typeof(RectTransform), typeof(Image), typeof(Mask));
            wellGO.transform.SetParent(_cell.transform, false);
            _wellRect = (RectTransform)wellGO.transform;
            _wellRect.anchorMin = new Vector2(0f, 0f);
            _wellRect.anchorMax = new Vector2(1f, 1f);
            _wellRect.pivot = new Vector2(0.5f, 0.5f);
            _wellRect.offsetMin = new Vector2(wellPad.x, wellPad.y);
            _wellRect.offsetMax = new Vector2(-wellPad.x, -(numberH + rowGap));

            var wellBack = wellGO.GetComponent<Image>();
            wellBack.sprite = UiRoundedSprite.Make(8f, 0f, Color.white, Color.clear);
            wellBack.type = Image.Type.Sliced;
            wellBack.color = trayConfig != null ? trayConfig.wellBackColor : WellBackFallback;
            wellBack.raycastTarget = false;
            wellGO.GetComponent<Mask>().showMaskGraphic = true;

            var liquidGO = new GameObject("WellLiquid", typeof(RectTransform), typeof(Image));
            liquidGO.transform.SetParent(wellGO.transform, false);
            var lrt = (RectTransform)liquidGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _wellLiquid = liquidGO.GetComponent<Image>();
            _wellLiquid.sprite = UiRoundedSprite.Make(8f, 0f, Color.white, Color.clear);
            _wellLiquid.type = Image.Type.Filled;
            _wellLiquid.fillMethod = Image.FillMethod.Vertical;
            _wellLiquid.fillOrigin = (int)Image.OriginVertical.Bottom;
            _wellLiquid.fillAmount = 0f;
            _wellLiquid.color = trayConfig != null ? trayConfig.wellLiquidColor : WellLiquidFallback;
            _wellLiquid.raycastTarget = false;

            var surfaceGO = new GameObject("WellSurface", typeof(RectTransform), typeof(Image));
            surfaceGO.transform.SetParent(wellGO.transform, false);
            _surfaceRect = (RectTransform)surfaceGO.transform;
            _surfaceRect.anchorMin = new Vector2(0f, 0.5f);
            _surfaceRect.anchorMax = new Vector2(1f, 0.5f);
            _surfaceRect.pivot = new Vector2(0.5f, 0.5f);
            _surfaceRect.sizeDelta = new Vector2(0f, 4f);
            _surfaceRect.anchoredPosition = Vector2.zero;
            _surfaceImage = surfaceGO.GetComponent<Image>();
            _surfaceImage.sprite = UiRoundedSprite.Make(2f, 0f, Color.white, Color.clear);
            _surfaceImage.type = Image.Type.Sliced;
            _surfaceImage.color = trayConfig != null ? trayConfig.wellSurfaceColor : WellSurfaceFallback;
            _surfaceImage.raycastTarget = false;
            _surfaceImage.enabled = false;
        }
    }
}
