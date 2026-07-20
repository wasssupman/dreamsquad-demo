using UnityEngine;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-deck-page unit 1 — shared card frame/fallback color + label,
    // lifted from DreamcatcherDeckBuilderView's FrameColorOf/ArtFallbackOf so the
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
    }
}
