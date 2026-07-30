using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // page-local-presets unit 2 — 스쿼드/드림캐쳐 페이지가 공유하는 프리셋 조작 바.
    // 목록 팝업 + 이름 필드 + 버튼 4개 + dirty 배지를 만들고 **이벤트만 raise** 한다.
    //
    // 프레젠테이션 전용이다 — 프리셋이 무엇인지, 무엇이 확정인지, 언제 dim 인지 모른다.
    // 그 판단은 전부 페이지 컨트롤러 소유이고 여기로는 결과만 내려온다(SetEntries /
    // SetDirty / SetButtonEnabled). 그래서 이 파일에는 PlayerProfile·SquadPreset·
    // ProfileStore 참조가 하나도 없다.
    //
    // 목록이 TMP_Dropdown 이 아닌 이유: 셀에 이름 + 편성 썸네일을 같이 그려야 하는데
    // 기본 위젯은 리치 셀을 만들 수 없다. SquadRosterBrowser 의 스크롤 골격을 재사용한다.
    public class PresetBarView : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private float rowHeight = 92f;
        [SerializeField] private Vector2 buttonSize = new Vector2(150f, 74f);
        [SerializeField] private float popupWidth = 560f;
        [SerializeField] private float cellHeight = 120f;

        public event Action<string> PresetPicked;   // 목록에서 다른 프리셋 선택(전환 요청)
        public event Action CreateClicked;          // [+] 셀
        public event Action CommitClicked;          // [선택]
        public event Action SaveClicked;            // [저장]
        public event Action ResetClicked;           // [리셋]
        public event Action DeleteClicked;          // [삭제]
        public event Action<string> NameCommitted;  // onEndEdit 에서만 발화

        // 컨트롤러가 넘기는 목록 1행. thumbs 는 **이미 로드된** 카탈로그 스프라이트를
        // 그대로 받는다 — 뷰가 에셋을 찾지 않으므로 신규 로드 0.
        public struct Entry
        {
            public string id;
            public string name;
            public Sprite[] thumbs;
            public bool committed;
        }

        private static readonly Color BarBg = new Color(0.10f, 0.11f, 0.15f, 1f);
        private static readonly Color FieldBg = new Color(0.16f, 0.18f, 0.24f, 1f);
        private static readonly Color BtnNeutral = new Color(0.24f, 0.26f, 0.32f, 1f);
        private static readonly Color BtnAccent = new Color(0.16f, 0.5f, 0.28f, 1f);
        private static readonly Color BtnDanger = new Color(0.48f, 0.20f, 0.22f, 1f);
        private static readonly Color BtnDim = new Color(0.18f, 0.19f, 0.22f, 1f);
        private static readonly Color DirtyText = new Color(1f, 0.78f, 0.35f, 1f);
        private static readonly Color PopupBg = new Color(0.07f, 0.08f, 0.11f, 0.98f);
        private static readonly Color CellBg = new Color(0.13f, 0.14f, 0.19f, 1f);
        private static readonly Color CellSel = new Color(0.20f, 0.34f, 0.62f, 1f);
        private static readonly Color CommitBadge = new Color(0.20f, 0.55f, 0.32f, 0.96f);

        private bool _built;
        private TMP_Text _pickerLabel;
        private TMP_InputField _nameField;
        private TMP_Text _dirtyBadge;
        private GameObject _popup;
        private RectTransform _popupContent;

        private Image _commitBg, _saveBg, _resetBg, _deleteBg;
        private Button _commitBtn, _saveBtn, _resetBtn, _deleteBtn;

        private readonly List<GameObject> _cells = new List<GameObject>();
        private string _viewingId;
        private bool _canCreate = true;

        // ---- public API ---------------------------------------------------

        public void Init(TMP_FontAsset f) { if (f != null) font = f; }

        public void SetEntries(IReadOnlyList<Entry> entries, string viewingId, bool canCreate)
        {
            EnsureBuilt();
            _viewingId = viewingId;
            _canCreate = canCreate;

            string current = null;
            if (entries != null)
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i].id == viewingId) current = entries[i].name;
            if (_pickerLabel != null)
                _pickerLabel.text = (string.IsNullOrEmpty(current) ? "프리셋" : current) + "  ▼";

            RebuildCells(entries);
        }

        public void SetName(string name)
        {
            EnsureBuilt();
            // SetTextWithoutNotify — 컨트롤러가 값을 밀어넣는 것이 NameCommitted 로
            // 되돌아오면 무한 왕복이 된다.
            if (_nameField != null) _nameField.SetTextWithoutNotify(name ?? "");
        }

        public void SetDirty(bool dirty)
        {
            EnsureBuilt();
            if (_dirtyBadge != null) _dirtyBadge.gameObject.SetActive(dirty);
            // [저장]을 엑센트로 — 계약 4. 배지와 색이 함께 "지금 저장해야 반입된다"를 말한다.
            if (_saveBg != null && _saveBtn != null)
                _saveBg.color = _saveBtn.interactable ? (dirty ? BtnAccent : BtnNeutral) : BtnDim;
        }

        // dim 조건은 컨트롤러가 판단한다 — 뷰는 받은 대로만 반영한다.
        public void SetButtonEnabled(bool commit, bool save, bool reset, bool delete)
        {
            EnsureBuilt();
            Apply(_commitBtn, _commitBg, commit, BtnNeutral);
            Apply(_saveBtn, _saveBg, save, BtnNeutral);
            Apply(_resetBtn, _resetBg, reset, BtnNeutral);
            Apply(_deleteBtn, _deleteBg, delete, BtnDanger);
        }

        public void ClosePopup()
        {
            if (_popup != null) _popup.SetActive(false);
        }

        private static void Apply(Button b, Image bg, bool on, Color onColor)
        {
            if (b != null) b.interactable = on;
            if (bg != null) bg.color = on ? onColor : BtnDim;
        }

        // ---- build --------------------------------------------------------

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var self = (RectTransform)transform;
            var bg = gameObject.GetComponent<Image>();
            if (bg == null) bg = gameObject.AddComponent<Image>();
            bg.color = BarBg;

            // 1행: 피커 + 이름 + 버튼 4. 바 높이는 고정이고 배지는 그 안에서 토글된다 —
            // 배지 등장으로 바가 커지면 아래 밴드가 흔들린다.
            var row = Rect("Row", self, new Vector2(0f, 0f), new Vector2(1f, 1f));
            row.offsetMin = new Vector2(12f, 6f);
            row.offsetMax = new Vector2(-12f, -6f);

            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.UpperLeft;
            hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            _pickerLabel = MakePicker(row);
            _nameField = MakeNameField(row);
            _commitBtn = MakeButton(row, "선택", BtnNeutral, () => CommitClicked?.Invoke(), out _commitBg);
            _saveBtn = MakeButton(row, "저장", BtnNeutral, () => SaveClicked?.Invoke(), out _saveBg);
            _resetBtn = MakeButton(row, "리셋", BtnNeutral, () => ResetClicked?.Invoke(), out _resetBg);
            _deleteBtn = MakeButton(row, "삭제", BtnDanger, () => DeleteClicked?.Invoke(), out _deleteBg);

            // 2행 배지 — dirty 일 때만. 계약 4: 팝업이 아니라 상시 표시.
            var badgeGo = new GameObject("DirtyBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeGo.transform.SetParent(self, false);
            var brt = (RectTransform)badgeGo.transform;
            brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(-24f, 30f); brt.anchoredPosition = new Vector2(0f, 4f);
            _dirtyBadge = badgeGo.GetComponent<TextMeshProUGUI>();
            _dirtyBadge.text = "● 미저장 변경 — 반입은 지금 저장분";
            _dirtyBadge.fontSize = 22; _dirtyBadge.color = DirtyText;
            _dirtyBadge.alignment = TextAlignmentOptions.Left;
            _dirtyBadge.raycastTarget = false;
            if (font != null) _dirtyBadge.font = font;
            badgeGo.SetActive(false);

            BuildPopup(self);
            UiLayer.Apply(gameObject);
        }

        private TMP_Text MakePicker(Transform parent)
        {
            var go = new GameObject("Picker", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(popupWidth * 0.62f, rowHeight * 0.8f);
            go.GetComponent<Image>().color = FieldBg;
            go.GetComponent<Button>().onClick.AddListener(TogglePopup);

            var t = Text(go.transform, "프리셋  ▼", 26, TextAlignmentOptions.Left);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(14f, 0f); rt.offsetMax = new Vector2(-10f, 0f);
            return t;
        }

        private TMP_InputField MakeNameField(Transform parent)
        {
            var go = new GameObject("NameField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(popupWidth * 0.5f, rowHeight * 0.8f);
            go.GetComponent<Image>().color = FieldBg;

            var textArea = Rect("TextArea", go.transform, Vector2.zero, Vector2.one);
            textArea.offsetMin = new Vector2(12f, 4f); textArea.offsetMax = new Vector2(-12f, -4f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var text = Text(textArea, "", 26, TextAlignmentOptions.Left);
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            var field = go.GetComponent<TMP_InputField>();
            field.textViewport = textArea;
            field.textComponent = (TMP_Text)text;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 24;
            field.text = "";
            // **onValueChanged 는 쓰지 않는다** — 키스트로크마다 dirty 가 뜨고, 한글 IME
            // 조합 중 상태까지 새어 들어온다. 확정/포커스아웃에서 1회만 알린다.
            field.onEndEdit.AddListener(v => NameCommitted?.Invoke(v));
            return field;
        }

        private Button MakeButton(Transform parent, string label, Color color, Action onClick, out Image bgOut)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = buttonSize;
            bgOut = go.GetComponent<Image>();
            bgOut.color = color;
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick());

            var t = Text(go.transform, label, 26, TextAlignmentOptions.Center);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return btn;
        }

        // 팝업은 페이지 루트가 아니라 이 바의 자식이되 **바 밖으로 내려오는** 패널이다.
        // 브라우저 그리드 위에 그려져야 하므로 열 때 SetAsLastSibling 한다.
        private void BuildPopup(RectTransform self)
        {
            _popup = new GameObject("PresetPopup", typeof(RectTransform), typeof(Image));
            _popup.transform.SetParent(self, false);
            var prt = (RectTransform)_popup.transform;
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(0f, 0f);
            prt.pivot = new Vector2(0f, 1f);
            prt.anchoredPosition = new Vector2(12f, -4f);
            prt.sizeDelta = new Vector2(popupWidth, cellHeight * 5f);
            _popup.GetComponent<Image>().color = PopupBg;

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_popup.transform, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(6f, 6f); srt.offsetMax = new Vector2(-6f, -6f);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vrt = (RectTransform)viewport.transform;
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content",
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _popupContent = (RectTransform)content.transform;
            _popupContent.anchorMin = new Vector2(0f, 1f); _popupContent.anchorMax = new Vector2(1f, 1f);
            _popupContent.pivot = new Vector2(0.5f, 1f);
            _popupContent.anchoredPosition = Vector2.zero; _popupContent.sizeDelta = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f; vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childControlWidth = true; vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = vrt; scroll.content = _popupContent;

            _popup.SetActive(false);
        }

        private void TogglePopup()
        {
            if (_popup == null) return;
            bool next = !_popup.activeSelf;
            _popup.SetActive(next);
            if (!next) return;

            // 팝업은 바 밖(아래)으로 내려오는데 UGUI 렌더 순서는 계층 순서다. 두 페이지
            // 빌더는 PresetBar 를 BrowserPanel **앞에** 만들므로, 팝업을 바 안에서만
            // SetAsLastSibling 해도 브라우저 그리드가 그 위에 그려져 목록이 가려진다.
            // 그래서 **바 자체**를 페이지 루트의 마지막 형제로 올린다(바는 자기 밴드를
            // 독점하므로 올려도 다른 밴드를 가리지 않는다).
            transform.SetAsLastSibling();
            _popup.transform.SetAsLastSibling();
        }

        // ---- cells --------------------------------------------------------

        private void RebuildCells(IReadOnlyList<Entry> entries)
        {
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i] != null) Destroy(_cells[i]);
            _cells.Clear();
            if (_popupContent == null) return;

            if (entries != null)
                for (int i = 0; i < entries.Count; i++) AddCell(entries[i]);

            AddCreateCell();
        }

        private void AddCell(Entry e)
        {
            var root = new GameObject("PresetCell", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(_popupContent, false);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(0f, cellHeight);
            var le = root.AddComponent<LayoutElement>();
            le.minHeight = cellHeight; le.preferredHeight = cellHeight;

            bool isViewing = e.id == _viewingId;
            root.GetComponent<Image>().color = isViewing ? CellSel : CellBg;

            string captured = e.id;
            root.GetComponent<Button>().onClick.AddListener(() =>
            {
                ClosePopup();
                PresetPicked?.Invoke(captured);
            });

            var label = Text(root.transform, string.IsNullOrEmpty(e.name) ? "(이름 없음)" : e.name,
                24, TextAlignmentOptions.TopLeft);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0.5f); lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(12f, 0f); lrt.offsetMax = new Vector2(-12f, -6f);

            if (e.committed)
            {
                var badge = new GameObject("Committed", typeof(RectTransform), typeof(Image));
                badge.transform.SetParent(root.transform, false);
                badge.GetComponent<Image>().color = CommitBadge;
                var art = (RectTransform)badge.transform;
                art.anchorMin = new Vector2(1f, 1f); art.anchorMax = new Vector2(1f, 1f);
                art.pivot = new Vector2(1f, 1f);
                art.sizeDelta = new Vector2(74f, 30f); art.anchoredPosition = new Vector2(-8f, -6f);
                var bt = Text(badge.transform, "확정", 19, TextAlignmentOptions.Center);
                var btr = bt.rectTransform;
                btr.anchorMin = Vector2.zero; btr.anchorMax = Vector2.one;
                btr.offsetMin = Vector2.zero; btr.offsetMax = Vector2.zero;
            }

            // 편성 썸네일 — 하단 절반에 좌측부터. 스프라이트는 컨트롤러가 준 것을 그대로 쓴다.
            if (e.thumbs != null)
            {
                float size = cellHeight * 0.40f;
                for (int i = 0; i < e.thumbs.Length; i++)
                {
                    var sp = e.thumbs[i];
                    var thumb = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
                    thumb.transform.SetParent(root.transform, false);
                    var trt = (RectTransform)thumb.transform;
                    trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(0f, 0f);
                    trt.pivot = new Vector2(0f, 0f);
                    trt.sizeDelta = new Vector2(size, size);
                    trt.anchoredPosition = new Vector2(12f + i * (size + 4f), 8f);
                    var img = thumb.GetComponent<Image>();
                    img.sprite = sp;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    // 스프라이트가 없는 칸(빈 슬롯)은 어두운 플레이스홀더로 남긴다.
                    if (sp == null) img.color = new Color(1f, 1f, 1f, 0.10f);
                }
            }

            _cells.Add(root);
        }

        private void AddCreateCell()
        {
            var root = new GameObject("CreateCell", typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(_popupContent, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(0f, cellHeight * 0.6f);
            var le = root.AddComponent<LayoutElement>();
            le.minHeight = cellHeight * 0.6f; le.preferredHeight = cellHeight * 0.6f;
            root.GetComponent<Image>().color = _canCreate ? CellBg : BtnDim;

            var btn = root.GetComponent<Button>();
            btn.interactable = _canCreate;
            btn.onClick.AddListener(() =>
            {
                ClosePopup();
                CreateClicked?.Invoke();
            });

            var t = Text(root.transform, _canCreate ? "+  새 프리셋" : "+  상한 도달", 24, TextAlignmentOptions.Center);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            if (!_canCreate) t.color = new Color(1f, 1f, 1f, 0.45f);

            _cells.Add(root);
        }

        // ---- primitives ---------------------------------------------------

        private RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return rt;
        }

        private TMP_Text Text(Transform parent, string content, int size, TextAlignmentOptions align)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = content; t.fontSize = size; t.color = Color.white;
            t.alignment = align; t.raycastTarget = false;
            if (font != null) t.font = font;
            return t;
        }
    }
}
