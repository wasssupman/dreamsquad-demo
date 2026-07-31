using UnityEngine;

namespace Wassup.UI
{
    /// 여러 화면이 공유하는 **라운드렉트/원** UI 스프라이트를 런타임에 굽는다.
    /// 새 아트 없이 네이비 플레이트·골드 테두리·순위 배지 등을 코드로 만든다.
    /// (score-hud-impact-upgrade 의 사설 MakeRoundedRectSprite 를 공용화 —
    ///  result-screen-visual-upgrade unit 0.)
    ///
    /// 소비처가 하나뿐인 특수 형상(예: START CTA 의 리본 배너 — LobbyNeonCta)은 여기 두지 않고
    /// 그 소비처가 직접 굽는다. 여기 올리는 기준은 "형상이 실제로 재사용되는가"다.
    public static class UiRoundedSprite
    {
        /// 9-slice 라운드렉트 스프라이트. border>0 이면 border px 두께의 테두리 링을
        /// borderColor 로, 안쪽은 fill 로 채운다. Image.Type.Sliced 로 사용.
        public static Sprite Make(float radius, float border, Color fill, Color borderColor)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(radius));
            int b = Mathf.Max(0, Mathf.RoundToInt(border));
            int pad = 2 * b + 8;                 // stretchable center strip (keeps 9-slice valid)
            int size = 2 * r + pad;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            float bx = half - r, by = half - r;  // half-extent of the straight region
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x - half) - bx;
                    float qy = Mathf.Abs(y - half) - by;
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                               Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float sd = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r; // <=0 inside
                    float aa = Mathf.Clamp01(0.5f - sd);                        // edge antialias
                    Color c = (b > 0 && sd > -b) ? borderColor : fill;
                    c.a *= aa;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            float bd = r + b + 1;                // 9-slice border (< size/2 by construction)
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                 100f, 0, SpriteMeshType.FullRect, new Vector4(bd, bd, bd, bd));
        }

        /// 손패 카드 면(헤더 밴드형) 전용 풀렉트 투톤 스프라이트 — dreamcatcher-hand-card-face
        /// unit 1. 상단 headerFrac 비율은 headerFill, 나머지는 bodyFill, 외곽은 borderColor
        /// 링 + 라운드 코너. 9-slice 아님 — UiCardFaceMesh 가 outer UV 를 격자 전체에
        /// 스트레치하므로 대상 RectTransform 과 같은 종횡비로 굽는다(권장 2x 해상도).
        public static Sprite MakeCardFace(int width, int height, float radius, float border,
            Color headerFill, Color bodyFill, Color borderColor, float headerFrac)
        {
            int w = Mathf.Max(8, width), h = Mathf.Max(8, height);
            int r = Mathf.Max(1, Mathf.RoundToInt(radius));
            int b = Mathf.Max(0, Mathf.RoundToInt(border));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[w * h];
            float halfW = (w - 1) * 0.5f, halfH = (h - 1) * 0.5f;
            float bx = halfW - r, by = halfH - r;            // straight-region half extents
            float headerYMin = (h - 1) * (1f - Mathf.Clamp01(headerFrac)); // 텍스처 y = 아래→위
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float qx = Mathf.Abs(x - halfW) - bx;
                    float qy = Mathf.Abs(y - halfH) - by;
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                               Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float sd = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r; // <=0 inside
                    float aa = Mathf.Clamp01(0.5f - sd);
                    Color c = (b > 0 && sd > -b) ? borderColor
                        : (y >= headerYMin ? headerFill : bodyFill);
                    c.a *= aa;
                    px[y * w + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        /// 꽉 찬 원 스프라이트(순위 배지용). border>0 이면 borderColor 링을 두른다.
        /// 고정 크기로 쓰므로 9-slice 불필요 — Image.Type.Simple 로 사용.
        public static Sprite MakeCircle(int diameter, Color fill, float border = 0f, Color borderColor = default)
        {
            int size = Mathf.Max(2, diameter);
            int b = Mathf.Max(0, Mathf.RoundToInt(border));
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var px = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            float rOuter = half;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half));
                    float sd = dist - rOuter;                       // <=0 inside the disc
                    float aa = Mathf.Clamp01(0.5f - sd);            // edge antialias
                    Color c = (b > 0 && sd > -b) ? borderColor : fill;
                    c.a *= aa;
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
