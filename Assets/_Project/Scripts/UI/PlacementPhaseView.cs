using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // Phase 6 placement countdown overlay. Sits between Draft confirm and
    // battle start: grants the starting cost, shows a countdown, and lets the
    // player either wait for the timer to elapse or tap START BATTLE to begin.
    public class PlacementPhaseView : MonoBehaviour
    {
        [SerializeField] private BattleBridge bridge;
        // gift-phase (review m2) — draftController 구독은 GiftPhaseView 로 이관되어 미사용,
        // 필드 제거.
        [SerializeField] private GameManager gameManager;

        // ingame-ui-upgrade unit 0 — START 버튼을 우하단(dock 코너)으로 이동 + 캐주얼
        // 배경 그래픽 슬롯. startButtonBackground 할당 시 그 스프라이트, 비면 UiRoundedSprite
        // 절차 플레이트(다크 네이비 + 골드 테두리) 폴백. 실제 그래픽은 unit 1(Codex).
        [Header("Start button (casual bg image + TMP label)")]
        [Tooltip("Codex 생성 캐주얼 버튼 배경. 미할당 시 절차 플레이트 폴백")]
        [SerializeField] private Sprite startButtonBackground;
        // 코너에서도 눈에 띄도록 밝은 앰버 바디 + 밝은 골드 림. 폴백 플레이트 톤.
        [SerializeField] private Color startFillColor = new Color(0.96f, 0.58f, 0.14f, 1f);
        [SerializeField] private Color startBorderColor = new Color(1f, 0.9f, 0.55f, 1f);
        [SerializeField] private float startBorderWidth = 5f;
        [SerializeField] private float startCornerRadius = 30f;
        [Tooltip("라벨색(캐주얼 버튼: 크림/화이트 + 아래 외곽선)")]
        [SerializeField] private Color startLabelColor = new Color(1f, 0.97f, 0.88f, 1f);
        [Tooltip("게임용 디스플레이 폰트(Bangers SDF 권장). 미지정 시 TMP 기본")]
        [SerializeField] private TMP_FontAsset startLabelFont;
        [Tooltip("라벨 외곽선 색(스티커 느낌)")]
        [SerializeField] private Color startLabelOutlineColor = new Color(0.35f, 0.13f, 0.02f, 1f);
        [Range(0f, 0.5f)]
        [SerializeField] private float startLabelOutlineWidth = 0.22f;
        [Header("Start button juice (코너에서 잘 보이도록)")]
        [Tooltip("버튼 뒤 골드 오라 색(펄스로 시선 유도)")]
        [SerializeField] private Color startAuraColor = new Color(1f, 0.72f, 0.25f, 0.5f);
        [Tooltip("아이들 브리딩 펄스 배수(1=없음)")]
        [SerializeField] private float startPulseScale = 1.06f;
        [SerializeField] private float startPulsePeriod = 0.9f;

        private GameObject _panel;
        private TextMeshProUGUI _countdownLabel;
        private Button _startButton;
        private RectTransform _startButtonRt;
        private RectTransform _startAuraRt;
        private Tween _startPulse;
        private Tween _startAuraPulse;
        private float _remaining;
        private bool _active;
        private bool _built;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        // gift-phase unit 3 — 진입 신호(DraftConfirmed/PlacementRequested)는 이제
        // GiftPhaseView 가 받아 선물 페이즈를 거친 뒤 BeginPlacementPhase() 를 직접 호출한다.
        // 여기서 직접 구독하면 선물을 건너뛰고 이중 진입하므로 구독하지 않는다.
        private void OnDisable()
        {
            SetStartJuice(false);
        }

        public void BeginPlacementPhase()
        {
            if (!_built) BuildCanvas();
            var cfg = gameManager != null ? gameManager.CostConfig : null;
            float duration = cfg != null ? cfg.placementPhaseDuration : 30f;

            if (gameManager != null) gameManager.SetPhase(GamePhase.Placement);
            if (gameManager != null && gameManager.CostRuntime != null) gameManager.CostRuntime.ResetToStart();
            if (bridge != null) bridge.BeginPlacement();

            _remaining = duration;
            _active = true;
            _panel.SetActive(true);
            SetStartJuice(true);
        }

        private void Update()
        {
            if (!_active) return;
            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) { _remaining = 0f; FinishPlacement(); return; }
            _countdownLabel.text = $"배치 단계  ·  {Mathf.CeilToInt(_remaining)}초";
        }

        private void OnStartClicked() => FinishPlacement();

        private void FinishPlacement()
        {
            _active = false;
            SetStartJuice(false);
            _panel.SetActive(false);
            if (gameManager != null) gameManager.SetPhase(GamePhase.Battle);
            if (gameManager != null && gameManager.CostRuntime != null) gameManager.CostRuntime.BeginRegen();
            if (bridge != null) bridge.StartBattle();
        }

        // Idle "look here" juice: breathing button scale + a larger, faster gold aura
        // pulse. useUnscaledTime so it keeps living through any timeScale changes.
        private void SetStartJuice(bool on)
        {
            if (_startPulse.isAlive) _startPulse.Stop();
            if (_startAuraPulse.isAlive) _startAuraPulse.Stop();
            if (_startButtonRt != null) _startButtonRt.localScale = Vector3.one;
            if (_startAuraRt != null) _startAuraRt.localScale = Vector3.one;
            if (!on) return;

            if (_startButtonRt != null)
                _startPulse = Tween.Scale(_startButtonRt, Vector3.one, Vector3.one * startPulseScale,
                    startPulsePeriod, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);

            if (_startAuraRt != null)
            {
                // Aura breathes with roughly double the button's amplitude for a soft halo.
                float auraScale = 1f + (startPulseScale - 1f) * 2f;
                _startAuraPulse = Tween.Scale(_startAuraRt, Vector3.one, Vector3.one * auraScale,
                    startPulsePeriod, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo, useUnscaledTime: true);
            }
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);

            _panel = new GameObject("PlacementPanel", typeof(RectTransform));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;

            // Top-center countdown banner
            var banner = new GameObject("Banner", typeof(RectTransform), typeof(Image));
            banner.transform.SetParent(_panel.transform, false);
            var brt = (RectTransform)banner.transform;
            brt.anchorMin = new Vector2(0.5f, 1f);
            brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, -90f);
            brt.sizeDelta = new Vector2(560f, 72f);
            banner.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(banner.transform, false);
            var lrt = (RectTransform)labelGO.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            _countdownLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _countdownLabel.text = "배치 단계";
            _countdownLabel.fontSize = 36;
            _countdownLabel.color = Color.yellow;
            _countdownLabel.alignment = TextAlignmentOptions.Center;

            // Bottom-right START BATTLE button — sits in the same corner the battle-phase
            // NextWaveDock later occupies (placement/battle are exclusive phases, so no
            // visual clash). A pulsing gold aura + idle breathing pull the eye to the
            // corner; background is the casual graphic if assigned, else a procedural
            // amber+gold plate fallback.
            const float btnW = 280f, btnH = 104f;

            // Corner wrapper: places the whole widget at the dock corner. Aura + button
            // are centered siblings under it so each animates its own scale independently.
            var wrapGO = new GameObject("StartButtonWrap", typeof(RectTransform));
            wrapGO.transform.SetParent(_panel.transform, false);
            var wrapRt = (RectTransform)wrapGO.transform;
            wrapRt.anchorMin = new Vector2(1f, 0f);
            wrapRt.anchorMax = new Vector2(1f, 0f);
            wrapRt.pivot = new Vector2(1f, 0f);
            wrapRt.anchoredPosition = new Vector2(-40f, 40f); // aligns with NextWaveDock corner
            wrapRt.sizeDelta = new Vector2(btnW, btnH);

            // Pulsing gold aura behind the button (centered, larger; scale/alpha animate).
            var auraGO = new GameObject("Aura", typeof(RectTransform), typeof(Image));
            auraGO.transform.SetParent(wrapGO.transform, false);
            _startAuraRt = (RectTransform)auraGO.transform;
            _startAuraRt.anchorMin = new Vector2(0.5f, 0.5f);
            _startAuraRt.anchorMax = new Vector2(0.5f, 0.5f);
            _startAuraRt.pivot = new Vector2(0.5f, 0.5f);
            _startAuraRt.sizeDelta = new Vector2(btnW + 44f, btnH + 44f);
            var auraImg = auraGO.GetComponent<Image>();
            auraImg.sprite = UiRoundedSprite.Make(startCornerRadius + 12f, 0f, startAuraColor, startAuraColor);
            auraImg.type = Image.Type.Sliced;
            auraImg.raycastTarget = false;

            // Button (centered). Color lives in the sprite (white Image tint) so the
            // Button's default ColorTint still multiplies white for hover/press feedback.
            var btnGO = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(wrapGO.transform, false);
            _startButtonRt = (RectTransform)btnGO.transform;
            _startButtonRt.anchorMin = new Vector2(0.5f, 0.5f);
            _startButtonRt.anchorMax = new Vector2(0.5f, 0.5f);
            _startButtonRt.pivot = new Vector2(0.5f, 0.5f);
            _startButtonRt.sizeDelta = new Vector2(btnW, btnH);
            var btnImg = btnGO.GetComponent<Image>();
            if (startButtonBackground != null)
            {
                // Ornate fixed-aspect graphic — stretch as-is (9-slice would warp the
                // decorated corners). Button rect matches its ~2.6:1 aspect.
                btnImg.sprite = startButtonBackground;
                btnImg.type = Image.Type.Simple;
            }
            else
            {
                btnImg.sprite = UiRoundedSprite.Make(startCornerRadius, startBorderWidth, startFillColor, startBorderColor);
                btnImg.type = Image.Type.Sliced;
            }
            _startButton = btnGO.GetComponent<Button>();
            _startButton.onClick.AddListener(OnStartClicked);

            var btnLabelGO = new GameObject("Label", typeof(RectTransform));
            btnLabelGO.transform.SetParent(btnGO.transform, false);
            var blrt = (RectTransform)btnLabelGO.transform;
            blrt.anchorMin = Vector2.zero; blrt.anchorMax = Vector2.one;
            blrt.offsetMin = Vector2.zero; blrt.offsetMax = Vector2.zero;
            var bl = btnLabelGO.AddComponent<TextMeshProUGUI>();
            if (startLabelFont != null) bl.font = startLabelFont;
            bl.text = "전투 시작";
            bl.color = startLabelColor;
            bl.alignment = TextAlignmentOptions.Center;
            bl.textWrappingMode = TextWrappingModes.NoWrap;
            bl.enableAutoSizing = true;
            bl.fontSizeMin = 22f;
            bl.fontSizeMax = 46f;
            bl.characterSpacing = 2f;
            bl.raycastTarget = false;
            // Sticker outline for that casual-game punch (instanced material, shared
            // atlas untouched). Skipped if the font has no SDF material.
            if (startLabelOutlineWidth > 0f && bl.fontMaterial != null)
            {
                var mat = bl.fontMaterial; // instance
                mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                mat.SetColor(ShaderUtilities.ID_OutlineColor, startLabelOutlineColor);
                mat.SetFloat(ShaderUtilities.ID_OutlineWidth, startLabelOutlineWidth);
            }

            UiLayer.Apply(gameObject);
        }
    }
}
