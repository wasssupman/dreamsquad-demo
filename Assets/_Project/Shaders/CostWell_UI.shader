// tray-cost-well 5 — 코스트 물통 액체 UI 셰이더.
// UI/Default 골격(스텐실/클립 호환) + 프래그먼트에서 수위를 잘라내는 액체.
//
// 왜 Image.Type.Filled 를 안 쓰는가: Filled 는 지오메트리를 잘라내므로 셰이더가
// 수면 위치를 모른다 → 출렁이는 표면을 만들 수 없고, 스프라이트가 rect 에 맞춰
// 늘어나 "늘린 이미지" 인상이 남는다. 여기서는 Type.Simple(풀 rect)로 두고
// _Fill uniform 을 프래그먼트에서 파형과 합쳐 잘라낸다.
Shader "Wassup/UI/CostWell"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Fill ("Fill (0..1)", Range(0,1)) = 0.5
        _LiquidBottom ("Liquid Bottom", Color) = (0.85, 0.42, 0.06, 1)
        _LiquidTop ("Liquid Top", Color) = (1, 0.82, 0.25, 1)
        _SurfaceColor ("Surface Highlight", Color) = (1, 0.98, 0.86, 0.95)
        _SurfaceThickness ("Surface Thickness (uv)", Range(0.005, 0.2)) = 0.055

        _WaveAmp ("Wave Amplitude (uv)", Range(0, 0.08)) = 0.018
        _WaveFreq ("Wave Frequency", Float) = 9
        _WaveSpeed ("Wave Speed", Float) = 1.6

        _GlassStrength ("Glass Highlight", Range(0, 1)) = 0.22
        _DepthShade ("Depth Shading", Range(0, 1)) = 0.55

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        // Well 은 Mask 컴포넌트를 쓴다 — 스텐실 스캐폴드가 없으면 액체가 둥근
        // 모서리 밖으로 삐져나온다.
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _Fill;
            fixed4 _LiquidBottom;
            fixed4 _LiquidTop;
            fixed4 _SurfaceColor;
            float _SurfaceThickness;
            float _WaveAmp;
            float _WaveFreq;
            float _WaveSpeed;
            float _GlassStrength;
            float _DepthShade;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 col = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                float2 uv = IN.texcoord;

                // 수면 = 두 개의 어긋난 사인 합 (한 개면 기계적으로 보인다).
                // 빈 통/가득 찬 통에서는 진폭을 0 으로 죽인다 — 경계에서 파형이
                // 바닥이나 천장을 넘어 삐져나오는 걸 막는다.
                float waveMask = smoothstep(0.0, 0.06, _Fill) * smoothstep(1.0, 0.94, _Fill);
                float w1 = sin(uv.x * _WaveFreq + _Time.y * _WaveSpeed);
                float w2 = sin(uv.x * _WaveFreq * 1.73 - _Time.y * _WaveSpeed * 0.81);
                float level = _Fill + (w1 * 0.65 + w2 * 0.35) * _WaveAmp * waveMask;

                float d = level - uv.y;              // 양수 = 수면 아래(액체)
                float liquid = step(0.0, d);

                // 깊이 음영 — 바닥으로 갈수록 진하게. 평평한 단색이 아니게 만드는 핵심.
                float depth = saturate(d / max(level, 0.0001));
                half3 body = lerp(_LiquidTop.rgb, _LiquidBottom.rgb, depth * _DepthShade);

                // 수면 하이라이트 — 표면 밴드. 별도 Image 없이 여기서 만든다.
                float surf = smoothstep(_SurfaceThickness, 0.0, abs(d)) * waveMask;
                body += _SurfaceColor.rgb * surf * _SurfaceColor.a;

                // 유리 반사 — 좌측 세로 스트라이프 2줄. 용기가 유리라는 신호.
                float glass = smoothstep(0.055, 0.0, abs(uv.x - 0.17)) * 0.7
                            + smoothstep(0.028, 0.0, abs(uv.x - 0.29)) * 0.4;
                body += glass * _GlassStrength;

                col.rgb = body;
                col.a *= liquid;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}
