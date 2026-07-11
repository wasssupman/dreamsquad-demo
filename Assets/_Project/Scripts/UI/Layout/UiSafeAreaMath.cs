using UnityEngine;

namespace Wassup.UI.Layout
{
    public readonly struct UiSafeAreaAnchors
    {
        public static readonly UiSafeAreaAnchors FullScreen =
            new UiSafeAreaAnchors(Vector2.zero, Vector2.one);

        public UiSafeAreaAnchors(Vector2 anchorMin, Vector2 anchorMax)
        {
            AnchorMin = anchorMin;
            AnchorMax = anchorMax;
        }

        public Vector2 AnchorMin { get; }
        public Vector2 AnchorMax { get; }
    }

    public static class UiSafeAreaMath
    {
        public static UiSafeAreaAnchors ToAnchors(Rect safeArea, Vector2 screenSize)
        {
            if (!IsPositiveFinite(screenSize.x) || !IsPositiveFinite(screenSize.y) ||
                !IsFinite(safeArea.x) || !IsFinite(safeArea.y) ||
                !IsPositiveFinite(safeArea.width) || !IsPositiveFinite(safeArea.height))
            {
                return UiSafeAreaAnchors.FullScreen;
            }

            float minX = Mathf.Clamp(safeArea.xMin, 0f, screenSize.x);
            float minY = Mathf.Clamp(safeArea.yMin, 0f, screenSize.y);
            float maxX = Mathf.Clamp(safeArea.xMax, 0f, screenSize.x);
            float maxY = Mathf.Clamp(safeArea.yMax, 0f, screenSize.y);

            if (maxX <= minX || maxY <= minY)
                return UiSafeAreaAnchors.FullScreen;

            return new UiSafeAreaAnchors(
                new Vector2(minX / screenSize.x, minY / screenSize.y),
                new Vector2(maxX / screenSize.x, maxY / screenSize.y));
        }

        private static bool IsPositiveFinite(float value) => value > 0f && IsFinite(value);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
