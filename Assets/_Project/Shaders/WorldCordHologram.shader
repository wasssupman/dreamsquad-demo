// keyring-unify 2 — 키링 홀로그램 월드 셰이더 (인게임 LineRenderer/SpriteRenderer 용).
// UICordHologram 과 동일 효과(KeyringHologramCommon.hlsl 공유)를 URP unlit 가산으로 렌더.
// _LengthAxis 로 길이축을 선택: 줄(LineRenderer, textureMode=Stretch)=1(uv.x), 고리(SpriteRenderer)=0(uv.y).
Shader "Wassup/World/CordHologram"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture (grayscale beam/ring)", 2D) = "white" {}

        _ColorA ("Holo Color A (len=0)", Color) = (0.2, 0.95, 1, 1)
        _ColorB ("Holo Color B (len=1)", Color) = (1, 0.35, 1, 1)
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
        _LengthAxis ("Length Axis (0=uv.y, 1=uv.x)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "PreviewType"="Plane"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha One // 가산 발광 — UGUI 판과 동일 블렌드

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "KeyringHologramCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial) // SRP Batcher 호환
            float4 _MainTex_ST;
            float4 _ColorA;
            float4 _ColorB;
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
            float _LengthAxis;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float t = _Time.y;

                // 정준 uv(canon.x=폭, canon.y=길이)로 정규화 — 텍스처는 UI 기준(폭=x, 길이=y)으로
                // 저작되었으므로 LineRenderer(_LengthAxis=1, u=길이)는 샘플 전 swap 이 필요.
                float2 canon = (_LengthAxis > 0.5) ? float2(IN.uv.y, IN.uv.x) : IN.uv;
                float lenUv = canon.y;

                // 행 글리치: 길이축으로 행을 정하고 폭 방향(canon.x) 어긋남 (UGUI 판과 동형)
                canon.x += KeyringGlitchOffset(lenUv, t, _GlitchSpeed, _GlitchAmount);

                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, canon);

                float fl;
                float3 rgb = KeyringHoloColor(tex.rgb, tex.a, lenUv, t,
                    _ColorA.rgb, _ColorB.rgb, _Intensity,
                    _ScanDensity, _ScanSpeed, _ScanStrength,
                    _FlickerSpeed, _FlickerStrength,
                    _PulseSpeed, _PulseWidth, _PulseStrength, fl);

                float4 col;
                col.rgb = rgb * IN.color.rgb;
                col.a = tex.a * IN.color.a * fl;
                return col;
            }
            ENDHLSL
        }
    }
}
