using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI;
using Wassup.UI.Layout;

namespace Wassup.BattleCoreUnity.Hud
{
    // battle-core-rebuild unit 8a — "꿈결 위기!!" 보스 등장 경보 배너. 옛 `UI/BossWarningView.cs`(240줄,
    // boss-wave-cadence unit 1)의 복사·적응본이다. 바뀐 것은 **구동**뿐이다: 브리지가 bake 중에 `Show()` 를
    // 부르던 것을, 이 컴포넌트가 자기 구독(`UnitSpawned`)으로 받는다(계약 12 — 풀마다 자기 구독).
    // 슬램인 → 홀드 → 페이드 연출·팔레트·크기는 옛 씬 저작값 그대로(`BattleScene.unity` 직렬화 = 아래 기본값,
    // 폰트·머티리얼·비네트 스프라이트는 새 씬에 같은 에셋을 배선한다).
    [DisallowMultipleComponent]
    public sealed class CoreBossWarning : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

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
        private Sequence _seq;

        /// <summary>배너를 연 횟수. 「보스 스폰 → 배너 1회」의 증언 창이다(소리는 단언할 수 없지만 호출은 셀 수 있다).</summary>
        public int ShownCount { get; private set; }

        public bool Showing => _panel != null && _panel.activeSelf;

        private void Awake()
        {
            BuildCanvas();
            if (_panel != null) _panel.SetActive(false);
        }

        private void OnEnable()
        {
            ShownCount = 0;
            if (_driver != null) _driver.Subscribe(ViewOrder.Overhead, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            HideNow();
        }

        // 옛 구동 = 보스 스폰 순간(`BattleBridge.cs:10116~10121` — `tier == Boss` 인 적이 bake 될 때 `Show()`).
        // 판별은 **옛 판별 그대로** 적 저작의 `tier` 다. 코어의 `WaveStarted` 보스 플래그는 「그 웨이브가
        // 보스 웨이브인가」이고 옛 경보는 「보스가 태어났나」였다 — 분열·특수 스폰에서 둘이 갈린다.
        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.UnitSpawned:
                    if ((UnitKind)e.Arg != UnitKind.Enemy) return;
                    var enemies = _driver.EnemyAssets;
                    if (e.DefIndex < 0 || e.DefIndex >= enemies.Count) return;
                    var data = enemies[e.DefIndex];
                    if (data != null && data.tier == EnemyTier.Boss) Show();
                    return;

                // 옛 `OnPhaseChanged` — Battle 이 아니면 즉시 끊는다(판 경계·종료).
                case CoreEventKind.MatchStarted:
                case CoreEventKind.MatchEnded:
                    HideNow();
                    return;
            }
        }

        // 보스 스폰 순간 호출. 재진입 = 재시작(진행 중 배너를 끊고 새로 연다).
        // 스티키 가드(_showing) 없음 — 첫 배너 후 콜백이 실패해 가드가 굳으면 이후 보스(예:
        // 10웨이브)가 삼켜지던 문제를 원천 제거. 보스 웨이브 간격 ≫ 배너라 실제 재시작은 드묾.
        public void Show()
        {
            if (!_built) BuildCanvas();
            if (_panel == null) return;

            if (_seq.isAlive) _seq.Stop();

            ShownCount++;
            _panel.SetActive(true);
            if (SoundManager.Instance != null) SoundManager.Instance.PlayBossWarning();
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

        // teardown 전용(OnDisable / 판 경계): 진행 중 시퀀스를 끊고 상태를 원복.
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
