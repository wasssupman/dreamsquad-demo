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
        private Material _liquidMaterial;
        private bool _geometryPushed;
        // defender-placement-cooldown 2 (사용자 결정 2026-07-22) — 쿨타임 셀에서 이관한 액체 juice.
        // 기포(수면까지 떠오름) + 테두리 림 글로우(호흡). 시간 기반 stateless 구동(Update). 색은
        // trayConfig.wellBubbleColor/wellRimColor. 플레인 UI 이미지라 별도 머티리얼 정리 불필요.
        private Image[] _wellBubbles;
        private Image _wellRim;

        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int LiquidBottomId = Shader.PropertyToID("_LiquidBottom");
        private static readonly int LiquidTopId = Shader.PropertyToID("_LiquidTop");
        private static readonly int SurfaceId = Shader.PropertyToID("_SurfaceColor");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");

        // 용기 배경·테두리·액체 클립이 공유하는 코너 반경(px). 세 곳이 어긋나면
        // 모서리에 어두운 틈이 생긴다.
        private const float WellCornerRadius = 8f;
        private TextMeshProUGUI _pulseLabel;
        private Coroutine _pulse;

        private bool _built;
        private bool _phaseVisible;
        private bool _suppressed;

        // unit 2 — 폴링 판정 상태. _prevInt = -1 은 sentinel(재동기화)이다.
        // 0 으로 두면 안 된다: startingCost == maxCost == 10 이라 매치가 늘
        // Current == Max 에서 시작하고, 첫 프레임 delta = +10 + 상한 도달로
        // MaxBurst 가 배치 진입마다 오발한다.
        private int _prevInt = -1;
        private float _prevFill;
        private TextMeshProUGUI _gainLabel;
        private Coroutine _wellTick;
        private Coroutine _maxBurst;
        private Coroutine _valuePunch;
        private Coroutine _spendFlash;
        private Coroutine _gain;

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
            PushWellGeometry(); // 폭이 바뀌면 라운드 코너 종횡비도 다시 잡아야 한다
        }

        // 셰이더가 라운드 코너를 그리므로 종횡비를 알아야 한다 — 안 넘기면 코너가
        // 타원으로 찌그러진다. 레이아웃이 아직 안 잡혔으면 다음 호출로 미룬다.
        private void PushWellGeometry()
        {
            if (_liquidMaterial == null || _wellRect == null) return;
            float w = _wellRect.rect.width;
            float h = _wellRect.rect.height;
            if (w <= 1f || h <= 1f) return;
            _liquidMaterial.SetFloat(AspectId, w / h);
            // 액체의 라운드 반경을 용기 배경/테두리와 정확히 일치시킨다. 어긋나면
            // 그 차이만큼 어두운 배경이 모서리에 드러난다(액체가 덜 채워진 것처럼 보임).
            // 셰이더의 _Radius 는 높이 기준 정규화값이라 픽셀 반경을 높이로 나눈다.
            _liquidMaterial.SetFloat(RadiusId, WellCornerRadius / h);
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

        // 런타임 머티리얼 인스턴스는 직접 정리한다(프로젝트 관례 —
        // DepthParallaxView / DraftCardVfxDriver 와 같은 패턴).
        private void OnDestroy()
        {
            if (_liquidMaterial == null) return;
            if (Application.isPlaying) Destroy(_liquidMaterial);
            else DestroyImmediate(_liquidMaterial);
            _liquidMaterial = null;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            // 위치/크기 계산 없음 — 레이아웃 그룹과 DefenderSelector 가 소유한다.
            _phaseVisible = phase == GamePhase.Placement || phase == GamePhase.Battle;
            // 페이즈 전환은 ResetToStart 를 동반한다(배치 진입마다 10/10 으로 점프).
            // 재동기화하지 않으면 그 점프가 MaxBurst 로 오발한다.
            _prevInt = -1;
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
            if (_cell == null || !_cell.activeInHierarchy)
            {
                // 트레이가 통째로 꺼진 구간(플립·Draft·Result)에는 폴링이 멈춘다.
                // 그 사이 코스트는 계속 변하므로 다음 노출은 재동기화로 시작한다
                // — 안 그러면 손패를 닫는 순간 자연 충전이 "+N 획득"으로 오발한다.
                _prevInt = -1;
                return;
            }
            var runtime = gameManager != null ? gameManager.CostRuntime : null;
            if (runtime == null) return;

            int curInt = CostWellMath.DisplayInt(runtime.Current);
            float fill = CostWellMath.WellFill(runtime.Current, runtime.Max);
            bool atMax = runtime.Current >= runtime.Max;

            ApplyFill(fill);
            ApplyValue(curInt, runtime.Max);
            if (!_geometryPushed) { _geometryPushed = true; PushWellGeometry(); }
            UpdateWellFlair(fill); // 기포 + 림 글로우(시간 기반 stateless)

            // sentinel — 첫 프레임/재동기화는 값만 흡수하고 연출 없이 지나간다.
            if (_prevInt < 0)
            {
                _prevInt = curInt;
                _prevFill = fill;
                return;
            }

            int delta = curInt - _prevInt;
            if (delta > 0)
            {
                // 위에서 아래로 첫 매치에서 멈춘다. max 에 닿는 프레임은 정수 증가 +
                // 물통 넘침 + 상한 도달이 동시에 참이라, max 가 맨 위에 있어야
                // 연출 3개가 겹쳐 터지지 않는다.
                if (atMax)
                {
                    PlayMaxBurst();
                }
                else if (delta == 1 && fill < _prevFill - CostWellMath.FillEpsilon)
                {
                    // 소수부가 되감겼다 = 자연 충전으로 물통이 넘쳤다.
                    // AddCost 는 정수만 더해 소수부를 보존하므로 이 조건에 안 걸린다
                    // (epsilon 이 없으면 float32 1 ULP 드리프트로 약 10% 가 오분류).
                    PlayWellTick();
                    PlayValuePunch();
                }
                else
                {
                    PlayGainFloat(delta);
                    PlayValuePunch();
                }
            }
            else if (delta < 0)
            {
                PlaySpendFlash();
            }

            _prevInt = curInt;
            _prevFill = fill;
        }

        private void ApplyValue(int shown, float max)
        {
            if (_valueText == null) return;
            _valueText.text = $"{shown}<size=58%>/{Mathf.RoundToInt(max)}</size>";
        }

        // 리젠 정지(배치 페이즈) 표현은 두지 않는다 — 물통이 그냥 비어 있으면 된다
        // (사용자 결정 2026-07-21). 리뷰는 "30초간 멈춘 위젯이 고장으로 읽힌다"며
        // dim + 글리프를 요구했으나, 실제로는 배치 단계 화면이 더 지저분해졌다.

        // 셰이더 모드면 수위를 uniform 으로 넘긴다 — 표면 파형·깊이 음영·수면
        // 하이라이트를 프래그먼트가 만든다(별도 Surface Image 불필요).
        // 폴백(머티리얼 미할당)이면 예전처럼 Image.Type.Filled 로 자른다.
        private void ApplyFill(float fill)
        {
            if (_liquidMaterial != null)
            {
                _liquidMaterial.SetFloat(FillId, fill);
                return;
            }
            if (_wellLiquid == null) return;
            _wellLiquid.fillAmount = fill;
            var baseColor = trayConfig != null ? trayConfig.wellLiquidColor : WellLiquidFallback;
            var fullColor = trayConfig != null ? trayConfig.wellLiquidFullColor : WellLiquidFallback;
            _wellLiquid.color = Color.Lerp(baseColor, fullColor, fill);
        }

        // defender-placement-cooldown 2 — 물통 기포 + 테두리 림 글로우. 상태 저장 없이
        // Time.unscaledTime 으로 계산(위치/알파만). 기포는 현재 수위(fill) 아래에서만 떠오른다.
        private void UpdateWellFlair(float fill)
        {
            float time = Time.unscaledTime;

            if (_wellBubbles != null && _wellRect != null)
            {
                float w = _wellRect.rect.width, h = _wellRect.rect.height;
                float surfaceY = Mathf.Max(0f, fill * h);
                float baseA = trayConfig != null ? trayConfig.wellBubbleColor.a : 0.4f;
                for (int b = 0; b < _wellBubbles.Length; b++)
                {
                    var img = _wellBubbles[b];
                    if (img == null) continue;
                    float phase = Mathf.Repeat(time * (0.28f + 0.07f * b) + b * 0.5f, 1f); // 0→1 상승 루프
                    float y = phase * surfaceY;                 // 바닥→수면까지만
                    float x = w * 0.5f + Mathf.Sin(time * 1.6f + b * 2.3f) * (w * 0.16f);
                    img.rectTransform.anchoredPosition = new Vector2(x, y);
                    float a = Mathf.Sin(phase * Mathf.PI) * (surfaceY > 4f ? 1f : 0f);
                    var c = img.color; c.a = baseA * a; img.color = c;
                }
            }

            if (_wellRim != null)
            {
                float baseA = trayConfig != null ? trayConfig.wellRimColor.a : 0.5f;
                float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(time * 2.0f)); // 느린 호흡
                var c = _wellRim.color; c.a = baseA * pulse; _wellRim.color = c;
            }
        }

        // ── 연출 (unit 2) ────────────────────────────────────────────
        //
        // 각성 게이지(AwakeningGaugeView)에서 어휘를 가져오되 의도적으로 가른다.
        // 각성도 "차오르는 액체"인데 그쪽은 보유량이고 카드를 쓰면 내려간다 —
        // 물통은 진행률이라 소비해도 안 내려간다. 동작 규칙이 정반대인 두 위젯이
        // Battle 에 동시에 떠 있으므로, MaxIdle 펄스와 큰 바운스는 각성 전용으로
        // 남기고 여기서는 짧은 틱 스냅을 쓴다.

        private void PlayWellTick()
        {
            if (_wellTick != null) StopCoroutine(_wellTick);
            _wellTick = StartCoroutine(WellTickRoutine());
        }

        private System.Collections.IEnumerator WellTickRoutine()
        {
            const float dur = 0.14f;
            float t = 0f;
            while (t < dur && _wellRect != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                _wellRect.localScale = new Vector3(1f, Mathf.Lerp(1.1f, 1f, k * k), 1f);
                yield return null;
            }
            if (_wellRect != null) _wellRect.localScale = Vector3.one;
            _wellTick = null;
        }

        private void PlayMaxBurst()
        {
            if (_maxBurst != null) StopCoroutine(_maxBurst);
            _maxBurst = StartCoroutine(MaxBurstRoutine());
        }

        private System.Collections.IEnumerator MaxBurstRoutine()
        {
            const float dur = 0.42f;
            float t = 0f;
            var target = _cell != null ? _cell.transform : null;
            while (t < dur && target != null)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = k < 0.34f
                    ? Mathf.Lerp(1f, 1.12f, k / 0.34f)
                    : (k < 0.68f
                        ? Mathf.Lerp(1.12f, 0.97f, (k - 0.34f) / 0.34f)
                        : Mathf.Lerp(0.97f, 1f, (k - 0.68f) / 0.32f));
                target.localScale = Vector3.one * s;
                yield return null;
            }
            if (target != null) target.localScale = Vector3.one;
            _maxBurst = null;
        }

        private void PlayValuePunch()
        {
            if (_valuePunch != null) StopCoroutine(_valuePunch);
            _valuePunch = StartCoroutine(ValuePunchRoutine());
        }

        private System.Collections.IEnumerator ValuePunchRoutine()
        {
            const float dur = 0.16f;
            float t = 0f;
            var rt = _valueText != null ? _valueText.rectTransform : null;
            while (t < dur && rt != null)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Abs(2f * Mathf.Clamp01(t / dur) - 1f);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, k);
                yield return null;
            }
            if (rt != null) rt.localScale = Vector3.one;
            _valuePunch = null;
        }

        private void PlaySpendFlash()
        {
            if (_spendFlash != null) StopCoroutine(_spendFlash);
            _spendFlash = StartCoroutine(SpendFlashRoutine());
        }

        private System.Collections.IEnumerator SpendFlashRoutine()
        {
            const float dur = 0.3f;
            float t = 0f;
            while (t < dur && _valueText != null)
            {
                t += Time.unscaledDeltaTime;
                _valueText.color = Color.Lerp(PulseWarn, ValueColor, Mathf.Clamp01(t / dur));
                yield return null;
            }
            if (_valueText != null) _valueText.color = ValueColor;
            _spendFlash = null;
        }

        // 순 델타 기준이다. 같은 프레임에 소비+획득이 겹치면(온플레이스 GainCost)
        // 표시는 순 변화량이 되고, cost == gain 이면 연출이 없다 — 보유량이 실제로
        // 안 변했으므로 계약상 정상이다.
        private void PlayGainFloat(int delta)
        {
            if (_gain != null) StopCoroutine(_gain);
            _gain = StartCoroutine(GainRoutine(delta));
        }

        private System.Collections.IEnumerator GainRoutine(int delta)
        {
            if (_gainLabel == null) yield break;
            const float dur = 0.58f;
            var rt = _gainLabel.rectTransform;
            var start = new Vector2(0f, 0f);
            var end = new Vector2(0f, 26f);
            _gainLabel.text = "+" + delta;
            _gainLabel.gameObject.SetActive(true);
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                rt.anchoredPosition = Vector2.Lerp(start, end, 1f - (1f - k) * (1f - k));
                var c = _gainLabel.color;
                c.a = 1f - Mathf.Clamp01((k - 0.5f) * 2f);
                _gainLabel.color = c;
                yield return null;
            }
            _gainLabel.gameObject.SetActive(false);
            _gain = null;
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

            // 순서 = 그리기 순서. 물통이 셀 전체를 쓰고 숫자가 그 위에 겹친다
            // (각성 게이지와 같은 구성 — 액체가 공간을 다 쓰고 값은 그 위에 얹힘).
            BuildWell(wellPad);
            BuildValueRow(numberH, numberSize);
            BuildGainLabel(numberH);

            UiLayer.Apply(_cell);
        }

        // 값은 물통 위에 겹쳐 셀 중앙에 놓인다(각성 게이지와 같은 구성).
        // ⚡ 아이콘은 두지 않는다 — 셀이 154px 로 좁아 중앙 정렬을 방해하고,
        // 물통 자체가 자원을 상징한다(사용자 결정 2026-07-21).
        private void BuildValueRow(float numberH, float numberSize)
        {
            var valueGO = new GameObject("Value", typeof(RectTransform));
            valueGO.transform.SetParent(_cell.transform, false);
            var vrt = (RectTransform)valueGO.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = new Vector2(6f, 6f);
            vrt.offsetMax = new Vector2(-6f, -6f);
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
            _valueText.alignment = TextAlignmentOptions.Center;
            _valueText.textWrappingMode = TextWrappingModes.NoWrap;
            _valueText.raycastTarget = false;
            // 액체 위에 겹치므로 아웃라인 + 언더레이가 필수다. 물통이 가득 차면
            // 밝은 금색 위에 흰 글자가 놓여 아웃라인 없이는 안 읽힌다
            // (각성 게이지가 ApplyNumberOutline 을 두는 이유와 같다).
            // 배경이 어두운 물통 ↔ 밝은 금색 액체로 극단을 오가므로 두껍게 간다.
            var mat = _valueText.fontMaterial;
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.04f, 0.05f, 0.09f, 1f));
            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.45f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.45f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.3f);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.01f, 0.02f, 0.04f, 0.95f));
        }

        // 외부 획득(AddCost) 시 숫자 위로 떠오르는 "+N".
        private void BuildGainLabel(float numberH)
        {
            var go = new GameObject("GainDelta", typeof(RectTransform));
            go.transform.SetParent(_cell.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-6f, -numberH * 0.5f);
            rt.sizeDelta = new Vector2(60f, 30f);
            _gainLabel = go.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _gainLabel.font = numberFont;
            _gainLabel.fontSize = 24f;
            _gainLabel.fontStyle = FontStyles.Bold;
            _gainLabel.color = ValueColor;
            _gainLabel.alignment = TextAlignmentOptions.Right;
            _gainLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _gainLabel.raycastTarget = false;
            go.SetActive(false);
        }

        // 하단: Mask 용기 + 아래→위로 차오르는 액체 + 표면 하이라이트.
        // 각성 게이지(AwakeningGaugeView)의 ChargeWell 구성을 세로 직사각으로 옮긴 것.
        private void BuildWell(Vector2 wellPad)
        {
            // Mask 를 쓰지 않는다. Mask 는 IMaterialModifier 라 스텐실 프로퍼티를 넣은
            // 별도 머티리얼 인스턴스를 만들고, 그러면 원본에 쓴 _Fill 이 렌더에
            // 전파되지 않아 물통이 굳는다(움직임 없는 단색 한 장이 된다).
            // 라운드 코너는 셰이더가 SDF 로 직접 그린다.
            var wellGO = new GameObject("Well", typeof(RectTransform), typeof(Image));
            wellGO.transform.SetParent(_cell.transform, false);
            _wellRect = (RectTransform)wellGO.transform;
            _wellRect.anchorMin = new Vector2(0f, 0f);
            _wellRect.anchorMax = new Vector2(1f, 1f);
            _wellRect.pivot = new Vector2(0.5f, 0.5f);
            // 셀 거의 전체. 숫자 영역을 따로 떼지 않는다 — 값은 물통 위에 겹친다.
            _wellRect.offsetMin = new Vector2(wellPad.x, wellPad.y);
            _wellRect.offsetMax = new Vector2(-wellPad.x, -wellPad.y);

            var wellBack = wellGO.GetComponent<Image>();
            wellBack.sprite = UiRoundedSprite.Make(WellCornerRadius, 0f, Color.white, Color.clear);
            wellBack.type = Image.Type.Sliced;
            wellBack.color = trayConfig != null ? trayConfig.wellBackColor : WellBackFallback;
            wellBack.raycastTarget = false;

            var liquidGO = new GameObject("WellLiquid", typeof(RectTransform), typeof(Image));
            liquidGO.transform.SetParent(wellGO.transform, false);
            var lrt = (RectTransform)liquidGO.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            _wellLiquid = liquidGO.GetComponent<Image>();
            _wellLiquid.sprite = UiRoundedSprite.Make(0f, 0f, Color.white, Color.clear);
            _wellLiquid.raycastTarget = false;

            var liquidMat = trayConfig != null ? trayConfig.wellLiquidMaterial : null;
            if (liquidMat != null)
            {
                // 셰이더 모드: 풀 rect(Simple)를 그대로 두고 수위를 프래그먼트가
                // 자른다. Filled 는 지오메트리를 잘라 셰이더가 수면을 모르므로
                // 출렁임을 만들 수 없다.
                _wellLiquid.type = Image.Type.Simple;
                _wellLiquid.color = Color.white;
                _liquidMaterial = new Material(liquidMat)
                {
                    name = "CostWellLiquid (Instance)",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                if (trayConfig != null)
                {
                    _liquidMaterial.SetColor(LiquidBottomId, trayConfig.wellLiquidColor);
                    _liquidMaterial.SetColor(LiquidTopId, trayConfig.wellLiquidFullColor);
                    _liquidMaterial.SetColor(SurfaceId, trayConfig.wellSurfaceColor);
                }
                _liquidMaterial.SetFloat(FillId, 0f);
                _wellLiquid.material = _liquidMaterial;
                PushWellGeometry();
            }
            else
            {
                // 폴백: 머티리얼 미할당이면 예전 방식(출렁임 없는 단색 채움).
                _wellLiquid.type = Image.Type.Filled;
                _wellLiquid.fillMethod = Image.FillMethod.Vertical;
                _wellLiquid.fillOrigin = (int)Image.OriginVertical.Bottom;
                _wellLiquid.fillAmount = 0f;
                _wellLiquid.color = trayConfig != null ? trayConfig.wellLiquidColor : WellLiquidFallback;
            }

            // 용기 윤곽. Mask 밖(Well 의 형제)에 둬야 잘리지 않는다. 액체가 가득
            // 차도 물통의 경계가 남아 "채워진 그릇"으로 읽힌다.
            var frameGO = new GameObject("WellFrame", typeof(RectTransform), typeof(Image));
            frameGO.transform.SetParent(_cell.transform, false);
            var frt = (RectTransform)frameGO.transform;
            frt.anchorMin = _wellRect.anchorMin;
            frt.anchorMax = _wellRect.anchorMax;
            frt.offsetMin = _wellRect.offsetMin;
            frt.offsetMax = _wellRect.offsetMax;
            var frameImg = frameGO.GetComponent<Image>();
            frameImg.sprite = UiRoundedSprite.Make(WellCornerRadius, 2f, Color.clear,
                trayConfig != null ? trayConfig.fallbackBorder : new Color(0.94f, 0.72f, 0.24f, 1f));
            frameImg.type = Image.Type.Sliced;
            frameImg.raycastTarget = false;

            // defender-placement-cooldown 2 — 물통 안 기포(wellGO 자식, 액체 위에 뜸). alpha 0 이면 생성 안 함.
            var bubbleColor = trayConfig != null ? trayConfig.wellBubbleColor : new Color(0.85f, 0.95f, 1f, 0.4f);
            if (bubbleColor.a > 0f)
            {
                _wellBubbles = new Image[2];
                for (int b = 0; b < _wellBubbles.Length; b++)
                {
                    var bgo = new GameObject($"WellBubble{b}", typeof(RectTransform), typeof(Image));
                    bgo.transform.SetParent(wellGO.transform, false);
                    var brt = (RectTransform)bgo.transform;
                    brt.anchorMin = brt.anchorMax = Vector2.zero; // 물통 좌하단 기준 좌표
                    brt.pivot = new Vector2(0.5f, 0.5f);
                    brt.sizeDelta = new Vector2(9f + 2f * b, 9f + 2f * b);
                    var bi = bgo.GetComponent<Image>();
                    bi.sprite = UiRoundedSprite.Make(6f, 0f, Color.white, Color.clear); // 라운드 채움 = 원형
                    bi.type = Image.Type.Sliced;
                    bi.color = bubbleColor;
                    bi.raycastTarget = false;
                    _wellBubbles[b] = bi;
                }
            }

            // 테두리 림 글로우(호흡 펄스). WellFrame(정적 테두리) 위에 얹어 물통이 은은히 빛나게. alpha 0 이면 없음.
            var rimColor = trayConfig != null ? trayConfig.wellRimColor : new Color(0.6f, 0.9f, 1f, 0.5f);
            if (rimColor.a > 0f)
            {
                var rimGO = new GameObject("WellRim", typeof(RectTransform), typeof(Image));
                rimGO.transform.SetParent(_cell.transform, false);
                var rimRt = (RectTransform)rimGO.transform;
                rimRt.anchorMin = _wellRect.anchorMin; rimRt.anchorMax = _wellRect.anchorMax;
                rimRt.offsetMin = _wellRect.offsetMin; rimRt.offsetMax = _wellRect.offsetMax;
                _wellRim = rimGO.GetComponent<Image>();
                _wellRim.sprite = UiRoundedSprite.Make(WellCornerRadius, 3f, Color.clear, Color.white); // 테두리만
                _wellRim.type = Image.Type.Sliced;
                _wellRim.color = rimColor;
                _wellRim.raycastTarget = false;
            }
        }
    }
}
