using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // score-tally-sequence unit 2 — 전투 종료 후 결과 화면 직전의 점수 합산 연출.
    //
    // 우상단 HUD 점수(= 킬점수)에 시간점수 → 스트레스점수를 순차로 더한다. 롤업·펀치·
    // 플래시는 ScoreHudView 가 이미 갖고 있으므로 여기서는 **값을 밀어주기만** 한다
    // (AddScore). 새 카운터를 만들지 않는다.
    //
    // 정렬 순서 5 — 배틀(0) 위, ScoreHud(6) 아래. 딤이 전장을 덮되 점수는 그 위에 뜬다.
    //
    // 시간은 전부 unscaled 다. 전투 종료 시 `_running=false` 가 ECS `BattleRunning` 으로
    // 흘러 시뮬이 이미 멈춰 있고, 이 연출은 그와 무관하게 흘러야 한다.
    public class ScoreTallyView : MonoBehaviour
    {
        private const int SortingOrder = 5;

        // 시선은 마지막 적이 죽은 자리(보드 중앙)에 있는데 점수는 우상단이다. 바로 합산하면
        // 첫 축을 통째로 놓친다. 그래서 "전투 끝 → 화면 전환 → 여기를 봐라 → 숫자" 순으로
        // 인지 단계를 쪼갠다. 전부 스킵 가능하므로 반복 플레이의 부담은 없다.
        [Header("Timing (unscaled)")]
        [Tooltip("마지막 적이 죽은 뒤의 여운. 이 시간 동안 딤이 서서히 깔린다.")]
        [SerializeField] private float preRollSec = 1f;
        [Tooltip("딤이 차오르는 시간. 여운과 **동시에** 진행된다 — 보통 여운과 같게 둔다.")]
        [SerializeField] private float dimFadeSec = 1f;
        [Tooltip("라벨이 먼저 뜨고 숫자가 움직이기까지의 리드타임 — 시선 유도")]
        [SerializeField] private float labelLeadSec = 0.25f;
        [Tooltip("축 하나의 롤업 상한")]
        [SerializeField] private float perAxisSec = 0.7f;
        [Tooltip("축 사이 쉼")]
        [SerializeField] private float betweenAxisSec = 0.35f;
        [Tooltip("마지막 축 이후 결과 화면까지의 여운")]
        [SerializeField] private float tailSec = 0.5f;

        [Header("Look")]
        // HUD 점수와 같은 서체를 써야 "같은 숫자가 자란다"로 읽힌다. 비우면 TMP 기본
        // (LiberationSans)이라 바로 옆 Kanit 숫자와 서체가 갈린다.
        [Tooltip("ScoreHudView 와 동일한 폰트/머티리얼. Null → TMP 기본.")]
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private Material labelMaterial;
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color labelColor = new Color(1f, 0.78f, 0.28f, 1f);
        [SerializeField] private float labelFontSize = 56f;
        // HUD 배지 블록 하단은 cornerPadding 36 + plateTopInset 20 + plateSize.y 148
        // + leakPlateGap 10 + leakPlateSize.y 64 = **278**. 이 값이 그보다 작으면 라벨이
        // 배지에 가린다 — Tally 캔버스(5)가 ScoreHud(6) **아래**라 배지가 위에 그려진다.
        [Tooltip("라벨이 HUD 점수 배지 아래로 내려오는 정도(px). 배지 하단 278 보다 커야 한다.")]
        [SerializeField] private float labelTopOffset = 296f;

        private Image _dim;
        private TextMeshProUGUI _label;
        private CanvasGroup _labelGroup;
        private bool _built;
        private bool _skipRequested;
        private Coroutine _running;

        private void Awake()
        {
            Build();
            gameObject.SetActive(false);
        }

        // 전투 종료 → 결과 화면 사이. onDone 은 **반드시** 호출된다(스킵·중단 포함) —
        // 여기서 끊기면 결과 화면이 영영 안 뜬다.
        public void Play(ScoreMath.BattleScore score, ScoreHudView hud, System.Action onDone)
        {
            if (!_built) Build();
            if (_running != null) StopCoroutine(_running);
            _skipRequested = false;
            gameObject.SetActive(true);
            _running = StartCoroutine(Sequence(score, hud, onDone));
        }

        public void Skip() => _skipRequested = true;

        private IEnumerator Sequence(ScoreMath.BattleScore score, ScoreHudView hud, System.Action onDone)
        {
            SetLabel(null);

            // 1) 전투 여운 + 딤을 **동시에**. 따로 하면 "아무 일 없음 → 갑자기 어두워짐"
            //    두 사건이 계단처럼 끊긴다. 겹치면 "전투가 잦아든다"는 하나의 제스처가 된다.
            //    램프가 길어 초반엔 거의 투명하므로(1초 중 0.3초 시점에 0.17) 마지막 킬
            //    VFX 를 가리지 않는다.
            var dimFade = StartCoroutine(FadeDim(0f, dimColor.a, dimFadeSec));
            yield return WaitUnscaled(preRollSec);
            if (dimFade != null) StopCoroutine(dimFade);
            SetDimAlpha(dimColor.a); // 여운이 램프보다 짧게 끝나도 딤은 확정한다

            // 2) 시선 유도 — 숫자가 움직이기 전에 배지를 한 번 때린다.
            bool hasAny = score.Time > 0 || score.Stress > 0;
            if (hasAny && !_skipRequested)
            {
                hud?.PulseAttention();
                yield return WaitUnscaled(labelLeadSec);
            }

            // 4) 축 순차 합산. 0점 축은 건너뛴다 — 패배 시 시간·스트레스가 0이라
            //    "0을 굴리는" 민망한 장면이 남는다.
            yield return AddAxis("시간", score.Time, hud);
            yield return AddAxis("스트레스", score.Stress, hud);

            SetLabel(null);
            if (!_skipRequested) yield return WaitUnscaled(tailSec);

            _running = null;
            // onDone 을 SetActive(false) **앞**에 둔다. 지금은 두 줄 사이에 yield 가 없어
            // 어느 순서든 동작하지만, 훗날 사이에 yield 한 줄이 끼면 비활성화가 코루틴을
            // 스케줄에서 빼버려 **결과 화면이 영영 안 뜬다.** 순서로 그 함정을 없앤다.
            onDone?.Invoke();
            gameObject.SetActive(false);
        }

        private IEnumerator AddAxis(string name, int points, ScoreHudView hud)
        {
            if (points <= 0) yield break;

            // 라벨을 먼저 띄우고 한 박자 뒤에 숫자를 움직인다. 동시에 하면 어디를 봐야
            // 할지 모르는 사이에 롤업이 끝나버린다.
            SetLabel($"{name}  +{points:N0}");
            yield return WaitUnscaled(labelLeadSec);
            if (_skipRequested) { hud?.AddScore(points); yield break; }

            hud?.AddScore(points);

            // 롤업이 끝나면 바로 다음으로 — 고정 대기보다 리듬이 산다. 다만 상한을 둬서
            // 보간이 오래 끌어도 연출이 늘어지지 않게 한다.
            float t = 0f;
            while (t < perAxisSec && !_skipRequested)
            {
                if (hud != null && hud.RollSettled && t > perAxisSec * 0.35f) break;
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_skipRequested) yield break;
            yield return WaitUnscaled(betweenAxisSec);
        }

        private IEnumerator WaitUnscaled(float sec)
        {
            float t = 0f;
            while (t < sec && !_skipRequested) { t += Time.unscaledDeltaTime; yield return null; }
        }

        private IEnumerator FadeDim(float from, float to, float sec)
        {
            if (_dim == null) yield break;
            float t = 0f;
            while (t < sec && !_skipRequested)
            {
                t += Time.unscaledDeltaTime;
                SetDimAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / Mathf.Max(0.0001f, sec))));
                yield return null;
            }
            SetDimAlpha(to);
        }

        private void SetDimAlpha(float a)
        {
            if (_dim == null) return;
            var c = dimColor; c.a = a; _dim.color = c;
        }

        private void SetLabel(string text)
        {
            if (_label == null) return;
            bool show = !string.IsNullOrEmpty(text);
            _label.text = show ? text : string.Empty;
            if (_labelGroup != null) _labelGroup.alpha = show ? 1f : 0f;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: SortingOrder);
            // order 5 에는 CostDisplay·DreamcatcherHandView·DraftView 도 있다. 동순위면
            // 그리기 순서가 계층 정렬에 의존해 불안정하고, 딤이 손패 위/아래 어디에 깔릴지
            // 보장되지 않는다(Tally 진입 프레임엔 아직 겹쳐 있다). overrideSorting 으로
            // 이 캔버스를 독립 단위로 확정한다 — ScoreHud(6) 아래는 그대로 유지된다.
            roots.Canvas.overrideSorting = true;
            roots.Canvas.sortingOrder = SortingOrder;

            // 전체 화면 딤 + 스킵 입력. 딤 자체를 버튼으로 써서 어디를 눌러도 넘어간다.
            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dimGo.transform.SetParent(roots.FullBleedRoot, false);
            var rt = (RectTransform)dimGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _dim = dimGo.GetComponent<Image>();
            // 명시적 solid 스프라이트 — null-sprite Image 는 전체 화면 쿼드를 신뢰성 있게 안 그린다.
            _dim.sprite = UiRoundedSprite.Make(2f, 0f, Color.white, Color.white);
            _dim.type = Image.Type.Sliced;
            SetDimAlpha(0f);
            dimGo.GetComponent<Button>().onClick.AddListener(Skip);

            // 합산 중인 축 라벨 — HUD 점수 배지 바로 아래, 화면 우측.
            var labelGo = new GameObject("AxisLabel", typeof(RectTransform), typeof(CanvasGroup));
            labelGo.transform.SetParent(roots.SafeAreaRoot, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(1f, 1f);
            lrt.sizeDelta = new Vector2(460f, 72f);
            lrt.anchoredPosition = new Vector2(-36f, -labelTopOffset);
            _labelGroup = labelGo.GetComponent<CanvasGroup>();
            _labelGroup.alpha = 0f;

            _label = labelGo.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) _label.font = labelFont;
            if (labelMaterial != null) _label.fontSharedMaterial = labelMaterial;
            _label.fontSize = labelFontSize;
            _label.color = labelColor;
            _label.alignment = TextAlignmentOptions.MidlineRight;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.fontStyle = FontStyles.Bold;
            _label.raycastTarget = false;

            UiLayer.Apply(gameObject);
        }
    }
}
