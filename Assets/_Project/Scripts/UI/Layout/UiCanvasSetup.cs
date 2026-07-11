using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI.Layout
{
    public readonly struct UiCanvasRoots
    {
        public UiCanvasRoots(Canvas canvas, RectTransform fullBleedRoot, RectTransform safeAreaRoot)
        {
            Canvas = canvas;
            FullBleedRoot = fullBleedRoot;
            SafeAreaRoot = safeAreaRoot;
        }

        public Canvas Canvas { get; }
        public RectTransform FullBleedRoot { get; }
        public RectTransform SafeAreaRoot { get; }
    }

    public static class UiCanvasSetup
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private const string FullBleedRootName = "FullBleedRoot";
        private const string SafeAreaRootName = "SafeAreaRoot";

        public static UiCanvasRoots Ensure(GameObject host, int sortingOrder)
        {
            var canvas = host.GetComponent<Canvas>();
            if (canvas == null) canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = host.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            if (host.GetComponent<GraphicRaycaster>() == null)
                host.AddComponent<GraphicRaycaster>();

            var fullBleedRoot = EnsureRoot(host.transform, FullBleedRootName);
            StretchToParent(fullBleedRoot);

            var safeAreaRoot = EnsureRoot(host.transform, SafeAreaRootName);
            StretchToParent(safeAreaRoot);
            var safeAreaFitter = safeAreaRoot.GetComponent<UiSafeAreaFitter>();
            if (safeAreaFitter == null)
                safeAreaFitter = safeAreaRoot.gameObject.AddComponent<UiSafeAreaFitter>();
            else
                safeAreaFitter.RefreshNow();

            return new UiCanvasRoots(canvas, fullBleedRoot, safeAreaRoot);
        }

        private static RectTransform EnsureRoot(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;

            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            return (RectTransform)root.transform;
        }

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }
    }
}
