using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;
using Wassup.UI.Layout;

namespace Wassup.UI
{
    // outgame-tutorial unit 1 — the blocking layer. Dim pieces tile everything
    // except the focused rects and carry raycastTarget, so the lobby is inert;
    // the holes have no graphic at all, which lets the raycast fall through to
    // MenuCanvas (order 0) and the focused button is pressed for real.
    //
    // Must live on its own GameObject: TutorialGuidanceView calls
    // UiCanvasSetup.Ensure(gameObject, 10) on itself, so sharing a GameObject
    // would make the two views fight over one Canvas and its sortingOrder.
    public sealed class OutgameTutorialOverlay : MonoBehaviour
    {
        private const int SortingOrder = 9;

        // Keep below half the smallest gap between two focused buttons, or the
        // padded holes merge and the dim strip between them disappears. The lobby
        // column leaves 24px between Squad and Dreamcatcher, so 12 is the ceiling.
        [SerializeField] private float holePadding = 6f;
        [SerializeField] private float fadeSeconds = 0.22f;

        public event Action Tapped;

        private RectTransform _fullBleedRoot;
        private GameObject _root;
        private CanvasGroup _group;
        private bool _built;
        private Tween _fade;
        private Rect _areaRect;

        private readonly List<Image> _pieces = new List<Image>();
        private readonly List<Rect> _holeRects = new List<Rect>();
        private readonly List<Rect> _pieceRects = new List<Rect>();
        private readonly List<RectTransform> _targets = new List<RectTransform>();
        private readonly List<Vector3> _cornerCache = new List<Vector3>();
        private readonly Vector3[] _corners = new Vector3[4];

        // No Awake(): building lazily and never calling Hide() from Awake is
        // deliberate. TutorialGuidanceView.Awake() runs an unconditional Hide(),
        // so a controller that shows first and gets the view's Awake second has
        // its message silently switched off. This view must not repeat that.
        private void OnDestroy()
        {
            _fade.Stop();
            Tapped = null;
        }

        public void Show()
        {
            EnsureBuilt();
            _root.SetActive(true);
            _fade.Stop();
            _group.alpha = 0f;
            _fade = Tween.Alpha(_group, 1f, fadeSeconds, Ease.OutQuad);
        }

        public void Hide()
        {
            if (!_built) return;
            _fade.Stop();
            _targets.Clear();
            _cornerCache.Clear();
            _holeRects.Clear();
            ReleasePieces();
            _root.SetActive(false);
        }

        /// null 또는 빈 목록이면 구멍 없는 풀 dim.
        public void SetHoles(IReadOnlyList<RectTransform> targets)
        {
            EnsureBuilt();
            _targets.Clear();
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    RectTransform target = targets[i];
                    // menuRoot is toggled by ApplyAuthGate/RaiseExclusive, so a
                    // focus target can legitimately be inactive here.
                    if (target == null || !target.gameObject.activeInHierarchy) continue;
                    _targets.Add(target);
                }
            }

            // The first layout of a freshly loaded scene has not been flushed yet,
            // and GetWorldCorners would report stale rects.
            Canvas.ForceUpdateCanvases();
            _cornerCache.Clear();
            Rebuild();
        }

        /// 포커스 링용 합집합 RectTransform 을 매달 부모. 좌표계는 홀 rect 와 같다.
        public RectTransform EnsureHostRoot()
        {
            EnsureBuilt();
            return _fullBleedRoot;
        }

        /// 마지막 SetHoles 로 계산된 홀들의 합집합(오버레이 로컬, padding 제외).
        /// TutorialGuidanceView.FocusUi 가 대상 하나만 받으므로, 버튼 여러 개를
        /// 링 하나로 감싸려면 호출자가 이 값으로 임시 RectTransform 을 만든다.
        public bool TryGetHoleBounds(out Rect bounds)
        {
            bounds = default;
            if (_holeRects.Count == 0) return false;

            bounds = _holeRects[0];
            for (int i = 1; i < _holeRects.Count; i++)
            {
                Rect r = _holeRects[i];
                float xMin = Mathf.Min(bounds.xMin, r.xMin);
                float yMin = Mathf.Min(bounds.yMin, r.yMin);
                float xMax = Mathf.Max(bounds.xMax, r.xMax);
                float yMax = Mathf.Max(bounds.yMax, r.yMax);
                bounds = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            }
            return true;
        }

        private void LateUpdate()
        {
            if (!_built || !_root.activeSelf) return;

            // 화면 크기·safe area 는 모바일에서 시작 직후 몇 프레임에 걸쳐 확정된다.
            // 홀이 없는 단계(풀 dim)는 추종할 대상이 없으므로, 영역 자체의 변화를
            // 따로 감시하지 않으면 조각이 옛 크기로 남아 화면을 덜 덮는다.
            if (_fullBleedRoot.rect != _areaRect)
            {
                Rebuild();
                return;
            }
            if (_targets.Count == 0) return;
            if (!TargetCornersChanged()) return;
            Rebuild();
        }

        private bool TargetCornersChanged()
        {
            int expected = _targets.Count * 4;
            if (_cornerCache.Count != expected) return true;

            int c = 0;
            for (int i = 0; i < _targets.Count; i++)
            {
                RectTransform target = _targets[i];
                if (target == null) return true;
                target.GetWorldCorners(_corners);
                for (int k = 0; k < 4; k++, c++)
                    if (_cornerCache[c] != _corners[k]) return true;
            }
            return false;
        }

        private void Rebuild()
        {
            _areaRect = _fullBleedRoot.rect;
            _holeRects.Clear();
            _cornerCache.Clear();

            for (int i = 0; i < _targets.Count; i++)
            {
                RectTransform target = _targets[i];
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                if (TryLocalRect(target, out Rect local)) _holeRects.Add(local);
            }

            OutgameTutorialDimLayout.Subtract(_areaRect, _holeRects, holePadding, _pieceRects);
            ApplyPieces();
        }

        private bool TryLocalRect(RectTransform target, out Rect result)
        {
            result = default;
            target.GetWorldCorners(_corners);

            // Cache before converting so LateUpdate compares the same source values.
            for (int k = 0; k < 4; k++) _cornerCache.Add(_corners[k]);

            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Camera eventCamera = targetCanvas != null &&
                targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? targetCanvas.worldCamera
                    : null;

            float xMin = float.MaxValue, yMin = float.MaxValue;
            float xMax = float.MinValue, yMax = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCamera, _corners[k]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _fullBleedRoot, screen, null, out Vector2 local))
                    return false;
                if (local.x < xMin) xMin = local.x;
                if (local.y < yMin) yMin = local.y;
                if (local.x > xMax) xMax = local.x;
                if (local.y > yMax) yMax = local.y;
            }

            result = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
            return true;
        }

        private void ApplyPieces()
        {
            for (int i = 0; i < _pieceRects.Count; i++)
            {
                Image piece = i < _pieces.Count ? _pieces[i] : CreatePiece();
                Rect rect = _pieceRects[i];
                var rt = (RectTransform)piece.transform;
                rt.anchoredPosition = rect.center;
                rt.sizeDelta = rect.size;
                piece.gameObject.SetActive(true);
            }
            for (int i = _pieceRects.Count; i < _pieces.Count; i++)
                _pieces[i].gameObject.SetActive(false);
        }

        private void ReleasePieces()
        {
            for (int i = 0; i < _pieces.Count; i++)
                if (_pieces[i] != null) _pieces[i].gameObject.SetActive(false);
            _pieceRects.Clear();
        }

        private Image CreatePiece()
        {
            var go = new GameObject($"Dim{_pieces.Count}", typeof(RectTransform), typeof(Image),
                typeof(OutgameTutorialTapZone));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root.transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var image = go.GetComponent<Image>();
            image.color = UiOverlay.Dim;
            image.raycastTarget = true;

            go.GetComponent<OutgameTutorialTapZone>().Pressed = () => Tapped?.Invoke();

            _pieces.Add(image);
            return image;
        }

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            UiCanvasRoots roots = UiCanvasSetup.Ensure(gameObject, SortingOrder);
            _fullBleedRoot = roots.FullBleedRoot;

            _root = new GameObject("DimRoot", typeof(RectTransform), typeof(CanvasGroup));
            var rt = (RectTransform)_root.transform;
            rt.SetParent(_fullBleedRoot, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _group = _root.GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _root.SetActive(false);
        }
    }
}
