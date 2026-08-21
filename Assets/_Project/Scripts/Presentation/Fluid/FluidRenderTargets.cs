using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // fluid-paint-mixing unit 0 — 유체 솔버가 핑퐁하는 RenderTexture 세트의 소유·수명 관리.
    // 순수 아님(RenderTexture 를 잡는 런타임 헬퍼). 해상도는 FluidMath 로 산출, 포맷은 SystemInfo 폴백.
    // FluidPaintSim(unit 2)이 Allocate → 매 프레임 Blit 체인(Swap*) → Release 로 쓴다.
    public sealed class FluidRenderTargets
    {
        // 핑퐁 쌍: Read = 이번 패스 입력, Write = 출력. 패스 후 Swap 으로 교대한다.
        private RenderTexture _velRead, _velWrite;
        private RenderTexture _dyeRead, _dyeWrite;
        private RenderTexture _pressureRead, _pressureWrite;
        // 단일 버퍼(같은 프레임 안에서 곧바로 소비되므로 핑퐁 불필요).
        private RenderTexture _divergence, _curl;
        // 안정 출력 핸들: dye 는 핑퐁이라 DyeRead 가 매 프레임 다른 RT 를 가리킨다. 소비자(RawImage/머티리얼)가
        // 고정 핸들을 한 번만 잡게 Display 로 매 스텝 복사한다(Display 패스).
        private RenderTexture _display;

        public RenderTexture VelocityRead => _velRead;
        public RenderTexture VelocityWrite => _velWrite;
        public RenderTexture DyeRead => _dyeRead;
        public RenderTexture DyeWrite => _dyeWrite;
        public RenderTexture PressureRead => _pressureRead;
        public RenderTexture PressureWrite => _pressureWrite;
        public RenderTexture Divergence => _divergence;
        public RenderTexture Curl => _curl;
        public RenderTexture Display => _display;

        public Vector2Int SimResolution { get; private set; }
        public Vector2Int DyeResolution { get; private set; }
        public Vector2 SimTexelSize { get; private set; }
        public Vector2 DyeTexelSize { get; private set; }

        public bool IsAllocated => _velRead != null;

        // surface 크기(픽셀)로 화면비를 잡아 sim/dye 해상도를 산출하고 RT 를 잡는다.
        // 재호출 시 기존 세트를 먼저 Release (해상도 변경·표면 리사이즈 대응).
        public void Allocate(FluidSimConfig cfg, int surfaceWidth, int surfaceHeight)
        {
            Release();
            if (cfg == null) return;

            float aspect = surfaceHeight > 0 ? (float)surfaceWidth / surfaceHeight : 1f;
            SimResolution = FluidMath.CalcResolution(cfg.simResolution, aspect);
            DyeResolution = FluidMath.CalcResolution(cfg.dyeResolution, aspect);
            SimTexelSize = FluidMath.TexelSize(SimResolution);
            DyeTexelSize = FluidMath.TexelSize(DyeResolution);

            // velocity=2채널, dye=색(4채널), pressure/divergence/curl=스칼라(1채널).
            var velFmt = PickFormat(cfg.preferHalfFloat, RenderTextureFormat.RGHalf, RenderTextureFormat.RGFloat, RenderTextureFormat.ARGBHalf);
            var scalarFmt = PickFormat(cfg.preferHalfFloat, RenderTextureFormat.RHalf, RenderTextureFormat.RFloat, RenderTextureFormat.RGHalf);
            var dyeFmt = PickFormat(cfg.preferHalfFloat, RenderTextureFormat.ARGBHalf, RenderTextureFormat.ARGBFloat, RenderTextureFormat.ARGB32);

            _velRead = Create(SimResolution, velFmt, "Fluid_VelocityA");
            _velWrite = Create(SimResolution, velFmt, "Fluid_VelocityB");
            _dyeRead = Create(DyeResolution, dyeFmt, "Fluid_DyeA");
            _dyeWrite = Create(DyeResolution, dyeFmt, "Fluid_DyeB");
            _pressureRead = Create(SimResolution, scalarFmt, "Fluid_PressureA");
            _pressureWrite = Create(SimResolution, scalarFmt, "Fluid_PressureB");
            _divergence = Create(SimResolution, scalarFmt, "Fluid_Divergence");
            _curl = Create(SimResolution, scalarFmt, "Fluid_Curl");
            _display = Create(DyeResolution, dyeFmt, "Fluid_Display");

            // 새 RenderTexture 의 초기 내용은 미보장 → 명시 clear. 안 하면 첫 프레임에 쓰레기 값이
            // 이류·압력 반복으로 번져 NaN/무한 소용돌이가 될 수 있다.
            ClearAll();
        }

        // 모든 RT 를 투명 검정으로 초기화. RenderTexture.active 를 복원해 호출측 렌더 상태를 건드리지 않는다.
        private void ClearAll()
        {
            var prev = RenderTexture.active;
            ClearOne(_velRead); ClearOne(_velWrite);
            ClearOne(_dyeRead); ClearOne(_dyeWrite);
            ClearOne(_pressureRead); ClearOne(_pressureWrite);
            ClearOne(_divergence); ClearOne(_curl); ClearOne(_display);
            RenderTexture.active = prev;
        }

        private static void ClearOne(RenderTexture rt)
        {
            Graphics.SetRenderTarget(rt);
            GL.Clear(false, true, Color.clear);
        }

        public void SwapVelocity() { (_velRead, _velWrite) = (_velWrite, _velRead); }
        public void SwapDye() { (_dyeRead, _dyeWrite) = (_dyeWrite, _dyeRead); }
        public void SwapPressure() { (_pressureRead, _pressureWrite) = (_pressureWrite, _pressureRead); }

        public void Release()
        {
            Destroy(ref _display);
            Destroy(ref _velRead); Destroy(ref _velWrite);
            Destroy(ref _dyeRead); Destroy(ref _dyeWrite);
            Destroy(ref _pressureRead); Destroy(ref _pressureWrite);
            Destroy(ref _divergence); Destroy(ref _curl);
        }

        // preferHalf 면 half→full→fallback, 아니면 full→half→fallback 순으로 첫 지원 포맷.
        // fallback 은 최후의 보루(ARGB32/RGHalf 는 사실상 항상 지원)이므로 무조건 반환한다.
        private static RenderTextureFormat PickFormat(bool preferHalf, RenderTextureFormat half, RenderTextureFormat full, RenderTextureFormat fallback)
        {
            var first = preferHalf ? half : full;
            var second = preferHalf ? full : half;
            if (SystemInfo.SupportsRenderTextureFormat(first)) return first;
            if (SystemInfo.SupportsRenderTextureFormat(second)) return second;
            return fallback;
        }

        private static RenderTexture Create(Vector2Int res, RenderTextureFormat fmt, string name)
        {
            var rt = new RenderTexture(res.x, res.y, 0, fmt)
            {
                name = name,
                filterMode = FilterMode.Bilinear,   // advection 의 bilerp 는 하드웨어 선형 샘플에 기댄다
                wrapMode = TextureWrapMode.Clamp,    // 경계는 셰이더가 반사 처리; 텍스처 랩은 clamp
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = false,           // Blit(프래그먼트) 경로 — 컴퓨트 랜덤라이트 불필요
                antiAliasing = 1,
            };
            rt.Create();
            return rt;
        }

        private static void Destroy(ref RenderTexture rt)
        {
            if (rt == null) return;
            if (RenderTexture.active == rt)
                RenderTexture.active = null;
            rt.Release();
            if (Application.isPlaying) Object.Destroy(rt);
            else Object.DestroyImmediate(rt);
            rt = null;
        }
    }
}
