using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Wassup.UI
{
    // card-crumple-unfold — 손패 카드 art 를 N×N 격자로 테셀레이트하고, 각 버텍스에
    // "구겨진 오프셋(uv1)+크리스 깊이(uv2)"를 정적으로 굽는다. 셰이더(Wassup/UI/CardCrumple)가
    // per-instance `_Unfold` 로 crumpled↔flat 을 보간: pos.xy += crumple*(1-_Unfold).
    //
    // 주의(root-cause): uv1/uv2 는 Canvas.additionalShaderChannels(TexCoord1|TexCoord2)를
    // 켜야 셰이더에 도달한다(DreamcatcherHandView.BuildCanvas). 안 켜면 스트립돼 변위 0.
    // 전송(unit 0.5): 메시 정적(rebuild=layout), `_Unfold` 만 머티리얼 float → 매프레임 CPU 0.
    // rest 무회귀: `_Unfold=1` → 변위/AO 0 → 일반 스프라이트와 픽셀 동일.
    // 폴백: Subdivisions==1 → base Image 경로. D1 = art-only(frame/드래그/CanvasGroup 무변).
    public class UiCardFaceMesh : Image
    {
        [SerializeField, Range(1, 32)] private int subdivisions = 28; // 높을수록 크리스 샤프
        [Header("Crumple bake (cellular = 각진 패싯)")]
        [SerializeField] private float crumpleAmplitude = 14f; // px, 셀 패싯 어긋남(낮추면 warp↓ 각짐↑)
        [SerializeField] private float crumpleFrequency = 6f;  // 셀 밀도(높을수록 면 많고 잘게)
        [SerializeField] private float creaseWidth = 14f;       // 클수록 얇고 샤프한 접힘선
        [SerializeField] private float creaseSharp = 2.5f;      // 클수록 날카로운 라인
        [SerializeField] private float creaseAO = 1.1f;         // 접힘선 그림자 세기(진하게)
        [SerializeField] private float facetShadeStrength = 1.1f; // 면별 밝기 차등(로우폴리 종이감)

        private static readonly Vector3 Normal = new Vector3(0f, 0f, -1f);
        private static readonly Vector4 Tangent = new Vector4(1f, 0f, 0f, -1f);

        private Material _matInstance;
        private float _unfold = 1f;

        // 딜이 per-card 로 구동. 1 = 평면(rest), 0 = 완전히 구겨짐.
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
                int v = Mathf.Clamp(value, 1, 32);
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
                CrumpleAt(fx, fy, out Vector2 offset, out float crease, out float facetShade);
                var uv1 = new Vector4(offset.x, offset.y, 0f, 0f);
                var uv2 = new Vector4(crease, facetShade, 0f, 0f);
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

        // 정규화 격자 (fx,fy)∈[0,1] → 셀룰러 패싯 오프셋(px) + 셀 경계 접힘선(0..1).
        // 셀룰러(Voronoi): 각 셀이 평평한 면(facet)으로 어긋나고, 셀 경계에서 인접 셀의 서로 다른
        // 오프셋이 만나 샤프한 접힘선(crease)이 된다 → 매끈 distortion 이 아니라 "각진" 구김.
        private static readonly Vector3 LightDir = new Vector3(-0.42f, 0.62f, 0.66f).normalized;

        private void CrumpleAt(float fx, float fy, out Vector2 offset, out float crease, out float facetShade)
        {
            float u = fx * crumpleFrequency, v = fy * crumpleFrequency;
            Cellular(u, v, out Vector2 dir, out float edgeDist);
            offset = dir * crumpleAmplitude; // 엣지 고정 없음 → 실루엣까지 각지게(단색 카드 가시)
            crease = Mathf.Pow(Mathf.Clamp01(1f - edgeDist * creaseWidth), Mathf.Max(0.5f, creaseSharp));
            // 로우폴리 종이: 면(facet) 기울기 방향(정규화)을 낮은 z 노멀로 → 면마다 밝기 확 갈림.
            var dn = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.right;
            var n = new Vector3(dn.x, dn.y, 0.32f).normalized;
            float sh = Vector3.Dot(n, LightDir); // ≈ [-0.75, 0.75]
            facetShade = Mathf.Clamp(1f + sh * facetShadeStrength, 0.35f, 1.7f);
        }

        // 2D 셀룰러: 가장 가까운 셀의 랜덤 방향(패싯 어긋남) + 셀 경계까지 거리(접힘선).
        private static void Cellular(float px, float py, out Vector2 dir, out float edgeDist)
        {
            int bx = Mathf.FloorToInt(px), by = Mathf.FloorToInt(py);
            float lx = px - bx, ly = py - by;
            float closest = 100f, second = 100f;
            dir = Vector2.zero;
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                int cx = bx + x, cy = by + y;
                var site = new Vector2(x + Hash(cx, cy) - lx, y + Hash(cx * 7 + 13, cy * 3 + 5) - ly);
                float d = site.sqrMagnitude;
                if (d < closest)
                {
                    second = closest; closest = d;
                    float ang = Hash(cx * 3 + 1, cy * 5 + 2) * 6.2831853f;
                    float mag = 0.45f + Hash(cx + 9, cy + 4) * 0.55f; // 셀마다 어긋남 크기 다르게
                    dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * mag;
                }
                else if (d < second) second = d;
            }
            edgeDist = Mathf.Sqrt(second) - Mathf.Sqrt(closest); // 경계 근처 ≈ 0
        }

        // 정수 해시(잘 분포). Mathf.Sin(...)*43758 은 C# float 에서 퇴화(값이 ~0.5 로 뭉침).
        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFFu) / 16777216f; // [0,1)
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
