using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // gimmick-match-integration unit 2 — 배치 페이즈 기믹 안내 카드.
    // GameManager.PhaseChanged 구독: Placement 진입 시 배정 기믹(제목+설명) 표시, 다른 페이즈엔 숨김.
    // 좌상단 메뉴버튼(order 1000) 회피 위해 상단 중앙(카운트다운 배너 아래) 배치 + sortingOrder 8.
    // AssignedGimmick==null(기믹 비활성) 이면 Placement 라도 표시 안 함.
    public class GimmickGuideView : MonoBehaviour
    {
        [SerializeField] private float topOffset = 176f; // 카운트다운 배너(y=-90,h=72) 아래
        [SerializeField] private float cardWidth = 640f;

        private GameObject _card;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _bodyLabel;
        private bool _built;
        private bool _subscribed;

        private void Awake()
        {
            BuildCanvas();
            if (_card != null) _card.SetActive(false);
        }

        private void OnEnable() => TrySubscribe();

        // Instance 가 Awake 시점에 없었으면(실행 순서) 여기서 재시도.
        private void Start() => TrySubscribe();

        private void OnDisable()
        {
            if (_subscribed && GameManager.Instance != null)
                GameManager.Instance.PhaseChanged -= OnPhaseChanged;
            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            var gm = GameManager.Instance;
            if (gm == null) return;
            gm.PhaseChanged += OnPhaseChanged;
            _subscribed = true;
            OnPhaseChanged(gm.CurrentPhase); // 현재 페이즈 동기(재시작/late-enable 대비)
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            var gm = GameManager.Instance;
            bool show = phase == GamePhase.Placement && gm != null && gm.AssignedGimmick != null;
            if (show) Populate(gm.AssignedGimmick);
            if (_card != null) _card.SetActive(show);
        }

        private void Populate(Wassup.Data.GimmickData g)
        {
            if (_titleLabel != null)
                _titleLabel.text = string.IsNullOrEmpty(g.displayName) ? g.gimmickId : g.displayName;
            if (_bodyLabel != null) _bodyLabel.text = g.description;
        }

        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 8);

            _card = new GameObject("GimmickGuideCard",
                typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            _card.transform.SetParent(roots.SafeAreaRoot, false);
            var rt = (RectTransform)_card.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -topOffset);
            rt.sizeDelta = new Vector2(cardWidth, 0f); // 높이는 ContentSizeFitter 가 결정

            var bg = _card.GetComponent<Image>();
            bg.sprite = UiRoundedSprite.Make(18f, 3f,
                new Color(0.05f, 0.06f, 0.12f, 0.82f), new Color(1f, 0.82f, 0.35f, 0.9f));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false; // 배치 입력 비차단

            var vlg = _card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 16, 20);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = _card.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // 머리말(UI 문구 — 데이터 아님) / 제목(displayName) / 본문(description)
            MakeLabel("Header", "이번 판 특수 룰", 22f, new Color(1f, 0.82f, 0.35f, 1f), FontStyles.Bold);
            _titleLabel = MakeLabel("Title", "", 40f, Color.white, FontStyles.Bold);
            _bodyLabel = MakeLabel("Body", "", 26f, new Color(0.9f, 0.92f, 0.96f, 1f), FontStyles.Normal);

            UiLayer.Apply(gameObject);
        }

        private TextMeshProUGUI MakeLabel(string labelName, string text, float size, Color color, FontStyles style)
        {
            var go = new GameObject(labelName, typeof(RectTransform));
            go.transform.SetParent(_card.transform, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
