using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // Bottom-left cost readout. Compact energy badge (ingame-ui-upgrade unit 2, rev
    // 2026-07-09b): a casual HUD panel with an energy bolt icon, a big current value +
    // small "/max", and a short bar gauge (one segment per integer of the pool; the
    // leading segment fills bottom→top as regen approaches the next integer). Art kit
    // (panel / bolt / bar filled+empty) is authored; each sprite has a procedural
    // fallback so the badge never breaks when a slot is empty. Hidden outside
    // Placement / Battle phases. Pure presentation — reads CostRuntime, never mutates.
    public class CostDisplay : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Header("Cost art (미할당 시 절차 폴백)")]
        [Tooltip("HUD 패널 배경")]
        [SerializeField] private Sprite costPanelSprite;
        [Tooltip("에너지 아이콘(라이트닝 볼트)")]
        [SerializeField] private Sprite costEnergyIcon;
        [Tooltip("게이지 한 칸(채움) 바")]
        [SerializeField] private Sprite costBarFilled;
        [Tooltip("게이지 한 칸(빈칸) 바")]
        [SerializeField] private Sprite costBarEmpty;
        [Tooltip("큰 숫자용 폰트(Jua/Anton 권장). 미지정 시 TMP 기본 볼드")]
        [SerializeField] private TMP_FontAsset numberFont;
        // action-tray unit 2 — 트레이 공유 geometry(레일 치수·overlap·phase 높이).
        // 미할당이면 기존 부유 배지(363×112, y164) 그대로 — 무회귀 폴백.
        [SerializeField] private BattleHudTrayConfig trayConfig;

        // Fallback palette (used only when the matching sprite is missing).
        private static readonly Color PlateColor = new(0.07f, 0.09f, 0.13f, 0.92f);
        private static readonly Color PlateBorder = new(0.28f, 0.55f, 0.42f, 0.95f);
        private static readonly Color BarFilled = new(1f, 0.82f, 0.22f, 1f);
        private static readonly Color BarEmpty = new(0.20f, 0.21f, 0.26f, 1f);
        private static readonly Color IconFallback = new(1f, 0.78f, 0.24f, 1f);
        private static readonly Color ValueColor = Color.white;

        private const float PlateW = 363f;
        private const float PlateH = 112f;

        private GameObject _panel;
        private Transform _barRow;
        private TextMeshProUGUI _valueText;
        private Image[] _bars;      // per-integer leading-fill overlays (vertical)
        private int _barCount;
        private Sprite _barEmptyFallback;
        private Sprite _barFilledFallback;
        private bool _built;
        private bool _phaseVisible;
        private bool _suppressed; // 드림캐쳐 핸드 오픈 중 true (SetSuppressed)
        // action-tray unit 4 — 비용 부족 rail pulse. 코루틴 핸들 1개로 연속 입력
        // 누적 방지(재시작 전 강제 리셋).
        private Image _plateImage;
        private Coroutine _pulse;
        private TextMeshProUGUI _pulseLabel;
        private static readonly Color PulseWarn = new(1f, 0.34f, 0.28f, 1f);

        // action-tray unit 4 — 비용 부족 슬롯 드래그 차단 피드백: 0.6초 rail pulse +
        // "코스트 N 부족" 라벨. 표시 중 재호출은 리셋 후 재시작(상태 누적 없음).
        public void PulseInsufficient(int missing)
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (_pulse != null) { StopCoroutine(_pulse); ResetPulseVisual(); }
            _pulse = StartCoroutine(PulseRoutine(Mathf.Max(1, missing)));
        }

        private System.Collections.IEnumerator PulseRoutine(int missing)
        {
            EnsurePulseLabel();
            _pulseLabel.text = $"코스트 {missing} 부족";
            _pulseLabel.gameObject.SetActive(true);
            if (_plateImage != null) _plateImage.color = new Color(1f, 0.72f, 0.68f, 1f);
            if (_valueText != null) _valueText.color = PulseWarn;
            yield return new WaitForSecondsRealtime(0.6f); // UI 는 슬로모 무관 실시간
            ResetPulseVisual();
            _pulse = null;
        }

        private void ResetPulseVisual()
        {
            if (_pulseLabel != null) _pulseLabel.gameObject.SetActive(false);
            if (_plateImage != null) _plateImage.color = Color.white;
            if (_valueText != null) _valueText.color = ValueColor;
        }

        private void EnsurePulseLabel()
        {
            if (_pulseLabel != null) return;
            var go = new GameObject("InsufficientLabel", typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);
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
            go.SetActive(false);
        }

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

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
            _phaseVisible = phase == GamePhase.Placement || phase == GamePhase.Battle;
            // action-tray unit 2 — 레일은 트레이 top edge 추종(같은 config geometry,
            // snap 전환). DefenderSelector.OnPhaseChanged 의 트레이 리사이즈와 같은
            // 프레임에 정합된다.
            if (trayConfig != null && _panel != null && (phase == GamePhase.Placement || phase == GamePhase.Battle))
                ((RectTransform)_panel.transform).anchoredPosition = new Vector2(0f, RailYFor(phase));
            RefreshVisible();
        }

        // 레일 하단 y = 트레이 y + phase 트레이 높이 − overlap (트레이 상단 겹침).
        private float RailYFor(GamePhase phase)
        {
            float trayH = phase == GamePhase.Battle ? trayConfig.battleSize.y : trayConfig.placementSize.y;
            return trayConfig.anchoredY + trayH - trayConfig.railOverlap;
        }

        // battle-hud-layout 1 rev — 중앙 이동으로 배지(y164~276)가 드림캐쳐 핸드
        // (y32~264)와 겹치게 됨. 핸드 오픈 중엔 스트립과 함께 배지도 퇴장한다
        // ("유닛 손패 + 유닛 재화" 한 세트). HandView 는 신호만 보내고 표시 결정은
        // 여기서 결합해 PhaseChanged 구독 순서 경합을 피한다.
        public void SetSuppressed(bool value)
        {
            _suppressed = value;
            RefreshVisible();
        }

        private void RefreshVisible()
        {
            if (_panel == null) return;
            bool show = _phaseVisible && !_suppressed;
            if (show) EnsureBars();
            _panel.SetActive(show);
        }

        private void EnsureBars()
        {
            if (!_built) BuildCanvas();
            if (_barRow == null) return;
            if (gameManager == null || gameManager.CostRuntime == null) return;
            int max = Mathf.RoundToInt(gameManager.CostRuntime.Max);
            if (max <= 0) return;
            if (max == _barCount && _bars != null) return;

            for (int i = _barRow.childCount - 1; i >= 0; i--)
                Destroy(_barRow.GetChild(i).gameObject);

            var emptySprite = costBarEmpty != null ? costBarEmpty : _barEmptyFallback;
            var filledSprite = costBarFilled != null ? costBarFilled : _barFilledFallback;

            _bars = new Image[max];
            for (int i = 0; i < max; i++)
            {
                var bar = new GameObject($"Bar{i}", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(_barRow, false);
                var bg = bar.GetComponent<Image>();
                bg.sprite = emptySprite;
                bg.preserveAspect = true;
                bg.raycastTarget = false;

                var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillGO.transform.SetParent(bar.transform, false);
                var frt = (RectTransform)fillGO.transform;
                frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
                frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
                var fill = fillGO.GetComponent<Image>();
                fill.sprite = filledSprite;
                fill.preserveAspect = true;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Vertical;
                fill.fillOrigin = (int)Image.OriginVertical.Bottom;
                fill.fillAmount = 0f;
                fill.raycastTarget = false;

                _bars[i] = fill;
            }
            _barCount = max;

            UiLayer.Apply(gameObject);
        }

        private void Update()
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (gameManager == null || gameManager.CostRuntime == null) return;
            var rt = gameManager.CostRuntime;
            EnsureBars();

            float current = rt.Current;
            if (_bars != null)
                for (int i = 0; i < _bars.Length; i++)
                    _bars[i].fillAmount = Mathf.Clamp01(current - i);

            if (_valueText != null)
                _valueText.text = $"{rt.CurrentInt}<size=52%>/{Mathf.RoundToInt(rt.Max)}</size>";
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 5);

            _barEmptyFallback = UiRoundedSprite.Make(4f, 0f, BarEmpty, BarEmpty);
            _barFilledFallback = UiRoundedSprite.Make(4f, 0f, BarFilled, BarFilled);

            // Compact HUD panel, bottom-center above the DefenderSelector strip.
            // battle-hud-layout 1 — 코스트-스트립 한 클러스터: "코스트 확인→슬롯
            // 선택→드래그"가 한 시선 안에 돌도록 중앙 스트립 위에 밀착(CR 엘릭서 바 관례).
            // action-tray unit 2 — trayConfig 있으면 compact rail(트레이 top 겹침),
            // 없으면 기존 부유 배지 그대로(무회귀 폴백).
            bool railMode = trayConfig != null;
            float plateW = railMode ? trayConfig.railSize.x : PlateW;
            float plateH = railMode ? trayConfig.railSize.y : PlateH;
            _panel = new GameObject("CostPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            // 레일: placement 트레이 top 겹침 / 레거시: 스트립 상단 위 12px 갭(y164).
            prt.anchoredPosition = new Vector2(0f, railMode ? RailYFor(GamePhase.Placement) : 164f);
            prt.sizeDelta = new Vector2(plateW, plateH);
            // Landscape badge with even inner padding (Pad). 9-sliced panel so the
            // rounded corners stay crisp when stretched wide. Top row = bolt + inline
            // "N/Max"; bottom row = bar gauge. Compact height, no floating whitespace.
            float Pad = railMode ? 10f : 18f;
            float TopRowH = railMode ? 26f : 46f;
            float BarRowH = railMode ? 20f : 24f;
            var plate = _panel.GetComponent<Image>();
            // 시안 정합 — 레일은 별도 캡슐이 아니라 트레이와 같은 fill/border 의
            // 탭(overlap 으로 실루엣 연결). 레거시는 기존 스프라이트/팔레트.
            plate.sprite = railMode
                ? UiRoundedSprite.Make(12f, 2f, trayConfig.fallbackFill, trayConfig.fallbackBorder)
                : (costPanelSprite != null
                    ? costPanelSprite
                    : UiRoundedSprite.Make(20f, 3f, PlateColor, PlateBorder));
            plate.type = Image.Type.Sliced;
            plate.raycastTarget = false;
            _plateImage = plate; // unit 4 — pulse 틴트 대상

            // Energy bolt — rail: 좌측 세로 중앙(한 줄 시안) / legacy: top-left.
            float iconSize = railMode ? 24f : 44f;
            var iconGO = new GameObject("EnergyIcon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(_panel.transform, false);
            var irt = (RectTransform)iconGO.transform;
            if (railMode)
            {
                irt.anchorMin = new Vector2(0f, 0.5f);
                irt.anchorMax = new Vector2(0f, 0.5f);
                irt.pivot = new Vector2(0f, 0.5f);
                irt.anchoredPosition = new Vector2(Pad + 2f, 0f);
            }
            else
            {
                irt.anchorMin = new Vector2(0f, 1f);
                irt.anchorMax = new Vector2(0f, 1f);
                irt.pivot = new Vector2(0f, 1f);
                irt.anchoredPosition = new Vector2(Pad, -(Pad + (TopRowH - iconSize) * 0.5f));
            }
            irt.sizeDelta = new Vector2(iconSize, iconSize);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = costEnergyIcon != null ? costEnergyIcon
                : UiRoundedSprite.Make(12f, 0f, IconFallback, IconFallback);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Big current value + inline "/max" — rail: 볼트 옆(한 줄) / legacy: top row.
            float valueX = Pad + iconSize + (railMode ? 8f : 14f);
            float valueW = railMode ? 92f : (plateW - valueX - Pad);
            _valueText = MakeText("Value", railMode ? 26f : 40f, ValueColor, TextAlignmentOptions.MidlineLeft);
            _valueText.richText = true;
            var vrt = _valueText.rectTransform;
            if (railMode)
            {
                vrt.anchorMin = new Vector2(0f, 0f);
                vrt.anchorMax = new Vector2(0f, 1f);
                vrt.pivot = new Vector2(0f, 0.5f);
                vrt.anchoredPosition = new Vector2(valueX, 0f);
                vrt.sizeDelta = new Vector2(valueW, 0f);
            }
            else
            {
                vrt.anchorMin = new Vector2(0f, 1f);
                vrt.anchorMax = new Vector2(0f, 1f);
                vrt.pivot = new Vector2(0f, 1f);
                vrt.anchoredPosition = new Vector2(valueX, -Pad);
                vrt.sizeDelta = new Vector2(valueW, TopRowH);
            }
            _valueText.fontStyle = FontStyles.Bold;
            _valueText.text = "0";

            // Bar gauge — rail: 숫자 오른쪽 세로 중앙(한 줄 시안) / legacy: 하단 행.
            var rowGO = new GameObject("BarRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGO.transform.SetParent(_panel.transform, false);
            var rrt = (RectTransform)rowGO.transform;
            if (railMode)
            {
                float barX = valueX + valueW + 6f;
                float inset = (plateH - BarRowH) * 0.5f;
                rrt.anchorMin = new Vector2(0f, 0f);
                rrt.anchorMax = new Vector2(1f, 1f);
                rrt.pivot = new Vector2(0.5f, 0.5f);
                rrt.offsetMin = new Vector2(barX, inset);
                rrt.offsetMax = new Vector2(-(Pad + 4f), -inset);
            }
            else
            {
                rrt.anchorMin = new Vector2(0f, 0f);
                rrt.anchorMax = new Vector2(1f, 0f);
                rrt.pivot = new Vector2(0.5f, 0f);
                rrt.offsetMin = new Vector2(Pad, Pad);
                rrt.offsetMax = new Vector2(-Pad, Pad + BarRowH);
            }
            var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            _barRow = rowGO.transform;

            UiLayer.Apply(gameObject);
        }

        private TextMeshProUGUI MakeText(string name, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) tmp.font = numberFont;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
