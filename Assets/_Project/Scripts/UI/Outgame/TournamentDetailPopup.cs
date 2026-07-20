using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core.Api;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // tournament-history Unit 3 — modal tournament ranking popup. Fetches the
    // detail via TournamentApi.GetResult and paints participant ranking through the
    // shared LeaderboardList (same look as the battle result popup). Self-building
    // canvas (ResultScreen precedent); created lazily by TournamentHistoryPanel.
    //
    // Guests never reach here (history is empty for them). Every async callback is
    // epoch-guarded: a response for a previously opened entry is dropped when the
    // popup has since been reopened for another tournament or closed.
    public class TournamentDetailPopup : MonoBehaviour
    {
        private static readonly Color GoldColor = new Color(1f, 0.78f, 0.28f, 1f);
        private static readonly Color NavyFill = new Color(0.05f, 0.06f, 0.10f, 0.98f);
        private static readonly Color BadgeTextDark = new Color(0.10f, 0.09f, 0.06f, 1f);
        private static readonly Color SubText = new Color(0.72f, 0.76f, 0.82f, 1f);

        private const float PanelW = 760f;
        private const float PanelH = 940f;
        private const float Pad = 34f;
        private const float HeaderH = 120f;
        private const float FooterH = 128f;

        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _statusLabel;
        private RectTransform _listContent;
        private Button _closeButton;
        private Sprite _tabSprite;
        private Sprite _buttonSprite;
        private LeaderboardList _leaderboard;
        private bool _built;
        private int _epoch;

        // Fetches and shows the ranking for one tournament entry. Safe to call
        // repeatedly — the previous fetch is invalidated by the epoch bump.
        public void Show(string entryId)
        {
            if (!_built) BuildCanvas();
            int epoch = ++_epoch;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _titleLabel.text = "토너먼트 결과";
            _leaderboard.Render(_listContent, null); // clear stale rows
            SetStatus("불러오는 중...");

            string baseUrl = UserSession.GameServerBaseUrl;
            string idToken = UserSession.IdToken;
            if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(entryId))
            {
                SetStatus("결과를 불러올 수 없습니다.");
                return;
            }

            TournamentApi.GetResult(baseUrl, idToken, entryId, (data, error) =>
            {
                if (this == null || epoch != _epoch || !isActiveAndEnabled) return;
                if (data == null)
                {
                    SetStatus("결과 조회에 실패했습니다.");
                    Debug.LogWarning($"[TournamentDetailPopup] result fetch failed: {error}");
                    return;
                }
                if (!string.IsNullOrEmpty(data.name)) _titleLabel.text = data.name;
                var rows = LeaderboardList.BuildRows(data.entries, data.maxEntryCount, UserSession.Current?.userId);
                _leaderboard.Render(_listContent, rows);
                SetStatus(rows.Count == 0 ? "참가 기록이 없습니다." : "");
            });
        }

        public void Hide()
        {
            _epoch++; // invalidate any in-flight fetch
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
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

            _tabSprite = UiRoundedSprite.Make(30f, 0f, Color.white, Color.white);
            _buttonSprite = UiRoundedSprite.Make(30f, 0f, Color.white, Color.white);
            _leaderboard = new LeaderboardList();

            // Sits above the lobby history panel; nested canvas needs overrideSorting.
            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 3000);
            roots.Canvas.overrideSorting = true;
            roots.Canvas.sortingOrder = 3000;

            // Full-screen dim — clicking it closes the popup.
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image), typeof(Button));
            dim.transform.SetParent(roots.FullBleedRoot, false);
            StretchFull((RectTransform)dim.transform);
            var dimImg = dim.GetComponent<Image>();
            dimImg.sprite = UiRoundedSprite.Make(2f, 0f, Color.white, Color.white);
            dimImg.type = Image.Type.Sliced;
            dimImg.color = UiOverlay.Dim;
            dim.GetComponent<Button>().onClick.AddListener(Hide);

            var panel = new GameObject("DetailPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(roots.SafeAreaRoot, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelW, PanelH);
            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = UiRoundedSprite.Make(32f, 4f, NavyFill,
                new Color(GoldColor.r, GoldColor.g, GoldColor.b, 0.95f));
            panelImg.type = Image.Type.Sliced;

            BuildHeader(panelRect);
            BuildList(panelRect);
            BuildFooter(panelRect);

            UiLayer.Apply(gameObject);
        }

        private void BuildHeader(RectTransform panel)
        {
            var tab = new GameObject("Tab", typeof(RectTransform), typeof(Image));
            tab.transform.SetParent(panel, false);
            var tabImg = tab.GetComponent<Image>();
            tabImg.sprite = _tabSprite;
            tabImg.type = Image.Type.Sliced;
            tabImg.color = GoldColor;
            var tabRt = (RectTransform)tab.transform;
            tabRt.anchorMin = tabRt.anchorMax = new Vector2(0.5f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.sizeDelta = new Vector2(560f, 92f);
            tabRt.anchoredPosition = new Vector2(0f, -Pad);

            _titleLabel = CreateLabel(tab.transform, "Title", "토너먼트 결과", 44,
                TextAlignmentOptions.Center, BadgeTextDark);
            _titleLabel.fontStyle = FontStyles.Bold;
            StretchFull((RectTransform)_titleLabel.transform);
        }

        private void BuildList(RectTransform panel)
        {
            var list = new GameObject("Leaderboard", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(VerticalLayoutGroup));
            list.transform.SetParent(panel, false);
            var lr = (RectTransform)list.transform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.offsetMin = new Vector2(Pad, FooterH + 8f);
            lr.offsetMax = new Vector2(-Pad, -(Pad + HeaderH + 8f));

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
            _listContent = lr;

            // Status overlay (loading / empty / error) — centered over the list well.
            _statusLabel = CreateLabel(panel, "Status", "", 28, TextAlignmentOptions.Center, SubText);
            var sr = (RectTransform)_statusLabel.transform;
            sr.anchorMin = new Vector2(0f, 0f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.offsetMin = new Vector2(Pad, FooterH + 8f);
            sr.offsetMax = new Vector2(-Pad, -(Pad + HeaderH + 8f));
            _statusLabel.gameObject.SetActive(false);
        }

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
            btnRt.sizeDelta = new Vector2(320f, 88f);
            btnRt.anchoredPosition = new Vector2(0f, Pad);
            _closeButton = btn.GetComponent<Button>();
            _closeButton.onClick.AddListener(Hide);

            var label = CreateLabel(btn.transform, "Label", "닫기", 34, TextAlignmentOptions.Center, BadgeTextDark);
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
