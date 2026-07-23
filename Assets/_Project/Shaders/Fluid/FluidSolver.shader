// fluid-paint-mixing unit 1 — 축소 유체 솔버 (RenderTexture 핑퐁, Graphics.Blit 패스 체인).
// Ported from PavelDoGreat/WebGL-Fluid-Simulation (script.js fragment shaders).
//   Copyright (c) 2017 Pavel Dobryakov — MIT License.
//   https://github.com/PavelDoGreat/WebGL-Fluid-Simulation/blob/master/LICENSE
//
// 패스 인덱스 계약 (FluidPaintSim 이 이 번호로 Blit — 순서 변경 금지):
//   0 Advection · 1 Divergence · 2 Curl · 3 Vorticity · 4 Pressure(Jacobi)
//   5 GradientSubtract · 6 Splat · 7 Clear · 8 Display
// 텍스처는 named uniform 으로만 읽는다(_MainTex 미사용). 소비자가 패스마다 SetTexture 후 Blit.
Shader "Wassup/Fluid/FluidSolver"
{
    Properties
    {
        _MainTex ("Ignored (Blit src)", 2D) = "black" {}
    }

    SubShader
    {
        // 솔버는 값을 덮어쓴다 — 알파블렌드/뎁스 없음. 오프스크린 RT 간 풀스크린 blit.
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        // ── Pass 0 · Advection ─────────────────────────────────────────
        // 백트레이스 후 수동 bilerp (모바일 half-float 선형필터 미지원 대비 — 항상 안전).
        Pass
        {
            Name "Advection"
            CGPROGRAM
            #pragma vertex VertSimple
            #pragma fragment frag
            #include "FluidCommon.cginc"

            sampler2D _Velocity;
            sampler2D _Source;
            float2 _DyeTexelSize;
            float _Dt;
            float _Dissipation;

            float4 bilerp(sampler2D s, float2 uv, float2 tsize)
            {
                float2 st = uv / tsize - 0.5;
                float2 iuv = floor(st);
                float2 fuv = frac(st);
                float4 a = tex2D(s, (iuv + float2(0.5, 0.5)) * tsize);
                float4 b = tex2D(s, (iuv + float2(1.5, 0.5)) * tsize);
                float4 c = tex2D(s, (iuv + float2(0.5, 1.5)) * tsize);
                float4 d = tex2D(s, (iuv + float2(1.5, 1.5)) * tsize);
                return lerp(lerp(a, b, fuv.x), lerp(c, d, fuv.x), fuv.y);
            }

            float4 frag(v2fSimple i) : SV_Target
            {
                float2 coord = i.uv - _Dt * tex2D(_Velocity, i.uv).xy * _TexelSize;
                float4 result = bilerp(_Source, coord, _DyeTexelSize);
                float decay = 1.0 + _Dissipation * _Dt;
                return result / decay;
            }
            ENDCG
        }

        // ── Pass 1 · Divergence ────────────────────────────────────────
        Pass
        {
            Name "Divergence"
            CGPROGRAM
            #pragma vertex VertNeighbors
            #pragma fragment frag
            #include "FluidCommon.cginc"

            sampler2D _Velocity;

            float4 frag(v2fNeighbors i) : SV_Target
            {
                float L = tex2D(_Velocity, i.vL).x;
                float R = tex2D(_Velocity, i.vR).x;
                float T = tex2D(_Velocity, i.vT).y;
                float B = tex2D(_Velocity, i.vB).y;
                float2 C = tex2D(_Velocity, i.uv).xy;
                if (i.vL.x < 0.0) { L = -C.x; }
                if (i.vR.x > 1.0) { R = -C.x; }
                if (i.vT.y > 1.0) { T = -C.y; }
                if (i.vB.y < 0.0) { B = -C.y; }
                float div = 0.5 * (R - L + T - B);
                return float4(div, 0.0, 0.0, 1.0);
            }
            ENDCG
        }

        // ── Pass 2 · Curl ──────────────────────────────────────────────
        Pass
        {
            Name "Curl"
            CGPROGRAM
            #pragma vertex VertNeighbors
            #pragma fragment frag
            #include "FluidCommon.cginc"

            sampler2D _Velocity;

            float4 frag(v2fNeighbors i) : SV_Target
            {
                float L = tex2D(_Velocity, i.vL).y;
                float R = tex2D(_Velocity, i.vR).y;
                float T = tex2D(_Velocity, i.vT).x;
                float B = tex2D(_Velocity, i.vB).x;
                float vorticity = R - L - T + B;
                return float4(0.5 * vorticity, 0.0, 0.0, 1.0);
            }
            ENDCG
        }

        // ── Pass 3 · Vorticity confinement ─────────────────────────────
        Pass
        {
            Name "Vorticity"
            CGPROGRAM
            #pragma vertex VertNeighbors
            #pragma fragment frag
            #include "FluidCommon.cginc"

            sampler2D _Velocity;
            sampler2D _Curl;
            float _CurlStrength;
            float _Dt;

            float4 frag(v2fNeighbors i) : SV_Target
            {
                float L = tex2D(_Curl, i.vL).x;
                float R = tex2D(_Curl, i.vR).x;
                float T = tex2D(_Curl, i.vT).x;
                float B = tex2D(_Curl, i.vB).x;
                float C = tex2D(_Curl, i.uv).x;

                float2 force = 0.5 * float2(abs(T) - abs(B), abs(R) - abs(L));
                force /= length(force) + 0.0001;
                force *= _CurlStrength * C;
                force.y *= -1.0;

                float2 velocity = tex2D(_Velocity, i.uv).xy;
                velocity += force * _Dt;
                velocity = clamp(velocity, -1000.0, 1000.0);
                return float4(velocity, 0.0, 1.0);
            }
            ENDCG
        }

        // ── Pass 4 · Pressure (Jacobi iteration) ───────────────────────
        Pass
        {
            Name "Pressure"
            CGPROGRAM
            #pragma vertex VertNeighbors
            #pragma fragment frag
            #include "FluidCommon.cginc"

            sampler2D _Pressure;
            sampler2D _Divergence;

            float4 frag(v2fNeighbors i) : SV_Target
            {
                float L = tex2D(_Pressure, i.vL).x;
                float R = tex2D(_Pressure, i.vR).x;
                float T = tex2D(_Pressure, i.vT).x;
                float B = tex2D(_Pressure, i.vB).x;
                float divergence = tex2D(_Divergence, i.uv).x;
                float pressure = (L + R + B + T - divergence) * 0.25;
                return float4(pressure, 0.0, 0.0, 1.0);
            }
            ENDCG
        }

        // ── Pass 5 · Gradient subtract ─────────────────────────────────
        Pass
        {
            Name "GradientSubtract"
            CGPROGRAM
            #pragma vertex VertNeighbors
            #pragma fragment frag
            #include "FluidCommon.cginc"

            sampler2D _Pressure;
            sampler2D _Velocity;

            float4 frag(v2fNeighbors i) : SV_Target
            {
                float L = tex2D(_Pressure, i.vL).x;
                float R = tex2D(_Pressure, i.vR).x;
                float T = tex2D(_Pressure, i.vT).x;
                float B = tex2D(_Pressure, i.vB).x;
                float2 velocity = tex2D(_Velocity, i.uv).xy;
                velocity -= float2(R - L, T - B);
                return float4(velocity, 0.0, 1.0);
            }
            ENDCG
        }

        // ── Pass 6 · Splat (색·힘 주입) ────────────────────────────────
        Pass
        {
            Name "Splat"
            CGPROGRAM
            #pragma vertex VertSimple
            #pragma fragment frag
            #include "FluidCommon.cginc"

            sampler2D _Target;
            float _AspectRatio;
            float3 _SplatColor;
            float2 _SplatPoint;
            float _SplatRadius;

            float4 frag(v2fSimple i) : SV_Target
            {
                float2 p = i.uv - _SplatPoint.xy;
                p.x *= _AspectRatio;
                float3 splat = exp(-dot(p, p) / _SplatRadius) * _SplatColor;
                float3 base = tex2D(_Target, i.uv).xyz;
                return float4(base + splat, 1.0);
            }
            ENDCG
        }

        // ── Pass 7 · Clear (스칼라 배율 — pressure init = ×PRESSURE) ────
        Pass
        {
            Name "Clear"
            CGPROGRAM
            #pragma vertex VertSimple
            #pragma fragment frag
            #include "FluidCommon.cginc"

            sampler2D _Target;
            float _ClearValue;

            float4 frag(v2fSimple i) : SV_Target
            {
                return _ClearValue * tex2D(_Target, i.uv);
            }
            ENDCG
        }

        // ── Pass 8 · Display (dye 복사 + 선택적 가장자리 마스크) ────────
        // _EdgeMask>0 이면 테두리에서 그 폭(uv)만큼만 색을 남기고 중앙은 비운다("가장자리만 분포").
        Pass
        {
            Name "Display"
            CGPROGRAM
            #pragma vertex VertSimple
            #pragma fragment frag
            #include "FluidCommon.cginc"

            sampler2D _Source;
            float _EdgeMask;

            float4 frag(v2fSimple i) : SV_Target
            {
                float4 c = tex2D(_Source, i.uv);
                if (_EdgeMask > 0.0)
                {
                    // 가장 가까운 변까지의 거리(0=테두리, 0.5=중앙). 그 폭 안에서만 남긴다.
                    float dEdge = min(min(i.uv.x, 1.0 - i.uv.x), min(i.uv.y, 1.0 - i.uv.y));
                    c *= 1.0 - smoothstep(0.0, _EdgeMask, dEdge);
                }
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
