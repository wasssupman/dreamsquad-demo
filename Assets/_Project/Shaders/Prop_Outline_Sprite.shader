// Wassup/Prop Outline (Sprite)
// 모든 배경 프랍(SpriteRenderer 빌보드)용 단일 URP 포워드 셰이더.
// 텍스처는 SpriteRenderer 가 _MainTex 로 공급. 각 프랍의 현 룩을 유지(Lit/Unlit 토글)하면서
// 실루엣 바깥에 알파 팽창 외곽선을 합성한다. 베이스는 알파 블렌딩이라 소프트 엣지 보존.
// docs/spec/prop-outline-shader/0_outline_shader.md
Shader "Wassup/Prop Outline (Sprite)"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor]   _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Cutoff ("Silhouette Cutoff", Range(0, 1)) = 0.5

        [Space(6)] [Header(Lighting)]
        [Toggle(_LIT_ON)] _Lit ("Simple Lit", Float) = 0
        // 컷아웃 프랍(나무/바위)은 ZWrite On 으로 깊이 정렬 보존. 순수 반투명이면 Off 가능.
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1

        [Space(6)] [Header(Outline)]
        [Toggle(_OUTLINE_ON)] _OutlineEnabled ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.05, 0.04, 0.03, 1)
        // 텍스처 해상도와 무관한 상대 두께(스프라이트 짧은 변의 비율). 해상도가 천차만별이라 텍셀 고정은 부적합.
        _OutlineWidth ("Outline Width (relative)", Range(0, 0.2)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4  _BaseColor;
            half   _Cutoff;
            half4  _OutlineColor;
            half   _OutlineWidth;
        CBUFFER_END

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize; // Unity 자동 제공(텍셀 크기). CBUFFER 밖.
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite [_ZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _LIT_ON
            #pragma shader_feature_local_fragment _OUTLINE_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half3  viewDirWS  : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs posn = GetVertexPositionInputs(IN.positionOS.xyz);

                // 스프라이트 메시는 NORMAL 이 없을 수 있다 → 카메라를 향하는 쿼드 노멀로 폴백.
                float3 nOS = IN.normalOS;
                if (dot(nOS, nOS) < 1e-5) nOS = float3(0, 0, -1);
                VertexNormalInputs nrm = GetVertexNormalInputs(nOS);

                OUT.positionCS = posn.positionCS;
                OUT.positionWS = posn.positionWS;
                OUT.normalWS   = nrm.normalWS;
                OUT.viewDirWS  = GetWorldSpaceViewDir(posn.positionWS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color;
                OUT.fogFactor  = ComputeFogFactor(posn.positionCS.z);
                return OUT;
            }

            half SampleAlpha (float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color * _BaseColor;
                half a = tex.a;

                // ---- 베이스 색 (Lit 토글) ----
                half3 baseRGB = tex.rgb;
                #ifdef _LIT_ON
                {
                    SurfaceData sd = (SurfaceData)0;
                    sd.albedo     = tex.rgb;
                    sd.specular   = half3(0, 0, 0);
                    sd.smoothness = 0;
                    sd.occlusion  = 1;
                    sd.emission   = half3(0, 0, 0);
                    sd.alpha      = 1;
                    sd.normalTS   = half3(0, 0, 1);

                    half3 nWS = normalize(IN.normalWS);

                    InputData id = (InputData)0;
                    id.positionWS              = IN.positionWS;
                    id.normalWS                = nWS;
                    id.viewDirectionWS         = normalize(IN.viewDirWS);
                    id.shadowCoord             = TransformWorldToShadowCoord(IN.positionWS);
                    id.fogCoord                = IN.fogFactor;
                    id.bakedGI                 = SampleSH(nWS);
                    id.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                    id.shadowMask              = half4(1, 1, 1, 1);

                    baseRGB = UniversalFragmentBlinnPhong(id, sd).rgb;
                }
                #endif

                // ---- 내부 가장자리 스트로크(알파 침식): 실루엣 '안쪽' 가장자리만 ----
                // 보이는 아트 픽셀(a>=cutoff) 중, _OutlineWidth 이내에 배경(<cutoff) 이웃이 있으면 가장자리.
                // 아트 위에 그려지므로 배경(하늘/다른 나무) 무관하게 또렷. 텍스처 페인트 그림자(<cutoff)는
                // 아트로 치지 않으므로 발밑 링 아티팩트 없음.
                half strokeMask = 0;
                #ifdef _OUTLINE_ON
                if (a >= _Cutoff)
                {
                    // 해상도 독립 두께: 짧은 변(min(w,h)) 기준 비율. _MainTex_TexelSize=(1/w,1/h,w,h).
                    float minDim = min(_MainTex_TexelSize.z, _MainTex_TexelSize.w);
                    float2 t = _MainTex_TexelSize.xy * (_OutlineWidth * minDim);
                    float2 d = t * 0.70710678; // 대각 정규화(1/sqrt2)

                    half minA = 1;
                    minA = min(minA, SampleAlpha(IN.uv + float2( t.x, 0)));
                    minA = min(minA, SampleAlpha(IN.uv + float2(-t.x, 0)));
                    minA = min(minA, SampleAlpha(IN.uv + float2(0,  t.y)));
                    minA = min(minA, SampleAlpha(IN.uv + float2(0, -t.y)));
                    minA = min(minA, SampleAlpha(IN.uv + float2( d.x,  d.y)));
                    minA = min(minA, SampleAlpha(IN.uv + float2( d.x, -d.y)));
                    minA = min(minA, SampleAlpha(IN.uv + float2(-d.x,  d.y)));
                    minA = min(minA, SampleAlpha(IN.uv + float2(-d.x, -d.y)));

                    strokeMask = (minA < _Cutoff) ? 1 : 0; // 이웃에 배경 있음 → 가장자리
                }
                #endif

                // ---- 합성: 가장자리는 외곽선색으로, 알파는 아트 그대로(소프트 엣지/현 룩 보존) ----
                half3 rgb = lerp(baseRGB, _OutlineColor.rgb, strokeMask);
                rgb = MixFog(rgb, IN.fogFactor);
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
