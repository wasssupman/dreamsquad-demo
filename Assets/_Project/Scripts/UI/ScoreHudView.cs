using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // Runtime-built live score, screen top-center just below the timer. Increments
    // per enemy kill (BattleBridge.DrainEnemyKilledEvents -> OnEnemyKilled). Stylish
    // increase driven by PrimeTween: an elastic punch + white-hot->gold flash, using
    // the Kanit Bold Italic SDF font (dynamic sporty oblique, deliberately distinct
    // from the Bangers SDF damage popups).
    // Same-frame kills (AoE wipes) coalesce into one intensity-scaled hit.
    //
    // score-tally-sequence unit 0 — 더 이상 표시 전용이 아니다. 이 숫자는 최종 점수의
    // **킬축과 같은 값**이고(잡은 마리 수 — kill-race unit 1), 결과 연출이 여기서
    // 이어서 시간·스트레스를 더한다.
    public class ScoreHudView : MonoBehaviour
    {
        [Header("Style font (Kanit Bold Italic SDF + outline). Null -> TMP default.")]
        [SerializeField] private TMP_FontAsset scoreFont;
        [SerializeField] private Material scoreMaterial;

        [Header("Increase feel")]
        [Tooltip("표시 숫자가 목표로 따라붙는 속도(클수록 빠름)")]
        [SerializeField] private float rollLerp = 14f;
        [Tooltip("처치 시 펀치 스케일 배수(1=없음)")]
        [SerializeField] private float punchScale = 1.5f;
        [Tooltip("펀치/플래시 지속(초)")]
        [SerializeField] private float punchDuration = 0.28f;
        [Tooltip("같은 프레임 다처치(AoE)마다 펀치 강도 증가분")]
        [SerializeField] private float multiKillBoost = 0.35f;
        [Tooltip("다처치 펀치 강도 상한 배수")]
        [SerializeField] private float maxMultiKillBoost = 2.5f;
        [Tooltip("처치 순간 화이트핫 플래시 색")]
        [SerializeField] private Color flashColor = new Color(1f, 0.97f, 0.85f);
        [Tooltip("안착 리치 골드 색")]
        [SerializeField] private Color baseColor = new Color(1f, 0.78f, 0.28f);

        [Header("Impact burst (procedural gold spark quads)")]
        [SerializeField] private ScoreBurstStyle burst = new ScoreBurstStyle();

        [Header("Glow & shine (additive, Wassup/UI/Additive)")]
        [Tooltip("Additive UI 머티리얼. Null → 기본(알파 블렌드).")]
        [SerializeField] private Material additiveMaterial;
        [Tooltip("소프트 라디얼 글로우 스프라이트")]
        [SerializeField] private Sprite glowSprite;
        [Tooltip("소프트 세로 바 샤인 스프라이트")]
        [SerializeField] private Sprite shineSprite;
        [SerializeField] private Color glowColor = new Color(1f, 0.68f, 0.24f, 1f);
        [SerializeField] private float glowSize = 200f;
        [Tooltip("평상시 은은한 글로우 알파 (숫자 가독 우선 — 낮게)")]
        [SerializeField] private float glowRestAlpha = 0.05f;
        [Tooltip("처치 순간 플래시 글로우 알파 (숫자를 덮지 않게 절제)")]
        [SerializeField] private float glowFlashAlpha = 0.22f;
        [SerializeField] private float glowFlashDuration = 0.35f;
        [SerializeField] private float glowPulseScale = 1.35f;
        [Tooltip("은은한 글린트 — 알파 낮게(직선 이동이 튀지 않게)")]
        [SerializeField] private Color shineColor = new Color(1f, 0.96f, 0.82f, 0.22f);
        [SerializeField] private float shineWidth = 24f;
        [Tooltip("대각 기울기(도)")]
        [SerializeField] private float shineTiltDeg = 18f;
        [Tooltip("좌→우 스윕 거리(px)")]
        [SerializeField] private float shineTravel = 340f;
        [Tooltip("스윕 시간 — 짧게(빠른 섬광)")]
        [SerializeField] private float shineDuration = 0.25f;

        [Header("Screen feedback")]
        [Tooltip("처치 시 패널 UI-space 셰이크 강도(px). 배틀 카메라는 안 건드림.")]
        [SerializeField] private float kickStrength = 11f;
        [SerializeField] private float kickDuration = 0.3f;
        // score-tally-sequence unit 0 — 가장자리 플래시는 **순간 화력** 기준이다.
        // 원래는 누계 N 점마다(milestoneInterval) 터졌는데, 킬점수 축척으로 바뀐 뒤
        // 간격을 키우니 보스에서만 터져 거의 안 보였다. "몰아친 순간"에 터지는 게 타격감에 맞다.
        //
        // three-minute-survival unit 3 — **점수 단위가 100 → 1 로 바뀌어 임계를 다시 잡았다**
        // (일반 1 / 엘리트 3 / 보스 10). 옛 300 을 그대로 두면 한 판 총점이 수십~수백인데
        // 1초 창 합계가 300 을 넘을 일이 없어 플래시가 영원히 안 터진다.
        // 잡몹 4기 동시 처치(4) 또는 보스 1기(10) 정도가 "몰아친 순간"이다.
        [Tooltip("이 시간 안에 번 점수를 합산한다(초)")]
        [SerializeField] private float burstWindowSec = 1f;
        [Tooltip("윈도우 합계가 이 값 이상이면 가장자리 플래시. 일반 1 / 엘리트 3 / 보스 10 기준.")]
        [SerializeField] private int burstScoreThreshold = 4;
        [Tooltip("풀스크린 비네트 스프라이트(가장자리 밝음). Null → 플래시 생략.")]
        [SerializeField] private Sprite vignetteSprite;
        [SerializeField] private Color milestoneColor = new Color(1f, 0.8f, 0.35f, 1f);
        [SerializeField] private float milestoneFlashAlpha = 0.5f;
        [SerializeField] private float milestoneDuration = 0.55f;

        [Header("Sound (SoundManager)")]
        [Tooltip("처치 틱 기본 피치")]
        [SerializeField] private float soundPitchBase = 1f;
        [Tooltip("빠른 연속 처치 시 피치 상한")]
        [SerializeField] private float soundPitchMax = 1.7f;
        [Tooltip("처치당 피치 상승분(연속 시 누적)")]
        [SerializeField] private float soundPitchPerKill = 0.06f;
        [Tooltip("피치 heat 감쇠(1/s) — 처치 멈추면 기본 피치로")]
        [SerializeField] private float soundHeatDecay = 1.4f;

        [Header("Layout")]
        // The star of the HUD: the timer no longer stacks above the score (it moved
        // to the bottom-right NextWaveDock), so the score owns the top-center and sits
        // near the top edge, larger than before.
        [SerializeField] private float valueFontSize = 104f;
        [SerializeField] private float captionFontSize = 30f;
        [Tooltip("배지를 화면 우상단 모서리에 붙이고 이만큼 여백(px)")]
        [SerializeField] private float cornerPadding = 36f;

        [Header("Badge plate (static frame — battle-hud Unit 5)")]
        [Tooltip("어두운 반투명 플레이트 fill 색")]
        [SerializeField] private Color plateColor = new Color(0.04f, 0.055f, 0.08f, 0.62f);
        [SerializeField] private Color plateBorderColor = new Color(1f, 0.78f, 0.28f, 0.95f);
        [SerializeField] private float plateBorderWidth = 3f;
        [SerializeField] private float plateCornerRadius = 26f;
        [SerializeField] private Vector2 plateSize = new Vector2(360f, 148f);
        [Tooltip("플레이트 상단이 패널 top 아래로 들어가는 양(탭이 걸치도록)")]
        [SerializeField] private float plateTopInset = 20f;
        [Header("Score tab (SCORE 라벨 배너)")]
        [SerializeField] private Color tabColor = new Color(1f, 0.78f, 0.28f, 1f);
        [SerializeField] private Color tabTextColor = new Color(0.1f, 0.08f, 0.04f, 1f);
        [SerializeField] private Vector2 tabSize = new Vector2(196f, 46f);

        // 웨이브 진행은 원래 좌상단 시간 배지의 **종속 캡션**이었다(작은 회색 한 줄).
        // 시계 밑에 붙어 있으니 «시간에 딸린 부연»으로 읽혔는데, 실제로는 판을 읽는 1급
        // 정보다 — 지금 몇 번째 파도이고 몇 개가 남았는가. 그래서 은퇴한 스트레스 배지가
        // 쓰던 자리와 문법(점수 아래 컴패니언 플레이트 + 골드 탭)을 그대로 물려받는다.
        [Header("Wave badge (score companion)")]
        [SerializeField] private Vector2 wavePlateSize = new Vector2(360f, 64f);
        [SerializeField] private float wavePlateGap = 10f;
        [SerializeField] private Vector2 waveTabSize = new Vector2(112f, 42f);
        [SerializeField] private float waveValueFontSize = 38f;
        [SerializeField] private Color waveValueColor = new Color(1f, 0.9f, 0.66f, 1f);
        [Tooltip("웨이브가 넘어갈 때 숫자 펀치 배수")]
        [SerializeField] private float wavePunchScale = 1.18f;
        [SerializeField] private float wavePunchDuration = 0.22f;
        [Tooltip("웨이브가 넘어가는 순간 플래시 색")]
        [SerializeField] private Color waveFlashColor = Color.white;

        // next-wave-dock-legibility rev 8 — **상단 중앙 대형 카운트다운.**
        //
        // rev 6·7 은 최상단 12px 게이지였고 「전혀 원하던 게 아니다, 잘 보이지도 않는다」로
        // 반려됐다. 원인 셋:
        //  ① **바가 정보를 안 준다.** 폭 ~1860px 에 3분을 담으면 1초가 1px 도 안 움직인다.
        //     결국 진짜 정보는 26pt 숫자 하나였는데, 그건 이 HUD 에서 가장 작은 글자다
        //     (점수 104 · 스트레스 38).
        //  ② **시선을 끌어줄 이웃이 없다.** 우상단 점수는 처치마다 펀치·플래시·버스트가
        //     터져 눈이 자연히 간다. 최상단 띠는 혼자 있었다.
        //  ③ **시각 문법이 달랐다.** 이 HUD 에서 «중요한 값»은 전부 남색 플레이트 + 골드 탭 +
        //     굵은 숫자다. 타이머만 얇은 트랙 바를 써서 무의식적으로 «배경 크롬»으로 읽혔다.
        //
        // 「시간=자원이니 게이지」라는 전제가 어긋났다. 게이지는 HP·마나처럼 **직접 채우고
        // 쓰는** 자원의 문법이고, 이 타이머는 **한 방향으로 흐르며 판단을 트리거하는 시계**다.
        // 경쟁전 시계의 관례는 큰 숫자다.
        //
        // 그래서 **점수와 같은 플레이트+탭 문법**을 재사용한다 — 학습 없이 «이것도 1급 스탯»
        // 으로 읽힌다. 자리는 비어 있던 상단 중앙.
        [Header("Match timer badge (좌상단 — 메뉴 아이콘 아래)")]
        [Tooltip("씬 MenuButton 을 햄버거 아이콘으로 줄인 크기. 시간 배지가 그 아래로 내려온다.")]
        [SerializeField] private float menuIconSize = 72f;
        [Tooltip("메뉴 아이콘과 시간 배지 사이 간격")]
        [SerializeField] private float menuGap = 12f;
        [SerializeField] private Vector2 timerPlateSize = new Vector2(320f, 150f);
        [SerializeField] private Vector2 timerTabSize = new Vector2(150f, 46f);
        [SerializeField] private float timerValueFontSize = 76f;
        [Tooltip("10초 이하에서 자릿수를 줄이고 이 크기로 키운다")]
        [SerializeField] private float timerFinalFontSize = 96f;
        [SerializeField] private float timerCaptionFontSize = 20f;
        [SerializeField] private Color timerNormalColor = Color.white;
        [Tooltip("30초 이하")]
        [SerializeField] private Color timerWarnColor = new Color(1f, 0.72f, 0.24f, 1f);
        [Tooltip("10초 이하")]
        [SerializeField] private Color timerFinalColor = new Color(1f, 0.33f, 0.28f, 1f);
        [Header("Heart stress rim (heart-stress-axis unit 3)")]
        // ⚠ 타이머 마지막 10초 연출(붉은 대형 숫자 + 매초 붉은 비네트)과 **종반에 정확히
        // 겹친다** — 스트레스는 판 후반에 가장 높기 쉽다. 그래서 형태로 가른다:
        // 타이머는 **중앙 비네트 원샷**, 스트레스는 **가장자리 림 지속**. 색도 살짝 어긋나게.
        [SerializeField] private Color stressRimColor = new Color(0.86f, 0.09f, 0.14f, 1f);
        [Tooltip("수위 100%일 때 림의 최대 알파(지속). 판을 가리면 안 되므로 낮게.")]
        [SerializeField, Range(0f, 1f)] private float stressRimMaxAlpha = 0.3f;
        [Tooltip("스트레스가 오른 프레임의 추가 알파(순간).")]
        [SerializeField, Range(0f, 1f)] private float stressSpikeAlpha = 0.45f;
        [Tooltip("스파이크가 사라지는 데 걸리는 초.")]
        [SerializeField, Min(0.05f)] private float stressSpikeDecaySec = 0.35f;
        [Tooltip("스파이크가 최대가 되는 상승분(스트레스 0~100 기준). 이 값 이상이면 포화.")]
        [SerializeField, Min(0.5f)] private float stressSpikeFullRise = 8f;

        [SerializeField] private float timerWarnSeconds = 30f;
        [SerializeField] private float timerFinalSeconds = 10f;
        [Tooltip("초가 바뀔 때 pop 강도(평시 / 30초 / 10초)")]
        [SerializeField] private float timerTick = 0.12f;
        [SerializeField] private float timerTickWarn = 0.20f;
        [SerializeField] private float timerTickFinal = 0.35f;
        [Tooltip("30초·10초 구간의 숨쉬기 배율 / 주기(초)")]
        [SerializeField] private Vector2 timerBreathWarn = new Vector2(1.06f, 1.0f);
        [SerializeField] private Vector2 timerBreathFinal = new Vector2(1.12f, 0.4f);

        private GameObject _panel;
        private Vector2 _panelBasePos;
        private Image _plateImage;
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _value;
        private RectTransform _valueRect;
        // rev 8 — 상단 중앙 카운트다운 배지
        private GameObject _timerRoot;
        private RectTransform _timerPlateRect;
        private TextMeshProUGUI _timerValue;
        private RectTransform _timerValueRect;
        private TextMeshProUGUI _waveValue;
        private RectTransform _waveValueRect;
        private Tween _wavePunchTween, _waveColorTween;
        private Tween _timerTickTween, _timerBreathTween;
        private int _lastTimerSec = -1;
        private int _lastWaveNumber = -2;
        // 0 = 평시 · 1 = 30초 · 2 = 10초. 구간이 바뀔 때만 색·숨쉬기·폰트를 다시 건다.
        private int _timerStage = -1;
        private bool _built;
        private bool _subscribed;

        private int _targetScore;
        private float _shownScore;
        private int _pendingKills;
        private Tween _punchTween;
        private Tween _colorTween;
        private ScoreBurstPool _burstPool;
        private Image _glowImage;
        private RectTransform _glowRect;
        private Image _shineImage;
        private RectTransform _shineRect;
        private float _glowFlash;
        private float _shineT = 2f;
        private float _shineBaseY;
        private Image _vignetteImage;

        // heart-stress-axis unit 3 — 마음 스트레스 화면 연출. **비네트에 얹지 않는다.**
        // `FlashVignette` 는 원샷 페이드 모델이고 소비자가 이미 둘(점수 마일스톤 · 타이머
        // 마지막 10초)인데 `_milestoneFlash` 하나를 Mathf.Max 로 다툰다. 스트레스는
        // **지속 상태**라 성격이 다르고, 셋째가 끼면 서로를 먹는다.
        private Image _stressRimImage;
        private float _stressLevel;   // 0~1 수위 — (b) 맥박
        private float _stressSpike;   // 0~1 순간 — (a) 개별 튐, 지수 감쇠
        private float _stressPulse = 1f;
        private float _milestoneFlash;
        // 비네트를 두 곳이 쓴다(점수 마일스톤 · 10초 카운트다운) — 색이 달라서 틴트를 든다.
        private Color _vignetteTint;
        // 최근 획득 이력 (시각, 점수). burstWindowSec 을 지난 항목은 버린다.
        private readonly System.Collections.Generic.List<(float time, int points)> _burstWindow = new();
        // 한 번 터진 뒤 재무장까지의 쿨다운 — 지속 사격 중 매 프레임 터지는 걸 막는다.
        private float _burstCooldownUntil;
        private Tween _kickPosTween;
        private float _soundHeat;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopFeedbackTweens();
        }

        private void Update()
        {
            EnsureSubscribed();

            if (_panel == null || !_panel.activeSelf)
            {
                PushShakeHeat(0f); // 패널 꺼짐(비배틀) — 카메라 셰이크 잔류 방지
                return;
            }

            // Count-up roll: ease the shown number toward the target (unscaled so it
            // keeps climbing during the timeScale=0 drag-catcher modal).
            _shownScore = Mathf.Lerp(_shownScore, _targetScore, Mathf.Clamp01(Time.unscaledDeltaTime * rollLerp));
            if (Mathf.Abs(_targetScore - _shownScore) < 0.5f) _shownScore = _targetScore;
            if (_value != null) _value.text = Mathf.CeilToInt(_shownScore).ToString();

            _burstPool?.Tick(Time.unscaledDeltaTime);
            UpdateGlowShine(Time.unscaledDeltaTime);
            if (_soundHeat > 0f)
                _soundHeat = Mathf.Max(0f, _soundHeat - soundHeatDecay * Time.unscaledDeltaTime);

            // camera-direction unit 2 — 킬 스트릭 heat 를 카메라 셰이크로 미러(소유·산정은 여기,
            // Director 는 소비만). heat 상승은 본 컴포넌트 LateUpdate(킬 flush, order 0)에서
            // 일어나 Director(-90) LateUpdate 이후이므로, 다음 프레임 Update 푸시 → 같은 프레임
            // Director 소비 = 지연 정확히 1프레임(허용 계약). 감쇠분은 같은 프레임 반영.
            PushShakeHeat(_soundHeat);
        }

        private Wassup.Presentation.CameraDirector _cameraDirector;
        private bool _cameraDirectorMissWarned;

        private void PushShakeHeat(float heat)
        {
            if (_cameraDirector == null)
            {
                if (_cameraDirectorMissWarned) return;
                var cam = Camera.main;
                if (cam == null) return;
                _cameraDirector = cam.GetComponent<Wassup.Presentation.CameraDirector>();
                if (_cameraDirector == null)
                {
                    Debug.LogWarning("[ScoreHudView] CameraDirector 미배선 — 킬 스트릭 셰이크 생략.", this);
                    _cameraDirectorMissWarned = true;
                    return;
                }
            }
            float range = Mathf.Max(0.0001f, soundPitchMax - soundPitchBase);
            _cameraDirector.SetShakeHeat(heat / range);
        }

        // Flush accumulated kills once per frame (after all Update() drains), so an
        // AoE wipe that calls OnEnemyKilled many times this frame produces one scaled
        // slam rather than N stacked punches.
        private void LateUpdate()
        {
            if (_panel == null || !_panel.activeSelf) return;
            if (_pendingKills <= 0) return;
            int k = _pendingKills;
            _pendingKills = 0;
            TriggerHit(k);
        }

        // Called by BattleBridge once per enemy kill drained from EnemyKilledEvents.
        // Accumulates only; the visual hit is flushed in LateUpdate (see above).
        //
        // three-minute-kill-race unit 1 — **1킬 = 1점이라 가산량 인자가 사라졌다.**
        // 이 HUD 숫자가 곧 최종 점수이고 서버에 올라가는 수다(가공 지점이 하나도 없다).
        // 이력: 예전엔 처치당 고정 10 → 적별 killScore(잡몹 100 / 보스 2,000) → 티어
        // (1/3/10) 를 거쳤다. 그 축이 은퇴하면서 «몇 마리 잡았나» 하나로 수렴했다.
        public void OnEnemyKilled() => AddScore(1);

        // score-tally-sequence unit 2 — 처치가 아닌 가산(결과 연출의 축 합산).
        // 펀치·플래시·버스트 판정을 처치와 똑같이 태운다: 큰 값이 한 번에 들어오면
        // 버스트 임계를 넘겨 가장자리 플래시가 터지는데, 합산 순간의 타격감으로 알맞다.
        public void AddScore(int points)
        {
            int p = Mathf.Max(0, points);
            if (p <= 0) return;
            _targetScore += p;
            _pendingKills++;
            // 유입 시점에 기록한다 — 판정은 프레임당 1회 flush(TriggerHit)에서.
            _burstWindow.Add((Time.unscaledTime, p));
        }

        // 연출이 롤업 완료를 기다릴 때 쓴다(표시 숫자가 목표에 붙었는가).
        public bool RollSettled => Mathf.Abs(_targetScore - _shownScore) < 0.5f;

        // score-tally-sequence unit 2 rev — 값 변화 없는 시선 유도 펄스.
        // 마지막 적이 죽은 자리(보드 중앙)에 시선이 있는 상태로 합산이 시작되면 첫 축을
        // 놓친다. 숫자가 움직이기 전에 배지를 한 번 때려 "여기를 봐라"를 만든다.
        //
        // 버스트 창을 먼저 비운다: 안 그러면 직전 킬들이 남긴 점수를 읽어 **값이 하나도
        // 안 늘었는데 가장자리 플래시가 오발한다.** 지금은 펄스 시점이 창(1초)을 겨우
        // 넘겨 우연히 안 터지지만, preRollSec 를 조금만 줄여도 드러난다.
        public void PulseAttention()
        {
            _burstWindow.Clear();
            TriggerHit(1);
        }

        // 최근 burstWindowSec 안에 번 점수가 임계 이상이면 가장자리 플래시.
        // 지속 사격 중 매 프레임 터지지 않도록 플래시 지속시간만큼 쿨다운을 둔다.
        private void TryBurstFlash()
        {
            if (burstScoreThreshold <= 0 || burstWindowSec <= 0f) return;

            float now = Time.unscaledTime;
            float cutoff = now - burstWindowSec;
            int i = 0;
            while (i < _burstWindow.Count && _burstWindow[i].time < cutoff) i++;
            if (i > 0) _burstWindow.RemoveRange(0, i);

            if (now < _burstCooldownUntil) return;

            int sum = 0;
            for (int k = 0; k < _burstWindow.Count; k++) sum += _burstWindow[k].points;
            if (sum < burstScoreThreshold) return;

            _vignetteTint = milestoneColor;   // 카운트다운 플래시와 색이 다르다
            _milestoneFlash = 1f;
            _burstCooldownUntil = now + Mathf.Max(0.05f, milestoneDuration);
        }

        /// <summary>
        /// rev 7 — 최상단 바. 브리지가 매 프레임 밀어준다(HUD 가 브리지를 참조하지 않는
        /// 기존 방향 — OnEnemyKilled 와 같다).
        /// </summary>
        /// <param name="totalSec">판 전체 길이. 0 이하 = 무한 → 바를 숨긴다.</param>
        /// <summary>
        /// rev 8 — 상단 중앙 카운트다운. 브리지가 매 프레임 밀어준다(HUD 는 브리지를 모른다 —
        /// OnEnemyKilled 와 같은 방향).
        /// </summary>
        /// <param name="totalSec">판 전체 길이. 0 이하 = 무한 → 배지를 통째로 숨긴다.</param>
        public void SetTopBar(float remainingSec, float totalSec, int waveNumber)
        {
            // 웨이브 배지는 시계와 **다른 배지**다 — 시계의 무한모드 게이트(아래 early
            // return) 뒤에 두면 무한 판에서 파도 수가 영영 안 갱신된다.
            RefreshWaveBadge(waveNumber);

            if (_timerRoot == null) return;

            // 무한 모드는 «남은 시간»이 없다 — 시계를 그리면 거짓말이 된다.
            bool show = totalSec > 0f;
            if (_timerRoot.activeSelf != show) _timerRoot.SetActive(show);
            if (!show) return;

            float remaining = Mathf.Max(0f, remainingSec);
            int stage = remaining <= timerFinalSeconds ? 2
                      : remaining <= timerWarnSeconds ? 1 : 0;
            if (stage != _timerStage) ApplyTimerStage(stage);

            int min = (int)(remaining / 60f);
            int sec = (int)(remaining % 60f);
            int shownSec = min * 60 + sec;
            if (shownSec == _lastTimerSec) return;

            // 10초 이하는 **자릿수를 줄인다**(`0:09` → `9`). 파이널 카운트다운 관례이고,
            // 같은 폭에 글자가 하나면 그만큼 크게 넣을 수 있다.
            _timerValue.text = stage == 2 ? $"{sec}" : $"{min}:{sec:D2}";

            // 매초 pop. 구간이 올라갈수록 세진다 — 「몇 초 남았나」가 아니라 「급하다」가 몸에 온다.
            // 첫 표시(-1)엔 생략, unscaled 라 정지·슬로우 중에도 동작.
            if (_lastTimerSec >= 0)
            {
                if (_timerTickTween.isAlive) _timerTickTween.Stop();
                _timerValueRect.localScale = Vector3.one;
                float strength = stage == 2 ? timerTickFinal : stage == 1 ? timerTickWarn : timerTick;
                _timerTickTween = Tween.PunchScale(_timerValueRect, Vector3.one * strength,
                    stage == 2 ? 0.32f : 0.24f, useUnscaledTime: true);

                // 10초 구간은 매초 화면 가장자리도 함께 친다 — 트레이를 보고 있어도
                // 주변시로 잡힌다(점수의 마일스톤 플래시와 같은 비네트를 재사용).
                if (stage == 2) FlashVignette(timerFinalColor, 0.6f);
            }
            _lastTimerSec = shownSec;
        }

        /// <summary>heart-stress-axis unit 3 — 마음 스트레스 화면 연출.
        /// <paramref name="stress01"/> 수위(0~1) · <paramref name="pulse"/> 보드 잠식과 **같은
        /// 맥박 값**(위상 동기 — 계산 주체가 하나여야 화면과 보드가 같이 뛴다) ·
        /// <paramref name="riseAmount"/> 이번 프레임의 **넷 상승분**(0~100 스케일).
        ///
        /// ⚠ 「피해량」이 아니라 「넷 상승분」인 이유: 마음 피해를 실어 나르는 이벤트가 없어
        /// 브리지가 폴링한다. 같은 프레임에 악몽을 잡아 회복이 상쇄하면 **안 튀는 것이 옳다**
        /// — 실제로 스트레스가 안 올랐기 때문이다.</summary>
        public void SetHeartStress(float stress01, float pulse, float riseAmount)
        {
            _stressLevel = Mathf.Clamp01(stress01);
            _stressPulse = pulse;
            if (riseAmount > 0f)
                _stressSpike = Mathf.Max(_stressSpike,
                    Mathf.Clamp01(riseAmount / Mathf.Max(0.5f, stressSpikeFullRise)));
        }

        // 가장자리 플래시 — 점수 마일스톤과 같은 비네트를 재사용한다. 시선이 트레이에 있어도
        // 주변시로 잡히는 유일한 채널이라 마지막 10초에 매초 친다(강도는 낮게).
        private void FlashVignette(Color tint, float strength)
        {
            if (_vignetteImage == null) return;
            _vignetteTint = tint;
            _milestoneFlash = Mathf.Max(_milestoneFlash, strength);
        }

        // 구간이 바뀔 때만 색·폰트·숨쉬기를 다시 건다(매초 다시 걸면 트윈이 계속 끊긴다).
        private void ApplyTimerStage(int stage)
        {
            _timerStage = stage;
            var c = stage == 2 ? timerFinalColor : stage == 1 ? timerWarnColor : timerNormalColor;
            _timerValue.color = c;
            _timerValue.fontSize = stage == 2 ? timerFinalFontSize : timerValueFontSize;
            _lastTimerSec = -1;   // 자릿수 표기가 바뀌므로 다음 프레임에 다시 조립

            if (_timerBreathTween.isAlive) _timerBreathTween.Stop();
            _timerPlateRect.localScale = Vector3.one;
            if (stage == 0) return;

            var b = stage == 2 ? timerBreathFinal : timerBreathWarn;
            _timerBreathTween = Tween.Scale(_timerPlateRect, b.x, b.y,
                Ease.InOutSine, cycles: -1, CycleMode.Yoyo, useUnscaledTime: true);
        }

        // 점수와 **같은 문법**으로 짓는다: 남색 플레이트 + 골드 탭 + 굵은 숫자.
        // 다른 문법을 쓰면 «배경 크롬»으로 읽힌다(rev 6·7 이 그랬다).
        private void BuildTimerBadge(Transform safeAreaRoot)
        {
            _timerRoot = new GameObject("MatchTimerBadge", typeof(RectTransform));
            _timerRoot.transform.SetParent(safeAreaRoot, false);
            // rev 9 — **좌상단, 메뉴 버튼 아래**다.
            //
            // rev 8 은 상단 중앙이었고 「맵 영역을 가린다」로 반려됐다 — 보드가 화면 중앙을
            // 차지하므로 **중앙 상단도 보드 위**다. 큰 배지를 놓을 수 있는 곳은 코너뿐이고
            // 우상단은 점수가 이미 쓴다.
            //
            // 좌상단에는 씬의 `MenuButton`(햄버거)이 먼저 있다. 그 아래에 **왼쪽 변을 맞춰**
            // 세로로 정렬한다 — 코너 하나를 두 위젯이 나눠 쓰되 한 줄로 읽히게(사용자 결정).
            var rootRt = (RectTransform)_timerRoot.transform;
            rootRt.anchorMin = new Vector2(0f, 1f);
            rootRt.anchorMax = new Vector2(0f, 1f);
            rootRt.pivot = new Vector2(0f, 1f);
            rootRt.anchoredPosition = new Vector2(cornerPadding, -(cornerPadding + menuIconSize + menuGap));
            rootRt.sizeDelta = timerPlateSize;

            var plate = MakeSolidImage("TimerPlate", _timerRoot.transform);
            plate.sprite = MakeRoundedRectSprite(
                plateCornerRadius, plateBorderWidth, plateColor, plateBorderColor);
            plate.type = Image.Type.Sliced;
            _timerPlateRect = plate.rectTransform;
            _timerPlateRect.anchorMin = Vector2.zero;
            _timerPlateRect.anchorMax = Vector2.one;
            _timerPlateRect.offsetMin = Vector2.zero;
            _timerPlateRect.offsetMax = Vector2.zero;

            var tabGO = new GameObject("TimerTab", typeof(RectTransform), typeof(Image));
            tabGO.transform.SetParent(_timerRoot.transform, false);
            var tabImg = tabGO.GetComponent<Image>();
            tabImg.sprite = MakeRoundedRectSprite(timerTabSize.y * 0.5f, 0f, tabColor, tabColor);
            tabImg.type = Image.Type.Sliced;
            tabImg.raycastTarget = false;
            var tabRt = tabImg.rectTransform;
            tabRt.anchorMin = new Vector2(0.5f, 1f);
            tabRt.anchorMax = new Vector2(0.5f, 1f);
            tabRt.pivot = new Vector2(0.5f, 0.5f);
            tabRt.anchoredPosition = Vector2.zero;   // 플레이트 상단에 걸친다(SCORE 탭과 같다)
            tabRt.sizeDelta = timerTabSize;

            var tabLabel = MakeText("TimerTabLabel", tabGO.transform, captionFontSize,
                new Vector2(0.5f, 0.5f));
            var tlr = tabLabel.rectTransform;
            tlr.anchorMin = Vector2.zero;
            tlr.anchorMax = Vector2.one;
            tlr.offsetMin = Vector2.zero;
            tlr.offsetMax = Vector2.zero;
            tabLabel.text = "TIME";
            tabLabel.fontStyle = FontStyles.Bold;
            tabLabel.color = tabTextColor;

            _timerValue = MakeText("TimerValue", _timerRoot.transform, timerValueFontSize,
                new Vector2(0.5f, 0.5f));
            _timerValueRect = _timerValue.rectTransform;
            _timerValueRect.anchorMin = new Vector2(0.5f, 0.5f);
            _timerValueRect.anchorMax = new Vector2(0.5f, 0.5f);
            _timerValueRect.pivot = new Vector2(0.5f, 0.5f);
            // 웨이브 캡션이 아래를 쓰던 시절엔 +6 으로 올려 잡았다. 캡션이 점수 아래 자기
            // 배지로 나간 뒤로는 그만큼 아래가 비어 시계가 떠 보인다 — TIME 탭이 상단을
            // 잠식하는 만큼 내려서 «남은 안쪽 공간»의 중앙에 둔다.
            _timerValueRect.anchoredPosition = new Vector2(0f, -9f);
            _timerValueRect.sizeDelta = new Vector2(timerPlateSize.x - 24f, timerValueFontSize * 1.3f);
            _timerValue.fontStyle = FontStyles.Bold;
            _timerValue.color = timerNormalColor;
            _timerValue.text = "3:00";

            // 웨이브 진행은 여기 종속 캡션으로 살던 것을 점수 아래 자기 배지로 옮겼다
            // (BuildCanvas 의 WavePlate). 시계는 이제 «남은 시간» 하나만 말한다.
        }

        // 웨이브 배지 갱신. 탭이 「웨이브」를 이미 말하므로 숫자만 쓴다(스트레스 배지와
        // 같은 분업) — 값이 안 바뀌면 조립도 펀치도 하지 않는다.
        //
        // **총 개수(`N / M`)는 내보내지 않는다**(2026-08-20 사용자 지시). 지금 몇 번째
        // 파도인가만 말한다.
        private void RefreshWaveBadge(int waveNumber)
        {
            if (_waveValue == null) return;
            if (waveNumber == _lastWaveNumber) return;

            bool advanced = _lastWaveNumber >= 0 && waveNumber > _lastWaveNumber;
            _lastWaveNumber = waveNumber;
            _waveValue.text = waveNumber.ToString();

            // 파도가 넘어간 순간에만 펀치한다. 첫 표기(판 시작)에는 펀치하지 않는다 —
            // 아무 일도 안 일어났는데 섬광이 뜬다.
            if (!advanced || _panel == null || !_panel.activeSelf)
            {
                _waveValue.color = waveValueColor;
                if (_waveValueRect != null) _waveValueRect.localScale = Vector3.one;
                return;
            }

            if (_wavePunchTween.isAlive) _wavePunchTween.Stop();
            if (_waveColorTween.isAlive) _waveColorTween.Stop();
            _waveValueRect.localScale = Vector3.one;
            _waveValue.color = waveFlashColor;
            float strength = Mathf.Max(0f, wavePunchScale - 1f);
            _wavePunchTween = Tween.PunchScale(_waveValueRect, Vector3.one * strength,
                wavePunchDuration, useUnscaledTime: true);
            _waveColorTween = Tween.Color(_waveValue, waveFlashColor, waveValueColor,
                wavePunchDuration, Ease.OutQuad, useUnscaledTime: true);
        }

        private void TriggerHit(int killCount)
        {
            if (_valueRect == null || _value == null) return;

            float intensity = Mathf.Min(1f + Mathf.Max(0, killCount - 1) * multiKillBoost,
                                        Mathf.Max(1f, maxMultiKillBoost));

            // Elastic slam. PunchScale animates around the current scale and returns to
            // it, so reset to 1 first to avoid drift when a prior punch is interrupted.
            if (_punchTween.isAlive) _punchTween.Stop();
            _valueRect.localScale = Vector3.one;
            float strength = Mathf.Max(0f, punchScale - 1f) * intensity;
            _punchTween = Tween.PunchScale(_valueRect, Vector3.one * strength, punchDuration, useUnscaledTime: true);

            // White-hot -> rich gold flash (distinct from the damage numbers' multicolor).
            if (_colorTween.isAlive) _colorTween.Stop();
            _value.color = flashColor;
            _colorTween = Tween.Color(_value, flashColor, baseColor, punchDuration, Ease.OutQuad, useUnscaledTime: true);

            // Radial gold spark burst from the number center (behind the digits).
            if (_burstPool != null)
            {
                Vector2 center = _valueRect.anchoredPosition + new Vector2(0f, -_valueRect.rect.height * 0.5f);
                _burstPool.Emit(center, killCount);
            }

            // Flare the glow and launch a shine sweep.
            _glowFlash = 1f;
            _shineT = 0f;

            // UI-space panel kick (battle camera is never touched).
            //
            // **회전 펀치는 쓰지 않는다.** PunchLocalRotation 은 «현재» 회전을 기준으로
            // 흔들고 끝날 때 그 기준으로 되돌리는데, 연속 처치는 이 트윈을 중간에 Stop
            // 시킨다 — 되돌림이 안 걸린 각도가 다음 펀치의 기준이 되어 누적되고, 몰아치면
            // 배지가 통째로 뒤집힌 채로 남았다. 위치 흔들림만으로 타격감은 충분하다.
            //
            // 같은 이유로 흔들기 전에 기준 위치로 되돌린다(스케일 펀치가 localScale 을
            // 1 로 되돌리고 시작하는 것과 같은 방어).
            if (_panel != null)
            {
                if (_kickPosTween.isAlive) _kickPosTween.Stop();
                var kickRt = (RectTransform)_panel.transform;
                kickRt.anchoredPosition = _panelBasePos;
                kickRt.localRotation = Quaternion.identity;
                float ks = kickStrength * intensity;
                _kickPosTween = Tween.ShakeLocalPosition(_panel.transform,
                    new Vector3(ks, ks * 0.6f, 0f), kickDuration, useUnscaledTime: true);
            }

            // Burst edge-flash — 최근 burstWindowSec 안에 번 점수가 임계를 넘으면 터진다.
            // 누계 마일스톤(총점 N 단위)이 아니라 **순간 화력** 기준이다: 누계 기준은 킬점수
            // 축척으로 바뀐 뒤 보스에서만 터져서 거의 안 보였다. 잡몹 3기 동시처치나 보스
            // 1기가 같은 무게로 터진다.
            TryBurstFlash();

            // Score tick — pitch climbs on rapid consecutive kills (heat), decays over time.
            _soundHeat = Mathf.Min(_soundHeat + killCount * soundPitchPerKill,
                                   Mathf.Max(0f, soundPitchMax - soundPitchBase));
            SoundManager.Instance?.PlayScoreTick(soundPitchBase + _soundHeat);
        }

        private void UpdateGlowShine(float dt)
        {
            if (_glowImage != null)
            {
                if (_glowFlash > 0f)
                    _glowFlash = Mathf.Max(0f, _glowFlash - dt / Mathf.Max(0.0001f, glowFlashDuration));
                var gc = glowColor;
                gc.a = Mathf.Lerp(glowRestAlpha, glowFlashAlpha, _glowFlash);
                _glowImage.color = gc;
                if (_glowRect != null)
                {
                    float s = Mathf.Lerp(1f, glowPulseScale, _glowFlash);
                    _glowRect.localScale = new Vector3(s, s, 1f);
                }
            }

            if (_shineImage != null && _shineT <= 1f)
            {
                _shineT += dt / Mathf.Max(0.0001f, shineDuration);
                float t = Mathf.Clamp01(_shineT);
                if (_shineRect != null)
                    _shineRect.anchoredPosition = new Vector2(
                        Mathf.Lerp(-shineTravel * 0.5f, shineTravel * 0.5f, t), _shineBaseY);
                var sc = shineColor;
                sc.a = shineColor.a * Mathf.Sin(t * Mathf.PI); // fade in then out
                _shineImage.color = sc;
            }

            // heart-stress-axis unit 3 — 림 = 수위(지속) + 스파이크(순간). 둘을 더한다:
            // 수위만이면 「지금 맞았다」가 안 읽히고, 스파이크만이면 「얼마나 위험한가」가 안 읽힌다.
            if (_stressRimImage != null)
            {
                _stressSpike = Mathf.Max(0f,
                    _stressSpike - dt / Mathf.Max(0.05f, stressSpikeDecaySec));
                float level = stressRimMaxAlpha * _stressLevel * _stressPulse;
                float spike = stressSpikeAlpha * _stressSpike * _stressSpike;   // ease-out
                var rc = stressRimColor;
                rc.a = Mathf.Clamp01(level + spike);
                _stressRimImage.color = rc;
            }

            if (_vignetteImage != null && _milestoneFlash > 0f)
            {
                _milestoneFlash = Mathf.Max(0f, _milestoneFlash - dt / Mathf.Max(0.0001f, milestoneDuration));
                var vc = _vignetteTint;
                vc.a = milestoneFlashAlpha * _milestoneFlash * _milestoneFlash; // ease-out fade
                _vignetteImage.color = vc;
            }
        }

        private void StopFeedbackTweens()
        {
            if (_punchTween.isAlive) _punchTween.Stop();
            if (_colorTween.isAlive) _colorTween.Stop();
            // rev 9 — 타이머 배지 트윈도 여기서 걷는다. **숨쉬기는 `cycles: -1` 무한**이라
            // 빠뜨리면 OnDisable·씬 언로드에서 안 멈추고, 다음 판에 플레이트가 커진 채로
            // 시작하거나 PrimeTween 이 파괴된 rect 를 건드린다.
            if (_timerTickTween.isAlive) _timerTickTween.Stop();
            if (_timerBreathTween.isAlive) _timerBreathTween.Stop();
            if (_timerPlateRect != null) _timerPlateRect.localScale = Vector3.one;
            if (_timerValueRect != null) _timerValueRect.localScale = Vector3.one;
            // 구간 캐시도 함께 — 안 비우면 다음 판이 «이미 30초 구간»으로 시작해 색과
            // 숨쉬기가 안 걸린다(ApplyTimerStage 가 같은 stage 면 건너뛴다).
            if (_wavePunchTween.isAlive) _wavePunchTween.Stop();
            if (_waveColorTween.isAlive) _waveColorTween.Stop();
            if (_waveValueRect != null) _waveValueRect.localScale = Vector3.one;
            if (_waveValue != null) _waveValue.color = waveValueColor;
            _timerStage = -1;
            _lastTimerSec = -1;
            // 다음 판이 같은 «1» 로 시작해도 다시 조립하도록 캐시를 비운다.
            _lastWaveNumber = -2;
            if (_valueRect != null) _valueRect.localScale = Vector3.one;
            if (_value != null) _value.color = baseColor;
            _burstPool?.ClearAll();

            _glowFlash = 0f;
            _shineT = 2f;
            if (_glowImage != null) { var gc = glowColor; gc.a = glowRestAlpha; _glowImage.color = gc; }
            if (_glowRect != null) _glowRect.localScale = Vector3.one;
            if (_shineImage != null) { var sc = shineColor; sc.a = 0f; _shineImage.color = sc; }

            if (_kickPosTween.isAlive) _kickPosTween.Stop();
            if (_panel != null)
            {
                var prt = (RectTransform)_panel.transform;
                prt.anchoredPosition = _panelBasePos;
                prt.localRotation = Quaternion.identity;
            }
            _milestoneFlash = 0f;
            if (_vignetteImage != null) { var vc = milestoneColor; vc.a = 0f; _vignetteImage.color = vc; }
            _stressLevel = 0f; _stressSpike = 0f; _stressPulse = 1f;
            if (_stressRimImage != null) { var sc = stressRimColor; sc.a = 0f; _stressRimImage.color = sc; }
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
            // Apply current phase immediately in case Battle already started.
            OnPhaseChanged(GameManager.Instance.CurrentPhase);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (GameManager.Instance != null) GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Battle)
            {
                _targetScore = 0;
                _shownScore = 0f;
                _pendingKills = 0;
                _burstWindow.Clear();
                _burstCooldownUntil = 0f;
                _soundHeat = 0f;
                StopFeedbackTweens();
                if (_value != null) { _value.text = "0"; _value.color = baseColor; }
                if (_valueRect != null) _valueRect.localScale = Vector3.one;
                if (_panel != null) _panel.SetActive(true);
            }
            // score-tally-sequence unit 1 — Tally(결과 연출)에서는 패널을 유지한다.
            // 이 숫자가 연출의 주인공이다: 킬점수에서 시작해 시간·스트레스가 더해진다.
            // 리셋도 하지 않는다 — 전투에서 쌓인 값을 그대로 이어받아야 한다.
            else if (phase == GamePhase.Tally)
            {
                // **_pendingKills 를 비우지 않는다.** 같은 Update 안에서
                // DrainEnemyKilledEvents → (마감 판정) → SetPhase(Tally) 가 돌기 때문에,
                // 여기서 비우면 LateUpdate 게이트에 걸려 **판을 끝낸 그 킬만** 펀치·플래시·
                // 스파크·틱 사운드를 통째로 못 받는다. 하필 preRollSec("마지막 킬을 눈으로
                // 마무리할 시간") 동안 그게 그대로 노출된다.
                if (_panel != null) _panel.SetActive(true);
            }
            else if (_panel != null)
            {
                _pendingKills = 0;
                StopFeedbackTweens();
                _panel.SetActive(false);
            }
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 6);

            // rev 8 — 타이머 배지는 점수 패널의 자식이 아니다. 패널은 우상단 코너에 붙고
            // 폭이 플레이트에 묶여 있어서 상단 중앙에 따로 세운다.
            BuildTimerBadge(roots.SafeAreaRoot);

            // Badge anchored to the screen's top-right corner, cornerPadding px inset.
            // Panel width matches the plate so its centered children hug the right edge.
            _panel = new GameObject("ScorePanel", typeof(RectTransform));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = new Vector2(1f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            _panelBasePos = new Vector2(-cornerPadding, -cornerPadding);
            prt.anchoredPosition = _panelBasePos;
            prt.sizeDelta = new Vector2(plateSize.x,
                plateTopInset + plateSize.y + wavePlateGap + wavePlateSize.y);

            // Caption ("SCORE") is built later inside the badge tab (see below).

            _value = MakeText("Value", _panel.transform, valueFontSize, new Vector2(0.5f, 1f));
            _valueRect = _value.rectTransform;
            _valueRect.anchorMin = new Vector2(0.5f, 1f);
            _valueRect.anchorMax = new Vector2(0.5f, 1f);
            _valueRect.pivot = new Vector2(0.5f, 1f);
            _valueRect.anchoredPosition = new Vector2(0f, -48f);
            _valueRect.sizeDelta = new Vector2(480f, 124f);
            _value.text = "0";
            _value.color = baseColor;

            _burstPool = new ScoreBurstPool();
            _burstPool.Init((RectTransform)_panel.transform, burst);

            Vector2 valueCenter = _valueRect.anchoredPosition + new Vector2(0f, -_valueRect.rect.height * 0.5f);
            _shineBaseY = valueCenter.y;

            // Soft radial glow behind the number (subtle at rest, flares on each hit).
            _glowImage = MakeImage("Glow", _panel.transform, glowSprite);
            _glowRect = _glowImage.rectTransform;
            _glowRect.anchorMin = new Vector2(0.5f, 1f);
            _glowRect.anchorMax = new Vector2(0.5f, 1f);
            _glowRect.pivot = new Vector2(0.5f, 0.5f);
            _glowRect.anchoredPosition = valueCenter;
            _glowRect.sizeDelta = new Vector2(glowSize, glowSize);
            _glowRect.SetAsFirstSibling(); // behind burst + caption + value
            { var gc = glowColor; gc.a = glowRestAlpha; _glowImage.color = gc; }

            // Diagonal shine streak swept over the number on each hit (on top).
            _shineImage = MakeImage("Shine", _panel.transform, shineSprite);
            _shineRect = _shineImage.rectTransform;
            _shineRect.anchorMin = new Vector2(0.5f, 1f);
            _shineRect.anchorMax = new Vector2(0.5f, 1f);
            _shineRect.pivot = new Vector2(0.5f, 0.5f);
            _shineRect.anchoredPosition = new Vector2(0f, valueCenter.y);
            _shineRect.sizeDelta = new Vector2(shineWidth, 96f);
            _shineRect.localRotation = Quaternion.Euler(0f, 0f, shineTiltDeg);
            _shineRect.SetAsLastSibling();
            { var sc = shineColor; sc.a = 0f; _shineImage.color = sc; }

            // ── Badge plate (Unit 5): dark rounded plate + gold border behind the
            // number, and a "SCORE" gold tab straddling its top edge. Static frame — the
            // existing juice (punch/flash/burst/glow/shine) plays on top of this.
            _plateImage = MakeSolidImage("Plate", _panel.transform);
            _plateImage.sprite = MakeRoundedRectSprite(plateCornerRadius, plateBorderWidth, plateColor, plateBorderColor);
            _plateImage.type = Image.Type.Sliced;
            var platert = _plateImage.rectTransform;
            platert.anchorMin = new Vector2(0.5f, 1f);
            platert.anchorMax = new Vector2(0.5f, 1f);
            platert.pivot = new Vector2(0.5f, 1f);
            platert.anchoredPosition = new Vector2(0f, -plateTopInset);
            platert.sizeDelta = plateSize;
            platert.SetAsFirstSibling(); // behind glow + number + everything else in the panel

            var tabGO = new GameObject("ScoreTab", typeof(RectTransform), typeof(Image));
            tabGO.transform.SetParent(_panel.transform, false);
            var tabImg = tabGO.GetComponent<Image>();
            tabImg.sprite = MakeRoundedRectSprite(tabSize.y * 0.5f, 0f, tabColor, tabColor);
            tabImg.type = Image.Type.Sliced;
            tabImg.raycastTarget = false;
            var tabRt = tabImg.rectTransform;
            tabRt.anchorMin = new Vector2(0.5f, 1f);
            tabRt.anchorMax = new Vector2(0.5f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.anchoredPosition = new Vector2(0f, 2f);
            tabRt.sizeDelta = tabSize;
            tabRt.SetAsLastSibling(); // in front of the plate + number

            _caption = MakeText("Caption", tabGO.transform, captionFontSize, new Vector2(0.5f, 0.5f));
            var crt = _caption.rectTransform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            _caption.text = "점수";
            _caption.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            _caption.characterSpacing = 6f;
            _caption.color = tabTextColor;

            // Wave companion badge: 점수와 같은 남색/골드 문법이되 납작한 가로 위계라
            // 점수가 계속 주인공이다(은퇴한 스트레스 배지가 쓰던 비율 그대로).
            var wavePlate = MakeSolidImage("WavePlate", _panel.transform);
            wavePlate.sprite = MakeRoundedRectSprite(plateCornerRadius * 0.72f,
                plateBorderWidth, plateColor, plateBorderColor);
            wavePlate.type = Image.Type.Sliced;
            var wavePlateRt = wavePlate.rectTransform;
            wavePlateRt.anchorMin = new Vector2(0.5f, 1f);
            wavePlateRt.anchorMax = new Vector2(0.5f, 1f);
            wavePlateRt.pivot = new Vector2(0.5f, 1f);
            wavePlateRt.anchoredPosition = new Vector2(0f,
                -plateTopInset - plateSize.y - wavePlateGap);
            wavePlateRt.sizeDelta = wavePlateSize;

            var waveTab = MakeSolidImage("WaveTab", _panel.transform);
            waveTab.sprite = MakeRoundedRectSprite(waveTabSize.y * 0.5f, 0f, tabColor, tabColor);
            waveTab.type = Image.Type.Sliced;
            var waveTabRt = waveTab.rectTransform;
            waveTabRt.anchorMin = new Vector2(0.5f, 1f);
            waveTabRt.anchorMax = new Vector2(0.5f, 1f);
            waveTabRt.pivot = new Vector2(0f, 1f);
            waveTabRt.anchoredPosition = new Vector2(-wavePlateSize.x * 0.5f + 10f,
                -plateTopInset - plateSize.y - wavePlateGap - (wavePlateSize.y - waveTabSize.y) * 0.5f);
            waveTabRt.sizeDelta = waveTabSize;

            var waveCaption = MakeText("WaveCaption", waveTab.transform, captionFontSize * 0.72f,
                new Vector2(0.5f, 0.5f));
            var waveCaptionRt = waveCaption.rectTransform;
            waveCaptionRt.anchorMin = Vector2.zero;
            waveCaptionRt.anchorMax = Vector2.one;
            waveCaptionRt.offsetMin = Vector2.zero;
            waveCaptionRt.offsetMax = Vector2.zero;
            waveCaption.text = "웨이브";
            waveCaption.fontStyle = FontStyles.Bold;
            waveCaption.color = tabTextColor;

            _waveValue = MakeText("WaveValue", _panel.transform, waveValueFontSize,
                new Vector2(0.5f, 0.5f));
            _waveValueRect = _waveValue.rectTransform;
            _waveValueRect.anchorMin = new Vector2(0.5f, 1f);
            _waveValueRect.anchorMax = new Vector2(0.5f, 1f);
            _waveValueRect.pivot = new Vector2(0.5f, 0.5f);
            _waveValueRect.anchoredPosition = new Vector2(waveTabSize.x * 0.5f,
                -plateTopInset - plateSize.y - wavePlateGap - wavePlateSize.y * 0.5f);
            _waveValueRect.sizeDelta = new Vector2(wavePlateSize.x - waveTabSize.x - 32f,
                wavePlateSize.y);
            _waveValue.fontStyle = FontStyles.Bold;
            _waveValue.color = waveValueColor;
            _waveValue.text = "-";

            // Fullscreen milestone edge-flash vignette (on the canvas, behind the panel).
            _vignetteImage = MakeImage("MilestoneVignette", transform, vignetteSprite);
            var vrt = _vignetteImage.rectTransform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            vrt.SetAsFirstSibling();
            { var vc = milestoneColor; vc.a = 0f; _vignetteImage.color = vc; }

            // 스트레스 림 — 같은 전면 스프라이트를 쓰되 **자기 상태**를 갖는다(위 필드 주석).
            // 비네트 바로 위 형제 = 마일스톤/타이머 플래시가 림을 덮지 않는다.
            _stressRimImage = MakeImage("HeartStressRim", transform, vignetteSprite);
            var srt = _stressRimImage.rectTransform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            srt.SetSiblingIndex(1);
            _stressRimImage.raycastTarget = false;
            { var sc = stressRimColor; sc.a = 0f; _stressRimImage.color = sc; }

            UiLayer.Apply(gameObject);
        }

        private TextMeshProUGUI MakeText(string name, Transform parent, float size, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (scoreFont != null) tmp.font = scoreFont;
            if (scoreMaterial != null) tmp.fontSharedMaterial = scoreMaterial;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            return tmp;
        }

        private Image MakeImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            if (sprite != null) img.sprite = sprite;
            if (additiveMaterial != null) img.material = additiveMaterial;
            img.raycastTarget = false;
            return img;
        }

        // Plain alpha-blended Image (default material) — used for the badge plate/tab,
        // which must NOT use the additive material MakeImage applies.
        private Image MakeSolidImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        // Procedural rounded-rect sprite (SDF frame) for 9-slicing — shared helper
        // (result-screen-visual-upgrade unit 0). `border` > 0 draws a border-colored
        // ring `border` px thick around the edge; 0 → solid pill/fill.
        private static Sprite MakeRoundedRectSprite(float radius, float border, Color fill, Color borderColor)
            => UiRoundedSprite.Make(radius, border, fill, borderColor);
    }
}
