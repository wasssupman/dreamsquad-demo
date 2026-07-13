using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Wassup.UI
{
    // card-crumple-unfold unit 0 — 손패 카드의 art 자식을 N×N 격자로 테셀레이트하는
    // UGUI Graphic. 실제 구김 변위는 unit 1(UGUI 버텍스 셰이더)에서 이 촘촘한 메시
    // 위에 얹는다. 이 단계는 "서브디바이드된 평면 메시 + off 폴백" 인프라만 제공한다.
    //
    // rest 무회귀: 평면 격자는 일반 Image 와 픽셀 동일(선형 UV + preserveAspect 재현).
    // 폴백: subdivisions == 1 → base Image 경로 그대로(원본 쿼드). D1 = art-only 라서
    // root frame Image(드래그 raycast · CanvasGroup dim)는 건드리지 않는다.
    public class UiCardFaceMesh : Image
    {
        [SerializeField, Range(1, 24)] private int subdivisions = 8;

        // off 폴백 / 실기 튜닝용. 1 = 원본 쿼드(효과 없음).
        public int Subdivisions
        {
            get => subdivisions;
            set
            {
                int v = Mathf.Clamp(value, 1, 24);
                if (v == subdivisions) return;
                subdivisions = v;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            int n = Mathf.Max(1, subdivisions);
            if (n == 1) { base.OnPopulateMesh(vh); return; } // 폴백: 원본 Image 경로

            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            var s = overrideSprite;
            if (preserveAspect && s != null)
                PreserveAspect(ref r, new Vector2(s.rect.width, s.rect.height));
            Vector4 uv = s != null ? DataUtility.GetOuterUV(s) : new Vector4(0f, 0f, 1f, 1f);
            Color32 col = color;

            int stride = n + 1;
            for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                float fx = (float)x / n, fy = (float)y / n;
                var pos = new Vector3(Mathf.Lerp(r.xMin, r.xMax, fx), Mathf.Lerp(r.yMin, r.yMax, fy), 0f);
                var vuv = new Vector2(Mathf.Lerp(uv.x, uv.z, fx), Mathf.Lerp(uv.y, uv.w, fy));
                vh.AddVert(pos, col, vuv);
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i0 = y * stride + x, i1 = i0 + 1, i2 = i0 + stride, i3 = i2 + 1;
                vh.AddTriangle(i0, i2, i1);
                vh.AddTriangle(i1, i2, i3);
            }
        }

        // Image.PreserveSpriteAspectRatio 재현(private 라 직접 못 씀).
        private void PreserveAspect(ref Rect rect, Vector2 spriteSize)
        {
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;
            float spriteRatio = spriteSize.x / spriteSize.y;
            float rectRatio = rect.width / rect.height;
            var pivot = rectTransform.pivot;
            if (spriteRatio > rectRatio)
            {
                float oldHeight = rect.height;
                rect.height = rect.width * (1f / spriteRatio);
                rect.y += (oldHeight - rect.height) * pivot.y;
            }
            else
            {
                float oldWidth = rect.width;
                rect.width = rect.height * spriteRatio;
                rect.x += (oldWidth - rect.width) * pivot.x;
            }
        }
    }
}
