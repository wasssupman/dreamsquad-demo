Shader "Wassup/PlacementLiquidTile"
{
    // placement-cell-snap unit 7 rev — 포커스 셀 하이라이트 자체가 끈적한 액체.
    // 테두리(둥근사각 SDF)는 셀에 고정 = "릴리즈하면 여기" 계약. 내부 fill 은 손가락 방향
    // 액적과 smin(Quilez) 블렌드로 번지다(_Pull.z = t↑) 테두리를 넘는다 — 히스테리시스 밴드의 시각화.
    // 쿼드 = 셀 2배 크기(번짐이 셀 밖으로 나갈 여유). uv 0..1 → p ∈ [-1,1], 1 단위 = 1 셀.
    // ⚠️ 런타임 생성 쿼드가 쓰므로 반드시 .mat 에셋(TileSetData.placementLiquidMaterial)으로 참조 —
    //    Shader.Find 는 빌드 스트리핑에 걸린다(2026-07-15 출시 사고, DeployCutscenePlayer 참조).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _BorderColor ("Border Color", Color) = (0.45, 1.0, 0.55, 0.95)
        _FillColor   ("Fill Color",   Color) = (0.35, 0.95, 0.45, 0.45)
        _Pull        ("Pull (dir.xy, t, -)", Vector) = (1, 0, 0, 0)
        _BorderWidth ("Border Width (cell)", Range(0.01, 0.12)) = 0.045
        _CornerRadius("Corner Radius (cell)", Range(0.0, 0.25)) = 0.08
        _Inset       ("Fill Inset (cell)", Range(0.0, 0.2)) = 0.06
        _Reach       ("Tip Reach (cell)", Range(0.2, 1.4)) = 0.85
        _StretchPow  ("Stretch Pow (late accel)", Range(1, 5)) = 1.8
        _TipR        ("Tip Radius (base, grow, -, -)", Vector) = (0.20, 0.14, 0, 0)
        _NeckK       ("smin k (k0, neck shrink, -, -)", Vector) = (0.5, 0.25, 0, 0)
        _TipElong    ("Tip Elongation (per t)", Range(0, 2)) = 0.8
        _Lean        ("Fill Lean (cell per t)", Range(0, 0.3)) = 0.12
        _Feather     ("Edge Feather (cell)", Range(0.005, 0.06)) = 0.02
        _QuadCells   ("Quad Size (cells) - C# 동기", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            half4 _BorderColor;
            half4 _FillColor;
            float4 _Pull;
            float _BorderWidth;
            float _CornerRadius;
            float _Inset;
            float _Reach;
            float _StretchPow;
            float4 _TipR;
            float4 _NeckK;
            float _TipElong;
            float _Lean;
            float _Feather;
            float _QuadCells;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float SdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            // Quilez 다항식 smin — k = 블렌드 두께(거리 단위). 목(neck)이 여기서 나온다.
            float SminPoly(float a, float b, float k)
            {
                float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = (IN.uv - 0.5) * _QuadCells;    // 1 단위 = 1 셀. 쿼드 크기는 C#(LiquidQuadCells)과 동기 —
                                                          // 캔버스가 좁으면 혀가 쿼드 가장자리에서 칼로 잘린다.
                float t = clamp(_Pull.z, 0.0, 1.2);       // 1.2 까지 허용 — 스프링 오버슈트(출렁)가 보이게
                float2 dir = _Pull.xy;
                float dl = length(dir);
                dir = dl > 1e-4 ? dir / dl : float2(0.0, 0.0);

                // 고정 테두리 — 셀 경계의 둥근사각 윤곽. t/dir 과 무관(계약 표시).
                float boxD = SdRoundBox(p, float2(0.5, 0.5), _CornerRadius);
                float borderMask = smoothstep(-_BorderWidth - _Feather, -_BorderWidth + _Feather, boxD)
                                 * (1.0 - smoothstep(-_Feather, _Feather, boxD));

                // 액체 fill — "원이 이동"이 아니라 "질량이 흐른다"로 읽히게 하는 세 요소:
                // (1) lean: 몸통이 당김 쪽으로 팽창하고 반대쪽은 빠진다(질량 보존 느낌).
                float proj = clamp(dot(p, dir) / 0.5, -1.0, 1.0);
                float innerD = boxD + _BorderWidth + _Inset - _Lean * t * proj;

                // (2) 팁 = 당김 축으로 신장되는 타원(스케일드-length 근사 SDF) — 꼬리가 몸통 쪽을 향해
                //     방울이 '끌려나온' 형상. 완전 원이면 펠릿으로 읽힌다.
                float s = _Reach * pow(t, _StretchPow);
                float2 q = p - dir * s;
                float qAlong = dot(q, dir);
                float qPerp = length(q - qAlong * dir);
                float ax = 1.0 + _TipElong * t;
                float tipD = length(float2(qAlong / ax, qPerp)) - (_TipR.x + _TipR.y * t);

                // (3) 두꺼운 메니스커스: k 를 크게 유지(수축 완만) → 몸통-방울 연결 살이 남는다.
                float k = max(0.05, _NeckK.x * (1.0 - _NeckK.y * t));
                float f = SminPoly(innerD, tipD, k);
                float fillMask = 1.0 - smoothstep(-_Feather, _Feather, f);

                // 합성: 테두리를 fill 위에 over — 액체가 넘어도 프레임은 뚫고 보인다.
                half aB = borderMask * _BorderColor.a;
                half aF = fillMask * _FillColor.a;
                half outA = aB + aF * (1.0 - aB);
                half3 rgb = (_BorderColor.rgb * aB + _FillColor.rgb * aF * (1.0 - aB)) / max(outA, 1e-4);
                return half4(rgb, outA);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
