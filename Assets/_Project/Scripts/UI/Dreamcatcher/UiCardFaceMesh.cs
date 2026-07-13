using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Wassup.UI
{
    // card-crumple-unfold unit 0~1 — 손패 카드 art 자식을 N×N 격자로 테셀레이트하고,
    // 각 버텍스에 "구겨진 오프셋(uv1)+크리스 깊이(uv2)"를 정적으로 굽는다. 셰이더
    // (Wassup/UI/CardCrumple)가 per-instance `_Unfold` 로 flat↔crumpled 를 보간한다.
    //
    // 전송 계약(unit 0.5 spike): 메시는 정적(rebuild=layout 시만), `_Unfold` 만 per-instance
    // 머티리얼 float → 매프레임 CPU 0. Overlay 라 변위는 XY(+가짜 AO), Z inert.
    // rest 무회귀: `_Unfold=1` → 변위/AO 0 → 일반 스프라이트와 픽셀 동일.
    // 폴백: Subdivisions==1 → base Image 경로(구김 불가). D1 = art-only.
    public class UiCardFaceMesh : Image
    {
        [SerializeField, Range(1, 24)] private int subdivisions = 16;
        [Header("Crumple bake (paper-ball noise)")]
        [SerializeField] private float crumpleAmplitude = 18f; // px, 구김 세기
        [SerializeField] private float crumpleFrequency = 4.5f;
        [SerializeField, Range(1, 5)] private int crumpleOctaves = 4;
        [SerializeField] private float creaseSharp = 2f;       // 크리스 라인 날카로움
        [SerializeField] private float creaseAO = 0.55f;       // 프래그먼트 그림자 세기

        private static readonly Vector3 Normal = new Vector3(0f, 0f, -1f);
        private static readonly Vector4 Tangent = new Vector4(1f, 0f, 0f, -1f);

        private Material _matInstance;
        private float _unfold = 1f;

        // unit 2 딜이 per-card 로 구동. 1 = 평면(rest), 0 = 완전히 구겨짐.
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
            if (sh == null) return; // 폴백: 기본 UI 머티리얼(구김 없음)
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

            int stride = n + 1;
            for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                float fx = (float)x / n, fy = (float)y / n;
                var pos = new Vector3(Mathf.Lerp(r.xMin, r.xMax, fx), Mathf.Lerp(r.yMin, r.yMax, fy), 0f);
                var uv0 = new Vector4(Mathf.Lerp(uv.x, uv.z, fx), Mathf.Lerp(uv.y, uv.w, fy), 0f, 0f);
                CrumpleAt(fx, fy, out Vector2 offset, out float crease);
                var uv1 = new Vector4(offset.x, offset.y, 0f, 0f);
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

        // 정규화 격자 좌표 (fx,fy)∈[0,1] 에서 구김 오프셋(px)+크리스 깊이(0..1)를 베이크.
        // 종이-볼(D2): 저주파 fbm 벡터장으로 벌크 변위, 고주파 ridged 로 크리스.
        private void CrumpleAt(float fx, float fy, out Vector2 offset, out float crease)
        {
            float u = fx * crumpleFrequency, v = fy * crumpleFrequency;
            float dx = Fbm(u, v, crumpleOctaves) - 0.5f;
            float dy = Fbm(u + 41.3f, v + 17.9f, crumpleOctaves) - 0.5f;
            // 가장자리는 프레임에 붙어 있게 살짝 감쇠(중앙일수록 크게 구김).
            float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
            float falloff = Mathf.SmoothStep(0f, 0.18f, edge);
            offset = new Vector2(dx, dy) * (2f * crumpleAmplitude) * falloff;
            // 크리스 = 변위장 능선(sn≈0 인 얇은 선)에만 그림자 → 넓은 blackout 방지.
            float sn = Fbm(u * 1.7f, v * 1.7f, crumpleOctaves) - 0.5f;
            crease = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(sn) * 14f), Mathf.Max(0.5f, creaseSharp)) * falloff;
        }

        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < octaves; i++) { sum += amp * ValueNoise(x * freq, y * freq); freq *= 2f; amp *= 0.5f; }
            return sum;
        }

        private static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float ux = xf * xf * (3f - 2f * xf), uy = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        private static float Hash(int x, int y)
        {
            float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
            return h - Mathf.Floor(h);
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
