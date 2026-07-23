using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Spine.Unity;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // dreamcatcher-orb-dock unit 1 — 트레이 우측 분리 "드림캐쳐 항아리 독".
    // 코너 버스트 버튼(retired)을 대체한다. 세로 항아리에 큰 숫자(1순위 판독) + 세로 채움
    // + 코스트 눈금 + ready 림 + 발견성 라벨. 채움은 unit 2 피규어가 덮을 placeholder.
    // 탭=Toggled(기존 계약), open/close 상태 소유자는 여전히 DreamcatcherHandView.
    // 클래스명·public API·씬 배선(GameObject 1012444853, gaugeView 참조 2곳)은 유지.
    public class AwakeningGaugeView : MonoBehaviour
    {
        [SerializeField] private DreamcatcherHandController handController;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numberFont;

        [Header("Jar Colors")]
        [SerializeField] private Color backingColor = new Color(0.09f, 0.08f, 0.15f, 0.95f);
        [SerializeField] private Color chargedColor = new Color(0.43f, 0.86f, 0.92f, 0.95f);
        [SerializeField] private Color maxColor = new Color(1f, 0.77f, 0.12f, 1f);
        // 기존 authored 값 보존: haloColor→rimColor, dormantFrameColor→dormantColor.
        [FormerlySerializedAs("haloColor")]
        [SerializeField] private Color rimColor = new Color(0.56f, 0.43f, 1f, 0.9f);
        [FormerlySerializedAs("dormantFrameColor")]
        [SerializeField] private Color dormantColor = new Color(0.62f, 0.58f, 0.7f, 0.7f);
        [SerializeField] private Color tickColor = new Color(0.86f, 0.82f, 0.96f, 0.72f);
        // 오버플로우(낭비) 전용 색 — MAX 골드와 분리해 "지금 버려지는 중"을 명확히(손실회피).
        [SerializeField] private Color overflowColor = new Color(1f, 0.35f, 0.24f, 1f);
        [SerializeField] private float valuePunchScale = 1.18f;

        [Header("Placement")]
        [SerializeField] private float trayGap = 30f;      // 트레이 우측 엣지와의 간격(오탭 방지 여유)
        [SerializeField] private float baselineY = 18f;     // 하단 기준선
        [SerializeField] private float fallbackTrayHalf = 490f; // 트레이 미bind 시 폴백 반폭

        [Header("Figure Pile — Spine miniatures (unit 2b)")]
        // 더 작게·더 많이(사용자 재조정): ~16개 · ~22px. 흡수 비행 = 회전 미니어처.
        [SerializeField] private int maxFigures = 44; // unit 6 — 100 에서 항아리 가득
        [SerializeField] private float figureRadius = 11f;
        [SerializeField] private float figureGravity = 1500f;
        [SerializeField] private float figureDamping = 0.9f;
        // Spine 미니어처(대표 스켈레톤 + 머티리얼). 미배선 시 절차적 원 폴백(무회귀).
        [SerializeField] private AttackUnitData representativeUnit; // 대표 나이트메어(적 정체성 미제공)
        [SerializeField] private Material figureSkeletonMaterial;   // SkeletonGraphic 머티리얼
        [SerializeField] private string figureAnimation = "Idle";  // 동결 포즈(가독성=Idle; Die 도 가능)
        [SerializeField] private float figureScale = 0.22f;        // localScale(~22px, 더 작게)
        [SerializeField] private float figureFlightSeconds = 0.44f; // 킬 위치→항아리 비행 시간
        [SerializeField] private float figureFlightArc = 140f;      // 아치 솟음(px)
        [SerializeField] private float figureFlightStagger = 0.05f; // 한 획득의 여러 피규어 간 지연
        [SerializeField] private int maxConcurrentFlights = 4;      // 동시 비행 상한(다발 킬 clutter↓)
        [SerializeField] private float figureJostleInterval = 1.9f; // 주기적 통통 튕김 간격(초, 완화)
        [SerializeField] private float figureJostleStrength = 2.1f; // 통통 튕김 임펄스 세기(주변시 노이즈↓)
        [SerializeField] private Color[] figureTints =
        {
            new Color(0.62f, 0.5f, 0.9f, 1f),
            new Color(0.45f, 0.82f, 0.88f, 1f),
            new Color(0.55f, 0.62f, 0.95f, 1f),
        };

        // Layout consts (authored 아님 — 항아리 기하).
        const float DockWidth = 150f, DockHeight = 236f;
        const float JarWidth = 134f, JarHeight = 208f, JarBottom = 24f;
        const float JarBorder = 6f, InteriorPad = 9f;

        public event System.Action Toggled;
        public RectTransform HitRect => _panel != null ? (RectTransform)_panel.transform : null;

        // dreamcatcher-orb-dock unit 1 — DreamcatcherHandView.Start 가 트레이 RectTransform 을
        // 넘겨준다(씬 배선 없이 기존 참조로). LateUpdate 가 트레이 우측 엣지에 독을 정렬.
        public void BindTray(RectTransform trayRect) => _trayRect = trayRect;

        private RectTransform _trayRect;
        private GameObject _panel;
        private RectTransform _visualRoot;
        private Image _jarFrame;
        private Image _rim;
        private JarFigurePile _pile;
        // unit 3 — 흡수 비행. 킬 위치에서 항아리로 날아가는 고스트 → 도착 시 pile.SpawnAtTop.
        private RectTransform _safeArea;
        private Sprite _figureSprite;
        private int _pendingFlights;
        // 고스트 풀(재사용, GC 완화) + generation(전투 이탈/OnDisable 시 진행 비행 일괄 무효화).
        private readonly System.Collections.Generic.List<Graphic> _ghostPool = new System.Collections.Generic.List<Graphic>();
        private int _flightGen;
        // committed = 실제 pile 개수 + 비행 중. 게이지 목표와 이걸로 대사(desync 방지).
        private int FiguresCommitted => (_pile != null ? _pile.ActiveCount : 0) + _pendingFlights;
        private TextMeshProUGUI _valueLabel;
        private TextMeshProUGUI _gainLabel;
        private bool _built;
        private bool _open;
        private GamePhase _phase;
        private bool _suppressed;
        private int _lastShown = -1;
        private float _normalized;
        private float _readyThreshold = 1f;
        private bool _ready;
        private Coroutine _punch;
        private Coroutine _gain;
        private Coroutine _pulse;
        private Coroutine _readyPulse;
        private Coroutine _overflow;

        public void Pulse()
        {
            if (_panel == null || !_panel.activeInHierarchy) return;
            if (_pulse != null) StopCoroutine(_pulse);
            _pulse = StartCoroutine(PulseRoutine());
        }

        // HandView calls this at the same state boundary that owns slomo/strip switching.
        public void SetOpen(bool open)
        {
            _open = open;
            UpdateVisualState();
        }

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (handController != null)
            {
                handController.GaugeChanged += OnGaugeChanged;
                handController.AwakeningOverflowed += OnOverflow;
                handController.AwakeningGainedAt += OnAwakeningGainedAt;
            }
            if (GameManager.Instance != null)
            {
                GameManager.Instance.PhaseChanged += OnPhaseChanged;
                OnPhaseChanged(GameManager.Instance.CurrentPhase);
            }
        }

        private void OnDisable()
        {
            if (handController != null)
            {
                handController.GaugeChanged -= OnGaugeChanged;
                handController.AwakeningOverflowed -= OnOverflow;
                handController.AwakeningGainedAt -= OnAwakeningGainedAt;
            }
            if (GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            if (_visualRoot != null) _visualRoot.localScale = Vector3.one;
            CancelFlights(); // 진행 중 비행 정리(pending 오염·고아 고스트 방지)
        }

        // unit 4 — 게이지 상한에서 획득분 소멸(넘침). 손실 회피를 위해 시끄러운 경고:
        // 골드 림 플래시 + 짧은 통 흔들림. 상시 pulse 금지 계약과 달리 이벤트 반응이라 허용.
        private void OnOverflow(int lost)
        {
            if (_panel == null || !_panel.activeInHierarchy) return;
            if (_overflow != null) StopCoroutine(_overflow);
            _overflow = StartCoroutine(OverflowFlashRoutine());
            // 손실회피: 버려진 양을 -N 으로 명시(획득 +N 의 거울). MAX 에선 GaugeChanged 미발화라
            // +N 과 겹치지 않음. _gainLabel 재사용.
            if (lost > 0)
            {
                if (_gain != null) StopCoroutine(_gain);
                _gain = StartCoroutine(ShowLoss(lost));
            }
        }

        // ── 흡수 비행 (unit 3) ────────────────────────────────────────────────

        // 게이지 → 목표 피규어 수(반올림). 피규어 판독의 이산 단위.
        private int FiguresForGauge(int gauge)
        {
            int max = handController != null ? handController.GaugeMax : 100;
            if (max <= 0 || _pile == null) return 0;
            return Mathf.Clamp(Mathf.RoundToInt((float)gauge / max * _pile.Capacity), 0, _pile.Capacity);
        }

        // 정착 피규어를 목표까지 줄인다(소비/리셋/오버슈트 보정). 비행 중은 도착 시 재보정.
        private void TrimToTarget(int gauge)
        {
            if (_pile == null) return;
            int target = FiguresForGauge(gauge);
            while (_pile.ActiveCount > target) _pile.RemoveTop();
        }

        // 킬/사망 위치에서 피규어가 날아온다(입자=피규어). 획득으로 늘어난 목표만큼 비행 발사.
        // unit 6 — killedVisual = 죽은 유닛 스킨(null 이면 대표 스킨). 피규어/고스트가 그 스킨으로 렌더.
        private void OnAwakeningGainedAt(int applied, Vector3 worldPos, ISpineUnitVisualData killedVisual)
        {
            if (_pile == null || handController == null) return;
            int delta = FiguresForGauge(handController.Gauge) - FiguresCommitted;
            if (delta <= 0) return;

            bool canFly = _panel != null && _panel.activeInHierarchy && _safeArea != null && _figureSprite != null;
            Vector2 endLocal = default, startLocal = default;
            bool haveEnd = canFly && TryJarTopLocal(out endLocal);
            bool haveStart = canFly && TryWorldToSafeAreaLocal(worldPos, out startLocal);

            // 다발 킬 코얼레스: 동시 비행 상한(maxConcurrentFlights)까지만 실제 비행, 초과분은
            // 즉시 SpawnAtTop 으로 카운트만 반영해 화면 회전 스프라이트 폭주를 막는다(spec 배칭 계약).
            for (int i = 0; i < delta; i++)
            {
                if (haveStart && haveEnd && _pendingFlights < maxConcurrentFlights)
                {
                    StartCoroutine(FlightRoutine(startLocal, endLocal, i * figureFlightStagger, killedVisual));
                    _pendingFlights++;
                }
                else
                {
                    _pile.SpawnAtTop(killedVisual); // 폴백(패널 비활성/무효 좌표) 또는 동시 비행 상한 초과
                }
            }
        }

        // 항아리 상단중앙을 SafeAreaRoot 로컬로(피규어 착지 지점).
        private bool TryJarTopLocal(out Vector2 local)
        {
            local = default;
            if (_jarFrame == null || _safeArea == null) return false;
            Vector3 worldTop = _jarFrame.rectTransform.TransformPoint(new Vector3(0f, JarHeight, 0f));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, worldTop);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_safeArea, screen, null, out local);
        }

        private bool TryWorldToSafeAreaLocal(Vector3 world, out Vector2 local)
        {
            local = default;
            if (_safeArea == null) return false;
            if (_figureCamera == null) _figureCamera = Camera.main;
            if (_figureCamera == null) return false;
            Vector3 screen = _figureCamera.WorldToScreenPoint(world);
            if (screen.z <= 0f) return false; // 카메라 뒤 → 무효(폴백 top 스폰)
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_safeArea, screen, null, out local);
        }

        private Camera _figureCamera;

        // 고스트 풀에서 비활성 하나를 빌린다(GC 완화). 없으면 신설. Spine 미니어처 또는 Image 폴백.
        private Graphic GetGhost()
        {
            for (int i = 0; i < _ghostPool.Count; i++)
                if (_ghostPool[i] != null && !_ghostPool[i].gameObject.activeSelf) return _ghostPool[i];
            var go = new GameObject("AbsorbGhost", typeof(RectTransform));
            go.transform.SetParent(_safeArea, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            Graphic g;
            // 비행 고스트도 항아리 피규어와 같은 Spine 미니어처(사용자: 원 말고 피규어가 회전하며
            // 빨려듦). 미배선 시 원 폴백.
            if (SpineFigureBuilder.CanBuild(representativeUnit, figureSkeletonMaterial))
            {
                var sg = go.AddComponent<SkeletonGraphic>();
                SpineFigureBuilder.Setup(sg, representativeUnit, figureSkeletonMaterial, figureAnimation);
                g = sg;
            }
            else
            {
                rt.sizeDelta = new Vector2(figureRadius * 2f, figureRadius * 2f);
                var img = go.AddComponent<Image>();
                img.sprite = _figureSprite;
                img.raycastTarget = false;
                g = img;
            }
            go.SetActive(false);
            _ghostPool.Add(g);
            return g;
        }

        // 전투 이탈/OnDisable — 진행 중 비행을 일괄 무효화(pending 오염·전환 중 고아 고스트 방지).
        // gen 을 올리면 실행 중 FlightRoutine 이 다음 프레임에 스스로 중단한다.
        private void CancelFlights()
        {
            _flightGen++;
            _pendingFlights = 0;
            for (int i = 0; i < _ghostPool.Count; i++)
                if (_ghostPool[i] != null) _ghostPool[i].gameObject.SetActive(false);
        }

        // 킬 위치 → 항아리 상단으로 아치 비행. 도착 시 pile.SpawnAtTop + 목표 보정.
        // gen 이 바뀌면(전투 이탈) 즉시 중단(pending 은 CancelFlights 가 이미 0 으로 리셋).
        private IEnumerator FlightRoutine(Vector2 startLocal, Vector2 endLocal, float delay, ISpineUnitVisualData killedVisual)
        {
            int gen = _flightGen;
            var ghost = GetGhost();
            // unit 6 — 날아가는 고스트도 죽은 유닛 스킨으로(입자=그 적 피규어). 스켈레톤 불일치 시 스킵.
            if (ghost is SkeletonGraphic ghostSg) SpineFigureBuilder.Reskin(ghostSg, killedVisual);
            var grt = ghost.rectTransform;
            // Spine 미니어처는 base 스케일 = figureScale(원본 rig 가 큼). Image 폴백은 1(sizeDelta).
            float baseScale = ghost is SkeletonGraphic ? figureScale : 1f;
            float spinDir = ((_pendingFlights & 1) == 0) ? 1f : -1f; // 좌우 교차 회전
            grt.anchoredPosition = startLocal;
            grt.localRotation = Quaternion.identity;
            grt.localScale = Vector3.one * baseScale;
            ghost.gameObject.SetActive(true);

            float wait = 0f;
            while (wait < delay)
            {
                if (_flightGen != gen) { ghost.gameObject.SetActive(false); yield break; }
                wait += Time.unscaledDeltaTime;
                yield return null;
            }

            float dur = Mathf.Max(0.05f, figureFlightSeconds);
            float t = 0f;
            while (t < dur)
            {
                if (_flightGen != gen) { ghost.gameObject.SetActive(false); yield break; }
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float ease = 1f - (1f - k) * (1f - k);
                Vector2 p = Vector2.Lerp(startLocal, endLocal, ease);
                p.y += Mathf.Sin(k * Mathf.PI) * figureFlightArc; // 아치 솟음
                grt.anchoredPosition = p;
                // 뱅글뱅글 회전 + 빨려드는 축소(사용자 요청). 회전은 340°로 절제(clutter↓).
                grt.localRotation = Quaternion.Euler(0f, 0f, spinDir * k * 340f);
                grt.localScale = Vector3.one * baseScale * Mathf.Lerp(1.25f, 0.7f, k);
                yield return null;
            }

            ghost.gameObject.SetActive(false);
            _pendingFlights = Mathf.Max(0, _pendingFlights - 1);
            if (_pile != null)
            {
                _pile.SpawnAtTop(killedVisual); // unit 6 — 도착 피규어도 죽은 유닛 스킨
                TrimToTarget(handController != null ? handController.Gauge : 0);
            }
        }

        // 트레이 우측 엣지에 독을 정렬. 트레이·독 SafeAreaRoot 는 congruent(UiSafeAreaFitter)라
        // 트레이 폭 반값이 곧 우측 엣지 x. 폭은 매 프레임 갱신될 수 있어(슬롯 수 변화) 추종한다.
        private void LateUpdate()
        {
            if (_panel == null || !_panel.activeInHierarchy) return;
            float half = fallbackTrayHalf;
            if (_trayRect != null)
            {
                float w = _trayRect.rect.width;
                if (w > 1f) half = w * 0.5f;
            }
            var rt = (RectTransform)_panel.transform;
            var target = new Vector2(half + trayGap, baselineY);
            if ((rt.anchoredPosition - target).sqrMagnitude > 0.01f)
                rt.anchoredPosition = target;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            _phase = phase;
            ApplyPanelVisibility();
        }

        // first-session-tutorial — 첫 판은 배치만으로 승부를 보게 각성 UI 를 감춘다.
        // 표시 소유자는 여전히 이 뷰. FirstSessionTutorialController 가 SetSuppressed 로 갱신.
        public void SetSuppressed(bool suppressed)
        {
            if (_suppressed == suppressed) return;
            _suppressed = suppressed;
            ApplyPanelVisibility();
        }

        private void ApplyPanelVisibility()
        {
            if (_panel == null) return;
            // 캐시한 _phase 를 읽는다(GameManager.Instance.CurrentPhase 직독 금지 —
            // 같은 PhaseChanged 이벤트 안에서 구독자 순서에 따라 값이 갈린다).
            if (!_suppressed && _phase == GamePhase.Battle)
            {
                _panel.SetActive(true);
                Refresh(handController != null ? handController.Gauge : 0, punch: false);
            }
            else
            {
                CancelFlights(); // 전투 이탈 → 진행 비행 무효화(전환 중 고아 고스트 방지)
                _panel.SetActive(false);
            }
        }

        private void OnGaugeChanged(int value) => Refresh(value, punch: true);

        private void Refresh(int value, bool punch)
        {
            if (_valueLabel == null) return;
            int max = handController != null ? handController.GaugeMax : 100;
            _valueLabel.text = value.ToString();
            float previousNormalized = _normalized;
            _normalized = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
            // unit 6 — 단색 backing 제거(스프라이트 늘린 인상). 피규어 더미가 유일한 채움.
            // unit 3 — 증가분은 흡수 비행(OnAwakeningGainedAt)이 채운다. 여기선 감소(소비/리셋)만
            // 정착 피규어를 줄여 목표에 맞춘다(비행 중인 건 도착 시 self-correct).
            TrimToTarget(value);

            int delta = _lastShown >= 0 ? value - _lastShown : 0;
            if (punch && value != _lastShown && _panel != null && _panel.activeInHierarchy)
            {
                if (_punch != null) StopCoroutine(_punch);
                _punch = StartCoroutine(PunchValue());
                if (delta > 0)
                {
                    if (_gain != null) StopCoroutine(_gain);
                    _gain = StartCoroutine(ShowGain(delta));
                }
            }

            // ready 로 갓 넘어간 순간 rim 한 번 강조(unit 4 가 정밀 affordability·오버플로우 확장).
            if (_normalized >= _readyThreshold && previousNormalized < _readyThreshold
                && _panel != null && _panel.activeInHierarchy)
            {
                if (_readyPulse != null) StopCoroutine(_readyPulse);
                _readyPulse = StartCoroutine(ReadyPulseRoutine());
            }

            _lastShown = value;
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            bool dormant = _normalized <= 0.001f && !_open;
            _ready = _normalized >= _readyThreshold;
            if (_rim != null)
            {
                // 색은 max 여부로만 갈린다(골드 vs 보라). ready/open 은 알파(발화 강도)로 표현.
                Color c = _normalized >= 0.999f ? maxColor : rimColor;
                c.a = dormant ? 0f : (_ready || _open ? 1f : Mathf.Lerp(0.12f, 0.5f, _normalized));
                _rim.color = c;
            }
            if (_jarFrame != null)
                _jarFrame.color = dormant ? dormantColor : Color.white;
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 7);
            _safeArea = roots.SafeAreaRoot; // unit 3 — 흡수 비행 고스트 부모(전체 화면)
            // unit 2b — SkeletonGraphic 미니어처는 CanvasRenderer 에 uv1/uv2/normal/tangent 가
            // 실려야 정상 렌더(SquadCharacterPage 관례). 이 order 7 캔버스에 채널을 켠다.
            if (roots.Canvas != null)
                roots.Canvas.additionalShaderChannels |=
                    AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 |
                    AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;

            // 트레이 우측 분리 독. anchor 하단중앙, pivot 하단좌 → LateUpdate 가 x 를 트레이
            // 우측 엣지로 민다. 히트 영역 = 패널 전체(세로 항아리라 세로 히트 면적 충분).
            _panel = new GameObject("DreamcatcherJarDock", typeof(RectTransform), typeof(Image), typeof(Button));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var panelRect = (RectTransform)_panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0f, 0f);
            panelRect.anchoredPosition = new Vector2(fallbackTrayHalf + trayGap, baselineY);
            panelRect.sizeDelta = new Vector2(DockWidth, DockHeight);

            var hitGraphic = _panel.GetComponent<Image>();
            hitGraphic.color = new Color(1f, 1f, 1f, 0.001f);
            var button = _panel.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitGraphic;
            button.onClick.AddListener(() =>
            {
                SoundManager.Instance?.PlayUiTick();
                Toggled?.Invoke();
            });

            var visualGO = new GameObject("JarVisual", typeof(RectTransform));
            visualGO.transform.SetParent(_panel.transform, false);
            _visualRoot = (RectTransform)visualGO.transform;
            _visualRoot.anchorMin = Vector2.zero;
            _visualRoot.anchorMax = Vector2.one;
            _visualRoot.offsetMin = Vector2.zero;
            _visualRoot.offsetMax = Vector2.zero;

            // 항아리 몸체(배킹+테두리). 9-slice rounded rect.
            var jarGO = new GameObject("JarBody", typeof(RectTransform), typeof(Image));
            jarGO.transform.SetParent(_visualRoot, false);
            var jarRect = (RectTransform)jarGO.transform;
            jarRect.anchorMin = jarRect.anchorMax = new Vector2(0.5f, 0f);
            jarRect.pivot = new Vector2(0.5f, 0f);
            jarRect.anchoredPosition = new Vector2(0f, JarBottom);
            jarRect.sizeDelta = new Vector2(JarWidth, JarHeight);
            _jarFrame = jarGO.GetComponent<Image>();
            _jarFrame.sprite = UiRoundedSprite.Make(18f, JarBorder, backingColor, new Color(0.3f, 0.26f, 0.42f, 1f));
            _jarFrame.type = Image.Type.Sliced;
            _jarFrame.raycastTarget = false;

            float interiorW = JarWidth - 2f * InteriorPad;
            float interiorH = JarHeight - 2f * InteriorPad;

            // unit 6 — 세로 단색 backing 제거(스프라이트를 늘린 듯한 인상). 피규어 더미가 유일한 채움.
            BuildCostTicks(jarGO.transform, interiorW, interiorH, InteriorPad);

            // 게이지 비례 미니 피규어 더미(unit 2a). 인테리어를 채우고 pivot 하단중앙 →
            // JarFigurePhysics 로컬좌표를 anchoredPosition 에 직접 매핑. ticks 위·number 아래.
            var pileGO = new GameObject("FigurePile", typeof(RectTransform));
            pileGO.transform.SetParent(jarGO.transform, false);
            var pileRect = (RectTransform)pileGO.transform;
            pileRect.anchorMin = pileRect.anchorMax = new Vector2(0.5f, 0f);
            pileRect.pivot = new Vector2(0.5f, 0f);
            pileRect.anchoredPosition = new Vector2(0f, InteriorPad);
            pileRect.sizeDelta = new Vector2(interiorW, interiorH);
            _pile = pileGO.AddComponent<JarFigurePile>();
            _figureSprite = UiRoundedSprite.MakeCircle(48, Color.white, 5f, new Color(0.2f, 0.16f, 0.32f, 1f));
            var pileParams = new JarSimParams
            {
                gravity = figureGravity,
                damping = figureDamping,
                sleepMotionSq = 0.02f,
            };
            _pile.Configure(maxFigures, figureRadius, pileParams, representativeUnit, figureSkeletonMaterial,
                figureScale, figureAnimation, _figureSprite, figureTints);
            _pile.SetJostle(figureJostleInterval, figureJostleStrength);

            // 큰 숫자(1순위). 채움/피규어 위에 아웃라인으로 항상 읽히게.
            var valueGO = new GameObject("Value", typeof(RectTransform));
            valueGO.transform.SetParent(jarGO.transform, false);
            var valueRect = (RectTransform)valueGO.transform;
            valueRect.anchorMin = valueRect.anchorMax = new Vector2(0.5f, 0f);
            valueRect.pivot = new Vector2(0.5f, 0.5f);
            valueRect.anchoredPosition = new Vector2(0f, JarHeight * 0.5f);
            valueRect.sizeDelta = new Vector2(JarWidth - 8f, 78f);
            _valueLabel = valueGO.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _valueLabel.font = numberFont;
            _valueLabel.text = "0";
            _valueLabel.fontSize = 54f;
            _valueLabel.fontStyle = FontStyles.Bold;
            _valueLabel.color = Color.white;
            _valueLabel.alignment = TextAlignmentOptions.Center;
            _valueLabel.raycastTarget = false;
            ApplyNumberOutline(_valueLabel);

            // ready 림(테두리 발화 오버레이). 색·알파는 UpdateVisualState 가 구동.
            var rimGO = new GameObject("Rim", typeof(RectTransform), typeof(Image));
            rimGO.transform.SetParent(jarGO.transform, false);
            var rimRect = (RectTransform)rimGO.transform;
            rimRect.anchorMin = Vector2.zero;
            rimRect.anchorMax = Vector2.one;
            rimRect.offsetMin = Vector2.zero;
            rimRect.offsetMax = Vector2.zero;
            _rim = rimGO.GetComponent<Image>();
            _rim.sprite = UiRoundedSprite.Make(18f, JarBorder, Color.clear, Color.white);
            _rim.type = Image.Type.Sliced;
            _rim.color = Color.clear;
            _rim.raycastTarget = false;

            // 획득 +N 플로팅.
            var gainGO = new GameObject("GainDelta", typeof(RectTransform));
            gainGO.transform.SetParent(jarGO.transform, false);
            var gainRect = (RectTransform)gainGO.transform;
            gainRect.anchorMin = gainRect.anchorMax = new Vector2(0.5f, 0f);
            gainRect.pivot = new Vector2(0.5f, 0.5f);
            gainRect.anchoredPosition = new Vector2(0f, JarHeight * 0.5f + 34f);
            gainRect.sizeDelta = new Vector2(90f, 40f);
            _gainLabel = gainGO.AddComponent<TextMeshProUGUI>();
            if (numberFont != null) _gainLabel.font = numberFont;
            _gainLabel.fontSize = 28f;
            _gainLabel.fontStyle = FontStyles.Bold;
            _gainLabel.alignment = TextAlignmentOptions.Center;
            _gainLabel.raycastTarget = false;
            ApplyNumberOutline(_gainLabel);
            gainGO.SetActive(false);

            // 발견성 라벨 — 항아리 아래(채움/피규어에 가리지 않게). 라벨 계약 계승.
            var labelGO = new GameObject("DockLabel", typeof(RectTransform));
            labelGO.transform.SetParent(_visualRoot, false);
            var labelRect = (RectTransform)labelGO.transform;
            labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 2f);
            labelRect.sizeDelta = new Vector2(DockWidth, 20f);
            var dockLabel = labelGO.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) dockLabel.font = labelFont;
            dockLabel.text = "드림캐쳐";
            dockLabel.fontSize = 16f;
            dockLabel.fontStyle = FontStyles.Bold;
            dockLabel.color = new Color(0.86f, 0.82f, 0.96f, 0.92f);
            dockLabel.alignment = TextAlignmentOptions.Center;
            dockLabel.raycastTarget = false;

            ResolveReadyThreshold();
            UiLayer.Apply(gameObject);
            UpdateVisualState();
        }

        // 코스트 눈금: config 의 distinct 코스트마다 y=cost/max*interiorH 에 얇은 틱 + 소형 숫자.
        // 하드코딩 금지 — 데이터 파생. 현재 라이브 값은 3종 모두 20 → 틱 1개.
        private void BuildCostTicks(Transform jar, float interiorW, float interiorH, float pad)
        {
            var cfg = handController != null ? handController.Config : null;
            if (cfg == null) return;
            int max = handController.GaugeMax;
            if (max <= 0) return;

            var distinct = new List<int>();
            foreach (int c in new[] { cfg.costSquad, cfg.costUnit, cfg.costActive })
                if (c > 0 && c < max && !distinct.Contains(c)) distinct.Add(c);
            distinct.Sort();

            foreach (int c in distinct)
            {
                float y = pad + (float)c / max * interiorH;
                var tickGO = new GameObject("Tick" + c, typeof(RectTransform), typeof(Image));
                tickGO.transform.SetParent(jar, false);
                var tr = (RectTransform)tickGO.transform;
                tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0f);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.anchoredPosition = new Vector2(0f, y);
                tr.sizeDelta = new Vector2(interiorW, 3f);
                var img = tickGO.GetComponent<Image>();
                img.color = tickColor;
                img.raycastTarget = false;

                var lblGO = new GameObject("TickLabel" + c, typeof(RectTransform));
                lblGO.transform.SetParent(jar, false);
                var lr = (RectTransform)lblGO.transform;
                lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0f);
                lr.pivot = new Vector2(1f, 0.5f);
                lr.anchoredPosition = new Vector2(interiorW * 0.5f - 4f, y + 8f);
                lr.sizeDelta = new Vector2(34f, 18f);
                var lbl = lblGO.AddComponent<TextMeshProUGUI>();
                if (labelFont != null) lbl.font = labelFont;
                lbl.text = c.ToString();
                lbl.fontSize = 13f;
                lbl.color = tickColor;
                lbl.alignment = TextAlignmentOptions.Right;
                lbl.raycastTarget = false;
            }
        }

        private void ResolveReadyThreshold()
        {
            var cfg = handController != null ? handController.Config : null;
            int max = handController != null ? handController.GaugeMax : 100;
            if (cfg == null || max <= 0) { _readyThreshold = 1f; return; }
            int minCost = int.MaxValue;
            foreach (int c in new[] { cfg.costSquad, cfg.costUnit, cfg.costActive })
                if (c > 0 && c < minCost) minCost = c;
            _readyThreshold = minCost == int.MaxValue ? 1f : Mathf.Clamp01((float)minCost / max);
        }

        private IEnumerator PunchValue()
        {
            var rt = _valueLabel.rectTransform;
            const float duration = 0.16f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Abs(2f * Mathf.Clamp01(time / duration) - 1f);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, valuePunchScale, k);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _punch = null;
        }

        private IEnumerator ShowGain(int delta)
        {
            if (_gainLabel == null) yield break;
            const float duration = 0.58f;
            var rt = _gainLabel.rectTransform;
            Vector2 start = new Vector2(0f, JarHeight * 0.5f + 34f);
            Vector2 end = new Vector2(0f, JarHeight * 0.5f + 72f);
            _gainLabel.text = $"+{delta}";
            _gainLabel.gameObject.SetActive(true);
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                rt.anchoredPosition = Vector2.Lerp(start, end, 1f - (1f - k) * (1f - k));
                var c = chargedColor;
                c.a = 1f - Mathf.Clamp01((k - 0.5f) * 2f);
                _gainLabel.color = c;
                yield return null;
            }
            _gainLabel.gameObject.SetActive(false);
            _gain = null;
        }

        // 오버플로우 손실 -N (ShowGain 의 거울: 아래로 흘리고 낭비 색).
        private IEnumerator ShowLoss(int lost)
        {
            if (_gainLabel == null) yield break;
            const float duration = 0.62f;
            var rt = _gainLabel.rectTransform;
            Vector2 start = new Vector2(0f, JarHeight * 0.5f + 18f);
            Vector2 end = new Vector2(0f, JarHeight * 0.5f - 26f); // 아래로(손실)
            _gainLabel.text = $"-{lost}";
            _gainLabel.gameObject.SetActive(true);
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                rt.anchoredPosition = Vector2.Lerp(start, end, 1f - (1f - k) * (1f - k));
                var c = overflowColor;
                c.a = 1f - Mathf.Clamp01((k - 0.5f) * 2f);
                _gainLabel.color = c;
                yield return null;
            }
            _gainLabel.gameObject.SetActive(false);
            _gain = null;
        }

        private IEnumerator ReadyPulseRoutine()
        {
            if (_visualRoot == null) yield break;
            const float duration = 0.42f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                float scale = k < 0.34f
                    ? Mathf.Lerp(1f, 1.12f, k / 0.34f)
                    : (k < 0.68f
                        ? Mathf.Lerp(1.12f, 0.96f, (k - 0.34f) / 0.34f)
                        : Mathf.Lerp(0.96f, 1f, (k - 0.68f) / 0.32f));
                _visualRoot.localScale = Vector3.one * scale;
                yield return null;
            }
            _visualRoot.localScale = Vector3.one;
            _readyPulse = null;
        }

        private IEnumerator OverflowFlashRoutine()
        {
            const float duration = 0.6f;
            float time = 0f;
            Vector3 baseLocal = _visualRoot != null ? _visualRoot.localPosition : Vector3.zero;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                float flash = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 3f)) * (1f - k); // 3회 감쇠 깜빡임
                if (_rim != null)
                {
                    // MAX 상시 골드와 분리된 낭비 색으로 "지금 버려지는 중" 명확화.
                    var c = overflowColor;
                    c.a = Mathf.Max(0.4f, flash);
                    _rim.color = c;
                }
                if (_visualRoot != null)
                    _visualRoot.localPosition = baseLocal + new Vector3(Mathf.Sin(time * 62f) * 3.5f * (1f - k), 0f, 0f);
                yield return null;
            }
            if (_visualRoot != null) _visualRoot.localPosition = baseLocal;
            _overflow = null;
            UpdateVisualState(); // rim 색/알파 정상 복원
        }

        private IEnumerator PulseRoutine()
        {
            var rt = _visualRoot;
            const float duration = 0.22f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Abs(2f * Mathf.Clamp01(time / duration) - 1f);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, k);
                yield return null;
            }
            rt.localScale = Vector3.one;
            _pulse = null;
        }

        private static void ApplyNumberOutline(TextMeshProUGUI label)
        {
            if (label.font == null) return;
            var material = label.fontMaterial;
            material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.11f, 0.04f, 0.22f, 1f));
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.35f);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0.04f, 0.01f, 0.1f, 0.8f));
        }
    }
}
