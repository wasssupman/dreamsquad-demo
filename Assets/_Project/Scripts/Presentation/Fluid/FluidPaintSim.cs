using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // fluid-paint-mixing unit 2 — 축소 유체 솔버 구동 MonoBehaviour (순수 View 계층, ECS 무관).
    // 매 프레임 FluidSolver.mat 의 패스들을 Graphics.Blit 로 핑퐁하며 step(dt) 를 돌린다.
    // 색·힘 주입은 Splat(), 살아있는 배경은 자율 앰비언트 드라이버. dye 결과는 DyeTexture(안정 핸들)로 노출.
    // 값·타이밍은 전부 FluidSimConfig 에서 온다(하드코딩 금지).
    public sealed class FluidPaintSim : MonoBehaviour
    {
        [SerializeField] private FluidSimConfig config;
        [Tooltip("FluidSolver.mat (Shader.Find 스트리핑 회피 — 반드시 에셋 참조)")]
        [SerializeField] private Material solverMaterial;
        [Tooltip("RT 종횡비 산출용 기준 크기. 표면 어댑터가 SetSurfaceSize 로 갱신")]
        [SerializeField] private Vector2Int referenceSize = new(512, 512);
        [Tooltip("활성화 시 즉시 뿌리는 씨앗 색 얼룩 수 — 배경이 검게 시작하지 않게(속도 없음). 0=없음")]
        [SerializeField] private int seedSplats = 10;

        // 패스 인덱스 — FluidSolver.shader 의 SubShader 패스 순서와 반드시 일치.
        private const int PassAdvection = 0;
        private const int PassDivergence = 1;
        private const int PassCurl = 2;
        private const int PassVorticity = 3;
        private const int PassPressure = 4;
        private const int PassGradient = 5;
        private const int PassSplat = 6;
        private const int PassClear = 7;
        private const int PassDisplay = 8;

        private static readonly int IdTexelSize = Shader.PropertyToID("_TexelSize");
        private static readonly int IdDyeTexelSize = Shader.PropertyToID("_DyeTexelSize");
        private static readonly int IdVelocity = Shader.PropertyToID("_Velocity");
        private static readonly int IdSource = Shader.PropertyToID("_Source");
        private static readonly int IdCurl = Shader.PropertyToID("_Curl");
        private static readonly int IdCurlStrength = Shader.PropertyToID("_CurlStrength");
        private static readonly int IdPressure = Shader.PropertyToID("_Pressure");
        private static readonly int IdDivergence = Shader.PropertyToID("_Divergence");
        private static readonly int IdTarget = Shader.PropertyToID("_Target");
        private static readonly int IdDt = Shader.PropertyToID("_Dt");
        private static readonly int IdDissipation = Shader.PropertyToID("_Dissipation");
        private static readonly int IdAspectRatio = Shader.PropertyToID("_AspectRatio");
        private static readonly int IdSplatColor = Shader.PropertyToID("_SplatColor");
        private static readonly int IdSplatPoint = Shader.PropertyToID("_SplatPoint");
        private static readonly int IdSplatRadius = Shader.PropertyToID("_SplatRadius");
        private static readonly int IdClearValue = Shader.PropertyToID("_ClearValue");

        private readonly FluidRenderTargets _targets = new();
        private Material _mat;

        // 소비자가 한 번만 잡으면 되는 고정 dye 핸들(내부 핑퐁과 무관).
        public RenderTexture DyeTexture => _targets != null ? _targets.Display : null;
        public bool IsReady => _mat != null && _targets.IsAllocated;

        private void OnEnable()
        {
            if (config == null || solverMaterial == null)
            {
                Debug.LogError($"[FluidPaintSim] config/solverMaterial 미할당 — 비활성화. ({name})", this);
                enabled = false;
                return;
            }
            _mat = new Material(solverMaterial)
            {
                name = "FluidSolver (Instance)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            _targets.Allocate(config, Mathf.Max(1, referenceSize.x), Mathf.Max(1, referenceSize.y));

            // 씨앗 — 첫 프레임부터 색이 있게(검은 화면 방지). 속도 없이 은은한 색 얼룩만 뿌려
            // 이어지는 유동이 밀어 섞게 한다(터지는 임펄스 아님).
            SeedField();
        }

        private void OnDisable()
        {
            _targets.Release();
            if (_mat == null) return;
            if (Application.isPlaying) Destroy(_mat);
            else DestroyImmediate(_mat);
            _mat = null;
        }

        // 표면 크기가 바뀌면(어댑터 리사이즈) 종횡비에 맞춰 재할당. 같은 크기면 무시.
        public void SetSurfaceSize(int width, int height)
        {
            referenceSize = new Vector2Int(Mathf.Max(1, width), Mathf.Max(1, height));
            if (IsReady) _targets.Allocate(config, referenceSize.x, referenceSize.y);
        }

        private void Update()
        {
            if (!IsReady) return;

            // 원본과 동일하게 dt 를 1/60 으로 상한 — 프레임 스파이크가 이류 거리를 폭발시켜
            // 솔버가 발산하는 것을 막는다(저프레임에선 실시간보다 느리게 흐를 뿐).
            float dt = Mathf.Min(Time.deltaTime, 1f / 60f);
            Step(dt);
            EmitFlow();
        }

        private void Step(float dt)
        {
            var t = _targets;
            _mat.SetVector(IdTexelSize, t.SimTexelSize); // 이웃 오프셋·이류 스텝은 sim 텍셀 기준

            // 1. Curl(velocity) → curl
            _mat.SetTexture(IdVelocity, t.VelocityRead);
            Graphics.Blit(t.VelocityRead, t.Curl, _mat, PassCurl);

            // 2. Vorticity confinement(velocity, curl) → velocity ; swap
            _mat.SetTexture(IdVelocity, t.VelocityRead);
            _mat.SetTexture(IdCurl, t.Curl);
            _mat.SetFloat(IdCurlStrength, config.curl);
            _mat.SetFloat(IdDt, dt);
            Graphics.Blit(t.VelocityRead, t.VelocityWrite, _mat, PassVorticity);
            t.SwapVelocity();

            // 3. Divergence(velocity) → divergence
            _mat.SetTexture(IdVelocity, t.VelocityRead);
            Graphics.Blit(t.VelocityRead, t.Divergence, _mat, PassDivergence);

            // 4. Pressure init = pressure × PRESSURE(유지율) ; swap
            _mat.SetTexture(IdTarget, t.PressureRead);
            _mat.SetFloat(IdClearValue, config.pressure);
            Graphics.Blit(t.PressureRead, t.PressureWrite, _mat, PassClear);
            t.SwapPressure();

            // 5. Pressure Jacobi ×N — 비압축성 투영(이게 없으면 소용돌이 대신 번짐)
            _mat.SetTexture(IdDivergence, t.Divergence);
            for (int k = 0; k < config.pressureIterations; k++)
            {
                _mat.SetTexture(IdPressure, t.PressureRead);
                Graphics.Blit(t.PressureRead, t.PressureWrite, _mat, PassPressure);
                t.SwapPressure();
            }

            // 6. Gradient subtract(pressure, velocity) → velocity ; swap
            _mat.SetTexture(IdPressure, t.PressureRead);
            _mat.SetTexture(IdVelocity, t.VelocityRead);
            Graphics.Blit(t.VelocityRead, t.VelocityWrite, _mat, PassGradient);
            t.SwapVelocity();

            // 7. Advect velocity(self) ; swap — 소스=velocity 라 소스 텍셀도 sim
            _mat.SetTexture(IdVelocity, t.VelocityRead);
            _mat.SetTexture(IdSource, t.VelocityRead);
            _mat.SetVector(IdDyeTexelSize, t.SimTexelSize);
            _mat.SetFloat(IdDt, dt);
            _mat.SetFloat(IdDissipation, config.velocityDissipation);
            Graphics.Blit(t.VelocityRead, t.VelocityWrite, _mat, PassAdvection);
            t.SwapVelocity();

            // 8. Advect dye(velocity 로 색을 이동) ; swap — 소스=dye 라 소스 텍셀은 dye
            _mat.SetTexture(IdVelocity, t.VelocityRead);
            _mat.SetTexture(IdSource, t.DyeRead);
            _mat.SetVector(IdDyeTexelSize, t.DyeTexelSize);
            _mat.SetFloat(IdDissipation, config.densityDissipation);
            Graphics.Blit(t.DyeRead, t.DyeWrite, _mat, PassAdvection);
            t.SwapDye();

            // 9. Display: dye 프론트 → 안정 출력 핸들
            _mat.SetTexture(IdSource, t.DyeRead);
            Graphics.Blit(t.DyeRead, t.Display, _mat, PassDisplay);
        }

        // uv(0..1)에 velocity(velocityDelta)와 색(color)을 가우시안 splat 으로 주입. velocity·dye 각각 핑퐁.
        public void Splat(Vector2 uv, Vector2 velocityDelta, Color color)
        {
            if (!IsReady) return;
            var t = _targets;
            float aspect = t.SimResolution.y > 0 ? (float)t.SimResolution.x / t.SimResolution.y : 1f;
            float radius = CorrectRadius(config.splatRadius / 100f, aspect);

            _mat.SetFloat(IdAspectRatio, aspect);
            _mat.SetVector(IdSplatPoint, uv);
            _mat.SetFloat(IdSplatRadius, radius);

            // velocity 주입 (색 채널에 속도 델타를 실어 splat 패스가 base+delta)
            _mat.SetTexture(IdTarget, t.VelocityRead);
            _mat.SetVector(IdSplatColor, new Vector4(velocityDelta.x, velocityDelta.y, 0f, 1f));
            Graphics.Blit(t.VelocityRead, t.VelocityWrite, _mat, PassSplat);
            t.SwapVelocity();

            // dye(색) 주입
            _mat.SetTexture(IdTarget, t.DyeRead);
            _mat.SetVector(IdSplatColor, new Vector4(color.r, color.g, color.b, 1f));
            Graphics.Blit(t.DyeRead, t.DyeWrite, _mat, PassSplat);
            t.SwapDye();
        }

        // 원본 correctRadius — 가로가 긴 화면에서 splat 이 세로로 눌리지 않게 aspect 보정.
        private static float CorrectRadius(float radius, float aspect) => aspect > 1f ? radius * aspect : radius;

        // 연속 유동: 방출기 몇 개가 부드럽게 배회하며 매 프레임 은은한 색·힘을 흘려 넣는다.
        // 무작위 위치·강한 임펄스(방울 터짐)가 아니라, 코히런트한 방향으로 이어지는 리본 → "흐르는 액체".
        private void EmitFlow()
        {
            int n = config.ambientEmitters;
            if (n <= 0) return;
            float t = Time.time;
            for (int e = 0; e < n; e++)
            {
                // 위상이 다른 사인 합으로 화면 안쪽을 부드럽게 배회(가장자리 붙박이 방지).
                float px = 0.5f + 0.30f * Mathf.Sin(t * config.ambientDrift * (0.70f + 0.13f * e) + e * 2.1f);
                float py = 0.5f + 0.30f * Mathf.Sin(t * config.ambientDrift * (0.53f + 0.11f * e) * 1.3f + e * 4.2f);
                // 진행 방향도 서서히 회전 → 흐름이 감기며 컬을 만든다.
                float ang = t * config.ambientDrift * 1.7f + e * 2.0f + Mathf.Sin(t * 0.11f + e) * 1.4f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Splat(new Vector2(px, py), dir * config.ambientFlow, PickFlowColor(e, t));
            }
        }

        // 방출기별로 서서히 순환하는 색(리본마다 다른 색이 흐르며 섞임). per-frame 소량 가산이라 은은히 쌓인다.
        private Color PickFlowColor(int emitter, float t)
        {
            float amt = config.ambientColorAmount;
            var pal = config.palette;
            if (pal != null && pal.Length > 0)
            {
                float f = Mathf.Repeat(t * config.ambientColorCycle + (float)emitter / pal.Length, 1f) * pal.Length;
                int i0 = (int)f % pal.Length;
                int i1 = (i0 + 1) % pal.Length;
                return Color.Lerp(pal[i0], pal[i1], f - Mathf.Floor(f)) * amt;
            }
            float hue = Mathf.Repeat(t * config.ambientColorCycle + emitter * 0.37f, 1f);
            return Color.HSVToRGB(hue, 0.8f, 1f) * amt;
        }

        // 씨앗 — 속도 없이 은은한 색 얼룩만(터지는 임펄스 아님). 유동이 이어받아 밀어 섞는다.
        private void SeedField()
        {
            int seeds = Mathf.Max(0, seedSplats);
            for (int i = 0; i < seeds; i++)
            {
                var uv = new Vector2(Random.value, Random.value);
                Splat(uv, Vector2.zero, PickFlowColor(i, i * 1.7f) * 3f);
            }
        }
    }
}
