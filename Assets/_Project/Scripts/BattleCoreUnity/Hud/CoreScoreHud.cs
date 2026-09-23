using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.Presentation;

namespace Wassup.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 5b — **점수·시계·웨이브·마음.** 옛 `ScoreHudView`(1,137줄)의 후계다.
    //
    // 값은 전부 담당자 읽기 모델에서 온다: 점수 = `ScoreLedger`, 시계·웨이브 = `MatchClock`·
    // `WaveScheduler`, 마음 = `HeartMeter`. **HUD 는 하나도 세지 않는다** — 옛 HUD 가 남은
    // 시간을 자기 누산으로 들고 있던 자리가 「화면과 판정이 갈린다」의 단골이었다.
    //
    // 두 축이 성격이 다르다:
    //   · **연속값**(시계·코스트·마음 체력)은 매 프레임 **읽는다.** 사건으로 쏘면 판당 만 건이다.
    //   · **사건**(점수 변동·마음 붕괴)은 **구독한다.** 그래야 「방금 올랐다」를 연출할 수 있다.
    //
    // 마음 바는 `HeartStressPulse` 를 **그대로 재사용**한다(순수 함수 — 아키텍처 무참조).
    // 단계·심박·펀치의 산식은 저쪽이 정본이고 여기는 그 값을 크기·색으로 옮길 뿐이다.
    [DisallowMultipleComponent]
    public sealed class CoreScoreHud : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Header("마음 박동")]
        [SerializeField, Min(20f)] private float _restBpm = 52f;
        [SerializeField, Min(20f)] private float _maxBpm = 168f;
        [Tooltip("박동이 바 밝기를 얼마나 끌어내리나(0 = 안 뛴다).")]
        [SerializeField, Range(0f, 1f)] private float _beatDepth = 0.35f;
        [Tooltip("스트레스가 올랐을 때 바가 부푸는 정도.")]
        [SerializeField, Range(0f, 1f)] private float _punchDepth = 0.22f;
        [Tooltip("펀치를 최대로 치는 상승분(0~100 축).")]
        [SerializeField, Min(0.1f)] private float _punchFullRise = 12f;
        [SerializeField, Min(0.1f)] private float _punchDecayPerSec = 2.2f;

        private TextMeshProUGUI _score;
        private TextMeshProUGUI _clock;
        private TextMeshProUGUI _wave;
        private Image _heartFill;
        private RectTransform _heartRoot;
        private TextMeshProUGUI _heartLabel;

        private int _stage;
        private float _phase;
        private float _punch;
        private float _lastStress;
        private float _scorePop;
        private bool _built;

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Overhead, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
        }

        private void OnCoreEvent(CoreEvent e)
        {
            // 점수는 **사건으로** 튄다. 매 프레임 읽어도 숫자는 맞지만 「방금 올랐다」가 안 읽힌다.
            if (e.Kind == CoreEventKind.ScoreChanged) _scorePop = 1f;
        }

        private void Update()
        {
            if (_driver == null || !_driver.Running) return;
            if (!_built) Build();

            var match = _driver.Match;
            float dt = Time.unscaledDeltaTime;

            // ── 점수 ──
            _score.text = match.Score.Total.ToString();
            _scorePop = Mathf.MoveTowards(_scorePop, 0f, dt * 3.5f);
            _score.rectTransform.localScale = Vector3.one * (1f + 0.25f * _scorePop * _scorePop);

            // ── 시계 ──
            // 「남은 시간」은 `FixedLimit` 만의 답이다. 세는 판(`CountUp`)은 경과를 보여 준다 —
            // 0:00 으로 굳은 시계는 「멈췄다」로 읽힌다.
            var clock = match.Clock;
            _clock.text = clock.Remaining > 0f
                ? CoreHudUi.Clock(clock.Remaining)
                : CoreHudUi.Clock(clock.BattleTime);
            _clock.color = clock.Remaining > 0f && clock.Remaining <= 10f ? CoreHudUi.Bad : CoreHudUi.Ink;

            // ── 웨이브 ──
            int reached = match.Waves.WaveReached;
            int total = match.Waves.WaveCount;
            _wave.text = total > 0 ? $"WAVE {Mathf.Max(1, reached)} / {total}" : $"WAVE {Mathf.Max(1, reached)}";

            PaintHeart(match.Heart, dt);
        }

        // ── 마음 ──────────────────────────────────────────────────────────────
        private void PaintHeart(HeartMeter heart, float dt)
        {
            // 마음이 미저작인 판(체력 0)에서는 바 자체를 내린다. 빈 바를 두면 「마음이 이미
            // 다 닳았다」로 읽힌다 — `StressMath` 가 그 경우를 0 으로 답하는 것과 같은 판단이다.
            bool has = heart.MaxHealth > 0f;
            if (_heartRoot.gameObject.activeSelf != has) _heartRoot.gameObject.SetActive(has);
            if (!has) return;

            float stress01 = Mathf.Clamp01(heart.Stress / StressMath.Max);
            float rise = Mathf.Max(0f, heart.Stress - _lastStress);
            _lastStress = heart.Stress;

            _stage = HeartStressPulse.StageOf(stress01, _stage);
            float bpm = HeartStressPulse.Bpm(_stage, _restBpm, _maxBpm);
            _phase = HeartStressPulse.AdvancePhase(_phase, dt, bpm);
            float beat = HeartStressPulse.Beat(_phase);
            _punch = HeartStressPulse.AdvancePunch(_punch, rise, _punchFullRise, dt, _punchDecayPerSec);

            float ratio = heart.MaxHealth > 0f ? Mathf.Clamp01(heart.Health / heart.MaxHealth) : 0f;
            _heartFill.fillAmount = ratio;

            float brightness = HeartStressPulse.BeatScale(beat, _beatDepth * stress01);
            var warm = Color.Lerp(CoreHudUi.Good, CoreHudUi.Bad, stress01);
            _heartFill.color = new Color(warm.r * brightness, warm.g * brightness, warm.b * brightness, 1f);

            // 펀치는 **위로만** 부푼다 — 줄어드는 바는 「사라진다」로 읽힌다.
            float punchScale = HeartStressPulse.PunchScale(_punch, _punchDepth);
            _heartRoot.localScale = new Vector3(punchScale, punchScale, 1f);

            _heartLabel.text = $"{Mathf.CeilToInt(heart.Health)} / {Mathf.CeilToInt(heart.MaxHealth)}";
        }

        // ── 조립 ─────────────────────────────────────────────────────────────
        private void Build()
        {
            CoreHudUi.EnsureCanvas(gameObject);
            _built = true;

            _score = CoreHudUi.Label("Score",
                CoreHudUi.Rect("ScoreRow", transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                               new Vector2(40f, -34f), new Vector2(240f, 84f)),
                "0", 76f, CoreHudUi.Ink, TextAlignmentOptions.Left);

            CoreHudUi.Label("ScoreCaption",
                CoreHudUi.Rect("ScoreCaptionRow", transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                               new Vector2(42f, -112f), new Vector2(240f, 30f)),
                "처치", 26f, CoreHudUi.InkDim, TextAlignmentOptions.Left);

            _clock = CoreHudUi.Label("Clock",
                CoreHudUi.Rect("ClockRow", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                               new Vector2(0f, -30f), new Vector2(320f, 76f)),
                "0:00", 68f, CoreHudUi.Ink);

            _wave = CoreHudUi.Label("Wave",
                CoreHudUi.Rect("WaveRow", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                               new Vector2(0f, -104f), new Vector2(360f, 32f)),
                "WAVE 1", 28f, CoreHudUi.InkDim);

            _heartRoot = CoreHudUi.Rect("Heart", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                        new Vector2(0f, -146f), new Vector2(460f, 26f));
            _heartFill = CoreHudUi.Bar(_heartRoot, new Color(0.08f, 0.09f, 0.13f, 0.8f), CoreHudUi.Good);
            _heartLabel = CoreHudUi.Label("HeartLabel",
                CoreHudUi.Rect("HeartLabelRow", _heartRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                               new Vector2(0f, -4f), new Vector2(460f, 26f)),
                "", 22f, CoreHudUi.InkDim);
        }
    }
}
