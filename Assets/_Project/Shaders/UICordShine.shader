// lobby-keyring-drag 5 — 키링 줄 샤인 UI 셰이더.
// UI/Default 골격(스텐실/클립 호환) + uv.y 를 따라 흐르는 광택 밴드 + 셀 해시 트윙클.
// 줄 Image 는 simple stretch 라 uv.y 가 줄 전장 0..1 — 밴드가 줄 전체를 훑는다.
Shader "Wassup/UI/CordShine"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ShineColor ("Shine Color", Color) = (1, 0.96, 0.75, 1)
        _ShineSpeed ("Shine Speed (cycles/s)", Float) = 0.5
        _ShineWidth ("Shine Width (uv)", Range(0.02, 0.5)) = 0.12
        _ShineStrength ("Shine Strength", Range(0, 2)) = 0.9
        _SparkleScale ("Sparkle Scale", Float) = 10
        _SparkleSpeed ("Sparkle Speed", Float) = 2
        _SparkleStrength ("Sparkle Strength", Range(0, 2)) = 0.7

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

            fixed4 _ShineColor;
            float _ShineSpeed;
            float _ShineWidth;
            float _ShineStrength;
            float _SparkleScale;
            float _SparkleSpeed;
            float _SparkleStrength;

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
                half4 col = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // 광택 밴드: uv.y 진행 방향으로 주기 이동, 경계는 smoothstep 소프트
                float pos = frac(_Time.y * _ShineSpeed);
                float band = smoothstep(_ShineWidth, 0.0, abs(IN.texcoord.y - pos));

                // 트윙클: 시간에 따라 흐르는 셀 해시 → 희소한 셀만 발광 + 사인 점멸
                float2 cell = floor(IN.texcoord * _SparkleScale + float2(0.0, _Time.y * _SparkleSpeed));
                float h = hash21(cell);
                float tw = pow(h, 24.0) * (0.5 + 0.5 * sin(_Time.y * 6.0 + h * 40.0));

                col.rgb += _ShineColor.rgb * (band * _ShineStrength + tw * _SparkleStrength) * col.a;

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
