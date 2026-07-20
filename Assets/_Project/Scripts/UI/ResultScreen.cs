using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core;
using Wassup.Core.Api;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // Battle result overlay (DEFEAT / VICTORY) + tournament leaderboard + Lobby exit.
    // Disabled on Awake. The match is terminal here: the footer button leaves for
    // OutgameScene, so the next run re-enters through the lobby's loadout gate.
    // Navigating from the view itself mirrors MenuPopup.OnExit, the neighbouring
    // exit in this scene.
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

        // Dark text over the gold tab/footer/badge fills. (Row-scoped palette moved
        // to LeaderboardList — tournament-history Unit 1.)
        private static readonly Color BadgeTextDark = new Color(0.10f, 0.09f, 0.06f, 1f);

        private const float PanelW = 760f;
        private const float PanelH = 940f;
        private const float Pad = 34f;
        private const float HeaderH = 168f;
        private const float FooterH = 128f;

        private TextMeshProUGUI resultLabel;
        private TextMeshProUGUI scoreSubLabel;
        private TextMeshProUGUI statsLabel;
        private Image tabImage;
        private Button lobbyButton;
        private RectTransform _listContent;
        private bool _built;

        // Cached procedural sprites (baked once, reused).
        private Sprite _tabSprite;
        private Sprite _buttonSprite;

        // Shared ranked-row renderer (result popup + history detail popup).
        private LeaderboardList _leaderboard;

        private void Awake()
        {
            BuildCanvas();
            gameObject.SetActive(false);
            if (lobbyButton != null) lobbyButton.onClick.AddListener(OnLobbyClicked);
        }

        private void OnDestroy()
        {
            if (lobbyButton != null) lobbyButton.onClick.RemoveListener(OnLobbyClicked);
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
            _leaderboard.Render(_listContent, BuildBotRows(playerScore));
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        // No teardown first: the scene unloads wholesale. (MenuPopup.OnExit releases a
        // pause lease before leaving; the result screen holds none.)
        private void OnLobbyClicked() => SceneTransition.Go(SceneNames.Outgame);

        // tournament-play-report Unit 4 — swap the bot leaderboard for the real
        // tournament ranking once it arrives (popup shows the bot list first;
        // guests / fetch failures simply never get here). A response landing after
        // the popup closed (e.g. instant RESTART) is dropped.
        public void UpdateLeaderboard(TournamentApi.ResultData data, string ownUserId)
        {
            if (!gameObject.activeSelf) return;
            if (data == null || _listContent == null) return;

            var rows = LeaderboardList.BuildRows(data.entries, data.maxEntryCount, ownUserId);
            if (rows.Count == 0) return; // nothing meaningful to draw — keep the bot list
            _leaderboard.Render(_listContent, rows);
        }

        // Offline/guest fallback: bots around the player's score + a YOU row.
        private static List<LeaderboardList.Row> BuildBotRows(int playerScore)
        {
            var botScores = BotScoreGenerator.GenerateBotScores(5, playerScore, playerScore);
            var seed = new List<(string name, int score, bool isPlayer)>(botScores.Length + 1);
            for (int i = 0; i < botScores.Length; i++) seed.Add(($"봇-{i + 1}", botScores[i], false));
            seed.Add(("나", playerScore, true));
            seed.Sort((a, b) => b.score.CompareTo(a.score));

            var rows = new List<LeaderboardList.Row>(seed.Count);
            for (int i = 0; i < seed.Count; i++)
                rows.Add(new LeaderboardList.Row(i + 1, seed[i].name, seed[i].score, seed[i].isPlayer, false));
            return rows;
        }

        // ── Build ────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            BakeSprites();
            _leaderboard = new LeaderboardList();

            var roots = UiCanvasSetup.Ensure(gameObject, sortingOrder: 2000);
            var canvas = roots.Canvas;
            // Terminal game-over modal — must sit above ALL other UI so the dim covers
            // the battle HUD (ScoreHud 6, docks 7-8) and the MENU button (1000).
            // This ResultScreen lives *nested* under a root "ResultCanvas", so a plain
            // sortingOrder is IGNORED — overrideSorting=true is required for the nested
            // canvas to sort as its own unit above everything.
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000;

            // Full-screen dim behind the panel — shared overlay tone, no art BG.
            // Explicit solid sprite (not a null-sprite Image) so it reliably draws a
            // filled quad across the whole screen.
            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(roots.FullBleedRoot, false);
            StretchFull((RectTransform)dim.transform);
            var dimImg = dim.GetComponent<Image>();
            dimImg.sprite = UiRoundedSprite.Make(2f, 0f, Color.white, Color.white);
            dimImg.type = Image.Type.Sliced;
            dimImg.color = UiOverlay.Dim;

            // Panel.
            var panel = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(roots.SafeAreaRoot, false);
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

            var btn = new GameObject("LobbyButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(fr, false);
            var btnImg = btn.GetComponent<Image>();
            btnImg.sprite = _buttonSprite;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = goldColor;
            var btnRt = (RectTransform)btn.transform;
            btnRt.anchorMin = btnRt.anchorMax = btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.sizeDelta = new Vector2(320f, 88f);
            btnRt.anchoredPosition = Vector2.zero;
            lobbyButton = btn.GetComponent<Button>();

            var label = CreateLabel(btn.transform, "Label", "로비로", 34, TextAlignmentOptions.Center, BadgeTextDark);
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
