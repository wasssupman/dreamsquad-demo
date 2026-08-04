using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.Session;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // boss-wave-cadence unit 1 — "꿈결 위기!!" 보스 등장 경보 배너. Show() 한 번으로
    // 슬램인 → 홀드 → 페이드. BattleBridge 가 보스 스폰 순간(nightmareMechanics 감지) 호출한다.
    // 스타일 언어는 ScoreHudView 차용(런타임 절차 UI + Kanit SDF + PrimeTween), 팔레트만 위기 크림슨.
    public class BossWarningView : MonoBehaviour
    {
        [Header("Style font (Kanit Bold Italic SDF + outline — 스코어와 동일 에셋 할당). Null → TMP 기본.")]
        [SerializeField] private TMP_FontAsset warningFont;
        [SerializeField] private Material warningMaterial;
        [Tooltip("풀스크린 붉은 비네트 스프라이트(가장자리 밝음). Null → 비네트 생략.")]
        [SerializeField] private Sprite vignetteSprite;

        [Header("Text")]
        [SerializeField] private string warningText = "꿈결 위기!!";
        [SerializeField] private float fontSize = 150f;
        [Tooltip("안착 크림슨")]
        [SerializeField] private Color crimsonColor = new Color(0.86f, 0.12f, 0.14f, 1f);
        [Tooltip("슬램 순간 화이트핫 플래시")]
        [SerializeField] private Color whiteHotFlash = new Color(1f, 0.95f, 0.92f, 1f);

        [Header("Plate (다크 네이비 + 크림슨 보더)")]
        [SerializeField] private Vector2 plateSize = new Vector2(900f, 240f);
        [SerializeField] private Color plateColor = new Color(0.05f, 0.03f, 0.06f, 0.82f);
        [SerializeField] private Color plateBorderColor = new Color(0.86f, 0.12f, 0.14f, 0.95f);
        [SerializeField] private float plateCornerRadius = 24f;
        [SerializeField] private float plateBorderWidth = 3f;

        [Header("Red vignette")]
        [Tooltip("비네트 최대 색/알파(펄스 피크)")]
        [SerializeField] private Color vignetteColor = new Color(0.7f, 0.05f, 0.06f, 0.55f);

        [Header("Animation (unscaled — timeScale=0 모달 중에도 재생)")]
        [SerializeField] private float slamFromScale = 1.6f;
        [SerializeField] private float slamInDuration = 0.35f;
        [SerializeField] private float holdDuration = 1.4f;
        [SerializeField] private float fadeOutDuration = 0.5f;
        [Tooltip("배틀 HUD(score 6/dock 7)보다 위")]
        [SerializeField] private int sortingOrder = 8;

        private GameObject _panel;
        private RectTransform _panelRect;
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _text;
        private Image _vignette;
        private bool _built;
        private bool _subscribed;
        private Sequence _seq;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void Update()
        {
            EnsureSubscribed();
        }

        // battle-sim-extraction unit 13-B — Bridge 가 `Show()` 를 직접 부르던 방향에서
        // **뷰가 `BossSpawned` 를 구독**하는 방향으로 뒤집혔다. 세션은 매치마다 교체되므로
        // 구독 지점은 인스턴스가 아니라 정적 창구다. 이 컴포넌트는 항상 활성이고 자식 `_panel`
        // 만 토글하므로 OnEnable 이 판마다 재실행되지 않는다 — 구독 1회로 충분하다.
        private void OnEnable() => MatchSession.Events += OnSessionEvent;

        private void OnDisable()
        {
            MatchSession.Events -= OnSessionEvent;
            Unsubscribe();
            HideNow();
        }

        private void OnSessionEvent(SessionEvent evt)
        {
            if (evt.Kind == SessionEventKind.BossSpawned) Show();
        }

        // 보스 스폰 사실을 받으면 호출. 재진입 = 재시작(진행 중 배너를 끊고 새로 연다).
        // 스티키 가드(_showing) 없음 — 첫 배너 후 콜백이 실패해 가드가 굳으면 이후 보스(예:
        // 10웨이브)가 삼켜지던 문제를 원천 제거. 보스 웨이브 간격 ≫ 배너라 실제 재시작은 드묾.
        public void Show()
        {
            if (!_built) BuildCanvas();
            if (_panel == null) return;

            if (_seq.isAlive) _seq.Stop();

            _panel.SetActive(true);
            SoundManager.Instance?.PlayBossWarning();
            _canvasGroup.alpha = 1f;
            _panelRect.localScale = Vector3.one * slamFromScale;
            _text.color = whiteHotFlash;
            if (_vignette != null) _vignette.color = WithAlpha(vignetteColor, 0f);

            _seq = Sequence.Create(useUnscaledTime: true)
                .Group(Tween.Scale(_panelRect, Vector3.one, slamInDuration, Ease.OutBack))
                .Group(Tween.Color(_text, crimsonColor, slamInDuration, Ease.OutQuad));
            if (_vignette != null)
                _seq.Group(Tween.Color(_vignette, vignetteColor, slamInDuration, Ease.OutQuad));
            _seq.ChainDelay(holdDuration)
                .Chain(Tween.Alpha(_canvasGroup, 0f, fadeOutDuration, Ease.InQuad));
            if (_vignette != null)
                _seq.Group(Tween.Color(_vignette, WithAlpha(vignetteColor, 0f), fadeOutDuration, Ease.InQuad));
            // 페이드 완료 → 패널만 비활성. 시퀀스를 자기 콜백 안에서 Stop 하지 않는다(자기-Stop 예외로
            // 이후 리셋이 막히던 것이 10웨이브 미노출의 근본 원인이었다).
            _seq.ChainCallback(DeactivatePanel);
        }

        private void DeactivatePanel()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        // teardown 전용(OnDisable / PhaseChanged): 진행 중 시퀀스를 끊고 상태를 원복.
        private void HideNow()
        {
            if (_seq.isAlive) _seq.Stop();
            if (_panel != null)
            {
                _panel.SetActive(false);
                if (_canvasGroup != null) _canvasGroup.alpha = 1f;
                if (_panelRect != null) _panelRect.localScale = Vector3.one;
            }
            if (_vignette != null) _vignette.color = WithAlpha(vignetteColor, 0f);
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            if (GameManager.Instance == null) return;
            GameManager.Instance.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
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
            if (phase != GamePhase.Battle) HideNow();
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder);

            // Fullscreen red vignette on the full-bleed root (covers screen edges), behind the panel.
            if (vignetteSprite != null)
            {
                _vignette = MakeImage("CrisisVignette", roots.FullBleedRoot, vignetteSprite);
                var vrt = _vignette.rectTransform;
                vrt.anchorMin = Vector2.zero;
                vrt.anchorMax = Vector2.one;
                vrt.offsetMin = Vector2.zero;
                vrt.offsetMax = Vector2.zero;
                vrt.SetAsFirstSibling();
                _vignette.color = WithAlpha(vignetteColor, 0f);
            }

            // Centered banner panel.
            _panel = new GameObject("CrisisPanel", typeof(RectTransform), typeof(CanvasGroup));
            _panel.transform.SetParent(roots.SafeAreaRoot, false);
            _panelRect = (RectTransform)_panel.transform;
            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.anchoredPosition = Vector2.zero;
            _panelRect.sizeDelta = plateSize;
            _canvasGroup = _panel.GetComponent<CanvasGroup>();

            // Dark navy plate with crimson border.
            var plate = MakeSolidImage("Plate", _panel.transform);
            plate.sprite = UiRoundedSprite.Make(plateCornerRadius, plateBorderWidth, plateColor, plateBorderColor);
            plate.type = Image.Type.Sliced;
            var platert = plate.rectTransform;
            platert.anchorMin = Vector2.zero;
            platert.anchorMax = Vector2.one;
            platert.offsetMin = Vector2.zero;
            platert.offsetMax = Vector2.zero;
            platert.SetAsFirstSibling();

            // Crisis text.
            _text = MakeText("Text", _panel.transform, fontSize);
            var trt = _text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _text.text = warningText;
            _text.color = crimsonColor;

            UiLayer.Apply(gameObject);
        }

        private TextMeshProUGUI MakeText(string name, Transform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (warningFont != null) tmp.font = warningFont;
            if (warningMaterial != null) tmp.fontSharedMaterial = warningMaterial;
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
            img.raycastTarget = false;
            return img;
        }

        private Image MakeSolidImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
