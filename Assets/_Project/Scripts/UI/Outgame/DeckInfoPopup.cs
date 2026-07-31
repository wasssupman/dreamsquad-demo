using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core.Api;
using Wassup.Data;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // tournament-history-deck-view units 2·3 — 남의 덱을 보여주는 모달. **순수
    // 프레젠테이션**이다: 입력은 페이로드와 표시용 제목뿐이고 네트워크/세션/프로필을
    // 모른다. 파싱은 호출자가 하고(TournamentDeckInfo.Deserialize), 여기는 그린다.
    // 그래서 나중에 다른 출처(내 덱 미리보기 등)에도 그대로 붙는다.
    //
    // 카탈로그는 Setup 으로 주입한다 — 이 팝업은 히스토리 패널이 런타임에 만들어서
    // [SerializeField] 가 채워지지 않는다(씬에 오브젝트가 없다). 카탈로그가 null 이어도
    // 동작해야 한다: 그 경우 전 항목이 raw id 로 그려진다.
    //
    // 견고성 계약: payload null / 빈 배열 / 미해석 id / 카탈로그 null / 개수 초과가
    // 전부 정상 입력이다. 고정 슬롯을 쓰지 않고 온 만큼 그린다.
    public class DeckInfoPopup : MonoBehaviour
    {
        private static readonly Color GoldColor = new Color(1f, 0.78f, 0.28f, 1f);
        // 알파 1.0 — 모달이라 뒤가 비치면 안 된다. 0.98 이면 뒤 히스토리 행 텍스트가
        // 흐릿하게 읽힌다(어두운 패널 위 흰 글자라 2% 도 눈에 띈다). Play 에서 실측.
        private static readonly Color NavyFill = new Color(0.05f, 0.06f, 0.10f, 1f);
        private static readonly Color BadgeTextDark = new Color(0.10f, 0.09f, 0.06f, 1f);
        private static readonly Color SubText = new Color(0.72f, 0.76f, 0.82f, 1f);
        private static readonly Color CellFill = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color CellSelected = new Color(1f, 0.83f, 0.35f, 0.18f);
        private static readonly Color UnresolvedText = new Color(0.85f, 0.55f, 0.45f, 1f);

        private const float PanelW = 1200f;
        private const float PanelH = 900f;
        private const float Pad = 30f;
        private const float HeaderH = 132f;
        private const float FooterH = 104f;
        private const float DetailW = 380f;
        private const float ColGap = 20f;
        private const float PresetH = 64f;
        private const int SquadTab = 0;
        private const int DreamcatcherTab = 1;
        private const int PopupSortingOrder = 3200;

        private DefenderCatalog _unitCatalog;
        private DreamstoneCatalog _stoneCatalog;
        private DreamcatcherCardCatalog _cardCatalog;

        private TournamentDeckInfo.Payload _payload;
        private int _tab = SquadTab;
        // 탭별 선택 = (섹션, 항목) 인덱스. **id 가 아니라 인덱스**인 이유: 같은 카드 2장,
        // 같은 스톤 4개가 설계상 허용되므로 id 로 잡으면 어느 슬롯을 골랐는지 잃는다.
        private readonly int[] _selSection = { 0, 0 };
        private readonly int[] _selItem = { 0, 0 };

        // deck-info-preset-apply unit 1 — 탭별 이벤트 둘. Action<int> 하나로 탭 인덱스를
        // 넘기지 않는 이유는 호출측에서 0 이 무엇인지 읽히지 않기 때문이고, PresetApply.Target
        // 을 넘기지 않는 이유는 이 팝업이 프리셋 어휘를 몰라도 되기 때문이다(순수
        // 프레젠테이션). 페이로드도 넘기지 않는다 — 호출자(히스토리 패널)가 이미 갖고 있다.
        public event Action SquadApplyRequested;
        public event Action DreamcatcherApplyRequested;

        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _detailName;
        private TextMeshProUGUI _detailSub;
        private TextMeshProUGUI _detailBody;
        private TextMeshProUGUI _emptyLabel;
        private TextMeshProUGUI _detailEmpty;
        private Image _detailArt;
        private RectTransform _listContent;
        private RectTransform _detailRoot;
        private RectTransform _detailBodyContent;
        private RectTransform _listViewport;
        private readonly GameObject[] _presetButtons = new GameObject[2];
        private readonly Button[] _presetButtonBtns = new Button[2];
        private readonly Image[] _presetButtonImgs = new Image[2];
        private readonly TextMeshProUGUI[] _presetLabels = new TextMeshProUGUI[2];
        private Button _closeButton;
        private readonly Image[] _tabPlates = new Image[2];

        private Sprite _cellSprite;
        private Sprite _cellSelectedSprite;
        private Sprite _tabOn;
        private Sprite _tabOff;
        private Sprite _buttonSprite;
        private Sprite _artPlaceholder;
        private bool _built;

        // 히스토리 패널이 팝업을 만들면서 한 번 호출한다. 셋 중 무엇이 null 이어도 된다.
        public void Setup(DefenderCatalog units, DreamstoneCatalog stones, DreamcatcherCardCatalog cards)
        {
            _unitCatalog = units;
            _stoneCatalog = stones;
            _cardCatalog = cards;
        }

        // payload == null 은 정상 입력이다(덱 정보 없음).
        //
        // allowPresetApply: 내 덱을 볼 때는 false 를 넘겨 "프리셋 적용"을 **숨긴다**
        // — 내가 그때 쓴 덱을 내 프로필에 다시 쓰는 건 no-op 이다. 팝업은 그게 누구
        // 덱인지 모르므로(순수 프레젠테이션) 호출자가 판정해서 넘긴다.
        public void Show(TournamentDeckInfo.Payload payload, string title, bool allowPresetApply = true)
        {
            if (!_built) BuildCanvas();
            _payload = payload;
            SetPresetVisible(allowPresetApply);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            // BuildCanvas 는 비활성 상태에서 돌 수 있고(패널이 미리 만들어 둔다), 중첩
            // 캔버스의 overrideSorting 은 활성화 시점에 풀린다 — Play 에서 실측했다.
            // 활성화 뒤에 다시 박아야 히스토리 패널(2500) 위로 확실히 올라간다.
            var canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = PopupSortingOrder;
            }

            _titleLabel.text = string.IsNullOrWhiteSpace(title) ? "덱 정보" : title;
            _tab = SquadTab;
            ResetSelection(SquadTab);
            ResetSelection(DreamcatcherTab);
            RenderTab();
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
            if (_presetButtonBtns[SquadTab] != null)
                _presetButtonBtns[SquadTab].onClick.RemoveListener(ClickSquadPresetApply);
            if (_presetButtonBtns[DreamcatcherTab] != null)
                _presetButtonBtns[DreamcatcherTab].onClick.RemoveListener(ClickDreamcatcherPresetApply);
        }

        // ── Model ────────────────────────────────────────────────────────────
        private List<DeckInfoDisplay.Section> BuildSections(int tab)
        {
            var sections = new List<DeckInfoDisplay.Section>();
            if (_payload == null) return sections;

            if (tab == SquadTab)
            {
                sections.Add(new DeckInfoDisplay.Section("유닛",
                    DeckInfoDisplay.Units(_payload.squad?.units, _unitCatalog)));
                sections.Add(new DeckInfoDisplay.Section("드림스톤",
                    DeckInfoDisplay.Stones(_payload.squad?.stones, _stoneCatalog)));
            }
            else
            {
                sections.Add(new DeckInfoDisplay.Section("드림캐쳐 카드",
                    DeckInfoDisplay.Cards(_payload.dc?.cards, _cardCatalog)));
            }
            return sections;
        }

        // 첫 번째 **비어 있지 않은** 섹션의 첫 항목. 전부 비면 (0,-1) = 선택 없음.
        private void ResetSelection(int tab)
        {
            var sections = BuildSections(tab);
            for (int s = 0; s < sections.Count; s++)
            {
                if (sections[s].Items.Count == 0) continue;
                _selSection[tab] = s;
                _selItem[tab] = 0;
                return;
            }
            _selSection[tab] = 0;
            _selItem[tab] = -1;
        }

        // 버튼을 숨길 땐 목록이 그 자리까지 내려온다 — 그냥 끄면 64px 죽은 공간이
        // 남는다. 탭 전환 중에는 바뀌지 않으므로(Show 에서만 결정) "탭을 오갈 때
        // 자리가 뛰지 않는다"는 계약은 그대로다.
        private void SetPresetVisible(bool visible)
        {
            if (_presetButtons[SquadTab] == null || _listViewport == null) return;
            for (int i = 0; i < _presetButtons.Length; i++)
                if (_presetButtons[i] != null) _presetButtons[i].SetActive(visible);

            float bottom = Pad + FooterH + (visible ? PresetH + 10f : 0f);
            var min = new Vector2(Pad + DetailW + ColGap, bottom);
            _listViewport.offsetMin = min;
            if (_emptyLabel != null) ((RectTransform)_emptyLabel.transform).offsetMin = min;
        }

        internal bool IsPresetButtonVisible
            => _presetButtons[SquadTab] != null && _presetButtons[SquadTab].activeSelf;

        // 탭 전환 / 셀 선택 — 버튼 핸들러와 테스트가 **같은 경로**를 탄다.
        internal void SwitchTab(int tab)
        {
            if (!_built || _tab == tab || tab < 0 || tab >= _tabPlates.Length) return;
            _tab = tab;
            RenderTab();
        }

        internal void SelectCell(int section, int item)
        {
            if (!_built) return;
            _selSection[_tab] = section;
            _selItem[_tab] = item;
            RenderTab();
        }

        internal int CurrentTab => _tab;

        internal bool TryGetSelectedItem(out DeckInfoDisplay.Item item)
            => TryGetSelected(BuildSections(_tab), out item);

        private bool TryGetSelected(List<DeckInfoDisplay.Section> sections, out DeckInfoDisplay.Item item)
        {
            item = default;
            int s = _selSection[_tab];
            int i = _selItem[_tab];
            if (s < 0 || s >= sections.Count) return false;
            var items = sections[s].Items;
            if (i < 0 || i >= items.Count) return false;
            item = items[i];
            return true;
        }

        // ── Render ───────────────────────────────────────────────────────────
        private void RenderTab()
        {
            for (int t = 0; t < _tabPlates.Length; t++)
                if (_tabPlates[t] != null) _tabPlates[t].sprite = t == _tab ? _tabOn : _tabOff;

            var sections = BuildSections(_tab);
            RenderList(sections);
            RenderDetail(sections);
            RefreshPresetButtons();
        }

        private void RenderList(List<DeckInfoDisplay.Section> sections)
        {
            ClearChildren(_listContent);

            for (int s = 0; s < sections.Count; s++) CreateSection(sections[s], s);

            // 섹션이 하나도 없다 == 페이로드 자체가 없다(BuildSections 는 항상 고정 개수의
            // 섹션을 만든다). 섹션이 있는데 비어 있는 경우는 섹션별 "없음"이 담당하므로
            // 여기서 또 안내하지 않는다.
            _emptyLabel.text = "덱 정보가 없습니다.";
            _emptyLabel.gameObject.SetActive(sections.Count == 0);
        }

        private void CreateSection(DeckInfoDisplay.Section section, int sectionIndex)
        {
            var header = CreateLabel(_listContent, "SectionHeader", section.Title, 26,
                TextAlignmentOptions.MidlineLeft, GoldColor);
            var he = header.gameObject.AddComponent<LayoutElement>();
            he.preferredHeight = 38f;

            if (section.Items.Count == 0)
            {
                var none = CreateLabel(_listContent, "SectionEmpty", "없음", 22,
                    TextAlignmentOptions.MidlineLeft, SubText);
                none.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
                return;
            }

            // 가변 개수 그리드 — 고정 슬롯을 만들지 않는다(온 만큼 전부 그린다).
            var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup),
                typeof(ContentSizeFitter));
            grid.transform.SetParent(_listContent, false);
            var glg = grid.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(128f, 156f);
            glg.spacing = new Vector2(10f, 10f);
            glg.childAlignment = TextAnchor.UpperLeft;
            grid.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            grid.AddComponent<LayoutElement>().flexibleWidth = 1f;

            for (int i = 0; i < section.Items.Count; i++)
                CreateCell(grid.transform, section.Items[i], sectionIndex, i);
        }

        private void CreateCell(Transform parent, DeckInfoDisplay.Item item, int sectionIndex, int itemIndex)
        {
            bool selected = sectionIndex == _selSection[_tab] && itemIndex == _selItem[_tab];

            var go = new GameObject("Cell", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var plate = go.GetComponent<Image>();
            plate.sprite = selected ? _cellSelectedSprite : _cellSprite;
            plate.type = Image.Type.Sliced;

            int s = sectionIndex, i = itemIndex;
            go.GetComponent<Button>().onClick.AddListener(() => SelectCell(s, i));

            var art = new GameObject("Art", typeof(RectTransform), typeof(Image));
            art.transform.SetParent(go.transform, false);
            var artImg = art.GetComponent<Image>();
            artImg.sprite = item.Art != null ? item.Art : _artPlaceholder;
            artImg.preserveAspect = true;
            artImg.raycastTarget = false;
            artImg.color = item.Art != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            var artRt = (RectTransform)art.transform;
            artRt.anchorMin = new Vector2(0.5f, 1f);
            artRt.anchorMax = new Vector2(0.5f, 1f);
            artRt.pivot = new Vector2(0.5f, 1f);
            artRt.sizeDelta = new Vector2(100f, 100f);
            artRt.anchoredPosition = new Vector2(0f, -10f);

            var name = CreateLabel(go.transform, "Name", item.Name, 18,
                TextAlignmentOptions.Center, item.Resolved ? Color.white : UnresolvedText);
            name.textWrappingMode = TextWrappingModes.Normal;
            name.overflowMode = TextOverflowModes.Ellipsis;
            var nameRt = (RectTransform)name.transform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.sizeDelta = new Vector2(-12f, 42f);
            nameRt.anchoredPosition = new Vector2(0f, 6f);
        }

        private void RenderDetail(List<DeckInfoDisplay.Section> sections)
        {
            bool has = TryGetSelected(sections, out var item);

            // 좌측 컬럼을 통째로 끄지 않는다 — 380px 가 배경까지 사라져 구멍으로 보인다.
            // 대신 자리를 지키고 안내를 띄운다(카드 없이 플레이한 참가자의 드림캐쳐 탭,
            // 그리고 payload == null 인 모든 경우가 여기로 온다).
            _detailArt.gameObject.SetActive(has);
            _detailName.gameObject.SetActive(has);
            _detailSub.gameObject.SetActive(has);
            _detailBodyContent.gameObject.SetActive(has);
            _detailEmpty.gameObject.SetActive(!has);
            if (!has)
            {
                _detailEmpty.text = _payload == null ? "덱 정보가 없습니다." : "표시할 항목이 없습니다.";
                return;
            }

            _detailArt.sprite = item.Art != null ? item.Art : _artPlaceholder;
            _detailArt.color = item.Art != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            _detailName.text = item.Name;
            _detailName.color = item.Resolved ? Color.white : UnresolvedText;
            // 미해석 항목은 이름 자리에 id 가 들어가므로, 부제로 그 사실을 말해준다.
            _detailSub.text = item.Resolved ? item.Sub : $"{item.Sub} · {item.Id}";
            _detailBody.text = item.Body ?? "";
        }

        private static void ClearChildren(RectTransform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                child.SetParent(null, false);
                // EditMode 에서는 Destroy 가 예외를 던진다. 이 뷰의 견고성 계약을
                // EditMode 테스트로 고정하려면(두 번째 Show, 셀 클릭) 분기가 필요하다.
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        // ── Build ────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            _cellSprite = UiRoundedSprite.Make(12f, 0f, CellFill, CellFill);
            _cellSelectedSprite = UiRoundedSprite.Make(12f, 3f, CellSelected, GoldColor);
            _tabOn = UiRoundedSprite.Make(16f, 0f, GoldColor, GoldColor);
            _tabOff = UiRoundedSprite.Make(16f, 2f, new Color(1f, 1f, 1f, 0.06f),
                new Color(1f, 1f, 1f, 0.20f));
            _buttonSprite = UiRoundedSprite.Make(20f, 0f, Color.white, Color.white);
            _artPlaceholder = UiRoundedSprite.Make(12f, 0f, Color.white, Color.white);

            // **자기 rect 를 먼저 편다.** 이 팝업은 히스토리 패널이 런타임에 만든
            // 자식이라 RectTransform 이 기본값(100×100)이고, 중첩 캔버스는 rect 를
            // 구동하지 않는다 → 안 펴면 UiCanvasSetup 의 FullBleedRoot 가 그 100×100 에
            // 맞춰져 dim 이 화면을 못 덮는다(암전 없음 · 바깥 탭 닫기 죽음 · 뒤 페이지로
            // 클릭 통과). PresetConfirmPopup.EnsureBuilt 선례.
            StretchFull((RectTransform)transform);

            // 히스토리 패널(2500) 위에 뜬다. 중첩 캔버스라 overrideSorting 이 필수다.
            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: PopupSortingOrder);
            roots.Canvas.overrideSorting = true;
            roots.Canvas.sortingOrder = PopupSortingOrder;

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dim.transform.SetParent(roots.FullBleedRoot, false);
            StretchFull((RectTransform)dim.transform);
            var dimImg = dim.GetComponent<Image>();
            dimImg.sprite = UiRoundedSprite.Make(2f, 0f, Color.white, Color.white);
            dimImg.type = Image.Type.Sliced;
            dimImg.color = UiOverlay.Dim;
            dim.GetComponent<Button>().onClick.AddListener(Hide);

            var panel = new GameObject("DeckInfoPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(roots.SafeAreaRoot, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelW, PanelH);
            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = UiRoundedSprite.Make(30f, 4f, NavyFill,
                new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.95f));
            panelImg.type = Image.Type.Sliced;

            BuildHeader(panelRect);
            BuildDetail(panelRect);
            BuildList(panelRect);
            BuildFooter(panelRect);

            UiLayer.Apply(gameObject);
        }

        private void BuildHeader(RectTransform panel)
        {
            _titleLabel = CreateLabel(panel, "Title", "덱 정보", 40, TextAlignmentOptions.Center, GoldColor);
            _titleLabel.fontStyle = FontStyles.Bold;
            var tr = (RectTransform)_titleLabel.transform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-2f * Pad, 60f);
            tr.anchoredPosition = new Vector2(0f, -Pad * 0.6f);

            BuildTab(panel, SquadTab, "스쿼드", -150f);
            BuildTab(panel, DreamcatcherTab, "드림캐쳐", 150f);
        }

        private void BuildTab(RectTransform panel, int tab, string label, float x)
        {
            var go = new GameObject($"Tab{tab}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel, false);
            var img = go.GetComponent<Image>();
            img.sprite = tab == _tab ? _tabOn : _tabOff;
            img.type = Image.Type.Sliced;
            _tabPlates[tab] = img;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(280f, 56f);
            rt.anchoredPosition = new Vector2(x, -(Pad * 0.6f + 64f));

            int captured = tab;
            go.GetComponent<Button>().onClick.AddListener(() => SwitchTab(captured));

            var text = CreateLabel(go.transform, "Label", label, 26, TextAlignmentOptions.Center, BadgeTextDark);
            text.fontStyle = FontStyles.Bold;
            StretchFull((RectTransform)text.transform);
        }

        // 좌 = 선택 항목 상세.
        private void BuildDetail(RectTransform panel)
        {
            var root = new GameObject("Detail", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(panel, false);
            _detailRoot = (RectTransform)root.transform;
            _detailRoot.anchorMin = new Vector2(0f, 0f);
            _detailRoot.anchorMax = new Vector2(0f, 1f);
            _detailRoot.pivot = new Vector2(0f, 0.5f);
            _detailRoot.offsetMin = new Vector2(Pad, Pad + FooterH);
            _detailRoot.offsetMax = new Vector2(Pad + DetailW, -(Pad + HeaderH));
            var bg = root.GetComponent<Image>();
            bg.sprite = UiRoundedSprite.Make(18f, 0f, new Color(0f, 0f, 0f, 0.24f), new Color(0f, 0f, 0f, 0.24f));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;

            var art = new GameObject("Art", typeof(RectTransform), typeof(Image));
            art.transform.SetParent(root.transform, false);
            _detailArt = art.GetComponent<Image>();
            _detailArt.preserveAspect = true;
            _detailArt.raycastTarget = false;
            var artRt = (RectTransform)art.transform;
            artRt.anchorMin = artRt.anchorMax = new Vector2(0.5f, 1f);
            artRt.pivot = new Vector2(0.5f, 1f);
            artRt.sizeDelta = new Vector2(240f, 240f);
            artRt.anchoredPosition = new Vector2(0f, -24f);

            _detailName = CreateLabel(root.transform, "Name", "", 32, TextAlignmentOptions.Center, Color.white);
            _detailName.fontStyle = FontStyles.Bold;
            var nRt = (RectTransform)_detailName.transform;
            nRt.anchorMin = new Vector2(0f, 1f);
            nRt.anchorMax = new Vector2(1f, 1f);
            nRt.pivot = new Vector2(0.5f, 1f);
            nRt.sizeDelta = new Vector2(-24f, 46f);
            nRt.anchoredPosition = new Vector2(0f, -276f);

            _detailSub = CreateLabel(root.transform, "Sub", "", 22, TextAlignmentOptions.Center, SubText);
            var sRt = (RectTransform)_detailSub.transform;
            sRt.anchorMin = new Vector2(0f, 1f);
            sRt.anchorMax = new Vector2(1f, 1f);
            sRt.pivot = new Vector2(0.5f, 1f);
            sRt.sizeDelta = new Vector2(-24f, 34f);
            sRt.anchoredPosition = new Vector2(0f, -324f);

            // 설명은 스크롤한다 — 시트 구동 문안이라 길이가 자랄 수 있고, 고정 박스면
            // 조용히 잘린다(잘렸다는 사실조차 화면에 안 남는다).
            var viewport = new GameObject("BodyViewport", typeof(RectTransform),
                typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(root.transform, false);
            var vpRt = (RectTransform)viewport.transform;
            vpRt.anchorMin = new Vector2(0f, 0f);
            vpRt.anchorMax = new Vector2(1f, 1f);
            vpRt.offsetMin = new Vector2(18f, 18f);
            vpRt.offsetMax = new Vector2(-18f, -366f);

            var content = new GameObject("BodyContent", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            _detailBodyContent = (RectTransform)content.transform;
            _detailBodyContent.anchorMin = new Vector2(0f, 1f);
            _detailBodyContent.anchorMax = new Vector2(1f, 1f);
            _detailBodyContent.pivot = new Vector2(0.5f, 1f);
            _detailBodyContent.offsetMin = Vector2.zero;
            _detailBodyContent.offsetMax = Vector2.zero;
            var bvlg = content.GetComponent<VerticalLayoutGroup>();
            bvlg.childControlWidth = true;
            bvlg.childControlHeight = true;
            bvlg.childForceExpandWidth = true;
            bvlg.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bodyScroll = viewport.GetComponent<ScrollRect>();
            bodyScroll.content = _detailBodyContent;
            bodyScroll.viewport = vpRt;
            bodyScroll.horizontal = false;
            bodyScroll.vertical = true;
            bodyScroll.movementType = ScrollRect.MovementType.Elastic;
            bodyScroll.scrollSensitivity = 20f;

            _detailBody = CreateLabel(content.transform, "Body", "", 21,
                TextAlignmentOptions.TopLeft, new Color(0.88f, 0.90f, 0.94f, 1f));
            _detailBody.textWrappingMode = TextWrappingModes.Normal;

            // 선택이 없을 때 좌측 자리를 지키는 안내.
            _detailEmpty = CreateLabel(root.transform, "DetailEmpty", "", 26,
                TextAlignmentOptions.Center, SubText);
            StretchFull((RectTransform)_detailEmpty.transform);
            _detailEmpty.gameObject.SetActive(false);
        }

        // 우 = 목록(스크롤) + 하단 프리셋 적용 버튼.
        private void BuildList(RectTransform panel)
        {
            float left = Pad + DetailW + ColGap;

            var viewport = new GameObject("ListViewport", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(panel, false);
            var vpRt = (RectTransform)viewport.transform;
            vpRt.anchorMin = new Vector2(0f, 0f);
            vpRt.anchorMax = new Vector2(1f, 1f);
            vpRt.offsetMin = new Vector2(left, Pad + FooterH + PresetH + 10f);
            vpRt.offsetMax = new Vector2(-Pad, -(Pad + HeaderH));
            var vpImg = viewport.GetComponent<Image>();
            vpImg.sprite = UiRoundedSprite.Make(18f, 0f, new Color(0f, 0f, 0f, 0.24f), new Color(0f, 0f, 0f, 0.24f));
            vpImg.type = Image.Type.Sliced;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = (RectTransform)content.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 14, 14);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = vpRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 24f;
            _listContent = contentRt;
            _listViewport = vpRt;

            _emptyLabel = CreateLabel(panel, "Empty", "", 28, TextAlignmentOptions.Center, SubText);
            var eRt = (RectTransform)_emptyLabel.transform;
            eRt.anchorMin = new Vector2(0f, 0f);
            eRt.anchorMax = new Vector2(1f, 1f);
            eRt.offsetMin = vpRt.offsetMin;
            eRt.offsetMax = vpRt.offsetMax;
            _emptyLabel.gameObject.SetActive(false);

            BuildPresetButton(panel, left);
        }

        // 적용 대상을 탭 상태에 숨겨 두지 않는다. 스쿼드/드림캐쳐 버튼을 명시적으로
        // 분리해 한 번의 클릭이 정확히 한 종류의 프리셋만 요청하도록 한다.
        private void BuildPresetButton(RectTransform panel, float left)
        {
            const float gap = 10f;
            float width = PanelW - left - Pad;
            float half = (width - gap) * 0.5f;
            BuildPresetButton(panel, SquadTab, "SquadPresetApplyButton", "스쿼드 프리셋 저장",
                left, left + half, ClickSquadPresetApply);
            BuildPresetButton(panel, DreamcatcherTab, "DreamcatcherPresetApplyButton",
                "드림캐쳐 프리셋 저장", left + half + gap, PanelW - Pad,
                ClickDreamcatcherPresetApply);
            RefreshPresetButtons();
        }

        private void BuildPresetButton(RectTransform panel, int target, string objectName,
            string labelText, float xMin, float xMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(panel, false);
            _presetButtons[target] = go;
            _presetButtonImgs[target] = go.GetComponent<Image>();
            _presetButtonImgs[target].sprite = _buttonSprite;
            _presetButtonImgs[target].type = Image.Type.Sliced;
            _presetButtonImgs[target].color = new Color(1f, 1f, 1f, 0.10f);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(xMin, Pad + FooterH);
            rt.offsetMax = new Vector2(xMax, Pad + FooterH + PresetH);

            _presetButtonBtns[target] = go.GetComponent<Button>();
            _presetButtonBtns[target].interactable = false;
            _presetButtonBtns[target].onClick.AddListener(onClick);

            _presetLabels[target] = CreateLabel(go.transform, "Label", labelText, 24,
                TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0.55f));
            _presetLabels[target].fontStyle = FontStyles.Bold;
            StretchFull((RectTransform)_presetLabels[target].transform);
        }

        // 각 버튼은 자기 데이터만 보고 활성화된다. 활성 탭은 표시 상태일 뿐 저장 대상을
        // 바꾸지 않는다.
        private void RefreshPresetButtons()
        {
            RefreshPresetButton(SquadTab);
            RefreshPresetButton(DreamcatcherTab);
        }

        private void RefreshPresetButton(int target)
        {
            if (_presetButtonBtns[target] == null) return;
            var sections = BuildSections(target);
            int items = 0;
            for (int s = 0; s < sections.Count; s++) items += sections[s].Items.Count;
            bool usable = items > 0;

            _presetButtonBtns[target].interactable = usable;
            _presetButtonImgs[target].color = usable ? GoldColor : new Color(1f, 1f, 1f, 0.10f);
            _presetLabels[target].color = usable ? BadgeTextDark : new Color(1f, 1f, 1f, 0.55f);
        }

        internal void ClickSquadPresetApply() => ClickPresetApply(SquadTab);
        internal void ClickDreamcatcherPresetApply() => ClickPresetApply(DreamcatcherTab);

        // 기존 테스트/호출용 진입점은 활성 탭의 명시적 버튼으로 위임한다.
        internal void ClickPresetApply() => ClickPresetApply(_tab);

        private void ClickPresetApply(int target)
        {
            if (_presetButtonBtns[target] == null || !_presetButtonBtns[target].interactable) return;
            if (_presetButtons[target] == null || !_presetButtons[target].activeSelf) return;

            // 재진입/같은 프레임의 중복 클릭도 두 번째 요청을 만들 수 없게 먼저 잠근다.
            for (int i = 0; i < _presetButtonBtns.Length; i++)
                if (_presetButtonBtns[i] != null) _presetButtonBtns[i].interactable = false;

            if (target == SquadTab) SquadApplyRequested?.Invoke();
            else DreamcatcherApplyRequested?.Invoke();
            Hide();
        }

        internal bool IsPresetButtonInteractable
            => _presetButtonBtns[_tab] != null && _presetButtonBtns[_tab].interactable;

        internal bool IsSquadPresetButtonInteractable
            => _presetButtonBtns[SquadTab] != null && _presetButtonBtns[SquadTab].interactable;

        internal bool IsDreamcatcherPresetButtonInteractable
            => _presetButtonBtns[DreamcatcherTab] != null
                && _presetButtonBtns[DreamcatcherTab].interactable;

        private void BuildFooter(RectTransform panel)
        {
            var btn = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(panel, false);
            var btnImg = btn.GetComponent<Image>();
            btnImg.sprite = _buttonSprite;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = GoldColor;
            var btnRt = (RectTransform)btn.transform;
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.sizeDelta = new Vector2(300f, 74f);
            btnRt.anchoredPosition = new Vector2(0f, Pad * 0.7f);
            _closeButton = btn.GetComponent<Button>();
            _closeButton.onClick.AddListener(Hide);

            var label = CreateLabel(btn.transform, "Label", "닫기", 30, TextAlignmentOptions.Center, BadgeTextDark);
            label.fontStyle = FontStyles.Bold;
            StretchFull((RectTransform)label.transform);
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
            int fontSize, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
