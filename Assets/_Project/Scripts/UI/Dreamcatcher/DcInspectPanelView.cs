using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // unit-dreamcatcher-inspect unit 2 — 선택 유닛 옆에 붙는 부착 카드 상세 스택 패널.
    //
    // 손패 드래그 툴팁(DreamcatcherHandView.BuildTooltip)의 시각 문법을 계승하되 위젯은
    // 신규다 — 그 툴팁은 입력이 슬롯 인덱스이고 위치가 슬롯 homePos 에서 파생되며 생명주기가
    // 손패 뷰에 종속돼 재사용할 수 없다. 공유되는 것은 본문 포맷터(DreamcatcherCardText)뿐.
    //
    // 뷰는 Entity/BattleBridge 를 모른다 — 컨트롤러가 앵커 Transform 을 해석해 넘긴다
    // (DcIconStripSpawner→DcIconStripView 와 같은 역할 분담).
    public class DcInspectPanelView : MonoBehaviour
    {
        private const int PanelSortingOrder = 9; // SquadPrep(8) 위, MenuPopup(960) 아래
        private const float HiddenScale = 0.94f;
        private const float LerpK = 18f;

        [SerializeField] private TMP_FontAsset labelFont;

        [Header("Layout")]
        [SerializeField] private float panelWidth = 460f;
        [SerializeField] private float pad = 14f;
        [SerializeField] private float rowGap = 8f;
        [SerializeField] private float artHeight = 84f;
        [SerializeField] private float headerBodyGap = 6f;
        [Tooltip("유닛 앵커에서 패널까지 가로 간격(캔버스 단위)")]
        [SerializeField] private float gapFromUnit = 46f;
        [Tooltip("safe area 안쪽 여백(캔버스 단위)")]
        [SerializeField] private float edgeMargin = 10f;

        [Header("Colors")]
        [SerializeField] private Color fill = new Color(0.07f, 0.06f, 0.13f, 1f);        // 툴팁과 동일 네이비
        [SerializeField] private Color squadBorder = new Color(1f, 0.82f, 0.35f, 1f);    // 골드 — DcIconStrip 과 같은 색 언어
        [SerializeField] private Color unitBorder = new Color(0.45f, 0.85f, 1f, 1f);     // 청록
        [SerializeField] private Color costColor = new Color(0.79f, 0.65f, 1f, 1f);      // #C9A6FF — 손패 툴팁 코스트

        private class Row
        {
            public GameObject root;
            public RectTransform rect;
            public Image bg;
            public Image art;
            public TextMeshProUGUI header;
            public TextMeshProUGUI body;
        }

        private readonly List<Row> _rows = new List<Row>();
        private Canvas _canvas;
        private RectTransform _safeRoot;
        private GameObject _root;
        private RectTransform _rect;
        private CanvasGroup _group;
        private Sprite _squadPlate;
        private Sprite _unitPlate;
        private Transform _anchor;
        private Camera _camera;
        private bool _visible;
        private bool _built;

        private void Awake() => Build();

        private void Build()
        {
            if (_built) return;
            var roots = UiCanvasSetup.Ensure(gameObject, PanelSortingOrder);
            _canvas = roots.Canvas;
            _safeRoot = roots.SafeAreaRoot;

            _root = new GameObject("InspectPanel", typeof(RectTransform), typeof(CanvasGroup));
            _root.transform.SetParent(_safeRoot, false);
            _rect = (RectTransform)_root.transform;
            // pivot 을 좌측중앙 고정 → 좌우 플립을 pivot 이 아니라 좌표 계산으로 처리한다
            // (pivot 을 바꾸면 같은 position 이 다른 위치를 뜻해 클램프 수식이 갈라진다).
            _rect.anchorMin = new Vector2(0f, 0f);
            _rect.anchorMax = new Vector2(0f, 0f);
            _rect.pivot = new Vector2(0f, 0.5f);

            _group = _root.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _root.SetActive(false);
            _built = true;
        }

        // 컨트롤러가 앵커를 해석해 넘긴다. cards/costs 는 index 대응(같은 길이).
        public void Show(Transform anchor, Camera cam, IReadOnlyList<DreamcatcherCard> cards, IReadOnlyList<int> costs)
        {
            if (anchor == null || cam == null || cards == null || cards.Count == 0) { Hide(); return; }
            Build();
            EnsurePlates();

            _anchor = anchor;
            _camera = cam;

            // 측정 전에 루트를 켠다. 비활성 계층에서 AddComponent 된 TMP 는 Awake 가 돌지
            // 않아 기본 설정이 로드되지 않고, 그 상태의 GetPreferredValues 는 정답의 1/10 을
            // 답한다(실측 2026-07-15: 헤더 2.21 vs 22.0). 그러면 본문이 헤더 위로 겹친다.
            bool wasHidden = !_root.activeSelf;
            _root.SetActive(true);
            EnsureRows(cards.Count);

            float artW = artHeight * (2f / 3f); // 타로 아트 2:3
            float textX = pad + artW + pad;
            float textW = panelWidth - textX - pad;
            if (textW < 40f) textW = 40f; // 극단 튜닝값 방어

            float y = 0f;
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                bool used = i < cards.Count;
                row.root.SetActive(used);
                if (!used) continue;

                var card = cards[i];
                row.bg.sprite = card.type == CardType.Squad ? _squadPlate : _unitPlate;

                string title = string.IsNullOrEmpty(card.displayName) ? card.id : card.displayName;
                int cost = costs != null && i < costs.Count ? costs[i] : 0;
                row.header.text = $"<b>{title}</b>  <color=#{ColorUtility.ToHtmlStringRGB(costColor)}>{cost}</color>";
                row.body.text = DreamcatcherCardText.Body(card);

                bool hasArt = card.art != null;
                row.art.gameObject.SetActive(hasArt);
                if (hasArt) row.art.sprite = card.art;

                // 텍스트 대입 후 ForceMeshUpdate 로 레이아웃을 확정한 뒤 측정한다. 갓 생성된
                // TMP 는 첫 레이아웃 전까지 폰트 스케일이 서지 않아 측정값이 무의미하다.
                row.header.ForceMeshUpdate();
                row.body.ForceMeshUpdate();
                float headerH = row.header.GetPreferredValues(row.header.text, textW, 0f).y;
                float bodyH = row.body.GetPreferredValues(row.body.text, textW, 0f).y;

                var hrt = (RectTransform)row.header.transform;
                hrt.anchoredPosition = new Vector2(textX, -pad);
                hrt.sizeDelta = new Vector2(textW, headerH);
                var brt = (RectTransform)row.body.transform;
                brt.anchoredPosition = new Vector2(textX, -(pad + headerH + headerBodyGap));
                brt.sizeDelta = new Vector2(textW, bodyH);

                var art = (RectTransform)row.art.transform;
                art.anchoredPosition = new Vector2(pad, -pad);
                art.sizeDelta = new Vector2(artW, artHeight);

                float textH = pad + headerH + headerBodyGap + bodyH + pad;
                float artColH = pad + artHeight + pad;
                float rowH = Mathf.Max(textH, artColH);

                row.rect.anchoredPosition = new Vector2(0f, -y);
                row.rect.sizeDelta = new Vector2(panelWidth, rowH);
                y += rowH + rowGap;
            }
            y -= rowGap; // 마지막 행 뒤 간격 제거 (cards.Count >= 1 은 위에서 보장됨)

            _rect.sizeDelta = new Vector2(panelWidth, y);

            _visible = true;
            if (wasHidden)
            {
                _group.alpha = 0f;
                _rect.localScale = new Vector3(HiddenScale, HiddenScale, 1f);
            }
            Follow(); // 첫 프레임 위치 확정 — 안 하면 (0,0) 에서 날아온다
        }

        // 멱등 — 컨트롤러의 Close 는 미선택 상태에서도 불린다.
        public void Hide()
        {
            _visible = false;
            _anchor = null;
        }

        // 알파/스케일만 — 카메라 포즈와 무관하므로 Update 로 충분.
        private void Update()
        {
            if (_root == null || !_root.activeSelf) return;
            float a = 1f - Mathf.Exp(-LerpK * Time.deltaTime);
            _group.alpha = Mathf.Lerp(_group.alpha, _visible ? 1f : 0f, a);
            float s = Mathf.Lerp(_rect.localScale.x, _visible ? 1f : HiddenScale, a);
            _rect.localScale = new Vector3(s, s, 1f);
            if (!_visible && _group.alpha < 0.02f) _root.SetActive(false);
        }

        // 위치는 LateUpdate — CameraDirector(-90) 가 LateUpdate 에서 카메라 포즈를 확정한다.
        // Update 에서 따라가면 지난 프레임 카메라를 읽어, 카메라가 움직이는 동안(킥/브리딩/
        // 페이즈 pitch 전환) 패널만 1프레임 뒤처져 유닛에서 미끄러진다. DcIconStripView 선례.
        // 게이트는 _visible 만 본다 — _root.activeSelf 를 조건에 넣으면, 아래 z<=0 경로가
        // 루트를 끈 순간 이 메서드가 영구 early-return 해서 복구선(SetActive(true))에 영영
        // 도달하지 못한다. 그러면 앵커가 다시 화면 앞으로 와도 패널은 사라진 채 컨트롤러만
        // 슬로우 lease 를 쥐고 있게 된다. 활성/비활성은 Follow 가 소유한다.
        private void LateUpdate()
        {
            if (_root == null || !_visible) return;
            Follow();
        }

        private void Follow()
        {
            // 앵커 파괴(유닛 사망) — 컨트롤러가 AttachmentsChanged 로 곧 닫지만, 그 사이
            // 프레임에 NRE 를 내지 않도록 여기서도 방어한다.
            if (_anchor == null || _camera == null) { _visible = false; return; }

            var sp = _camera.WorldToScreenPoint(_anchor.position);
            // 카메라 뒤 — WorldToScreenPoint 가 x/y 를 반전시켜 엉뚱한 곳에 뜬다.
            // (SpineUnitView.TryGetScreenRect 도 같은 가드를 둔다.)
            if (sp.z <= 0f) { _root.SetActive(false); return; }
            if (!_root.activeSelf) _root.SetActive(true);

            float sf = _canvas != null ? _canvas.scaleFactor : 1f;
            if (sf <= 0f) sf = 1f;
            // sizeDelta 는 캔버스 레퍼런스 단위, position 은 스크린 px → 클램프는 px 로 환산해야 한다.
            float w = _rect.sizeDelta.x * sf;
            float h = _rect.sizeDelta.y * sf;
            float gap = gapFromUnit * sf;
            float m = edgeMargin * sf;
            var safe = Screen.safeArea;

            // 우측 우선, 넘치면 좌측 플립(손패 툴팁 선례). pivot 이 (0,0.5) 로 고정이라
            // x 는 항상 패널의 좌측 모서리를 뜻한다.
            float x = sp.x + gap;
            if (x + w > safe.xMax - m) x = sp.x - gap - w;
            x = Mathf.Clamp(x, safe.xMin + m, Mathf.Max(safe.xMin + m, safe.xMax - m - w));

            // 세로 클램프 — 3행 스택은 세로가 길어 상/하단을 넘길 수 있다(툴팁은 좌우 플립만 있음).
            float y = sp.y;
            float half = h * 0.5f;
            float lo = safe.yMin + m + half;
            float hi = safe.yMax - m - half;
            y = hi >= lo ? Mathf.Clamp(y, lo, hi) : (safe.yMin + safe.yMax) * 0.5f;

            _rect.position = new Vector3(x, y, 0f);
        }

        // 타입별 플레이트 2종 캐시(DcIconStripSpawner.EnsureFrameSprites 선례).
        // 손패 툴팁은 trayConfig.fallbackBorder 단색 보더를 쓰지만, 이 패널은 Squad/Unit
        // 구분이 정보라 아이콘 스트립의 색 언어(골드/청록)를 따른다.
        private void EnsurePlates()
        {
            if (_squadPlate == null) _squadPlate = UiRoundedSprite.Make(12f, 2f, fill, squadBorder);
            if (_unitPlate == null) _unitPlate = UiRoundedSprite.Make(12f, 2f, fill, unitBorder);
        }

        private void EnsureRows(int count)
        {
            while (_rows.Count < count)
            {
                var row = new Row();
                row.root = new GameObject("Row" + _rows.Count, typeof(RectTransform), typeof(Image));
                row.root.transform.SetParent(_root.transform, false);
                row.rect = (RectTransform)row.root.transform;
                row.rect.anchorMin = new Vector2(0f, 1f);
                row.rect.anchorMax = new Vector2(0f, 1f);
                row.rect.pivot = new Vector2(0f, 1f);

                row.bg = row.root.GetComponent<Image>();
                row.bg.type = Image.Type.Sliced;
                row.bg.color = Color.white;
                row.bg.raycastTarget = false; // 계약 — 카드 드롭/조준 판정 비간섭

                var shadow = row.root.AddComponent<Shadow>();
                shadow.effectDistance = new Vector2(0f, -4f);
                shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);

                var artGo = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artGo.transform.SetParent(row.root.transform, false);
                var art = (RectTransform)artGo.transform;
                art.anchorMin = new Vector2(0f, 1f);
                art.anchorMax = new Vector2(0f, 1f);
                art.pivot = new Vector2(0f, 1f);
                row.art = artGo.GetComponent<Image>();
                row.art.raycastTarget = false;
                row.art.preserveAspect = true;

                row.header = BuildLabel(row.root.transform, "Header", 22f, TextAlignmentOptions.TopLeft);
                row.body = BuildLabel(row.root.transform, "Body", 19f, TextAlignmentOptions.TopLeft);

                _rows.Add(row);
            }
        }

        private TextMeshProUGUI BuildLabel(Transform parent, string name, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (labelFont != null) tmp.font = labelFont;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            // 명시 설정 필수 — TMP_Settings 기본은 Normal 이지만 그 로드는 Awake 에서 일어난다.
            // 비활성 계층에서 생성되면 Awake 가 안 돌아 enum 기본값 0(=NoWrap)이 남고, 본문이
            // 행 플레이트 밖으로 흘러나간다(실측 2026-07-15).
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }
    }
}
