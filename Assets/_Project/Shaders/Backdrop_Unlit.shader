Shader "Wassup/Backdrop_Unlit"
{
    Properties
    {
        _MainTex   ("Texture", 2D)     = "white" {}
        _TintColor ("Tint Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"  = "Background"
            "Queue"       = "Background+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Backdrop_Unlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite Off
            ZTest  Always
            Cull   Off
            Blend  Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _TintColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                return col * _TintColor;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
