using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Wassup.UI
{
    // card-crumple-unfold — 손패 카드 art 를 N×N 격자로 테셀레이트하고, 각 버텍스에
    // "접힘 데이터(uv1)+접힘선 그림자(uv2)"를 정적으로 굽는다. 셰이더(Wassup/UI/CardCrumple)가
    // per-instance `_Unfold` 로 folded↔flat 을 보간한다.
    //
    // 효과 = 반으로 접기: 상단 절반이 중앙 접힘선 아래로 접혔다(Unfold=0) 펴진다(Unfold=1).
    // 결정론적이라 확실히 보이고 검증 가능. Overlay 라 실제 3D 회전은 못 쓰고, Y 접힘 +
    // 접힌 반쪽 음영으로 "종이 접힘"을 낸다.
    // rest 무회귀: `_Unfold=1` → 접힘/음영 0 → 일반 스프라이트와 픽셀 동일.
    // 폴백: Subdivisions==1 → base Image 경로. D1 = art-only(frame/드래그/CanvasGroup 무변).
    public class UiCardFaceMesh : Image
    {
        [SerializeField, Range(2, 24)] private int subdivisions = 12; // 짝수 → 접힘선에 버텍스 행
        [SerializeField] private float creaseWidth = 10f;             // 접힘선 그림자 폭
        [SerializeField] private float creaseSharp = 2f;
        [SerializeField] private float creaseAO = 0.65f;              // 프래그먼트 그림자 세기

        private static readonly Vector3 Normal = new Vector3(0f, 0f, -1f);
        private static readonly Vector4 Tangent = new Vector4(1f, 0f, 0f, -1f);

        private Material _matInstance;
        private float _unfold = 1f;

        // 딜이 per-card 로 구동. 1 = 평면(rest), 0 = 반으로 접힘.
        public float Unfold
        {
            get => _unfold;
            set
            {
                float v = Mathf.Clamp01(value);
                if (Mathf.Approximately(v, _unfold)) return;
                _unfold = v;
                if (_matInstance != null) _matInstance.SetFloat("_Unfold", _unfold);
            }
        }

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

        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (_matInstance != null) return;
            var sh = Shader.Find("Wassup/UI/CardCrumple");
            if (sh == null) return; // 폴백: 기본 UI 머티리얼(접힘 없음)
            _matInstance = new Material(sh) { name = "CardCrumpleInst", hideFlags = HideFlags.HideAndDontSave };
            _matInstance.SetFloat("_Unfold", _unfold);
            _matInstance.SetFloat("_CreaseAO", creaseAO);
            material = _matInstance;
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
            float yc = (r.yMin + r.yMax) * 0.5f; // 접힘선(local Y 중앙)

            int stride = n + 1;
            for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                float fx = (float)x / n, fy = (float)y / n;
                float ly = Mathf.Lerp(r.yMin, r.yMax, fy);
                var pos = new Vector3(Mathf.Lerp(r.xMin, r.xMax, fx), ly, 0f);
                var uv0 = new Vector4(Mathf.Lerp(uv.x, uv.z, fx), Mathf.Lerp(uv.y, uv.w, fy), 0f, 0f);
                // 상단 절반(fy>0.5)만 접힘선 위 거리를 실어 접히게. 하단은 고정.
                bool top = fy > 0.5f;
                float foldOffset = top ? (ly - yc) : 0f;
                float foldHalf = top ? 1f : 0f;
                var uv1 = new Vector4(foldOffset, foldHalf, 0f, 0f);
                // 접힘선 그림자: fy≈0.5 능선.
                float crease = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(fy - 0.5f) * creaseWidth), Mathf.Max(0.5f, creaseSharp));
                var uv2 = new Vector4(crease, 0f, 0f, 0f);
                vh.AddVert(pos, col, uv0, uv1, uv2, Vector4.zero, Normal, Tangent);
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

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_matInstance == null) return;
            if (Application.isPlaying) Destroy(_matInstance); else DestroyImmediate(_matInstance);
            _matInstance = null;
        }
    }
}
