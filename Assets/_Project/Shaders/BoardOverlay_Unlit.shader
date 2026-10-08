// battle-core-rebuild unit 5b — 보드 위에 그리는 것들(격자·배치 가이드·사거리 링·예고선)의 셰이더.
//
// 왜 새로 만드나: `LineRenderer` 는 `AddComponent` 로 붙여도 머티리얼이 **null 로 남는다**
// (실측 — 5b 의 첫 Play 에서 격자와 예고선이 전부 마젠타였다). 그리고 이 층이 필요로 하는
// 조합 — 정점색(선의 start/end 색) × 스프라이트 텍스처(칸·링 모양) × 알파 — 을 기존 런타임
// 머티리얼 둘(`SolidOpaque`·`SolidTransparent`)이 갖고 있지 않다: 그쪽은 단색이고 정점색도
// 텍스처도 안 읽는다.
//
// ⚠ **`Shader.Find` + `new Material` 로 때우지 않는다**(추가 제약). 모바일 shader stripping 이
// null 을 돌려주면 렌더가 깨지고, 그 증상은 에디터에서 안 보인다. 그래서 이 셰이더를 명시
// 추가하고 `Assets/Resources/RuntimeMaterials/BoardOverlay.mat` 으로 always-included 등록한다.
Shader "Somnia/Battle/BoardOverlay_Unlit"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "BoardOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            Lighting Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                // 정점색이 **색의 주인**이다 — 선은 start/end, 스프라이트는 렌더러의 color.
                // 머티리얼을 렌더러마다 복제하지 않으려는 것이 이 선택의 값이다.
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                return tex * input.color * half4(_BaseColor);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
