using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core.Api;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // tournament-history Unit 2 → tournament-history-deck-view unit 0 — lobby
    // history page, now **one depth**: left column lists my tournament entries,
    // right column shows the selected tournament's ranking. The old modal detail
    // popup is retired — selecting a row swaps only the right column, so entries
    // can be compared by clicking down the list.
    //
    // Visibility (SetActive) is owned by OutgameMenuController.RaiseExclusive; this
    // view loads on enable and reports close via onClose (LoginPanelView precedent).
    // Guests (empty IdToken) skip the API and show an empty state.
    //
    // Two independent fetches, two independent epochs: the list load and the
    // ranking load fail and retry separately (a failed ranking must still leave the
    // list usable so another tournament can be picked).
    public class TournamentHistoryPanel : MonoBehaviour
    {
        private static readonly Color GoldColor = new Color(1f, 0.78f, 0.28f, 1f);
        private static readonly Color NavyFill = new Color(0.05f, 0.06f, 0.10f, 0.98f);
        private static readonly Color SubText = new Color(0.72f, 0.76f, 0.82f, 1f);
        private static readonly Color RowFill = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color RowSelectedFill = new Color(1f, 0.83f, 0.35f, 0.18f);

        // 2-column: the page is landscape-only (ResultScreen precedent uses the same
        // widened panel). Left = entry list, right = ranking for the selection.
        private const float PanelW = 1440f;
        private const float PanelH = 960f;
        private const float Pad = 34f;
        private const float HeaderH = 150f;
        private const float ColGap = 20f;
        private const float LeftColW = 620f;
        private const float RankTitleH = 72f;
        private const float RowH = 96f;
        private const float RowInset = 24f;   // 행 좌변 여백
        private const float RightPad = 22f;   // 랭크/점수 컬럼의 우변 여백
        private const float RightColW = 200f; // 랭크/점수 컬럼 폭 (좌측 컬럼이 여기서 멈춘다)

        // 행 라벨 밴드 = (하단 inset, 상단 inset). 이름/랭크는 위, 날짜/점수는 아래.
        // 밴드는 반드시 offsetMin/offsetMax 로만 깎는다 — stretch 축에 anchoredPosition
        // 을 대입하면 밴드가 중앙으로 리셋돼 위/아래 라벨이 같은 rect 로 붕괴한다.
        private static readonly Vector2 TopBand = new Vector2(RowH * 0.42f, -12f);
        private static readonly Vector2 BottomBand = new Vector2(12f, -RowH * 0.42f);

        // Fired when the user backs out. OutgameMenuController subscribes and runs
        // its ClosePanels (re-shows the lobby menu that RaiseExclusive hid).
        public event Action onClose;

        private RectTransform _listContent;
        private RectTransform _rankContent;
        private TextMeshProUGUI _listStatus;
        private TextMeshProUGUI _rankStatus;
        private TextMeshProUGUI _rankTitle;
        private LeaderboardList _leaderboard;
        private Sprite _panelSprite;
        private Sprite _rowSprite;
        private Sprite _rowSelectedSprite;
        private Sprite _buttonSprite;
        private bool _built;
        private int _listEpoch;
        private int _rankEpoch;
        private string _selectedEntryId;

        // Row plates kept so the selection highlight can move without rebuilding.
        private readonly List<KeyValuePair<string, Image>> _rowPlates = new List<KeyValuePair<string, Image>>();

        private void OnEnable()
        {
            if (!_built) BuildCanvas();
            LoadEntries();
        }

        private void OnDisable()
        {
            // drop any in-flight fetch when the page closes
            _listEpoch++;
            _rankEpoch++;
        }

        // ── List (left column) ───────────────────────────────────────────────
        private void LoadEntries()
        {
            int epoch = ++_listEpoch;
            _rankEpoch++; // a fresh list invalidates whatever ranking was loading
            ClearRows();
            ClearRanking();
            SetListStatus("불러오는 중...");
            SetRankStatus("");
            _rankTitle.text = "";
            _selectedEntryId = null;

            AuthCredential credential = UserSession.Credential;
            string baseUrl = UserSession.GameServerBaseUrl;
            if (!UserSession.HasAccount) // guest / not signed in
            {
                SetEmptyBoth("로그인이 필요합니다.");
                return;
            }
            if (string.IsNullOrEmpty(baseUrl))
            {
                SetEmptyBoth("기록을 불러올 수 없습니다.");
                return;
            }

            TournamentApi.GetUnclaimedEntries(baseUrl, credential, (list, error) =>
            {
                if (this == null || epoch != _listEpoch || !isActiveAndEnabled) return;
                if (list == null)
                {
                    SetEmptyBoth("기록 조회에 실패했습니다.");
                    Debug.LogWarning($"[TournamentHistoryPanel] list fetch failed: {error}");
                    return;
                }

                var sorted = SortRecentFirst(list);
                if (sorted.Count == 0)
                {
                    SetEmptyBoth("참여한 토너먼트가 없습니다.");
                    return;
                }

                SetListStatus("");
                for (int i = 0; i < sorted.Count; i++) CreateRow(sorted[i]);

                // 진입 = 가장 최근 토너먼트 자동 포커스.
                Select(sorted[0].tournamentEntryId);
            });
        }

        // 최신 우선 정렬. 서버 정렬 순서에 기대지 않는다. createdTime 이 없거나 파싱
        // 불가한 항목은 **버리지 않고** 맨 뒤로 보낸다 — 표시 전용 필드 하나 때문에
        // 참가 기록이 목록에서 사라지면 안 된다. 같은 조건끼리는 원래 순서를 유지한다
        // (List.Sort 가 불안정 정렬이라 인덱스를 최종 타이브레이커로 쓴다).
        internal static List<TournamentApi.UserTournamentResultEntry> SortRecentFirst(
            IReadOnlyList<TournamentApi.UserTournamentResultEntry> list)
        {
            var keyed = new List<(TournamentApi.UserTournamentResultEntry entry, int index, long ticks, bool dated)>();
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == null) continue;
                    bool dated = TryParseCreated(list[i].createdTime, out DateTime dt);
                    keyed.Add((list[i], i, dated ? dt.Ticks : 0L, dated));
                }
            }

            keyed.Sort((a, b) =>
            {
                if (a.dated != b.dated) return a.dated ? -1 : 1;
                if (a.dated && a.ticks != b.ticks) return b.ticks.CompareTo(a.ticks); // 최신 우선
                return a.index.CompareTo(b.index);
            });

            var result = new List<TournamentApi.UserTournamentResultEntry>(keyed.Count);
            for (int i = 0; i < keyed.Count; i++) result.Add(keyed[i].entry);
            return result;
        }

        // 정렬과 표시가 같은 파싱을 쓴다 — 갈라지면 "화면엔 날짜가 있는데 정렬은 맨 뒤"
        // 같은 어긋남이 생긴다.
        internal static bool TryParseCreated(string iso, out DateTime local)
        {
            local = default;
            if (string.IsNullOrEmpty(iso)) return false;
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt)) return false;
            local = dt.ToLocalTime();
            return true;
        }

        // ISO-8601 → yyyy.MM.dd HH:mm. 같은 날 참가가 구분돼야 해서 시각까지 붙인다
        // (목록이 선택 UI 가 되고 최신순 정렬이 붙었다). 파싱 실패는 빈 문자열.
        internal static string FormatDate(string iso)
            => TryParseCreated(iso, out var dt)
                ? dt.ToString("yyyy.MM.dd HH:mm", CultureInfo.InvariantCulture)
                : "";

        private void ClearRows()
        {
            _rowPlates.Clear();
            if (_listContent == null) return;
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                var child = _listContent.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private void CreateRow(TournamentApi.UserTournamentResultEntry entry)
        {
            var go = new GameObject("HistoryRow", typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_listContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = RowH;

            var plate = go.GetComponent<Image>();
            plate.sprite = _rowSprite;
            plate.type = Image.Type.Sliced;
            plate.color = Color.white;

            string entryId = entry.tournamentEntryId;
            _rowPlates.Add(new KeyValuePair<string, Image>(entryId, plate));
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (string.IsNullOrEmpty(entryId)) return;
                Select(entryId);
            });

            // Tournament name (top-left).
            string title = string.IsNullOrEmpty(entry.tournamentName) ? "토너먼트" : entry.tournamentName;
            var name = CreateLabel(go.transform, "Name", title, 32, TextAlignmentOptions.TopLeft, Color.white);
            SetLeftColumn((RectTransform)name.transform, TopBand);

            // Date (bottom-left).
            var date = CreateLabel(go.transform, "Date", FormatDate(entry.createdTime), 22,
                TextAlignmentOptions.BottomLeft, SubText);
            SetLeftColumn((RectTransform)date.transform, BottomBand);

            // Rank (top-right) + score (bottom-right). 밴드가 행 절반이라 정렬도
            // Top/Bottom 으로 맞춘다 — Midline 이면 두 밴드의 글리프가 맞닿는다.
            string rankText = entry.rank > 0 ? $"{entry.rank}위" : "-";
            var rank = CreateLabel(go.transform, "Rank", rankText, 34, TextAlignmentOptions.TopRight, GoldColor);
            rank.fontStyle = FontStyles.Bold;
            SetRightColumn((RectTransform)rank.transform, TopBand);

            var score = CreateLabel(go.transform, "Score", $"{entry.score:N0}점", 24,
                TextAlignmentOptions.BottomRight, SubText);
            SetRightColumn((RectTransform)score.transform, BottomBand);
        }

        // ── Ranking (right column) ───────────────────────────────────────────
        private void Select(string entryId)
        {
            _selectedEntryId = entryId;
            for (int i = 0; i < _rowPlates.Count; i++)
            {
                var plate = _rowPlates[i].Value;
                if (plate == null) continue;
                plate.sprite = _rowPlates[i].Key == entryId ? _rowSelectedSprite : _rowSprite;
            }
            LoadRanking(entryId);
        }

        private void LoadRanking(string entryId)
        {
            int epoch = ++_rankEpoch;
            ClearRanking();
            _rankTitle.text = "";
            SetRankStatus("불러오는 중...");

            string baseUrl = UserSession.GameServerBaseUrl;
            AuthCredential credential = UserSession.Credential;
            if (!UserSession.HasAccount || string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(entryId))
            {
                SetRankStatus("결과를 불러올 수 없습니다.");
                return;
            }

            TournamentApi.GetResult(baseUrl, credential, entryId, (data, error) =>
            {
                if (this == null || epoch != _rankEpoch || !isActiveAndEnabled) return;
                if (data == null)
                {
                    SetRankStatus("결과 조회에 실패했습니다.");
                    Debug.LogWarning($"[TournamentHistoryPanel] result fetch failed: {error}");
                    return;
                }
                _rankTitle.text = string.IsNullOrEmpty(data.name) ? "토너먼트 결과" : data.name;
                var rows = LeaderboardList.BuildRows(data.entries, data.maxEntryCount,
                    UserSession.Current?.userId);
                _leaderboard.Render(_rankContent, rows);
                SetRankStatus(rows.Count == 0 ? "참가 기록이 없습니다." : "");
            });
        }

        private void ClearRanking()
        {
            if (_leaderboard != null) _leaderboard.Render(_rankContent, null);
        }

        // ── Status ───────────────────────────────────────────────────────────
        private void SetListStatus(string text) => SetStatus(_listStatus, text);
        private void SetRankStatus(string text) => SetStatus(_rankStatus, text);

        private void SetEmptyBoth(string text)
        {
            SetListStatus(text);
            SetRankStatus(text);
        }

        private static void SetStatus(TextMeshProUGUI label, string text)
        {
            if (label == null) return;
            label.text = text;
            label.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        // ── Build ────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            _panelSprite = UiRoundedSprite.Make(32f, 4f, NavyFill,
                new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.95f));
            _rowSprite = UiRoundedSprite.Make(14f, 0f, RowFill, RowFill);
            _rowSelectedSprite = UiRoundedSprite.Make(14f, 3f, RowSelectedFill, GoldColor);
            _buttonSprite = UiRoundedSprite.Make(28f, 0f, Color.white, Color.white);
            _leaderboard = new LeaderboardList();

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 2500);
            roots.Canvas.overrideSorting = true;
            roots.Canvas.sortingOrder = 2500;

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(roots.FullBleedRoot, false);
            StretchFull((RectTransform)dim.transform);
            var dimImg = dim.GetComponent<Image>();
            dimImg.sprite = UiRoundedSprite.Make(2f, 0f, Color.white, Color.white);
            dimImg.type = Image.Type.Sliced;
            dimImg.color = UiOverlay.Dim;
            dimImg.raycastTarget = true; // eat clicks behind the page

            var panel = new GameObject("HistoryPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(roots.SafeAreaRoot, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelW, PanelH);
            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = _panelSprite;
            panelImg.type = Image.Type.Sliced;

            BuildHeader(panelRect);
            BuildLeftColumn(panelRect);
            BuildRightColumn(panelRect);

            UiLayer.Apply(gameObject);
        }

        private void BuildHeader(RectTransform panel)
        {
            var title = CreateLabel(panel, "Title", "히스토리", 48, TextAlignmentOptions.Center, GoldColor);
            title.fontStyle = FontStyles.Bold;
            var tr = (RectTransform)title.transform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-2f * Pad, 80f);
            tr.anchoredPosition = new Vector2(0f, -Pad - 8f);

            // Back button (top-left).
            var back = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            back.transform.SetParent(panel, false);
            var backImg = back.GetComponent<Image>();
            backImg.sprite = _buttonSprite;
            backImg.type = Image.Type.Sliced;
            backImg.color = new Color(1f, 1f, 1f, 0.12f);
            var backRt = (RectTransform)back.transform;
            backRt.anchorMin = backRt.anchorMax = new Vector2(0f, 1f);
            backRt.pivot = new Vector2(0f, 1f);
            backRt.sizeDelta = new Vector2(96f, 72f);
            backRt.anchoredPosition = new Vector2(Pad * 0.5f, -Pad * 0.5f);
            back.GetComponent<Button>().onClick.AddListener(() => onClose?.Invoke());
            var backLabel = CreateLabel(back.transform, "Label", "←", 40, TextAlignmentOptions.Center, Color.white);
            StretchFull((RectTransform)backLabel.transform);
        }

        // 좌 컬럼 = 내 토너먼트 목록(스크롤).
        private void BuildLeftColumn(RectTransform panel)
        {
            var viewport = new GameObject("ListViewport", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(panel, false);
            var vpRt = (RectTransform)viewport.transform;
            vpRt.anchorMin = new Vector2(0f, 0f);
            vpRt.anchorMax = new Vector2(1f, 1f);
            vpRt.offsetMin = new Vector2(Pad, Pad);
            vpRt.offsetMax = new Vector2(-(PanelW - Pad - LeftColW), -(Pad + HeaderH));
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
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
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

            _listStatus = CreateLabel(panel, "ListStatus", "", 30, TextAlignmentOptions.Center, SubText);
            var sr = (RectTransform)_listStatus.transform;
            sr.anchorMin = new Vector2(0f, 0f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.offsetMin = vpRt.offsetMin;
            sr.offsetMax = vpRt.offsetMax;
            _listStatus.gameObject.SetActive(false);
        }

        // 우 컬럼 = 선택된 토너먼트의 랭킹. 최대 5슬롯이라 스크롤 없이 세로 배치.
        private void BuildRightColumn(RectTransform panel)
        {
            float leftEdge = Pad + LeftColW + ColGap;

            _rankTitle = CreateLabel(panel, "RankTitle", "", 36, TextAlignmentOptions.Center, GoldColor);
            _rankTitle.fontStyle = FontStyles.Bold;
            var trt = (RectTransform)_rankTitle.transform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(leftEdge, -(Pad + HeaderH + RankTitleH));
            trt.offsetMax = new Vector2(-Pad, -(Pad + HeaderH));

            var list = new GameObject("RankList", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(VerticalLayoutGroup));
            list.transform.SetParent(panel, false);
            var lr = (RectTransform)list.transform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.offsetMin = new Vector2(leftEdge, Pad);
            lr.offsetMax = new Vector2(-Pad, -(Pad + HeaderH + RankTitleH));

            var well = list.GetComponent<Image>();
            well.sprite = UiRoundedSprite.Make(18f, 0f, new Color(0f, 0f, 0f, 0.24f), new Color(0f, 0f, 0f, 0.24f));
            well.type = Image.Type.Sliced;
            well.raycastTarget = false;

            var vlg = list.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            _rankContent = lr;

            _rankStatus = CreateLabel(panel, "RankStatus", "", 30, TextAlignmentOptions.Center, SubText);
            var sr = (RectTransform)_rankStatus.transform;
            sr.anchorMin = new Vector2(0f, 0f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.offsetMin = lr.offsetMin;
            sr.offsetMax = lr.offsetMax;
            _rankStatus.gameObject.SetActive(false);
        }

        // 좌측 컬럼 — 행 좌변 여백부터 우측 컬럼이 시작되는 지점까지.
        private static void SetLeftColumn(RectTransform rt, Vector2 band)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(RowInset, band.x);
            rt.offsetMax = new Vector2(-RightColW, band.y);
        }

        // 우측 컬럼 — 행 우변에 붙는 RightColW 폭 박스. 행 폭이 레이아웃 그룹에 따라
        // 변하므로 우측 앵커 기준 음수 오프셋으로 잡는다.
        private static void SetRightColumn(RectTransform rt, Vector2 band)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(-RightColW, band.x);
            rt.offsetMax = new Vector2(-RightPad, band.y);
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
