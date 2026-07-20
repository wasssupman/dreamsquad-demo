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

        [Header("Timing (unscaled)")]
        [Tooltip("딤이 차오르는 시간")]
        [SerializeField] private float dimFadeSec = 0.25f;
        [Tooltip("축 하나가 더해지고 다음 축까지의 간격")]
        [SerializeField] private float perAxisSec = 0.8f;
        [Tooltip("축 사이 쉼")]
        [SerializeField] private float betweenAxisSec = 0.3f;
        [Tooltip("마지막 축 이후 결과 화면까지의 여운")]
        [SerializeField] private float tailSec = 0.5f;

        [Header("Look")]
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color labelColor = new Color(1f, 0.78f, 0.28f, 1f);
        [SerializeField] private float labelFontSize = 56f;
        [Tooltip("라벨이 HUD 점수 배지 아래로 내려오는 정도(px)")]
        [SerializeField] private float labelTopOffset = 250f;

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
            yield return FadeDim(0f, dimColor.a, dimFadeSec);

            // 0점 축은 건너뛴다 — 패배 시 시간·스트레스가 0이라 "0을 굴리는" 민망한
            // 장면이 남는다. 그 경우 연출은 딤만 스치고 곧장 결과로 간다.
            yield return AddAxis("시간", score.Time, hud);
            yield return AddAxis("스트레스", score.Stress, hud);

            SetLabel(null);
            if (!_skipRequested) yield return WaitUnscaled(tailSec);

            _running = null;
            gameObject.SetActive(false);
            onDone?.Invoke();
        }

        private IEnumerator AddAxis(string name, int points, ScoreHudView hud)
        {
            if (points <= 0) yield break;

            SetLabel($"{name}  +{points:N0}");
            hud?.AddScore(points);
            if (_skipRequested) yield break;

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
