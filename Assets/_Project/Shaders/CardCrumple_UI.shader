Shader "Wassup/UI/CardCrumple"
{
    // card-crumple-unfold unit 1 — 손패 카드 art 의 구김→펴짐. UGUI(UI/Default 계열) CG.
    // 버텍스 변위는 UiCardFaceMesh 가 정적 스트림에 구운 오프셋(TEXCOORD1)을 _Unfold 로
    // 보간: pos.xy += crumple * (1 - _Unfold). 크리스 AO(TEXCOORD2.x)는 프래그먼트 음영.
    // Screen Space Overlay 라 Z 변위는 무효 → XY 변위 + 가짜 AO 만.
    // _Unfold = 1 → 변위/AO 0 → 일반 스프라이트와 픽셀 동일(rest 무회귀).
    // 스텐실/_ClipRect/GUIZTest scaffold 는 DraftCardFoil_UI.shader 와 동일.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Unfold ("Unfold", Range(0,1)) = 1
        _CreaseAO ("Crease AO", Range(0,2)) = 0.9

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

            struct appdata_t
            {
                float4 vertex  : POSITION;
                float4 color   : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 crumple : TEXCOORD1; // baked XY offset (구겨진 위치 = flat + crumple)
                float2 crease  : TEXCOORD2; // .x = 크리스 깊이(0..1)
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 ao : TEXCOORD1;       // .x = crease * (1 - unfold)
                float4 worldPosition : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Unfold;
            float _CreaseAO;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                float k = 1.0 - saturate(_Unfold); // 1 = 완전히 구겨짐, 0 = 평면
                float4 pos = v.vertex;
                pos.xy += v.crumple.xy * k;
                o.worldPosition = pos;
                o.vertex = UnityObjectToClipPos(pos);
                o.uv = v.texcoord;
                o.ao = float2(saturate(v.crease.x) * k, 0);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;
                col.rgb *= saturate(1.0 - i.ao.x * _CreaseAO); // 크리스 그림자
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return col;
            }
            ENDCG
        }
    }
}
