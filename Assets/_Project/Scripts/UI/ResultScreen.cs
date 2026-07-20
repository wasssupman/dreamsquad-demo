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
    // (navy plate + gold border/tab, ScoreHudView palette) over a dimmed backdrop.
    //
    // result-screen-ranking-ui — rebuilt as a two-column ranking screen. The game is
    // landscape-only (ProjectSettings: portrait autorotation disabled), and the old
    // single-column panel used 880 of 1920 reference units, wasting ~520 on each
    // side. Left column = my result (outcome / hero score / rank / stats / exit),
    // right column = the standings. The extra width pays for a much larger type
    // scale, which is the point: this is read on a phone.
    //
    // The pure row model below is deliberately untouched — ResultLeaderboardModelTests
    // covers it.
    public class ResultScreen : MonoBehaviour
    {
        // Palette — visual constants matching the in-game HUD (ScoreHudView).
        // No serialized fields: keeps the scene component (and its diff) clean.
        private static readonly Color goldColor = new Color(1f, 0.78f, 0.28f, 1f);
        private static readonly Color navyFill = new Color(0.05f, 0.06f, 0.10f, 0.98f);
        private static readonly Color defeatColor = new Color(1f, 0.42f, 0.42f, 1f);
        private const string GoldHex = "FFC747"; // goldColor, for rich-text emphasis

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
        private static readonly Color ChipFill = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color ChipLabel = new Color(0.72f, 0.76f, 0.82f, 1f);
        private static readonly Color CaptionText = new Color(0.78f, 0.82f, 0.88f, 1f);
        private static readonly Color SectionText = new Color(0.62f, 0.67f, 0.74f, 1f);

        // Two-column geometry at the 1920x1080 reference.
        //   pad 30 | left 520 | gap 28 | right 832 | pad 30  = PanelW 1440
        // Panel height is content-driven (ContentSizeFitter): the right column's
        // list decides it, and the left column stretches to match.
        private const float PanelW = 1440f;
        private const float Pad = 30f;
        private const float ColGap = 28f;
        private const float LeftW = 520f;
        private const float RightW = 832f;
        private const float RowH = 56f;
        private const float RowGap = 6f;
        private const float ChipH = 60f;
        private const float ButtonH = 92f;

        private TextMeshProUGUI resultLabel;
        private TextMeshProUGUI heroScoreLabel;
        private TextMeshProUGUI captionLabel;
        private Image tabImage;
        private Button lobbyButton;
        private RectTransform _listContent;
        private RectTransform _chipsRoot;
        private bool _built;

        // Cached procedural sprites (baked once, reused across rows).
        private Sprite _tabSprite;
        private Sprite _buttonSprite;
        private Sprite _rowNormal;
        private Sprite _rowOwn;
        private Sprite _rowWaiting;
        private Sprite _chipPlate;
        private Sprite _badgeGold;
        private Sprite _badgeSilver;
        private Sprite _badgeBronze;
        private Sprite _badgeNavy;

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
        //
        // battle-score-formula unit 4 — HasBreakdown 이면 점수 3축을, 아니면 기존
        // 남은 시간/유출을 보여준다. 옛 오버로드 호출부가 남아 있어 둘 다 지원한다.
        public readonly struct MatchStats
        {
            public readonly float RemainingSec;
            public readonly int Leaks;
            public readonly bool HasBreakdown;
            public readonly int TimeScore, StressScore, KillScore;
            // 예산 만점(분모). 킬은 스폰 구성 의존이라 상수가 아니므로 분모가 없다(계약 7).
            public readonly int TimeBudget, StressBudget;

            public MatchStats(float remainingSec, int leaks)
            {
                RemainingSec = remainingSec; Leaks = leaks;
                HasBreakdown = false;
                TimeScore = StressScore = KillScore = 0;
                TimeBudget = StressBudget = 0;
            }

            public MatchStats(float remainingSec, int leaks, ScoreMath.BattleScore score,
                int timeBudget, int stressBudget)
            {
                RemainingSec = remainingSec; Leaks = leaks;
                HasBreakdown = true;
                TimeScore = score.Time; StressScore = score.Stress; KillScore = score.Kill;
                TimeBudget = timeBudget; StressBudget = stressBudget;
            }
        }

        public void ShowDefeat() => ShowResult("패배", 0, null);
        public void ShowDefeat(int playerScore) => ShowResult("패배", playerScore, null);
        public void ShowDefeat(int playerScore, float remainingSec, int leaks)
            => ShowResult("패배", playerScore, new MatchStats(remainingSec, leaks));
        public void ShowVictory() => ShowResult("승리", 0, null);
        public void ShowVictory(int playerScore) => ShowResult("승리", playerScore, null);
        public void ShowVictory(int playerScore, float remainingSec, int leaks)
            => ShowResult("승리", playerScore, new MatchStats(remainingSec, leaks));

        // battle-score-formula unit 4 — 점수 3축 분해를 실은 경로. 총점은 score.Total 이라
        // 따로 받지 않는다.
        public void ShowDefeat(ScoreMath.BattleScore score, float remainingSec, int leaks,
            int timeBudget, int stressBudget)
            => ShowResult("패배", score.Total,
                new MatchStats(remainingSec, leaks, score, timeBudget, stressBudget));

        public void ShowVictory(ScoreMath.BattleScore score, float remainingSec, int leaks,
            int timeBudget, int stressBudget)
            => ShowResult("승리", score.Total,
                new MatchStats(remainingSec, leaks, score, timeBudget, stressBudget));

        private void ShowResult(string resultText, int playerScore, MatchStats? stats)
        {
            if (!_built) BuildCanvas();
            resultLabel.text = resultText;
            bool win = resultText == "승리";
            if (tabImage != null) tabImage.color = win ? goldColor : defeatColor;

            if (heroScoreLabel != null)
            {
                heroScoreLabel.text = playerScore.ToString("N0");
                heroScoreLabel.color = win ? goldColor : defeatColor;
            }

            // The stat rows take generic (label, value) pairs so battle-score-formula
            // unit 4 can swap these two for the three score axes without touching
            // any layout code here.
            if (stats.HasValue && stats.Value.HasBreakdown)
            {
                // 예산 소모 모델이라 "얻은 점수 / 예산" 이 그대로 피드백이 된다.
                // 처치만 분모가 없다 — 킬 만점은 스폰 구성에 의존해 상수가 아니다(계약 7).
                var s = stats.Value;
                SetChips(new[]
                {
                    ("시간", $"{s.TimeScore:N0} / {s.TimeBudget:N0}"),
                    ("스트레스", $"{s.StressScore:N0} / {s.StressBudget:N0}"),
                    ("처치", $"{s.KillScore:N0}"),
                });
            }
            else if (stats.HasValue)
            {
                int t = Mathf.Max(0, Mathf.CeilToInt(stats.Value.RemainingSec));
                SetChips(new[]
                {
                    ("남은 시간", $"{t / 60}:{t % 60:D2}"),
                    ("유출", stats.Value.Leaks.ToString()),
                });
            }
            else SetChips(null);

            var rows = BuildPendingRows(playerScore);
            RenderRows(rows);
            UpdateCaption(rows);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        // No teardown first: the scene unloads wholesale. (MenuPopup.OnExit releases a
        // pause lease before leaving; the result screen holds none.)
        private void OnLobbyClicked() => SceneTransition.Go(SceneNames.Outgame);

        // tournament-play-report Unit 4 — swap the pending leaderboard for the real
        // tournament ranking once it arrives (popup shows the pending list first;
        // guests / fetch failures simply never get here). A response landing after
        // the popup closed (e.g. instant RESTART) is dropped.
        public void UpdateLeaderboard(TournamentApi.ResultData data, string ownUserId)
        {
            if (!gameObject.activeSelf) return;
            if (data == null || _listContent == null) return;

            var rows = BuildRows(data.entries, data.maxEntryCount, ownUserId);
            if (rows.Count == 0) return; // nothing meaningful to draw — keep the pending list
            RenderRows(rows);
            UpdateCaption(rows); // real ranking almost always moves the player off the pending state
        }

        // "순위 3 / 10" under the hero score. The rank comes out of the rendered rows,
        // so this has to be refreshed on every list change (pending → real ranking).
        // Rank 0 = unknown → no rank claim at all.
        private void UpdateCaption(List<Row> rows)
        {
            if (captionLabel == null) return;
            int rank = 0;
            if (rows != null)
            {
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i].IsPlayer) { rank = rows[i].Rank; break; }
            }
            captionLabel.text = rank > 0
                ? $"순위 <color=#{GoldHex}><b>{rank}</b></color> / {rows.Count}"
                : "내 점수";
        }

        // ── Pure row model ─────────────────────────────────────────────────────
        // Display row for one leaderboard slot. Both the pending fallback and the real
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

        // Offline/guest fallback, or the moment before the real ranking lands.
        //
        // This used to fabricate five bot scores around the player's own (0.5x-1.5x)
        // and rank the player among them. That invented a standing the player does
        // not have — and once the rows stopped saying "봇", the made-up numbers would
        // have read as real opponents' results. So the fallback now shows the pending
        // state it actually is: only the player's row carries a real value.
        //
        // Rank 0 means "unknown" — BuildRows never emits 0, and UpdateCaption drops
        // the rank when it sees it.
        //
        // Slot count depends on WHY we are in the fallback:
        //  - No tournament session (guest / not signed in / no server URL): this list
        //    is terminal, nothing will ever replace it, so a 10-slot bracket would
        //    imply a lobby that is filling up. Show 5.
        //  - Signed in, ranking still in flight: keep the server's usual
        //    maxEntryCount so the panel doesn't resize when the real rows land.
        //
        // The predicate matches TournamentMatchReporter (:33, :61), which gates on
        // IdToken. Do NOT switch this to UserSession.IsSignedIn — that is TRUE for
        // guests (LoginPanelView SKIP sets a user with an empty token), and guests
        // never get a tournament session.
        private const int TerminalPendingSlots = 5;
        private const int AwaitingPendingSlots = 10;

        private static int PendingSlotCount =>
            string.IsNullOrEmpty(UserSession.IdToken) ? TerminalPendingSlots : AwaitingPendingSlots;

        private static List<Row> BuildPendingRows(int playerScore)
        {
            int slots = PendingSlotCount;
            var rows = new List<Row>(slots)
            {
                new Row(0, "나", playerScore, true, false),
            };
            for (int i = 1; i < slots; i++)
                rows.Add(new Row(0, "참가자 찾는 중", 0, false, true));
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
            // deferred) — avoids a one-frame double list when the pending list swaps
            // for real data.
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                var child = _listContent.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            for (int i = 0; i < rows.Count; i++) CreateRow(rows[i]);
        }

        // null/empty hides the whole block (mirrors the old statsLabel behaviour).
        private void SetChips((string label, string value)[] items)
        {
            if (_chipsRoot == null) return;
            for (int i = _chipsRoot.childCount - 1; i >= 0; i--)
            {
                var child = _chipsRoot.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            if (items == null || items.Length == 0)
            {
                _chipsRoot.gameObject.SetActive(false);
                return;
            }
            _chipsRoot.gameObject.SetActive(true);
            for (int i = 0; i < items.Length; i++) CreateChip(items[i].label, items[i].value);
        }

        // Full-width stat row in the left column: label left, value right. Sizing is
        // layout-driven on purpose — measuring with GetPreferredValues() here throws a
        // NullReferenceException inside TMP, because the label is freshly
        // AddComponent'd and its font asset is not resolved yet. That threw
        // mid-ShowResult and the popup never reached its SetActive(true), i.e. the
        // whole result screen silently failed to appear.
        private void CreateChip(string label, string value)
        {
            var go = new GameObject("Stat", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(_chipsRoot, false);
            go.GetComponent<LayoutElement>().preferredHeight = ChipH;

            var plate = go.GetComponent<Image>();
            plate.sprite = _chipPlate;
            plate.type = Image.Type.Sliced;
            plate.color = Color.white; // fill baked into the sprite
            plate.raycastTarget = false;

            var key = CreateLabel(go.transform, "Label", label, 34, TextAlignmentOptions.MidlineLeft, ChipLabel);
            var kr = (RectTransform)key.transform;
            kr.anchorMin = new Vector2(0f, 0f);
            kr.anchorMax = new Vector2(1f, 1f);
            kr.offsetMin = new Vector2(26f, 0f);
            kr.offsetMax = new Vector2(-26f, 0f);

            var val = CreateLabel(go.transform, "Value", value, 34, TextAlignmentOptions.MidlineRight, goldColor);
            val.fontStyle = FontStyles.Bold;
            var vr = (RectTransform)val.transform;
            vr.anchorMin = new Vector2(0f, 0f);
            vr.anchorMax = new Vector2(1f, 1f);
            vr.offsetMin = new Vector2(26f, 0f);
            vr.offsetMax = new Vector2(-26f, 0f);
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
            badgeRt.anchoredPosition = new Vector2(16f, 0f);
            badgeRt.sizeDelta = new Vector2(46f, 46f);
            // Rank 0 = standing unknown (pending fallback) — never a real position.
            var badgeLabel = CreateLabel(badge.transform, "Num", row.Rank > 0 ? row.Rank.ToString() : "-", 28,
                TextAlignmentOptions.Center, badgeText);
            badgeLabel.fontStyle = FontStyles.Bold;
            StretchFull((RectTransform)badgeLabel.transform);

            Color textColor = row.IsWaiting ? WaitingText : row.IsPlayer ? goldColor : Color.white;

            // Name (left, after badge).
            var name = CreateLabel(go.transform, "Name", row.Name, 36, TextAlignmentOptions.MidlineLeft, textColor);
            if (row.IsPlayer) name.fontStyle = FontStyles.Bold;
            var nameRt = (RectTransform)name.transform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(76f, 0f);
            nameRt.offsetMax = new Vector2(-250f, 0f);

            // Score (right). Bold and a step above the name — a leaderboard is read
            // down the score column.
            string scoreText = row.IsWaiting ? "-" : row.Score.ToString("N0");
            var score = CreateLabel(go.transform, "Score", scoreText, 38, TextAlignmentOptions.MidlineRight, textColor);
            if (!row.IsWaiting) score.fontStyle = FontStyles.Bold;
            var scoreRt = (RectTransform)score.transform;
            scoreRt.anchorMin = new Vector2(1f, 0f);
            scoreRt.anchorMax = new Vector2(1f, 1f);
            scoreRt.pivot = new Vector2(1f, 0.5f);
            scoreRt.sizeDelta = new Vector2(230f, 0f);
            scoreRt.anchoredPosition = new Vector2(-20f, 0f);
        }

        // ── Build ────────────────────────────────────────────────────────────
        private void BuildCanvas()
        {
            if (_built) return;
            _built = true;

            BakeSprites();

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

            // Panel: two columns side by side. Height is content-driven — the right
            // column's row count decides it (5-slot pending vs 10-slot bracket), and
            // the left column stretches to match.
            var panel = new GameObject("ResultPanel", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            panel.transform.SetParent(roots.SafeAreaRoot, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelW, 600f); // y is driven by the fitter

            var panelHlg = panel.GetComponent<HorizontalLayoutGroup>();
            panelHlg.padding = new RectOffset((int)Pad, (int)Pad, (int)Pad, (int)Pad);
            panelHlg.spacing = ColGap;
            panelHlg.childAlignment = TextAnchor.UpperCenter;
            panelHlg.childControlWidth = true;
            panelHlg.childControlHeight = true;
            panelHlg.childForceExpandWidth = false;
            panelHlg.childForceExpandHeight = true; // left column fills the taller right one

            var panelFitter = panel.GetComponent<ContentSizeFitter>();
            panelFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            panelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var panelImg = panel.GetComponent<Image>();
            panelImg.sprite = UiRoundedSprite.Make(32f, 4f, navyFill, new Color(goldColor.r, goldColor.g, goldColor.b, 0.95f));
            panelImg.type = Image.Type.Sliced;

            BuildLeftColumn(panelRect);
            BuildRightColumn(panelRect);

            UiLayer.Apply(gameObject);
        }

        private void BakeSprites()
        {
            _tabSprite = UiRoundedSprite.Make(30f, 0f, Color.white, Color.white);
            _buttonSprite = UiRoundedSprite.Make(30f, 0f, Color.white, Color.white);
            _rowNormal = UiRoundedSprite.Make(14f, 0f, RowFill, RowFill);
            _rowOwn = UiRoundedSprite.Make(14f, 3f, OwnFill, goldColor);
            _rowWaiting = UiRoundedSprite.Make(14f, 0f, WaitingFill, WaitingFill);
            _chipPlate = UiRoundedSprite.Make(14f, 0f, ChipFill, ChipFill);
            _badgeGold = UiRoundedSprite.MakeCircle(48, BadgeGold);
            _badgeSilver = UiRoundedSprite.MakeCircle(48, BadgeSilver);
            _badgeBronze = UiRoundedSprite.MakeCircle(48, BadgeBronze);
            _badgeNavy = UiRoundedSprite.MakeCircle(48, BadgeNavy, 2f, new Color(goldColor.r, goldColor.g, goldColor.b, 0.6f));
        }

        // My result: outcome ribbon → hero score → rank → stat rows → exit button.
        // The outcome ribbon is deliberately SMALLER than the score: this screen
        // exists to report a score, and a big outcome tab over a small score read as
        // the opposite priority.
        private void BuildLeftColumn(RectTransform panel)
        {
            var col = new GameObject("MyResult", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(LayoutElement));
            col.transform.SetParent(panel, false);
            col.GetComponent<LayoutElement>().preferredWidth = LeftW;

            var vlg = col.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var cr = (RectTransform)col.transform;

            var tab = new GameObject("Ribbon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            tab.transform.SetParent(cr, false);
            tab.GetComponent<LayoutElement>().preferredHeight = 78f;
            tabImage = tab.GetComponent<Image>();
            tabImage.sprite = _tabSprite;
            tabImage.type = Image.Type.Sliced;
            tabImage.color = goldColor;
            resultLabel = CreateLabel(tab.transform, "ResultLabel", "", 48, TextAlignmentOptions.Center, BadgeTextDark);
            resultLabel.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            resultLabel.characterSpacing = 8f;
            StretchFull((RectTransform)resultLabel.transform);

            heroScoreLabel = CreateLabel(cr, "HeroScore", "", 140, TextAlignmentOptions.Center, goldColor);
            heroScoreLabel.fontStyle = FontStyles.Bold;
            heroScoreLabel.characterSpacing = -2f;
            AddLayoutHeight(heroScoreLabel.gameObject, 156f);

            captionLabel = CreateLabel(cr, "Caption", "", 32, TextAlignmentOptions.Center, CaptionText);
            captionLabel.fontStyle = FontStyles.SmallCaps;
            captionLabel.characterSpacing = 4f;
            captionLabel.richText = true; // rank value is emphasised in gold
            AddLayoutHeight(captionLabel.gameObject, 40f);

            AddFlexibleSpacer(cr);

            // Stat rows. Generic (label, value) pairs — see SetChips.
            var chips = new GameObject("Stats", typeof(RectTransform), typeof(VerticalLayoutGroup));
            chips.transform.SetParent(cr, false);
            _chipsRoot = (RectTransform)chips.transform;
            var chipVlg = chips.GetComponent<VerticalLayoutGroup>();
            chipVlg.spacing = 10f;
            chipVlg.childAlignment = TextAnchor.UpperCenter;
            chipVlg.childControlWidth = true;
            chipVlg.childControlHeight = true;
            chipVlg.childForceExpandWidth = true;
            chipVlg.childForceExpandHeight = false;
            chips.SetActive(false);

            AddFlexibleSpacer(cr);

            var btn = new GameObject("LobbyButton", typeof(RectTransform), typeof(Image),
                typeof(Button), typeof(LayoutElement));
            btn.transform.SetParent(cr, false);
            btn.GetComponent<LayoutElement>().preferredHeight = ButtonH;
            var btnImg = btn.GetComponent<Image>();
            btnImg.sprite = _buttonSprite;
            btnImg.type = Image.Type.Sliced;
            btnImg.color = goldColor;
            lobbyButton = btn.GetComponent<Button>();
            var label = CreateLabel(btn.transform, "Label", "로비로", 38, TextAlignmentOptions.Center, BadgeTextDark);
            label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            label.characterSpacing = 6f;
            StretchFull((RectTransform)label.transform);
        }

        // The standings. The list hugs its rows so 5-slot and 10-slot lists both look
        // intentional — a fixed height left dead space under the short one.
        private void BuildRightColumn(RectTransform panel)
        {
            var col = new GameObject("Standings", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(LayoutElement));
            col.transform.SetParent(panel, false);
            col.GetComponent<LayoutElement>().preferredWidth = RightW;

            var vlg = col.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var cr = (RectTransform)col.transform;

            var section = CreateLabel(cr, "SectionLabel", "랭킹", 30, TextAlignmentOptions.MidlineLeft, SectionText);
            section.fontStyle = FontStyles.SmallCaps;
            section.characterSpacing = 6f;
            AddLayoutHeight(section.gameObject, 40f);

            var list = new GameObject("Leaderboard", typeof(RectTransform), typeof(Image),
                typeof(RectMask2D), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            list.transform.SetParent(cr, false);

            var listFitter = list.GetComponent<ContentSizeFitter>();
            listFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var well = list.GetComponent<Image>();
            well.sprite = UiRoundedSprite.Make(18f, 0f, new Color(0f, 0f, 0f, 0.24f), new Color(0f, 0f, 0f, 0.24f));
            well.type = Image.Type.Sliced;
            well.raycastTarget = false;

            var listVlg = list.GetComponent<VerticalLayoutGroup>();
            listVlg.padding = new RectOffset(12, 12, 12, 12);
            listVlg.spacing = RowGap;
            listVlg.childAlignment = TextAnchor.UpperCenter;
            listVlg.childControlWidth = true;
            listVlg.childControlHeight = true;
            listVlg.childForceExpandWidth = true;
            listVlg.childForceExpandHeight = false;

            _listContent = (RectTransform)list.transform;
        }

        private static void AddLayoutHeight(GameObject go, float height)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        // Absorbs the slack when the left column is stretched to the right column's
        // height, so the exit button sits at the bottom instead of floating mid-panel.
        private static void AddFlexibleSpacer(RectTransform parent)
        {
            var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 0f;
            le.flexibleHeight = 1f;
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
