Shader "Wassup/EffectTilePulse"
{
    // effect-tiles — 효과 타일맵 전용 부드러운 발광 펄스(모든 효과 타일 균일 적용).
    // TilemapRenderer 는 타일맵당 머티리얼 1개라, 전용 _effectTilemap 에 이 머티리얼 하나를 건다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _PulseSpeed  ("Pulse Speed", Float) = 2.2
        _PulseAmount ("Pulse Amount", Range(0,1)) = 0.4
        _GlowColor   ("Glow Tint", Color) = (1,1,1,1)
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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float _PulseSpeed;
            float _PulseAmount;
            half4 _GlowColor;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;
                float pulse = 0.5 + 0.5 * sin(_Time.y * _PulseSpeed);
                half3 rgb = tex.rgb + tex.rgb * (_PulseAmount * pulse) * _GlowColor.rgb;
                return half4(rgb, tex.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
