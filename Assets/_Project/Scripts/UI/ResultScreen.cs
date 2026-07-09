using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.Api;

namespace Wassup.UI
{
    // Battle result overlay (DEFEAT / VICTORY) + tournament leaderboard + Restart.
    // Disabled on Awake. Emits RestartRequested when the player taps Restart;
    // BattleBridge subscribes to tear down and restart the match.
    //
    // result-screen-visual-upgrade — reskinned to the in-game HUD language
    // (navy plate + gold border/tab, ScoreHudView palette) over a dimmed season
    // backdrop. Layout is anchor-based (header / list / footer) so the Restart
    // button is pinned to the bottom bar and never overlaps the leaderboard.
    public class ResultScreen : MonoBehaviour
    {
        // Palette — visual constants matching the in-game HUD (ScoreHudView).
        // No serialized fields: keeps the scene component (and its diff) clean.
        private static readonly Color goldColor = new Color(1f, 0.78f, 0.28f, 1f);
        private static readonly Color navyFill = new Color(0.05f, 0.06f, 0.10f, 0.98f);
        private static readonly Color defeatColor = new Color(1f, 0.42f, 0.42f, 1f);

        // Row palette — kept private; these are visual constants, not tuning knobs.
        private static readonly Color RowFill = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color OwnFill = new Color(1f, 0.83f, 0.35f, 0.20f);
        private static readonly Color WaitingFill = new Color(1f, 1f, 1f, 0.03f);
        private static readonly Color WaitingText = new Color(0.60f, 0.63f, 0.65f, 1f); // #9AA0A6
        private static readonly Color BadgeGold = new Color(1f, 0.82f, 0.29f, 1f);
        private static readonly Color BadgeSilver = new Color(0.84f, 0.86f, 0.88f, 1f);
        private static readonly Color BadgeBronze = new Color(0.78f, 0.54f, 0.30f, 1f);
        private static readonly Color BadgeNavy = new Color(0.16f, 0.19f, 0.26f, 1f);
        private static readonly Color BadgeTextDark = new Color(0.10f, 0.09f, 0.06f, 1f);

        private const float PanelW = 760f;
        private const float PanelH = 940f;
        private const float Pad = 34f;
        private const float HeaderH = 168f;
        private const float FooterH = 128f;
        private const float RowH = 48f;

        public event Action RestartRequested;

        private TextMeshProUGUI resultLabel;
        private TextMeshProUGUI scoreSubLabel;
        private TextMeshProUGUI statsLabel;
        private Image tabImage;
        private Button restartButton;
        private RectTransform _listContent;
        private bool _built;

        // Cached procedural sprites (baked once, reused across rows).
        private Sprite _tabSprite;
        private Sprite _buttonSprite;
        private Sprite _rowNormal;
        private Sprite _rowOwn;
        private Sprite _rowWaiting;
        private Sprite _badgeGold;
        private Sprite _badgeSilver;
        private Sprite _badgeBronze;
        private Sprite _badgeNavy;

        private void Awake()
        {
            BuildCanvas();
            gameObject.SetActive(false);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        }

        private void OnDestroy()
        {
            if (restartButton != null) restartButton.onClick.RemoveListener(OnRestartClicked);
        }

        // End-of-match summary stats shown on the result popup (null → hidden).
        public readonly struct MatchStats
        {
            public readonly float RemainingSec;
            public readonly int Leaks;
            public MatchStats(float remainingSec, int leaks) { RemainingSec = remainingSec; Leaks = leaks; }
        }

        public void ShowDefeat() => ShowResult("패배", 0, null);
        public void ShowDefeat(int playerScore) => ShowResult("패배", playerScore, null);
        public void ShowDefeat(int playerScore, float remainingSec, int leaks)
            => ShowResult("패배", playerScore, new MatchStats(remainingSec, leaks));
        public void ShowVictory() => ShowResult("승리", 0, null);
        public void ShowVictory(int playerScore) => ShowResult("승리", playerScore, null);
        public void ShowVictory(int playerScore, float remainingSec, int leaks)
            => ShowResult("승리", playerScore, new MatchStats(remainingSec, leaks));

        private void ShowResult(string resultText, int playerScore, MatchStats? stats)
        {
            if (!_built) BuildCanvas();
            resultLabel.text = resultText;
            bool win = resultText == "승리";
            if (tabImage != null) tabImage.color = win ? goldColor : defeatColor;
            if (scoreSubLabel != null) scoreSubLabel.text = $"내 점수   {playerScore:N0}";
            if (statsLabel != null)
            {
                if (stats.HasValue)
                {
                    int t = Mathf.Max(0, Mathf.CeilToInt(stats.Value.RemainingSec));
                    statsLabel.text = $"시간 {t / 60}:{t % 60:D2}      유출 {stats.Value.Leaks}";
                    statsLabel.gameObject.SetActive(true);
                }
                else statsLabel.gameObject.SetActive(false);
            }
            RenderRows(BuildBotRows(playerScore));
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void OnRestartClicked() => RestartRequested?.Invoke();

        // tournament-play-report Unit 4 — swap the bot leaderboard for the real
        // tournament ranking once it arrives (popup shows the bot list first;
        // guests / fetch failures simply never get here). A response landing after
        // the popup closed (e.g. instant RESTART) is dropped.
        public void UpdateLeaderboard(TournamentApi.ResultData data, string ownUserId)
        {
            if (!gameObject.activeSelf) return;
            if (data == null || _listContent == null) return;

            var rows = BuildRows(data.entries, data.maxEntryCount, ownUserId);
            if (rows.Count == 0) return; // nothing meaningful to draw — keep the bot list
            RenderRows(rows);
        }

        // ── Pure row model ─────────────────────────────────────────────────────
        // Display row for one leaderboard slot. Both the bot fallback and the real
        // tournament ranking funnel through RenderRows via these rows.
        internal readonly struct Row
        {
            public readonly int Rank;
            public readonly string Name;
            public readonly int Score;
            public readonly bool IsPlayer;
            public readonly bool IsWaiting;

            public Row(int rank, string name, int score, bool isPlayer, bool isWaiting)
            {
                Rank = rank;
                Name = name;
                Score = score;
                IsPlayer = isPlayer;
                IsWaiting = isWaiting;
            }
        }

        // Tournament slots are pre-assigned (maxEntryCount, currently 10): every slot
        // is rendered, and slots no opponent has taken yet read WAITING... The dev
        // server omits the schema's `rank` field, so order by score and derive the
        // rank from position; a server-provided rank (>0) wins when it appears.
        internal static List<Row> BuildRows(IReadOnlyList<TournamentApi.ResultEntry> entries,
            int maxEntryCount, string ownUserId)
        {
            var sorted = new List<TournamentApi.ResultEntry>();
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                    if (entries[i] != null) sorted.Add(entries[i]);
            }
            sorted.Sort((a, b) => b.score.CompareTo(a.score));

            int totalSlots = Mathf.Max(maxEntryCount, sorted.Count);
            var rows = new List<Row>(Mathf.Max(0, totalSlots));
            for (int i = 0; i < totalSlots; i++)
            {
                if (i < sorted.Count)
                {
                    var e = sorted[i];
                    int rank = e.rank > 0 ? e.rank : i + 1;
                    bool isPlayer = !string.IsNullOrEmpty(ownUserId) && e.userId == ownUserId;
                    rows.Add(new Row(rank, DisplayName(e.userName), e.score, isPlayer, false));
                }
                else
                {
                    rows.Add(new Row(i + 1, "대기 중...", 0, false, true));
                }
            }
            return rows;
        }

        // Offline/guest fallback: bots around the player's score + a YOU row.
        private static List<Row> BuildBotRows(int playerScore)
        {
            var botScores = BotScoreGenerator.GenerateBotScores(5, playerScore, playerScore);
            var seed = new List<(string name, int score, bool isPlayer)>(botScores.Length + 1);
            for (int i = 0; i < botScores.Length; i++) seed.Add(($"봇-{i + 1}", botScores[i], false));
            seed.Add(("나", playerScore, true));
            seed.Sort((a, b) => b.score.CompareTo(a.score));

            var rows = new List<Row>(seed.Count);
            for (int i = 0; i < seed.Count; i++)
                rows.Add(new Row(i + 1, seed[i].name, seed[i].score, seed[i].isPlayer, false));
            return rows;
        }

        // empty names would collapse the row; long ones would overrun the score column.
        private static string DisplayName(string userName)
        {
            if (string.IsNullOrEmpty(userName)) return "?";
            return userName.Length <= 10 ? userName : userName.Substring(0, 10);
        }

        // ── Rendering ────────────────────────────────────────────────────────
        private void RenderRows(List<Row> rows)
        {
            if (_listContent == null) return;
            // Detach-then-destroy so old rows leave the layout this frame (Destroy is
            // deferred) — avoids a one-frame double list when bots swap for real data.
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                var child = _listContent.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            for (int i = 0; i < rows.Count; i++) CreateRow(rows[i]);
        }

        private void CreateRow(Row row)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(_listContent, false);
            go.GetComponent<LayoutElement>().preferredHeight = RowH;

            var plate = go.GetComponent<Image>();
            plate.sprite = row.IsWaiting ? _rowWaiting : row.IsPlayer ? _rowOwn : _rowNormal;
            plate.type = Image.Type.Sliced;
            plate.color = Color.white; // fill is baked into the sprite

            // Rank badge (left).
            Sprite badgeSprite = _badgeNavy;
            Color badgeText = WaitingText;
            if (!row.IsWaiting)
            {
                switch (row.Rank)
                {
                    case 1: badgeSprite = _badgeGold; badgeText = BadgeTextDark; break;
                    case 2: badgeSprite = _badgeSilver; badgeText = BadgeTextDark; break;
                    case 3: badgeSprite = _badgeBronze; badgeText = BadgeTextDark; break;
                    default: badgeSprite = _badgeNavy; badgeText = Color.white; break;
                }
            }
            var badge = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(go.transform, false);
            var badgeImg = badge.GetComponent<Image>();
            badgeImg.sprite = badgeSprite;
            badgeImg.raycastTarget = false;
            var badgeRt = (RectTransform)badge.transform;
            badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(14f, 0f);
            badgeRt.sizeDelta = new Vector2(38f, 38f);
            var badgeLabel = CreateLabel(badge.transform, "Num", row.Rank.ToString(), 22,
                TextAlignmentOptions.Center, badgeText);
            StretchFull((RectTransform)badgeLabel.transform);

            Color textColor = row.IsWaiting ? WaitingText : row.IsPlayer ? goldColor : Color.white;

            // Name (left, after badge).
            var name = CreateLabel(go.transform, "Name", row.Name, 30, TextAlignmentOptions.MidlineLeft, textColor);
            var nameRt = (RectTransform)name.transform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(66f, 0f);
            nameRt.offsetMax = new Vector2(-150f, 0f);

            // Score (right).
            string scoreText = row.IsWaiting ? "-" : row.Score.ToString("N0");
            var score = CreateLabel(go.transform, "Score", scoreText, 30, TextAlignmentOptions.MidlineRight, textColor);
            var scoreRt = (RectTransform)score.transform;
            scoreRt.anchorMin = new Vector2(1f, 0f);
            scoreRt.anchorMax = new Vector2(1f, 1f);
            scoreRt.pivot = new Vector2(1f, 0.5f);
            scoreRt.sizeDelta = new Vector2(140f, 0f);
            scoreRt.anchoredPosition = new Vector2(-22f, 0f);
        }

        // ── Build ────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            BakeSprites();

            // NB: use Unity's overridden == (not ??), which ?? bypasses — GetComponent
            // can return a fake-null wrapper in the editor that ?? treats as non-null.
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Terminal game-over modal — must sit above ALL other UI so the dim covers
            // the battle HUD (ScoreHud 6, docks 7-8) and the MENU button (1000).
            // This ResultScreen lives *nested* under a root "ResultCanvas", so a plain
            // sortingOrder is IGNORED — overrideSorting=true is required for the nested
            // canvas to sort as its own unit above everything.
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000;

            if (gameObject.GetComponent<CanvasScaler>() == null)
            {
                var scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            // Full-screen dim behind the panel — shared overlay tone, no art BG.
            // Explicit solid sprite (not a null-sprite Image) so it reliably draws a
            // filled quad across the whole screen.
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(transform, false);
            StretchFull((RectTransform)dim.transform);
            var dimImg = dim.GetComponent<Image>();
            dimImg.sprite = UiRoundedSprite.Make(2f, 0f, Color.white, Color.white);
            dimImg.type = Image.Type.Sliced;
            dimImg.color = UiOverlay.Dim;

            // Panel.
            var panel = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelW, PanelH);
            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = UiRoundedSprite.Make(32f, 4f, navyFill, new Color(goldColor.r, goldColor.g, goldColor.b, 0.95f));
            panelImg.type = Image.Type.Sliced;

            BuildHeader(panelRect);
            BuildList(panelRect);
            BuildFooter(panelRect);

            UiLayer.Apply(gameObject);
        }

        private void BakeSprites()
        {
            _tabSprite = UiRoundedSprite.Make(30f, 0f, Color.white, Color.white);
            _buttonSprite = UiRoundedSprite.Make(30f, 0f, Color.white, Color.white);
            _rowNormal = UiRoundedSprite.Make(14f, 0f, RowFill, RowFill);
            _rowOwn = UiRoundedSprite.Make(14f, 3f, OwnFill, goldColor);
            _rowWaiting = UiRoundedSprite.Make(14f, 0f, WaitingFill, WaitingFill);
            _badgeGold = UiRoundedSprite.MakeCircle(40, BadgeGold);
            _badgeSilver = UiRoundedSprite.MakeCircle(40, BadgeSilver);
            _badgeBronze = UiRoundedSprite.MakeCircle(40, BadgeBronze);
            _badgeNavy = UiRoundedSprite.MakeCircle(40, BadgeNavy, 2f, new Color(goldColor.r, goldColor.g, goldColor.b, 0.6f));
        }

        private void BuildHeader(RectTransform panel)
        {
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(panel, false);
            var hr = (RectTransform)header.transform;
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(-2f * Pad, HeaderH);
            hr.anchoredPosition = new Vector2(0f, -Pad);

            var tab = new GameObject("Tab", typeof(RectTransform), typeof(Image));
            tab.transform.SetParent(hr, false);
            tabImage = tab.GetComponent<Image>();
            tabImage.sprite = _tabSprite;
            tabImage.type = Image.Type.Sliced;
            tabImage.color = goldColor;
            var tabRt = (RectTransform)tab.transform;
            tabRt.anchorMin = tabRt.anchorMax = new Vector2(0.5f, 1f);
            tabRt.pivot = new Vector2(0.5f, 1f);
            tabRt.sizeDelta = new Vector2(460f, 92f);
            tabRt.anchoredPosition = Vector2.zero;

            resultLabel = CreateLabel(tab.transform, "ResultLabel", "", 60, TextAlignmentOptions.Center, BadgeTextDark);
            resultLabel.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            resultLabel.characterSpacing = 8f;
            StretchFull((RectTransform)resultLabel.transform);

            scoreSubLabel = CreateLabel(hr, "ScoreSub", "", 28, TextAlignmentOptions.Center, goldColor);
            scoreSubLabel.fontStyle = FontStyles.SmallCaps;
            scoreSubLabel.characterSpacing = 4f;
            var ssr = (RectTransform)scoreSubLabel.transform;
            ssr.anchorMin = ssr.anchorMax = new Vector2(0.5f, 1f);
            ssr.pivot = new Vector2(0.5f, 1f);
            ssr.sizeDelta = new Vector2(560f, 36f);
            ssr.anchoredPosition = new Vector2(0f, -96f);

            // End-of-match stats line (TIME m:ss / LEAKS n). Hidden until Show* passes stats.
            statsLabel = CreateLabel(hr, "StatsSub", "", 23, TextAlignmentOptions.Center,
                new Color(0.72f, 0.76f, 0.82f, 1f));
            statsLabel.fontStyle = FontStyles.SmallCaps;
            statsLabel.characterSpacing = 3f;
            var str = (RectTransform)statsLabel.transform;
            str.anchorMin = str.anchorMax = new Vector2(0.5f, 1f);
            str.pivot = new Vector2(0.5f, 1f);
            str.sizeDelta = new Vector2(560f, 32f);
            str.anchoredPosition = new Vector2(0f, -134f);
            statsLabel.gameObject.SetActive(false);
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
        }

        private void BuildFooter(RectTransform panel)
        {
            var footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(panel, false);
            var fr = (RectTransform)footer.transform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(1f, 0f);
            fr.pivot = new Vector2(0.5f, 0f);
            fr.sizeDelta = new Vector2(-2f * Pad, FooterH);
            fr.anchoredPosition = new Vector2(0f, Pad);

            var btn = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(fr, false);
            var btnImg = btn.GetComponent<Image>();
            btnImg.sprite = _buttonSprite;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = goldColor;
            var btnRt = (RectTransform)btn.transform;
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = new Vector2(320f, 88f);
            btnRt.anchoredPosition = Vector2.zero;
            restartButton = btn.GetComponent<Button>();

            var label = CreateLabel(btn.transform, "Label", "다시하기", 34, TextAlignmentOptions.Center, BadgeTextDark);
            label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            label.characterSpacing = 6f;
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
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
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
