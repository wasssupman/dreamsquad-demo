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
        // three-minute-kill-race unit 0 — 패배색은 제거했다. 판에서 지는 일이 없어져
        // 이 색을 칠할 상태가 존재하지 않는다.
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
        // battle-score-formula unit 7 — 원시 상태 행의 값 색. 칩 값이 전부 골드면 남은 시간·
        // 스트레스 누적도 점수로 읽히므로 상태 쪽만 낮춘다.
        private static readonly Color StateValue = new Color(0.88f, 0.91f, 0.95f, 1f);
        // 0.10 은 이 네이비 위에서 2px 이 사실상 안 보였다(스크린샷 확인). 헤어라인으로 보이는 선.
        private static readonly Color DividerFill = new Color(1f, 1f, 1f, 0.22f);

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
        private const float DividerH = 2f; // 상태 2줄 ↔ 점수 3축 구분선
        private const float ButtonH = 92f;

        private TextMeshProUGUI resultLabel;
        private TextMeshProUGUI heroScoreLabel;
        private TextMeshProUGUI captionLabel;
        private Image tabImage;
        private Button lobbyButton;
        private RectTransform _listContent;
        private RectTransform _statsRoot;
        private bool _built;

        // Cached procedural sprites (baked once, reused across rows).
        private Sprite _tabSprite;
        private Sprite _buttonSprite;
        private Sprite _rowNormal;
        private Sprite _rowOwn;
        private Sprite _rowWaiting;
        private Sprite _chipPlate;
        private Sprite _dividerFill;
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

        // three-minute-survival unit 7 — 결과 화면의 입력은 **판 성적 값 하나**(`MatchTally`)다.
        //
        // three-minute-kill-race unit 0 — 입구가 승/패 둘에서 **하나**로 줄었다. 판에서 지는
        // 일이 없어졌으므로 «어느 쪽으로 끝났나» 를 물을 자리가 없다. 라벨은 `결과` 고정이고
        // 색 분기도 사라졌다(패배색은 이제 쓰이지 않는다).
        // 줄 구성·문구 다듬기는 unit 4 몫 — 여기서는 승패 축만 걷어낸다.
        public void Show(MatchTally tally)
        {
            if (!_built) BuildCanvas();
            resultLabel.text = "결과";
            if (tabImage != null) tabImage.color = goldColor;

            if (heroScoreLabel != null)
            {
                // 총점 = 처치 점수. 서버에 올라간 수와 같은 값이다(가공 없음 — unit 6).
                heroScoreLabel.text = tally.Total.ToString("N0");
                heroScoreLabel.color = goldColor;
            }

            // The stat rows are generic StatRow values so battle-score-formula could
            // swap what they carry without touching any layout code here.
            //
            // 점수 분모(예산 만점)는 붙이지 않는다. 시간 만점은 t=0 클리어를 전제한 값이라
            // **현실적으로 도달 불가**인데, 그런 수치를 옆에 세우면 항상 한참 못 미친
            // 것처럼 보인다. 축 사이 상대 비교는 세 줄이 나란히 있는 것으로 충분하다.
            SetStatRows(new[]
            {
                StatRow.State("처치", $"{tally.KillCount:N0}기"),
                StatRow.State("남은 안정도", StabilityText(tally.Stability, tally.StabilityMax)),
                StatRow.State("도달 웨이브", $"{tally.WaveReached:N0}"),
            });

            // A ranking that landed while we were closed (the response usually beats
            // the ~4s tally sequence) was held by UpdateLeaderboard — consume it so
            // the popup opens already on the real rows: no pending flash, no
            // same-frame double render. Empty responses fall back to the pending
            // list, same rule as the live-update path.
            List<LeaderboardList.Row> rows = null;
            if (_heldRanking != null)
            {
                var held = LeaderboardList.BuildRows(_heldRanking.entries, _heldRanking.maxEntryCount, _heldOwnUserId);
                if (held.Count > 0) rows = held;
                _heldRanking = null;
                _heldOwnUserId = null;
            }
            if (rows == null) rows = BuildPendingRows(tally.Total);
            RenderRows(rows);
            UpdateCaption(rows);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            _heldRanking = null;
            _heldOwnUserId = null;
            gameObject.SetActive(false);
        }

        // No teardown first: the scene unloads wholesale. (MenuPopup.OnExit releases a
        // pause lease before leaving; the result screen holds none.)
        private void OnLobbyClicked() => SceneTransition.Go(SceneNames.Outgame);

        // A ranking that arrived while the popup was closed (tally still playing).
        // Consumed by the next ShowResult, cleared by Hide().
        private TournamentApi.ResultData _heldRanking;
        private string _heldOwnUserId;

        // tournament-play-report Unit 4 — swap the pending leaderboard for the real
        // tournament ranking once it arrives (guests / fetch failures simply never
        // get here). The popup usually isn't open yet — the response tends to beat
        // the ~4s tally sequence — so while closed the ranking is HELD, and the next
        // ShowResult opens directly on it. A held value can't leak across matches:
        // RESTART bumps the reporter epoch (an in-flight response is dropped before
        // reaching us — TournamentMatchReporter's single stale-response rule) and
        // Hide() clears the hold.
        public void UpdateLeaderboard(TournamentApi.ResultData data, string ownUserId)
        {
            if (data == null) return;
            if (!gameObject.activeSelf)
            {
                _heldRanking = data;
                _heldOwnUserId = ownUserId;
                return;
            }
            if (_listContent == null) return;

            var rows = LeaderboardList.BuildRows(data.entries, data.maxEntryCount, ownUserId);
            if (rows.Count == 0) return; // nothing meaningful to draw — keep the pending list
            RenderRows(rows);
            UpdateCaption(rows); // real ranking almost always moves the player off the pending state
        }

        // "순위 3 / 10" under the hero score. The rank comes out of the rendered rows,
        // so this has to be refreshed on every list change (pending → real ranking).
        // Rank 0 = unknown → no rank claim at all.
        private void UpdateCaption(List<LeaderboardList.Row> rows)
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

        // 행 모델은 LeaderboardList 것을 쓴다 — tournament-history unit 1 에서
        // 히스토리 상세 팝업과 공유하려고 추출된 것이다. 여기서 사본을 들고 있으면
        // 서버 계약(rank 필드·WAITING 슬롯)이 바뀔 때 두 곳을 고쳐야 하고, 테스트는
        // LeaderboardList 쪽만 덮는다. **렌더링은 공유하지 않는다** — 이 화면은 2컬럼
        // 스케일(RowH 56·이름 36·점수 38)이고 LeaderboardList.Render 는 48/30 하드코딩이다.

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
        // Always 5 slots = the server's tournament bracket size (maxEntryCount).
        // An earlier revision showed 10 while signed-in to match a server max that
        // has since shrunk to 5 — the stale bracket read as "10 participants"
        // whenever the ranking never replaced the pending list. If the server ever
        // grows the bracket, real rows still size by the response's maxEntryCount
        // (BuildRows); only this pre-landing placeholder count is pinned here.
        private const int PendingSlots = 5;

        private static List<LeaderboardList.Row> BuildPendingRows(int playerScore)
        {
            var rows = new List<LeaderboardList.Row>(PendingSlots)
            {
                new LeaderboardList.Row(0, "나", playerScore, true, false),
            };
            for (int i = 1; i < PendingSlots; i++)
                rows.Add(new LeaderboardList.Row(0, "참가자 찾는 중", 0, false, true));
            return rows;
        }

        // ── Rendering ────────────────────────────────────────────────────────
        private void RenderRows(List<LeaderboardList.Row> rows)
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

        // battle-score-formula unit 7 — 한 블록에 원시 상태(muted)와 점수(골드)가 섞이므로
        // 색 구분과 구분선을 값으로 나른다.
        private readonly struct StatRow
        {
            public readonly string Label, Value;
            public readonly bool Muted, IsDivider;

            private StatRow(string label, string value, bool muted, bool isDivider)
            {
                Label = label; Value = value; Muted = muted; IsDivider = isDivider;
            }

            public static StatRow State(string label, string value) => new StatRow(label, value, true, false);
            public static StatRow Score(string label, string value) => new StatRow(label, value, false, false);
            public static StatRow Divider => new StatRow(null, null, false, true);
        }

        // 남은 시간 표기 — `0:00` / `2:13`. ceil 이라 0.4초 남으면 1초로 올린다(0 은 0:00 그대로).
        public static string ClockText(float remainingSec)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(remainingSec));
            return $"{t / 60}:{t % 60:D2}";
        }

        // 스트레스 누적 표기. limit <= 0 = 한계 미표기.
        // three-minute-survival unit 3 — 결과 화면은 더 이상 이걸 쓰지 않지만(스트레스는 패배도
        // 점수도 만들지 않는다) 순수 표기 함수라 테스트가 참조한다.
        public static string StressText(int accrued, int limit)
            => limit > 0 ? $"{accrued} / {limit}" : accrued.ToString();

        // three-minute-survival unit 3 — 남은 안정도 표기: `12 / 20 (60%)`. max <= 0 = 값만.
        // 백분율을 같이 내는 이유는 동점 판정 기준(permille)이 비율이라, 화면에서 그 근거가
        // 읽혀야 하기 때문이다.
        public static string StabilityText(int stability, int max)
        {
            int v = Mathf.Max(0, stability);
            if (max <= 0) return v.ToString();
            int pct = Mathf.RoundToInt(Mathf.Clamp01(v / (float)max) * 100f);
            return $"{v} / {max} ({pct}%)";
        }

        // null/empty hides the whole block (mirrors the old statsLabel behaviour).
        private void SetStatRows(StatRow[] items)
        {
            if (_statsRoot == null) return;
            for (int i = _statsRoot.childCount - 1; i >= 0; i--)
            {
                var child = _statsRoot.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            if (items == null || items.Length == 0)
            {
                _statsRoot.gameObject.SetActive(false);
                return;
            }
            _statsRoot.gameObject.SetActive(true);
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i].IsDivider) CreateDivider();
                else CreateStatRow(items[i]);
            }
        }

        private void CreateDivider()
        {
            var go = new GameObject("Divider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(_statsRoot, false);
            go.GetComponent<LayoutElement>().preferredHeight = DividerH;
            var img = go.GetComponent<Image>();
            img.sprite = _dividerFill;
            img.type = Image.Type.Sliced;
            img.color = DividerFill;
            img.raycastTarget = false;
        }

        // Full-width stat row in the left column: label left, value right. Sizing is
        // layout-driven on purpose — measuring with GetPreferredValues() here throws a
        // NullReferenceException inside TMP, because the label is freshly
        // AddComponent'd and its font asset is not resolved yet. That threw
        // mid-ShowResult and the popup never reached its SetActive(true), i.e. the
        // whole result screen silently failed to appear.
        private void CreateStatRow(StatRow row)
        {
            var go = new GameObject("Stat", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(_statsRoot, false);
            go.GetComponent<LayoutElement>().preferredHeight = ChipH;

            var plate = go.GetComponent<Image>();
            plate.sprite = _chipPlate;
            plate.type = Image.Type.Sliced;
            plate.color = Color.white; // fill baked into the sprite
            plate.raycastTarget = false;

            var key = CreateLabel(go.transform, "Label", row.Label, 34, TextAlignmentOptions.MidlineLeft, ChipLabel);
            var kr = (RectTransform)key.transform;
            kr.anchorMin = new Vector2(0f, 0f);
            kr.anchorMax = new Vector2(1f, 1f);
            kr.offsetMin = new Vector2(26f, 0f);
            kr.offsetMax = new Vector2(-26f, 0f);

            // 상태 행은 muted + 보통 굵기, 점수 행은 골드 + 볼드 — 색과 무게 둘로 갈라야
            // 숫자를 훑을 때 점수 3축만 눈에 들어온다.
            var val = CreateLabel(go.transform, "Value", row.Value, 34, TextAlignmentOptions.MidlineRight,
                row.Muted ? StateValue : goldColor);
            if (!row.Muted) val.fontStyle = FontStyles.Bold;
            var vr = (RectTransform)val.transform;
            vr.anchorMin = new Vector2(0f, 0f);
            vr.anchorMax = new Vector2(1f, 1f);
            vr.offsetMin = new Vector2(26f, 0f);
            vr.offsetMax = new Vector2(-26f, 0f);
        }

        private void CreateRow(LeaderboardList.Row row)
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
            // three-minute-survival unit 6 — 서버가 준 점수를 그대로 그린다(인코딩 폐기).
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
            // column's row count decides it (pending 5-slot list, or however many
            // rows the server bracket lands), and the left column stretches to match.
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
            // 흰색 solid — 색은 Image.color 로 준다. _chipPlate 를 재사용하면 알파가 곱해져
            // (0.06 × 0.10) 사실상 안 보인다.
            _dividerFill = UiRoundedSprite.Make(2f, 0f, Color.white, Color.white);
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

            // Stat rows — see SetStatRows.
            var chips = new GameObject("Stats", typeof(RectTransform), typeof(VerticalLayoutGroup));
            chips.transform.SetParent(cr, false);
            _statsRoot = (RectTransform)chips.transform;
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

        // The standings. The list hugs its rows so any row count looks intentional —
        // a fixed height left dead space under short lists.
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
