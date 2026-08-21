using System.Collections;
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
    // + ready 림 + 발견성 라벨. 채움은 unit 2 피규어 더미가 담당.
    // 탭=Toggled(기존 계약), open/close 상태 소유자는 여전히 DreamcatcherHandView.
    // 클래스명·public API·씬 배선(GameObject 1012444853, gaugeView 참조 2곳)은 유지.
    //
    // unit 8 — 항아리 탭이 꺼진 뒤(JarTapEnabled=false) 이 독은 입력을 유도하지 않는
    // «수치 판독면» 이다. 그래서 평소에는 완전히 정지하고(상시 어필 어휘 전량 철거),
    // 각성치가 한 회분(=가장 싼 카드 코스트) 경계를 넘는 그 순간에만 짧게 터진다.
    // 회차를 선으로 그리지는 않는다(사용자 결정: 칸 구분 없음) — 사건으로만 말한다.
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
        // unit 8 — 주기적 통통 튕김(idle 노이즈)은 은퇴. 같은 임펄스 세기를 «회차 획득»
        // 사건의 1회 들썩(Hop)에만 쓴다. 씬이 authored 2.1 을 들고 있어 이름만 옮긴다.
        [FormerlySerializedAs("figureJostleStrength")]
        [SerializeField] private float figureHopStrength = 2.1f;
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

        // 2026-08-19 사용자 결정 — **항아리 탭 진입구를 끈다.** 드림캐쳐 손패는 이제 유닛
        // 선택으로만 열린다(DcInspectController.Select → DreamcatcherHandView.OpenForSelection).
        // 항아리는 각성치 판독 표면(큰 숫자·채움·피규어·ready 림)으로 그대로 남는다.
        //
        // 끄면 히트도 함께 놓는다(raycastTarget=false) — 그러지 않으면 손패가 열린 동안
        // 항아리 위가 dismiss 캐처에 닿지 않는 죽은 구역이 된다. 놓아주면 그 탭이 캐처로
        // 내려가 «바깥 탭 = 닫기» 가 항아리 위에서도 성립한다.
        //
        // ⚠ [SerializeField] 로 노출하지 않는다 — 인스펙터에서 켜고 씬을 저장하면 그 값이
        // 조용히 리포에 박힌다(DcInspectController.RelocationEnabled 와 같은 사유).
        // 진실원은 이 줄 하나다. true 로 되돌리면 Toggled 배선째 종전 동작이 부활한다.
        private static readonly bool JarTapEnabled = false;

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
        private int _lastShown = -1;
        private float _normalized;
        private float _readyThreshold = 1f;
        private bool _ready;
        private Coroutine _punch;
        private Coroutine _gain;
        private Coroutine _pulse;
        private Coroutine _overflow;
        // unit 8 — 회차 획득 한방(림 골드 플래시 + 독 미세 팝 + 피규어 들썩).
        private Coroutine _chargeBurst;
        private int _unitCost;   // 한 회분 = 가장 싼 카드 코스트(데이터 파생)

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
            if (_visualRoot != null) { _visualRoot.localScale = Vector3.one; _visualRoot.localRotation = Quaternion.identity; }
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
                g = SpineFigureBuilder.Build(
                    go, representativeUnit, figureSkeletonMaterial, figureAnimation);
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

        private void ApplyPanelVisibility()
        {
            if (_panel == null) return;
            // 캐시한 _phase 를 읽는다(GameManager.Instance.CurrentPhase 직독 금지 —
            // 같은 PhaseChanged 이벤트 안에서 구독자 순서에 따라 값이 갈린다).
            if (_phase == GamePhase.Battle)
            {
                _panel.SetActive(true);
                // 전투 진입마다 한 회분 코스트를 다시 뽑는다. Awake 에 굳혀두면 로비의 시트
                // 임포트로 코스트가 바뀐 세션에서 회차 연출이 영영 안 터진다(_unitCost 0/구값).
                ResolveReadyThreshold();
                Refresh(handController != null ? handController.Gauge : 0, animate: false);
            }
            else
            {
                CancelFlights(); // 전투 이탈 → 진행 비행 무효화(전환 중 고아 고스트 방지)
                _panel.SetActive(false);
            }
        }

        private void OnGaugeChanged(int value) => Refresh(value, animate: true);

        private void Refresh(int value, bool animate)
        {
            if (_valueLabel == null) return;
            int max = handController != null ? handController.GaugeMax : 100;
            _valueLabel.text = value.ToString();
            _normalized = max > 0 ? Mathf.Clamp01((float)value / max) : 0f;
            // unit 6 — 단색 backing 제거(스프라이트 늘린 인상). 피규어 더미가 유일한 채움.
            // unit 3 — 증가분은 흡수 비행(OnAwakeningGainedAt)이 채운다. 여기선 감소(소비/리셋)만
            // 정착 피규어를 줄여 목표에 맞춘다(비행 중인 건 도착 시 self-correct).
            TrimToTarget(value);

            int delta = _lastShown >= 0 ? value - _lastShown : 0;
            bool live = _panel != null && _panel.activeInHierarchy;
            if (animate && delta > 0 && live)
            {
                if (_gain != null) StopCoroutine(_gain);
                _gain = StartCoroutine(ShowGain(delta));
            }

            // unit 8 — 큰 숫자는 킬마다 «조용히» 갱신된다(예전엔 1점만 변해도 punch 가 튀었다).
            // 연출은 오직 여기 — 각성치가 한 회분 경계를 넘어 «쓸 수 있는 횟수» 가 오른 순간.
            // 두 회분을 한 번에 넘겨도 한방으로 합친다(연출 중복 금지). _lastShown < 0 인
            // 최초 표시(전투 진입 시 gaugeStart)는 사건이 아니라 상태라 터뜨리지 않는다.
            if (animate && live && _lastShown >= 0
                && AwakeningCharge.CountOf(value, _unitCost) > AwakeningCharge.CountOf(_lastShown, _unitCost))
            {
                if (_chargeBurst != null) StopCoroutine(_chargeBurst);
                _chargeBurst = StartCoroutine(ChargeBurstRoutine());
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
                // unit 7 — 강조 시점을 100(가득)이 아니라 ready(=최소 코스트 20)로. ready 면 골드,
                // 아직 못 쓰면 보라. ready/open 은 알파(발화 강도)로 표현.
                Color c = _ready ? maxColor : rimColor;
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
            hitGraphic.raycastTarget = JarTapEnabled;
            var button = _panel.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitGraphic;
            button.interactable = JarTapEnabled;
            if (JarTapEnabled)
            {
                button.onClick.AddListener(() =>
                {
                    SoundManager.Instance?.PlayUiTick();
                    Toggled?.Invoke();
                });
            }

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
            // unit 8 — 코스트 눈금(—— 20)도 제거. 회차를 «선» 으로 그리지 않기로 했고(사용자
            // 결정), "지금 쓸 수 있다" 는 이미 골드 림이 말한다. 항아리 안은 채움만 남는다.

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
            _pile.SetHopStrength(figureHopStrength);

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

        // 한 회분(=가장 싼 카드 코스트)을 데이터에서 뽑는다. ready 림 임계와 회차 연출
        // 트리거가 같은 값을 쓴다 — «쓸 수 있게 된 순간» 은 하나여야 하기 때문.
        private void ResolveReadyThreshold()
        {
            var cfg = handController != null ? handController.Config : null;
            int max = handController != null ? handController.GaugeMax : 100;
            _unitCost = cfg != null ? AwakeningCharge.UnitCost(cfg.costSquad, cfg.costUnit, cfg.costActive) : 0;
            _readyThreshold = (_unitCost <= 0 || max <= 0) ? 1f : Mathf.Clamp01((float)_unitCost / max);
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

        // unit 8 — 회차 획득 한방. 이 독의 유일한 자발적 움직임이다.
        // 숫자 punch + 독 미세 팝 + 림 골드 플래시 + 피규어 1회 들썩을 0.3초 안에 끝내고
        // 평소 상태(완전 정지)로 돌아온다. 피규어 들썩은 예전 idle 어필 루프에서 떼어와
        // 여기 사건에 붙인 것(Hop) — 상시 노이즈가 아니라 «칸이 하나 잠겼다» 의 촉감.
        private IEnumerator ChargeBurstRoutine()
        {
            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(PunchValue());
            _pile?.Hop();

            const float duration = 0.3f;
            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                // 솟았다 가라앉는 한 번의 팝(오버슛 없이 절제 — 판독면이지 버튼이 아니다).
                float bump = Mathf.Sin(k * Mathf.PI);
                if (_visualRoot != null)
                    _visualRoot.localScale = Vector3.one * (1f + bump * 0.08f);
                if (_rim != null)
                {
                    var c = maxColor;
                    c.a = Mathf.Max(_rim.color.a, bump);
                    _rim.color = c;
                }
                yield return null;
            }
            if (_visualRoot != null) _visualRoot.localScale = Vector3.one;
            _chargeBurst = null;
            UpdateVisualState(); // 림을 정적 상태색으로 복원
        }

        private IEnumerator OverflowFlashRoutine()
        {
            // unit 8 — 좌우 흔들림은 제거하고 림 플래시만 남긴다(평소 정지하는 판독면과
            // 같은 어휘). 낭비가 일어나는 사건 자체는 손실회피 때문에 계속 알린다.
            const float duration = 0.6f;
            float time = 0f;
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
                yield return null;
            }
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
