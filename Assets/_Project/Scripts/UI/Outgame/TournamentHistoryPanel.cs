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
    // tournament-history Unit 2 — lobby history page. On open, fetches my
    // (in-progress) tournament entries via TournamentApi.GetUnclaimedEntries and
    // lists them; a row tap opens the detail ranking popup for that
    // tournamentEntryId. Self-building canvas (ResultScreen precedent).
    //
    // Visibility (SetActive) is owned by OutgameMenuController.RaiseExclusive; this
    // view loads on enable and reports close via onClose (LoginPanelView precedent).
    // Guests (empty IdToken) skip the API and show an empty state.
    public class TournamentHistoryPanel : MonoBehaviour
    {
        private static readonly Color GoldColor = new Color(1f, 0.78f, 0.28f, 1f);
        private static readonly Color NavyFill = new Color(0.05f, 0.06f, 0.10f, 0.98f);
        private static readonly Color BadgeTextDark = new Color(0.10f, 0.09f, 0.06f, 1f);
        private static readonly Color SubText = new Color(0.72f, 0.76f, 0.82f, 1f);
        private static readonly Color RowFill = new Color(1f, 1f, 1f, 0.05f);

        private const float PanelW = 820f;
        private const float PanelH = 1240f;
        private const float Pad = 34f;
        private const float HeaderH = 150f;
        private const float RowH = 96f;

        // Fired when the user backs out. OutgameMenuController subscribes and runs
        // its ClosePanels (re-shows the lobby menu that RaiseExclusive hid).
        public event Action onClose;

        private RectTransform _listContent;
        private TextMeshProUGUI _statusLabel;
        private Sprite _panelSprite;
        private Sprite _rowSprite;
        private Sprite _buttonSprite;
        private TournamentDetailPopup _popup;
        private bool _built;
        private int _epoch;

        private void OnEnable()
        {
            if (!_built) BuildCanvas();
            LoadEntries();
        }

        private void OnDisable()
        {
            _epoch++; // drop any in-flight list fetch when the page closes
        }

        private void LoadEntries()
        {
            int epoch = ++_epoch;
            ClearRows();
            SetStatus("불러오는 중...");

            string idToken = UserSession.IdToken;
            string baseUrl = UserSession.GameServerBaseUrl;
            if (string.IsNullOrEmpty(idToken)) // guest / not signed in
            {
                SetStatus("로그인이 필요합니다.");
                return;
            }
            if (string.IsNullOrEmpty(baseUrl))
            {
                SetStatus("기록을 불러올 수 없습니다.");
                return;
            }

            TournamentApi.GetUnclaimedEntries(baseUrl, idToken, (list, error) =>
            {
                if (this == null || epoch != _epoch || !isActiveAndEnabled) return;
                if (list == null)
                {
                    SetStatus("기록 조회에 실패했습니다.");
                    Debug.LogWarning($"[TournamentHistoryPanel] list fetch failed: {error}");
                    return;
                }
                if (list.Count == 0)
                {
                    SetStatus("참여한 토너먼트가 없습니다.");
                    return;
                }
                SetStatus("");
                for (int i = 0; i < list.Count; i++) CreateRow(list[i]);
            });
        }

        private TournamentDetailPopup EnsurePopup()
        {
            if (_popup != null) return _popup;
            var go = new GameObject("TournamentDetailPopup", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _popup = go.AddComponent<TournamentDetailPopup>();
            go.SetActive(false);
            return _popup;
        }

        // ── Rows ─────────────────────────────────────────────────────────────
        private void ClearRows()
        {
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
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (string.IsNullOrEmpty(entryId)) return;
                EnsurePopup().Show(entryId);
            });

            // Tournament name (top-left).
            string title = string.IsNullOrEmpty(entry.tournamentName) ? "토너먼트" : entry.tournamentName;
            var name = CreateLabel(go.transform, "Name", title, 32, TextAlignmentOptions.TopLeft, Color.white);
            var nameRt = (RectTransform)name.transform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(24f, RowH * 0.42f);
            nameRt.offsetMax = new Vector2(-200f, -12f);

            // Date (bottom-left).
            var date = CreateLabel(go.transform, "Date", FormatDate(entry.createdTime), 22,
                TextAlignmentOptions.BottomLeft, SubText);
            var dateRt = (RectTransform)date.transform;
            dateRt.anchorMin = new Vector2(0f, 0f);
            dateRt.anchorMax = new Vector2(1f, 1f);
            dateRt.offsetMin = new Vector2(24f, 12f);
            dateRt.offsetMax = new Vector2(-200f, -RowH * 0.42f);

            // Rank + score (right).
            string rankText = entry.rank > 0 ? $"{entry.rank}위" : "-";
            var rank = CreateLabel(go.transform, "Rank", rankText, 34, TextAlignmentOptions.MidlineRight, GoldColor);
            rank.fontStyle = FontStyles.Bold;
            var rankRt = (RectTransform)rank.transform;
            rankRt.anchorMin = new Vector2(1f, 0f);
            rankRt.anchorMax = new Vector2(1f, 1f);
            rankRt.pivot = new Vector2(1f, 0.5f);
            rankRt.sizeDelta = new Vector2(180f, 0f);
            rankRt.offsetMin = new Vector2(rankRt.offsetMin.x, RowH * 0.42f);
            rankRt.anchoredPosition = new Vector2(-22f, 0f);

            var score = CreateLabel(go.transform, "Score", $"{entry.score:N0}점", 24,
                TextAlignmentOptions.MidlineRight, SubText);
            var scoreRt = (RectTransform)score.transform;
            scoreRt.anchorMin = new Vector2(1f, 0f);
            scoreRt.anchorMax = new Vector2(1f, 1f);
            scoreRt.pivot = new Vector2(1f, 0.5f);
            scoreRt.sizeDelta = new Vector2(180f, 0f);
            scoreRt.offsetMax = new Vector2(scoreRt.offsetMax.x, -RowH * 0.42f);
            scoreRt.anchoredPosition = new Vector2(-22f, 0f);
        }

        // ISO-8601 → yyyy.MM.dd; leaves blank if unparseable (display-only field).
        private static string FormatDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return "";
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToLocalTime().ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
            return "";
        }

        private void SetStatus(string text)
        {
            if (_statusLabel == null) return;
            _statusLabel.text = text;
            _statusLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        // ── Build ────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            _panelSprite = UiRoundedSprite.Make(32f, 4f, NavyFill,
                new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.95f));
            _rowSprite = UiRoundedSprite.Make(14f, 0f, RowFill, RowFill);
            _buttonSprite = UiRoundedSprite.Make(28f, 0f, Color.white, Color.white);

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
            BuildScrollList(panelRect);

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

        private void BuildScrollList(RectTransform panel)
        {
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(panel, false);
            var vpRt = (RectTransform)viewport.transform;
            vpRt.anchorMin = new Vector2(0f, 0f);
            vpRt.anchorMax = new Vector2(1f, 1f);
            vpRt.offsetMin = new Vector2(Pad, Pad);
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

            // Status overlay (loading / empty / error) — centered over the viewport.
            _statusLabel = CreateLabel(panel, "Status", "", 30, TextAlignmentOptions.Center, SubText);
            var sr = (RectTransform)_statusLabel.transform;
            sr.anchorMin = new Vector2(0f, 0f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.offsetMin = new Vector2(Pad, Pad);
            sr.offsetMax = new Vector2(-Pad, -(Pad + HeaderH));
            _statusLabel.gameObject.SetActive(false);
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
