using UnityEngine;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 1 — shared card frame/fallback color + label,
    // lifted from the retired DreamcatcherDeckBuilderView's FrameColorOf/ArtFallbackOf so the
    // new page's grid, deck strip and detail read one source (parity with the old
    // view). category(Subconscious) 우선 > type(Unit=금) > Normal/Squad(파랑).
    public static class CardCategoryStyle
    {
        private static readonly Color NormalFrame = new Color(0.14f, 0.17f, 0.28f, 1f);
        private static readonly Color UniqueFrame = new Color(0.42f, 0.30f, 0.08f, 1f);
        private static readonly Color SubconsciousFrame = new Color(0.34f, 0.18f, 0.48f, 1f);
        private static readonly Color ArtFallbackNormal = new Color(0.22f, 0.28f, 0.44f, 1f);
        private static readonly Color ArtFallbackUnique = new Color(0.55f, 0.40f, 0.14f, 1f);
        private static readonly Color ArtFallbackSubconscious = new Color(0.46f, 0.28f, 0.62f, 1f);

        private static bool IsSubconscious(DreamcatcherCard c) => c != null && c.category == CardCategory.Subconscious;

        public static Color Frame(DreamcatcherCard c)
            => IsSubconscious(c) ? SubconsciousFrame : (c != null && c.type == CardType.Unit ? UniqueFrame : NormalFrame);

        public static Color ArtFallback(DreamcatcherCard c)
            => IsSubconscious(c) ? ArtFallbackSubconscious : (c != null && c.type == CardType.Unit ? ArtFallbackUnique : ArtFallbackNormal);

        public static string Label(DreamcatcherCard c)
        {
            if (c == null) return "";
            if (IsSubconscious(c)) return "무의식";
            switch (c.type)
            {
                case CardType.Unit: return "유닛 부착";
                case CardType.Active: return "액티브";
                default: return "스쿼드 버프";
            }
        }

        // dreamcatcher-hand-card-face unit 0 — 인게임 손패 카드 면(헤더 밴드형)의 단일
        // 스타일 소스. BG 헤더 색 = 타입(사용 문법), 테두리 = 무의식 여부(리스크),
        // 태그 칩 = 적용 대상+역할. Active 청록은 신규 배정 — 보라는 무의식 테두리와
        // 각성 게이지가 이미 점유라 회피.
        private static readonly Color HandHeaderSquad = new Color(0.20f, 0.32f, 0.55f, 1f);
        private static readonly Color HandHeaderUnit = new Color(0.55f, 0.40f, 0.14f, 1f);
        private static readonly Color HandHeaderActive = new Color(0.13f, 0.42f, 0.42f, 1f);
        // 본문은 트레이 배킹(짙은 네이비)과 톤이 겹치면 "투명해 보인다"(2026-07-25 사용자
        // 피드백) — 배킹보다 한 단계 밝은 solid 슬레이트로 카드가 독립 판으로 읽히게.
        private static readonly Color HandBodyFill = new Color(0.16f, 0.15f, 0.27f, 1f);
        private static readonly Color HandBorderNeutral = new Color(0.05f, 0.04f, 0.10f, 1f);

        public static Color HandHeader(CardType type)
        {
            switch (type)
            {
                case CardType.Unit: return HandHeaderUnit;
                case CardType.Active: return HandHeaderActive;
                default: return HandHeaderSquad;
            }
        }

        public static Color HandBody() => HandBodyFill;

        public static Color HandBorder(DreamcatcherCard c)
            => IsSubconscious(c) ? SubconsciousFrame : HandBorderNeutral;

        // 태그 칩 문안: 타입 색은 배워야 하는 무명 코드라, 칩이 대상+역할을 글자로 해설한다.
        // Squad = 축 라벨 + " 버프", Unit = 부착 제스처, Active = 스킬 타겟 유형.
        public static string TargetTag(DreamcatcherCard c)
        {
            if (c == null) return "";
            switch (c.type)
            {
                // 적 지정(BountyMark)은 전용 필드가 없고 mechanics 파생 — 조준 라우팅과
                // 같은 판별(DreamcatcherCard.HasBountyMark)로 칩 문안을 가른다.
                case CardType.Unit: return c.HasBountyMark() ? "적 지정" : "아군 부착";
                case CardType.Active: return ActiveTargetTag(c.skill);
                default: return DreamcatcherCardText.AxisLabel(c.axis) + " 버프";
            }
        }

        private static string ActiveTargetTag(SkillData skill)
        {
            if (skill == null) return "필드"; // 미배선 config — 폴백(카드는 여전히 사용 가능)
            switch (skill.effect)
            {
                // active-dreamcatcher-tile-aim unit 0 — Active 는 전부 타일 대상(대상축 폐기).
                // 아군 버프도 "아군 지정" 이 아니라 타일 반경이다.
                case SkillEffectType.Portal: return "타일 2개";
                case SkillEffectType.Meteor:
                case SkillEffectType.SlowField:
                case SkillEffectType.Tornado:
                case SkillEffectType.PowerSurge:
                case SkillEffectType.RapidFire: return "타일 지정";
                default: return "필드";
            }
        }
    }
}
