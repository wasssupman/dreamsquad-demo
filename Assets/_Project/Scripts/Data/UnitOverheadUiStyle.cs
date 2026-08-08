using System;
using UnityEngine;

namespace Wassup.Data
{
    // three-minute-survival unit 1 — 오버헤드 바의 스킨 종류. 스킨이 3종이 된 순간
    // `bool defender` 조합은 거짓말을 시작하므로(GoalStability 는 방어유닛도 적도 아니다)
    // 뷰 계층은 이 enum 을 받는다. 카드행은 Defender 에서만 뜬다.
    public enum OverheadBarSkin
    {
        Defender,
        Enemy,
        GoalStability,
    }

    // unit-overhead-ui — 1920x1080 reference pixel 기반 공통 레이아웃과 진영별 skin.
    [CreateAssetMenu(fileName = "UnitOverheadUiStyle", menuName = "Wassup/Unit Overhead UI Style", order = 21)]
    public class UnitOverheadUiStyle : ScriptableObject
    {
        [Serializable]
        public struct BarSkin
        {
            [Range(0.1f, 1f)] public float tileWidthFraction;
            public float minWidth;
            public float maxWidth;
            public float height;
            public float inset;
            public float border;
            public float radius;
            public float shadowOffset;
            public Color frame;
            public Color track;
            public Color shadow;
            public Color fillLow;
            public Color fillHigh;
            public Color highlight;
            public float highlightHeight;
            public Color damageTrail;
            // shield-guardian-defender unit 2 — 실드 세그먼트 색(HP fill 끝에 이어붙는
            // 오버레이). struct 라 asset 에 값 필수 — 미기재 시 (0,0,0,0)=투명 함정.
            public Color shield;
            [Range(0f, 1f)] public float fullHealthAlpha;
            public bool clippedEnds;
        }

        [Header("Reference pixels")]
        [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField] private float headGap = 5f;
        [SerializeField] private float cardGap = 5f;
        [SerializeField] private float cardHeight = 28.8f;
        [SerializeField] private float cardSpacing = 4f;
        [SerializeField, Range(0.5f, 1f)] private float cardRowTileWidthFraction = 0.92f;
        // use-flow unit 3 — 부착 카드 발동 펄스. 아이콘이 ~29px 라 펀치 단독으론 안 보인다
        // (사용자 피드백 2026-07-29) → 행 펀치 + 확산 링 버스트(락온 확정 펄스와 같은 문법).
        // 에셋에 키 없으면 코드 기본값.
        [SerializeField] private float cardPulseScale = 1.8f;
        [SerializeField] private float cardPulseSec = 0.4f;
        [SerializeField] private float cardPulseRingScale = 1.6f;  // 링 최종 지름 = 행폭 × 이 값 (rev 2 축소 — 사용자)
        [SerializeField] private Color cardPulseRingColor = new Color(0.55f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color cardPlate = new Color(0.035f, 0.055f, 0.09f, 0.96f);
        [SerializeField] private Color unitCardBorder = new Color(0.08f, 0.82f, 0.9f, 1f);
        [SerializeField] private Color squadCardBorder = new Color(1f, 0.72f, 0.12f, 1f);
        [SerializeField] private float damageTrailCatchup = 2.8f;

        [Header("Stack indicator row (확장 unit 6)")]
        [SerializeField] private float stackGap = 4f;            // 드림캐쳐 행(없으면 바) 위 간격
        [SerializeField] private float stackIconHeight = 22f;    // 정사각 아이콘 높이(ref px)
        [SerializeField] private float stackSpacing = 3f;
        [SerializeField] private int stackRowMax = 4;            // 최대 표시 아이콘 수
        [SerializeField, Range(0.5f, 1f)] private float stackRowTileWidthFraction = 0.92f;
        [SerializeField] private Color stackBadgeColor = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color stackBadgePlate = new Color(0.05f, 0.06f, 0.09f, 0.9f);
        [SerializeField, Range(0.2f, 1f)] private float stackBadgeHeightFraction = 0.5f; // 아이콘 대비 배지 높이

        [Header("Defender — rounded cyan")]
        [SerializeField] private BarSkin defender = new BarSkin
        {
            tileWidthFraction = 0.88f, minWidth = 96f, maxWidth = 148f, height = 14f,
            inset = 2.5f, border = 2.5f, radius = 7f, shadowOffset = 2f,
            frame = new Color(0.018f, 0.03f, 0.045f, 1f),
            track = new Color(0.07f, 0.12f, 0.15f, 0.94f),
            shadow = new Color(0.005f, 0.012f, 0.02f, 0.72f),
            fillLow = new Color(0.05f, 0.58f, 0.66f, 1f), fillHigh = new Color(0.12f, 0.92f, 0.94f, 1f),
            highlight = new Color(0.78f, 1f, 1f, 0.86f), highlightHeight = 1.5f,
            damageTrail = new Color(1f, 0.78f, 0.18f, 0.95f), fullHealthAlpha = 0.94f, clippedEnds = false
        };

        [Header("Enemy — clipped coral")]
        [SerializeField] private BarSkin enemy = new BarSkin
        {
            tileWidthFraction = 0.74f, minWidth = 82f, maxWidth = 124f, height = 12f,
            inset = 2f, border = 2f, radius = 2f, shadowOffset = 2f,
            frame = new Color(0.055f, 0.022f, 0.032f, 1f),
            track = new Color(0.19f, 0.055f, 0.065f, 0.96f),
            shadow = new Color(0.012f, 0.004f, 0.008f, 0.72f),
            fillLow = new Color(0.75f, 0.12f, 0.08f, 1f), fillHigh = new Color(1f, 0.34f, 0.17f, 1f),
            highlight = new Color(1f, 0.78f, 0.58f, 0.8f), highlightHeight = 1.25f,
            damageTrail = new Color(1f, 0.68f, 0.15f, 0.9f), fullHealthAlpha = 0.92f, clippedEnds = true
        };

        // three-minute-survival unit 1 — 골 안정도 바. 유닛 바와 **같은 언어**(둥근 프레임·
        // 그라데이션 fill·데미지 트레일)를 쓰되 더 크고 색으로 구별한다. 만피에도 상시
        // 보여야 하므로 fullHealthAlpha = 1 이다 — 이 바는 판의 상태판이라 감쇠하면 안 된다.
        // Appended last (직렬화 back-compat: 기존 asset 은 이 initializer 를 그대로 받는다).
        [Header("Goal stability — large amber")]
        [SerializeField] private BarSkin goalStability = new BarSkin
        {
            tileWidthFraction = 1.6f, minWidth = 168f, maxWidth = 260f, height = 22f,
            inset = 3f, border = 3f, radius = 9f, shadowOffset = 3f,
            frame = new Color(0.05f, 0.035f, 0.015f, 1f),
            track = new Color(0.16f, 0.11f, 0.05f, 0.96f),
            shadow = new Color(0.01f, 0.008f, 0.004f, 0.78f),
            fillLow = new Color(0.85f, 0.28f, 0.1f, 1f), fillHigh = new Color(1f, 0.82f, 0.28f, 1f),
            highlight = new Color(1f, 0.95f, 0.75f, 0.9f), highlightHeight = 2f,
            damageTrail = new Color(1f, 0.4f, 0.2f, 0.95f),
            shield = new Color(0f, 0f, 0f, 0f),
            fullHealthAlpha = 1f, clippedEnds = false
        };

        [Header("Goal stability — value label")]
        [SerializeField] private float stabilityLabelGap = 3f;      // 바 위쪽 간격(ref px)
        [SerializeField] private float stabilityLabelFontSize = 26f;
        [SerializeField] private Color stabilityLabelColor = new Color(1f, 0.93f, 0.7f, 1f);

        public Vector2 ReferenceResolution => referenceResolution;
        public float HeadGap => Mathf.Max(0f, headGap);
        public float CardGap => Mathf.Max(0f, cardGap);
        public float CardHeight => Mathf.Max(1f, cardHeight);
        public float CardSpacing => Mathf.Max(0f, cardSpacing);
        public float CardRowTileWidthFraction => Mathf.Clamp01(cardRowTileWidthFraction);
        public float CardPulseScale => Mathf.Max(1f, cardPulseScale);
        public float CardPulseSec => Mathf.Max(0.05f, cardPulseSec);
        public float CardPulseRingScale => Mathf.Max(1f, cardPulseRingScale);
        public Color CardPulseRingColor => cardPulseRingColor;
        public Color CardPlate => cardPlate;
        public Color UnitCardBorder => unitCardBorder;
        public Color SquadCardBorder => squadCardBorder;
        public float DamageTrailCatchup => Mathf.Max(0.01f, damageTrailCatchup);
        public float StackGap => Mathf.Max(0f, stackGap);
        public float StackIconHeight => Mathf.Max(1f, stackIconHeight);
        public float StackSpacing => Mathf.Max(0f, stackSpacing);
        public int StackRowMax => Mathf.Max(0, stackRowMax);
        public float StackRowTileWidthFraction => Mathf.Clamp01(stackRowTileWidthFraction);
        public Color StackBadgeColor => stackBadgeColor;
        public Color StackBadgePlate => stackBadgePlate;
        public float StackBadgeHeightFraction => Mathf.Clamp(stackBadgeHeightFraction, 0.2f, 1f);
        public BarSkin Defender => defender;
        public BarSkin Enemy => enemy;
        public BarSkin GoalStability => goalStability;
        public float StabilityLabelGap => Mathf.Max(0f, stabilityLabelGap);
        public float StabilityLabelFontSize => Mathf.Max(1f, stabilityLabelFontSize);
        public Color StabilityLabelColor => stabilityLabelColor;

        // three-minute-survival unit 1 — 스킨 해석의 단일 지점. 뷰가 enum→BarSkin 매핑을
        // 두 곳(Show/Rebuild)에서 각자 하면 갈라진다.
        public BarSkin Skin(OverheadBarSkin kind) => kind switch
        {
            OverheadBarSkin.Enemy => enemy,
            OverheadBarSkin.GoalStability => goalStability,
            _ => defender,
        };

        public static Color EvaluateFill(in BarSkin skin, float ratio)
            => Color.Lerp(skin.fillLow, skin.fillHigh, Mathf.Clamp01(ratio));
    }
}
