using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // defender-relocation unit 5 — 선택 시 유닛 좌측에 부채꼴로 펼쳐지는 액션 플립북.
    // 버튼 3개: 이동모드(활성) + 더미2(플레이스홀더). 우측엔 DcInspectPanelView(부착 카드)가 뜨므로
    // 이 뷰는 좌측을 쓴다. flip 등장연출(스케일 OutBack + Y 회전 90→0, stagger).
    //
    // 뷰는 Entity/BattleBridge 를 모른다 — 컨트롤러가 앵커 Transform·카메라·콜백을 주입한다
    // (DcInspectPanelView 와 같은 역할 분담). 카드 패널과 달리 버튼은 raycastTarget=true(탭 수신).
    public class DcActionFlipbookView : MonoBehaviour
    {
        private const int SortingOrder = 10; // DcInspectPanelView(9) 위 — 버튼이 카드 패널에 안 가림
        private const float LerpK = 18f;

        [SerializeField] private TMP_FontAsset labelFont;
        [Header("Layout")]
        [Tooltip("유닛 앵커에서 버튼까지 반경(px)")]
        [SerializeField] private float radius = 96f;
        [Tooltip("버튼 지름(px)")]
        [SerializeField] private float buttonSize = 66f;
        [Tooltip("부채꼴 중심(좌=180°)에서 위/아래로 벌리는 각(도)")]
        [SerializeField] private float fanSpreadDeg = 38f;
        [Tooltip("버튼별 등장 stagger(초)")]
        [SerializeField] private float stagger = 0.06f;
        [Tooltip("버튼 하나의 등장 시간(초)")]
        [SerializeField] private float appearDur = 0.18f;

        [Header("Colors")]
        [SerializeField] private Color fill = new Color(0.10f, 0.09f, 0.17f, 1f);
        [SerializeField] private Color moveBorder = new Color(0.45f, 0.85f, 1f, 1f);   // 청록 — 활성
        [SerializeField] private Color dummyBorder = new Color(0.5f, 0.5f, 0.58f, 1f); // 회색 — 비활성

        private class Btn
        {
            public GameObject root;
            public RectTransform rect;
            public Button button;
            public Image bg;
            public TextMeshProUGUI label;
            public Vector2 offset; // 앵커 기준 오프셋(px)
            public bool active;
        }

        private Canvas _canvas;
        private RectTransform _safeRoot;
        private GameObject _root;
        private RectTransform _rect;
        private CanvasGroup _group;
        private readonly Btn[] _btns = new Btn[3];
        private Sprite _movePlate;
        private Sprite _dummyPlate;
        private Transform _anchor;
        private Camera _camera;
        private Action _onMove;
        private bool _visible;
        private bool _built;
        private float _showT; // 등장 진행 시간

        private void Awake() => Build();

        private void Build()
        {
            if (_built) return;
            var roots = UiCanvasSetup.Ensure(gameObject, SortingOrder);
            _canvas = roots.Canvas;
            _safeRoot = roots.SafeAreaRoot;

            _root = new GameObject("ActionFlipbook", typeof(RectTransform), typeof(CanvasGroup));
            _root.transform.SetParent(_safeRoot, false);
            _rect = (RectTransform)_root.transform;
            _rect.anchorMin = _rect.anchorMax = new Vector2(0f, 0f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _group = _root.GetComponent<CanvasGroup>();
            _group.alpha = 0f;

            _movePlate = UiRoundedSprite.Make(buttonSize * 0.5f, 3f, fill, moveBorder);
            _dummyPlate = UiRoundedSprite.Make(buttonSize * 0.5f, 3f, fill, dummyBorder);

            // 부채꼴 각: 좌(180°) 중심, 위/아래로 ±fanSpread. index 1 = 이동(중앙=정좌측).
            float[] ang = { 180f + fanSpreadDeg, 180f, 180f - fanSpreadDeg };
            string[] labels = { "", "이동", "" };
            for (int i = 0; i < 3; i++)
            {
                var b = new Btn { active = i == 1 };
                b.root = new GameObject(i == 1 ? "MoveBtn" : $"Dummy{i}",
                    typeof(RectTransform), typeof(Image), typeof(Button));
                b.root.transform.SetParent(_root.transform, false);
                b.rect = (RectTransform)b.root.transform;
                b.rect.sizeDelta = new Vector2(buttonSize, buttonSize);
                b.bg = b.root.GetComponent<Image>();
                b.bg.sprite = b.active ? _movePlate : _dummyPlate;
                b.bg.type = Image.Type.Sliced;
                b.bg.raycastTarget = b.active; // 더미는 raycast 안 받음(플레이스홀더)
                b.button = b.root.GetComponent<Button>();
                b.button.transition = Selectable.Transition.None;
                b.button.interactable = b.active;

                b.label = BuildLabel(b.root.transform, labels[i], b.active);

                float rad = ang[i] * Mathf.Deg2Rad;
                b.offset = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                _btns[i] = b;
            }
            _btns[1].button.onClick.AddListener(() => _onMove?.Invoke());

            _group.blocksRaycasts = false;
            _root.SetActive(false);
            _built = true;
        }

        public void Show(Transform anchor, Camera cam, bool moveEnabled, Action onMove)
        {
            if (anchor == null || cam == null) { Hide(); return; }
            Build();
            _anchor = anchor;
            _camera = cam;
            _onMove = onMove;
            _btns[1].button.interactable = moveEnabled;
            _btns[1].bg.raycastTarget = moveEnabled;
            _btns[1].label.alpha = moveEnabled ? 1f : 0.4f;

            _showT = 0f;
            _visible = true;
            _group.blocksRaycasts = true;
            _root.SetActive(true);
            Follow();
        }

        public void Hide()
        {
            _visible = false;
            _anchor = null;
            if (_group != null) _group.blocksRaycasts = false;
        }

        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            float a = 1f - Mathf.Exp(-LerpK * Time.unscaledDeltaTime);
            _group.alpha = Mathf.Lerp(_group.alpha, _visible ? 1f : 0f, a);

            if (_visible) _showT += Time.unscaledDeltaTime;

            for (int i = 0; i < _btns.Length; i++)
            {
                var b = _btns[i];
                float p = _visible
                    ? Mathf.Clamp01((_showT - i * stagger) / Mathf.Max(0.01f, appearDur))
                    : 0f;
                float s = OutBack(p);
                // flip: Y 회전 90→0 (ScreenSpace 라 가로 스쿼시로 읽힘) + 스케일 팝.
                b.rect.localScale = new Vector3(s, s, 1f);
                b.rect.localEulerAngles = new Vector3(0f, Mathf.Lerp(90f, 0f, p), 0f);
            }

            if (!_visible && _group.alpha < 0.02f) _root.SetActive(false);
        }

        // 위치는 LateUpdate — CameraDirector(-90) 가 LateUpdate 에서 카메라 포즈 확정(DcInspectPanelView 선례).
        private void LateUpdate()
        {
            if (_root == null || !_visible) return;
            Follow();
        }

        private void Follow()
        {
            if (_anchor == null || _camera == null) { _visible = false; return; }
            var sp = _camera.WorldToScreenPoint(_anchor.position);
            if (sp.z <= 0f) { _root.SetActive(false); return; }
            if (!_root.activeSelf) _root.SetActive(true);

            float sf = _canvas != null ? _canvas.scaleFactor : 1f;
            if (sf <= 0f) sf = 1f;
            _rect.position = new Vector3(sp.x, sp.y, 0f);
            // 버튼은 root(=유닛 스크린점) 기준 오프셋. 캔버스 스케일 보정.
            for (int i = 0; i < _btns.Length; i++)
                _btns[i].rect.anchoredPosition = _btns[i].offset;
        }

        private TextMeshProUGUI BuildLabel(Transform parent, string text, bool active)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) tmp.font = labelFont;
            tmp.text = text;
            tmp.fontSize = 22f;
            tmp.color = Color.white;
            tmp.alpha = active ? 1f : 0.4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            return tmp;
        }

        private static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = 2.70158f;
            t = Mathf.Clamp01(t);
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
