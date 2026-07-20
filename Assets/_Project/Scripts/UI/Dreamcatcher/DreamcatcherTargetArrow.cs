using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // dreamcatcher-awakening-hand rev 4-6 — StS-style targeting arrow: the card
    // stays seated in the hand and a dotted bezier arc runs from it to the
    // pointer. Runtime-built, asset-free, all raycastTarget=false.
    //
    // dreamcatcher-attach-lockon — 파스텔 3-상태(기본/부착가능/부착불가) + 삼각 화살촉.
    // 색·크기·아웃라인·끝점 블렌드는 SO(DreamcatcherFocusConfig)를 **매 SetPath 마다
    // 직접 읽어** 라이브 반영한다(Configure 는 참조만 저장 — 스냅샷 아님).
    public class DreamcatcherTargetArrow : MonoBehaviour
    {
        public enum ArrowState { None, Valid, Invalid }

        private const int DotCount = 14;
        private const float DotMinSize = 11f;
        private const float DotMaxSize = 22f;

        private readonly List<RectTransform> _dots = new List<RectTransform>();
        private readonly List<Image> _dotImages = new List<Image>();
        private readonly List<RectTransform> _outlineDots = new List<RectTransform>();
        private readonly List<Image> _outlineImages = new List<Image>();
        private RectTransform _head, _outlineHead;
        private Image _headImage, _outlineHeadImage;
        private Sprite _headSprite;

        // SO 노브(참조 — 매 SetPath 라이브 읽기). 미할당 시 기본값 폴백.
        private DreamcatcherFocusConfig _cfg;
        private float MinAlpha => _cfg != null ? _cfg.arrowMinAlpha : 0.7f;
        private float LockBlend => _cfg != null ? _cfg.arrowLockBlend : 0.7f;
        private float HeadSize => _cfg != null ? _cfg.arrowHeadSize : 54f;
        private float OutlinePad => _cfg != null ? _cfg.arrowOutlinePad : 3f;
        private Color OutlineColor => _cfg != null ? _cfg.arrowOutlineColor : new Color(0.05f, 0.05f, 0.08f, 0.85f);
        private Color NeutralCol => _cfg != null ? _cfg.arrowNeutralColor : new Color(1f, 1f, 1f, 0.7f);
        private Color ValidCol => _cfg != null ? _cfg.arrowValidColor : new Color(0.42f, 0.95f, 1f, 1f);
        private Color InvalidCol => _cfg != null ? _cfg.arrowInvalidColor : new Color(1f, 0.42f, 0.46f, 1f);

        public static DreamcatcherTargetArrow Create(Transform canvasRoot)
        {
            var go = new GameObject("TargetArrow", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var arrow = go.AddComponent<DreamcatcherTargetArrow>();
            arrow.Build();
            go.SetActive(false);
            return arrow;
        }

        // 참조만 저장 — SetPath 가 매 호출 읽으므로 SO 에셋 편집이 Play 중 즉시 반영.
        public void Configure(DreamcatcherFocusConfig cfg) => _cfg = cfg;

        private void Build()
        {
            _headSprite = MakeArrowHead();
            // Outline layer first (renders behind the main dashes).
            for (int i = 0; i < DotCount; i++)
            {
                var o = NewQuad($"Outline_{i}", out var oi);
                _outlineDots.Add(o);
                _outlineImages.Add(oi);
            }
            _outlineHead = NewQuad("OutlineHead", out _outlineHeadImage);
            _outlineHeadImage.sprite = _headSprite;
            _outlineHead.pivot = new Vector2(1f, 0.5f); // 피봇=화살촉 끝(뾰족한 곳이 endScreen)
            for (int i = 0; i < DotCount; i++)
            {
                var dot = NewQuad($"Dot_{i}", out var img);
                _dots.Add(dot);
                _dotImages.Add(img);
            }
            _head = NewQuad("Head", out _headImage);
            _headImage.sprite = _headSprite;
            _head.pivot = new Vector2(1f, 0.5f);
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

        // Quadratic bezier start→end with an upward lift; dashes rotate along the
        // tangent, triangular head at the end point. state = 색(기본/가능/불가).
        // lockCenter(대상 락온) 지정 시 끝점을 대상 중심으로 blend.
        public void SetPath(Vector2 startScreen, Vector2 endScreen, ArrowState state,
            Vector2? lockCenter = null)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (lockCenter.HasValue)
                endScreen = Vector2.Lerp(endScreen, lockCenter.Value, LockBlend);

            float dist = Vector2.Distance(startScreen, endScreen);
            Vector2 control = (startScreen + endScreen) * 0.5f
                              + Vector2.up * Mathf.Clamp(dist * 0.35f, 60f, 240f);
            Color color = state == ArrowState.Valid ? ValidCol
                        : state == ArrowState.Invalid ? InvalidCol : NeutralCol;
            Color outline = OutlineColor;
            float minAlpha = MinAlpha;
            float outlinePad = OutlinePad;

            for (int i = 0; i < _dots.Count; i++)
            {
                float t = (i + 1f) / (_dots.Count + 1f);
                Vector2 pos = Bezier(startScreen, control, endScreen, t);
                Vector2 tangent = BezierTangent(startScreen, control, endScreen, t);
                float size = Mathf.Lerp(DotMinSize, DotMaxSize, t);
                float rot = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                float a = color.a * Mathf.Lerp(minAlpha, 1f, t);

                var dot = _dots[i];
                dot.position = pos;
                dot.sizeDelta = new Vector2(size, size * 0.62f);
                dot.localEulerAngles = new Vector3(0f, 0f, rot);
                _dotImages[i].color = new Color(color.r, color.g, color.b, a);

                var od = _outlineDots[i];
                od.position = pos;
                od.sizeDelta = new Vector2(size + outlinePad, size * 0.62f + outlinePad);
                od.localEulerAngles = new Vector3(0f, 0f, rot);
                _outlineImages[i].color = new Color(outline.r, outline.g, outline.b,
                    outline.a * Mathf.Lerp(minAlpha, 1f, t));
            }

            float headSize = HeadSize;
            Vector2 headTangent = BezierTangent(startScreen, control, endScreen, 1f);
            float headRot = Mathf.Atan2(headTangent.y, headTangent.x) * Mathf.Rad2Deg;

            _outlineHead.position = endScreen;
            _outlineHead.sizeDelta = new Vector2(headSize + outlinePad * 2f, headSize + outlinePad * 2f);
            _outlineHead.localEulerAngles = new Vector3(0f, 0f, headRot);
            _outlineHeadImage.color = outline;

            _head.position = endScreen;
            _head.sizeDelta = new Vector2(headSize, headSize);
            _head.localEulerAngles = new Vector3(0f, 0f, headRot);
            _headImage.color = color;
        }

        // 오른쪽(+X)을 향한 삼각 화살촉을 절차적으로 굽는다(백=왼쪽 세로변, 뾰족=오른쪽
        // 중앙). 렉트 피봇을 (1,0.5)로 두면 뾰족한 끝이 endScreen 에 놓이고 tangent 로 회전.
        private static Sprite MakeArrowHead()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[N * N];
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float u = x / (float)(N - 1);       // 0(왼쪽 백) → 1(오른쪽 끝)
                    float v = Mathf.Abs(y - c) / c;     // 0(중앙) → 1(위/아래 끝)
                    float edge = (1f - v) - u;          // >=0 이면 삼각형 안
                    float aa = Mathf.Clamp01(edge * (N * 0.5f) + 0.5f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(aa * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
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
