using UnityEngine;

namespace Wassup.Presentation
{
    // fluid-paint-mixing unit 0 — 유체 솔버의 아키텍처-blind 순수 계산 (plain in/out, EditMode 테스트 대상).
    // GraphicsFormat/RenderTexture/Time 을 모른다. 해상도·텍셀 크기만 값으로 결정하고, 결정된 값을
    // 무엇에 쓸지는 소비자(FluidRenderTargets/FluidPaintSim)가 해석한다. 값 자체는 아키텍처를 모른다.
    public static class FluidMath
    {
        // 목표 해상도 + 종횡비(width/height) → 실제 (width, height).
        // 원본 WebGL-Fluid-Simulation getResolution 이식: 짧은 변=target, 긴 변=round(target×정규화aspect).
        // aspect≥1(가로가 긴 화면)이면 width 가 크고, aspect<1(세로가 긴 화면)이면 height 가 크다.
        // sim/dye 텍스처가 화면비를 따라야 유체가 원/사각으로 왜곡되지 않는다.
        public static Vector2Int CalcResolution(int target, float aspect)
        {
            // 비유한·비양수 방어: aspect 가 NaN/±Inf/≤0 이면 정사각으로 폴백해 최소한 그리기는 되게 한다.
            // (ARM64 float→int 캐스트 함정 — FlipbookMath 선례. NaN 비교는 전부 false 라 명시 검사한다.)
            if (target < 1) target = 1;
            if (!float.IsFinite(aspect) || aspect <= 0f) aspect = 1f;

            float ratio = aspect >= 1f ? aspect : 1f / aspect;
            int min = target;
            int max = Mathf.RoundToInt(target * ratio);
            if (max < 1) max = 1;

            return aspect >= 1f ? new Vector2Int(max, min) : new Vector2Int(min, max);
        }

        // 텍셀 크기 = (1/width, 1/height). 셰이더가 이웃 샘플 오프셋(vL/vR/vT/vB)에 쓴다.
        // 0 크기 RT 는 없어야 하지만 방어적으로 1 로 클램프해 Inf 텍셀을 막는다.
        public static Vector2 TexelSize(Vector2Int res)
        {
            float w = res.x >= 1 ? res.x : 1f;
            float h = res.y >= 1 ? res.y : 1f;
            return new Vector2(1f / w, 1f / h);
        }
    }
}
