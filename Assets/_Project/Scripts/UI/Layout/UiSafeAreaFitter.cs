using UnityEngine;

namespace Wassup.UI.Layout
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private Vector2 _lastScreenSize;
        private bool _hasApplied;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
        }

        private void OnEnable()
        {
            RefreshIfChanged(force: true);
        }

        private void Update()
        {
            RefreshIfChanged(force: false);
        }

        public void RefreshNow()
        {
            RefreshIfChanged(force: true);
        }

        private void RefreshIfChanged(bool force)
        {
            var screenSize = new Vector2(Screen.width, Screen.height);
            var safeArea = Screen.safeArea;
            if (!force && _hasApplied && screenSize == _lastScreenSize && safeArea == _lastSafeArea)
                return;

            if (_rectTransform == null)
                _rectTransform = (RectTransform)transform;

            var anchors = UiSafeAreaMath.ToAnchors(safeArea, screenSize);
            _rectTransform.anchorMin = anchors.AnchorMin;
            _rectTransform.anchorMax = anchors.AnchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
            _rectTransform.localScale = Vector3.one;

            _lastScreenSize = screenSize;
            _lastSafeArea = safeArea;
            _hasApplied = true;
        }
    }
}
