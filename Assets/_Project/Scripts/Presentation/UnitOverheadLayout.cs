using UnityEngine;

namespace Wassup.Presentation
{
    public static class UnitOverheadLayout
    {
        public static float ReferenceScale(float screenHeight, float referenceHeight)
            => IsFinitePositive(screenHeight) && IsFinitePositive(referenceHeight)
                ? screenHeight / referenceHeight : 1f;

        public static float BarWidth(float tileWidthReference, float fraction, float min, float max)
        {
            float safeMin = IsFinite(min) ? Mathf.Max(0f, min) : 0f;
            float safeMax = IsFinite(max) ? Mathf.Max(safeMin, max) : safeMin;
            float safeTile = IsFinite(tileWidthReference) ? Mathf.Max(0f, tileWidthReference) : 0f;
            float safeFraction = IsFinite(fraction) ? Mathf.Clamp01(fraction) : 0f;
            return Mathf.Clamp(safeTile * safeFraction, safeMin, safeMax);
        }

        // x=bar bottom, y=card row bottom. 1920x1080 reference pixel 계약의 단일 계산점.
        public static Vector2 VerticalOffsets(float headGap, float barHeight, float cardGap)
        {
            float head = NonNegative(headGap);
            return new Vector2(head, head + NonNegative(barHeight) + NonNegative(cardGap));
        }

        // 확장(unit 6) — 스택 아이콘 행 bottom. 드림캐쳐 행 위(카드행 bottom + 카드 높이 + gap).
        // cardRowHeight 0(카드 없음/적)이면 스택행이 카드행 자리(바 위 gap)로 내려온다.
        public static float StackRowBottom(float cardRowBottom, float cardRowHeight, float stackGap)
            => NonNegative(cardRowBottom) + NonNegative(cardRowHeight) + NonNegative(stackGap);

        public static Vector2 ScreenAnchor(float visualPivotX, Rect rendererRect)
        {
            float x = IsFinite(visualPivotX) ? visualPivotX : rendererRect.center.x;
            float y = IsFinite(rendererRect.yMax) ? rendererRect.yMax : 0f;
            return new Vector2(x, y);
        }

        public static float CardSpacing(float desiredSpacing, int count, float maxRowWidth)
        {
            int n = Mathf.Clamp(count, 0, 3);
            if (n <= 1) return 0f;
            float cap = NonNegative(maxRowWidth);
            return Mathf.Min(NonNegative(desiredSpacing), cap / (n - 1));
        }

        // 최대 3장. 2:3 세로 카드 비율을 유지하며 한 타일 폭 안으로 축소한다.
        public static Vector2 CardSize(float desiredHeight, float spacing, int count, float maxRowWidth)
        {
            int n = Mathf.Clamp(count, 0, 3);
            if (n == 0) return Vector2.zero;
            float h = IsFinitePositive(desiredHeight) ? desiredHeight : 1f;
            float gap = NonNegative(spacing);
            float w = h * (2f / 3f);
            float row = w * n + gap * (n - 1);
            float cap = NonNegative(maxRowWidth);
            if (row > cap)
            {
                float available = Mathf.Max(0f, cap - gap * (n - 1));
                w = available / n;
                h = w * 1.5f;
            }
            return new Vector2(w, h);
        }

        private static float NonNegative(float value) => IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        private static bool IsFinitePositive(float value) => IsFinite(value) && value > 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
