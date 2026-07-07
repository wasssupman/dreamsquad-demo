// lobby-keyring-drag 6 — 키링 홀로그램 UI 셰이더 (SF).
// UGUI 스텐실/클립 골격 + 가산 발광. 그레이스케일 빔 텍스처에 시안→마젠타
// 그라데이션 + 스캔라인 + 플리커 + 이동 펄스 + 행 글리치를 입힌다.
Shader "Wassup/UI/CordHologram"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ColorA ("Holo Color A (uv.y=0)", Color) = (0.2, 0.95, 1, 1)
        _ColorB ("Holo Color B (uv.y=1)", Color) = (1, 0.35, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.6
        _ScanDensity ("Scanline Density", Float) = 90
        _ScanSpeed ("Scanline Speed", Float) = 6
        _ScanStrength ("Scanline Strength", Range(0, 1)) = 0.35
        _FlickerSpeed ("Flicker Speed", Float) = 16
        _FlickerStrength ("Flicker Strength", Range(0, 1)) = 0.25
        _PulseSpeed ("Pulse Speed (cycles/s)", Float) = 0.8
        _PulseWidth ("Pulse Width (uv)", Range(0.02, 0.5)) = 0.15
        _PulseStrength ("Pulse Strength", Range(0, 3)) = 1.2
        _GlitchAmount ("Glitch Offset (uv)", Range(0, 0.5)) = 0.12
        _GlitchSpeed ("Glitch Speed", Float) = 8

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
        Blend SrcAlpha One // 가산 발광 — 어떤 배경 위에서도 빛나 보임
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

            fixed4 _ColorA;
            fixed4 _ColorB;
            float _Intensity;
            float _ScanDensity;
            float _ScanSpeed;
            float _ScanStrength;
            float _FlickerSpeed;
            float _FlickerStrength;
            float _PulseSpeed;
            float _PulseWidth;
            float _PulseStrength;
            float _GlitchAmount;
            float _GlitchSpeed;

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

            float hash21(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;
                float2 uv = IN.texcoord;

                // 행 글리치: 시간 해시가 임계 초과인 행만 uv.x 어긋남
                float row = floor(uv.y * 48.0);
                float g = hash21(float2(row, floor(t * _GlitchSpeed)));
                if (g > 0.92)
                    uv.x += (g - 0.96) * 2.0 * _GlitchAmount;

                half4 tex = tex2D(_MainTex, uv) + _TextureSampleAdd;

                // 그라데이션 (시안→마젠타)
                fixed3 grad = lerp(_ColorA.rgb, _ColorB.rgb, uv.y);

                // 스캔라인: 아래로 흐르는 감쇠 줄무늬
                float scan = 1.0 - _ScanStrength * (0.5 + 0.5 * sin(uv.y * _ScanDensity - t * _ScanSpeed));

                // 플리커: 시간 해시 기반 전체 밝기 떨림
                float fl = 1.0 - _FlickerStrength * hash21(float2(floor(t * _FlickerSpeed), 7.0));

                // 펄스: 줄을 따라 흐르는 밝은 밴드 (백색광)
                float pos = frac(t * _PulseSpeed);
                float pulse = smoothstep(_PulseWidth, 0.0, abs(uv.y - pos)) * _PulseStrength;

                half4 col;
                col.rgb = (grad * tex.rgb * scan + pulse.xxx * tex.a) * _Intensity * fl;
                col.rgb *= IN.color.rgb;
                col.a = tex.a * IN.color.a * fl;

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
