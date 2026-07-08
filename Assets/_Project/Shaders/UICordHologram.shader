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
            #include "KeyringHologramCommon.hlsl" // keyring-unify 2 — 효과 함수 공용(월드 셰이더와 공유)
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

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = _Time.y;
                float2 uv = IN.texcoord;

                // 행 글리치: 시간 해시가 임계 초과인 행만 uv.x 어긋남 (UGUI: 길이축=uv.y, 폭=uv.x)
                uv.x += KeyringGlitchOffset(uv.y, t, _GlitchSpeed, _GlitchAmount);

                half4 tex = tex2D(_MainTex, uv) + _TextureSampleAdd;

                // 그라데이션·스캔라인·플리커·펄스 — KeyringHologramCommon 공용 함수
                // (fixed→float 정밀도 승격은 keyring-unify 계약 6의 허용 예외)
                float fl;
                float3 rgb = KeyringHoloColor(tex.rgb, tex.a, uv.y, t,
                    _ColorA.rgb, _ColorB.rgb, _Intensity,
                    _ScanDensity, _ScanSpeed, _ScanStrength,
                    _FlickerSpeed, _FlickerStrength,
                    _PulseSpeed, _PulseWidth, _PulseStrength, fl);

                half4 col;
                col.rgb = rgb * IN.color.rgb;
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
