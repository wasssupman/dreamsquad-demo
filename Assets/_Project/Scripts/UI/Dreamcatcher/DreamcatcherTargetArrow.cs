using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // dreamcatcher-awakening-hand rev 4-6 — StS-style targeting arrow for
    // unit-targeting cards: the card stays seated in the hand and a dotted
    // bezier arc runs from it to the pointer, tinted red over a valid unit
    // (matches the spine hover tint). Runtime-built, asset-free (rotated
    // square dashes + a diamond head), all raycastTarget=false.
    public class DreamcatcherTargetArrow : MonoBehaviour
    {
        private const int DotCount = 14;
        private const float DotMinSize = 10f;
        private const float DotMaxSize = 20f;
        private const float HeadSize = 34f;

        private readonly List<RectTransform> _dots = new List<RectTransform>();
        private readonly List<Image> _dotImages = new List<Image>();
        private RectTransform _head;
        private Image _headImage;
        private Color _invalidColor = new Color(1f, 1f, 1f, 0.55f);

        // Parent should be the hand canvas root (ScreenSpaceOverlay) AFTER the
        // hand panel so the arc renders above the cards.
        public static DreamcatcherTargetArrow Create(Transform canvasRoot)
        {
            var go = new GameObject("TargetArrow", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var arrow = go.AddComponent<DreamcatcherTargetArrow>();
            arrow.Build();
            go.SetActive(false);
            return arrow;
        }

        private void Build()
        {
            for (int i = 0; i < DotCount; i++)
            {
                var dot = NewQuad($"Dot_{i}", out var img);
                _dots.Add(dot);
                _dotImages.Add(img);
            }
            _head = NewQuad("Head", out _headImage);
            _head.sizeDelta = new Vector2(HeadSize, HeadSize);
        }

        private RectTransform NewQuad(string name, out Image img)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        // Quadratic bezier start→end with an upward lift; dashes rotate along
        // the tangent (reads as a dotted arc), diamond head at the pointer.
        public void SetPath(Vector2 startScreen, Vector2 endScreen, bool validTarget, Color validColor)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            float dist = Vector2.Distance(startScreen, endScreen);
            Vector2 control = (startScreen + endScreen) * 0.5f
                              + Vector2.up * Mathf.Clamp(dist * 0.35f, 60f, 240f);
            var color = validTarget ? validColor : _invalidColor;

            for (int i = 0; i < _dots.Count; i++)
            {
                float t = (i + 1f) / (_dots.Count + 1f);
                Vector2 pos = Bezier(startScreen, control, endScreen, t);
                Vector2 tangent = BezierTangent(startScreen, control, endScreen, t);
                float size = Mathf.Lerp(DotMinSize, DotMaxSize, t);
                var dot = _dots[i];
                dot.position = pos;
                dot.sizeDelta = new Vector2(size, size * 0.62f); // dash proportions
                dot.localEulerAngles = new Vector3(0f, 0f,
                    Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                var img = _dotImages[i];
                img.color = new Color(color.r, color.g, color.b,
                    color.a * Mathf.Lerp(0.45f, 1f, t)); // tail fades out
            }

            Vector2 headTangent = BezierTangent(startScreen, control, endScreen, 1f);
            _head.position = endScreen;
            // Diamond: square rotated 45° relative to the travel direction.
            _head.localEulerAngles = new Vector3(0f, 0f,
                Mathf.Atan2(headTangent.y, headTangent.x) * Mathf.Rad2Deg + 45f);
            _headImage.color = color;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        private static Vector2 BezierTangent(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            return 2f * (1f - t) * (c - a) + 2f * t * (b - c);
        }
    }
}
