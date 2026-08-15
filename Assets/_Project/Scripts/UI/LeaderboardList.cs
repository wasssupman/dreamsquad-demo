using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Core.Api;

namespace Wassup.UI
{
    // tournament-history Unit 1 — the ranked leaderboard rendering extracted from
    // ResultScreen so the battle result popup and the lobby tournament-history
    // detail popup share one look. Pure row model (BuildRows) + procedural row
    // painting into a caller-owned content RectTransform.
    //
    // Not a MonoBehaviour: each presenter owns its own canvas/layout and delegates
    // just the row list here. Sprites are baked once per instance (ctor) and reused
    // across every Render call.
    public class LeaderboardList
    {
        // Row palette — visual constants (not tuning knobs), matching the in-game
        // HUD language. Copied here so this component is self-contained; ResultScreen
        // keeps its own header/footer palette.
        private static readonly Color Gold = new Color(1f, 0.78f, 0.28f, 1f);
        private static readonly Color RowFill = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color OwnFill = new Color(1f, 0.83f, 0.35f, 0.20f);
        private static readonly Color WaitingFill = new Color(1f, 1f, 1f, 0.03f);
        private static readonly Color WaitingText = new Color(0.60f, 0.63f, 0.65f, 1f); // #9AA0A6
        private static readonly Color BadgeGold = new Color(1f, 0.82f, 0.29f, 1f);
        private static readonly Color BadgeSilver = new Color(0.84f, 0.86f, 0.88f, 1f);
        private static readonly Color BadgeBronze = new Color(0.78f, 0.54f, 0.30f, 1f);
        private static readonly Color BadgeNavy = new Color(0.16f, 0.19f, 0.26f, 1f);
        private static readonly Color BadgeTextDark = new Color(0.10f, 0.09f, 0.06f, 1f);

        private const float RowH = 48f;
        private const float DeckBtnW = 108f;
        private const float DeckBtnH = 34f;
        // 점수 컬럼이 행 우변에서 차지하는 폭(sizeDelta 140 + 우여백 22). 덱보기 버튼과
        // 이름 컬럼이 전부 이 값에서 파생된다 — 예전엔 이름 inset(150)을 버튼 기준으로
        // 재사용해 버튼이 점수 rect 를 6px 물었다(점수가 7자리가 되면 겹친다).
        private const float ScoreColW = 162f;
        private const float ColSpacing = 8f;

        // Cached procedural sprites (baked once, reused across rows).
        private readonly Sprite _rowNormal;
        private readonly Sprite _rowOwn;
        private readonly Sprite _rowWaiting;
        private readonly Sprite _badgeGold;
        private readonly Sprite _badgeSilver;
        private readonly Sprite _badgeBronze;
        private readonly Sprite _badgeNavy;
        private readonly Sprite _deckBtn;

        public LeaderboardList()
        {
            _deckBtn = UiRoundedSprite.Make(10f, 2f, new Color(1f, 1f, 1f, 0.08f),
                new Color(Gold.r, Gold.g, Gold.b, 0.55f));
            _rowNormal = UiRoundedSprite.Make(14f, 0f, RowFill, RowFill);
            _rowOwn = UiRoundedSprite.Make(14f, 3f, OwnFill, Gold);
            _rowWaiting = UiRoundedSprite.Make(14f, 0f, WaitingFill, WaitingFill);
            _badgeGold = UiRoundedSprite.MakeCircle(40, BadgeGold);
            _badgeSilver = UiRoundedSprite.MakeCircle(40, BadgeSilver);
            _badgeBronze = UiRoundedSprite.MakeCircle(40, BadgeBronze);
            _badgeNavy = UiRoundedSprite.MakeCircle(40, BadgeNavy, 2f, new Color(Gold.r, Gold.g, Gold.b, 0.6f));
        }

        // ── Pure row model ─────────────────────────────────────────────────────
        // Display row for one leaderboard slot. Both the bot fallback and the real
        // tournament ranking funnel through Render via these rows.
        public readonly struct Row
        {
            public readonly int Rank;
            public readonly string Name;
            public readonly int Score;
            public readonly bool IsPlayer;
            public readonly bool IsWaiting;
            // tournament-history-deck-view unit 1 — 그 참가자가 그 판에 들고 간 덱의
            // 원문 페이로드. **파싱하지 않고** 실어만 나른다 — 해석은 팝업을 여는
            // 직전에 한 번이면 되고, 행 모델이 스키마를 알 이유가 없다.
            // 기록이 없는 참가(구 엔트리)와 대기 슬롯은 null.
            public readonly string DeckInfo;

            public Row(int rank, string name, int score, bool isPlayer, bool isWaiting,
                string deckInfo = null)
            {
                Rank = rank;
                Name = name;
                Score = score;
                IsPlayer = isPlayer;
                IsWaiting = isWaiting;
                DeckInfo = deckInfo;
            }
        }

        // Tournament slots are pre-assigned (maxEntryCount): every slot is rendered,
        // and slots no opponent has taken yet read WAITING... The dev server omits
        // the schema's `rank` field, so order by score and derive the rank from
        // position; a server-provided rank (>0) wins when it appears.
        public static List<Row> BuildRows(IReadOnlyList<TournamentApi.ResultEntry> entries,
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
                    rows.Add(new Row(rank, DisplayName(e.userName), e.score, isPlayer, false, e.deckInfo));
                }
                else
                {
                    rows.Add(new Row(i + 1, "대기 중...", 0, false, true));
                }
            }
            return rows;
        }

        // empty names would collapse the row; long ones would overrun the score column.
        private static string DisplayName(string userName)
        {
            if (string.IsNullOrEmpty(userName)) return "?";
            return userName.Length <= 10 ? userName : userName.Substring(0, 10);
        }

        // ── Rendering ────────────────────────────────────────────────────────
        // Repaints `content` with `rows`. Detach-then-destroy so old rows leave the
        // layout this frame (Destroy is deferred) — avoids a one-frame double list
        // when bots swap for real data.
        // onDeckView: null 이면 덱보기 버튼을 만들지 않는다(옵트인). 넘기면 대기 슬롯을
        // 제외한 모든 행에 버튼이 붙고, 눌린 행이 그대로 콜백으로 온다 — 덱 정보가 없는
        // 참가자도 포함이다(버튼이 행마다 있다 없다 하면 눌러도 되는지 매번 판단하게
        // 된다). "없음"을 말하는 건 팝업의 몫이다.
        public void Render(RectTransform content, IReadOnlyList<Row> rows, Action<Row> onDeckView = null)
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i);
                child.SetParent(null, false);
                // `using System` 이 들어와 Object 가 모호해진다 — 한정 필수.
                UnityEngine.Object.Destroy(child.gameObject);
            }
            if (rows == null) return;
            for (int i = 0; i < rows.Count; i++) CreateRow(content, rows[i], onDeckView);
        }

        private void CreateRow(RectTransform content, Row row, Action<Row> onDeckView)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(content, false);
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

            Color textColor = row.IsWaiting ? WaitingText : row.IsPlayer ? Gold : Color.white;

            // 덱보기 컬럼이 붙으면 이름이 그만큼 물러난다. **버튼이 없는 호출자의 여백은
            // 지금 값 그대로** — 공유 컴포넌트라 다른 소비처가 조용히 틀어지면 안 된다.
            // 대기 슬롯에는 버튼을 안 붙이지만 여백은 같이 준다(컬럼이 어긋나 보인다).
            float nameRightInset = onDeckView != null
                ? ScoreColW + ColSpacing + DeckBtnW + ColSpacing
                : 150f; // 버튼이 없는 호출자의 여백은 기존 값 그대로(공유 컴포넌트)

            // Name (left, after badge).
            var name = CreateLabel(go.transform, "Name", row.Name, 30, TextAlignmentOptions.MidlineLeft, textColor);
            var nameRt = (RectTransform)name.transform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(66f, 0f);
            nameRt.offsetMax = new Vector2(-nameRightInset, 0f);

            if (onDeckView != null && !row.IsWaiting) CreateDeckButton(go.transform, row, onDeckView);

            // Score (right).
            // three-minute-survival unit 6 — 서버가 준 점수를 **그대로** 그린다. 제출값
            // 인코딩이 폐기되면서 변환이 사라졌다(구 인코딩 기록은 10억대 원값으로 뜬다).
            string scoreText = row.IsWaiting ? "-" : row.Score.ToString("N0");
            var score = CreateLabel(go.transform, "Score", scoreText, 30, TextAlignmentOptions.MidlineRight, textColor);
            var scoreRt = (RectTransform)score.transform;
            scoreRt.anchorMin = new Vector2(1f, 0f);
            scoreRt.anchorMax = new Vector2(1f, 1f);
            scoreRt.pivot = new Vector2(1f, 0.5f);
            scoreRt.sizeDelta = new Vector2(140f, 0f);
            scoreRt.anchoredPosition = new Vector2(-22f, 0f);
        }

        // 점수 컬럼 왼쪽에 붙는 작은 액션 버튼. 행 자체를 누르는 방식이 아닌 이유:
        // 랭킹 행은 히스토리에서 선택 대상이 아니라 정보라서, 행 전체가 눌리면
        // 좌측 목록의 "행 = 선택" 규칙과 충돌한다.
        private void CreateDeckButton(Transform parent, Row row, Action<Row> onDeckView)
        {
            var go = new GameObject("DeckViewButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = _deckBtn;
            img.type = Image.Type.Sliced;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(DeckBtnW, DeckBtnH);
            rt.anchoredPosition = new Vector2(-(ScoreColW + ColSpacing), 0f);

            var captured = row;
            go.GetComponent<Button>().onClick.AddListener(() => onDeckView(captured));

            var label = CreateLabel(go.transform, "Label", "덱보기", 20,
                TextAlignmentOptions.Center, Color.white);
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
