Shader "Wassup/UI/DepthParallax"
{
    // depth-parallax unit 2 — 2D 아트에 뎁스맵 기반 2.5D 패럴랙스(Marvel Snap 카드류). UGUI(UI/Default 계열) CG.
    // 3중 큐가 모두 _Tilt 로 게이트되어 rest(_Tilt=0)에서 원본 스프라이트와 픽셀 동일(no-op 불변식).
    //   Cue A — 뎁스 UV 패럴랙스(frag, 코어): dependent read 정확히 1회.
    //   Cue B — 클립공간 사다리꼴(vert): ortho overlay 라 perspective divide 없음 → 코너 부호를 UV0 에서 유도.
    //   Cue C — 하이라이트 스윕(frag): length(_Tilt) 게이트, _Time 금지.
    // ScreenSpaceOverlay 는 URP 이후 렌더라 _CameraDepthTexture 부재 → 자기 _DepthTex 를 샘플.
    // 스캐폴드(Stencil·_ClipRect·GUIZTest·[PerRendererData] _MainTex·Transparent)는 CardCrumple_UI/DraftCardFoil_UI 와 동일.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DepthTex ("Depth", 2D) = "gray" {}
        _Tilt ("Tilt", Vector) = (0,0,0,0)
        _Amplitude ("Amplitude", Float) = 0.02
        _DepthCenter ("Depth Center", Float) = 0.5
        _DepthSign ("Depth Sign", Float) = 1
        _Persp ("Persp", Float) = 0.05
        _HiStrength ("Highlight Strength", Float) = 0.15
        _HiWidth ("Highlight Width", Float) = 0.25

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
            // lobby-background-parallax unit 0 — 3중 큐 산식은 모듈 .cginc 가 단일 소유(BackgroundDissolve 와 공유).
            #include "DepthParallax.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _DepthTex;
            fixed4 _Color;
            float4 _Tilt;
            float _Amplitude;
            float _DepthCenter;
            float _DepthSign;
            float _Persp;
            float _HiStrength;
            float _HiWidth;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                // clip/wPos 는 un-warped 원본 기준 — 사다리꼴은 클립공간 시각 트릭이라 rect 마스킹은 논리 위치로.
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;

                // Cue B — 클립공간 사다리꼴. delta 는 이미 _Persp·_Tilt 스케일 — 재곱 금지. _Tilt=0 → delta 0.
                o.vertex.xy += DepthParallaxTrapezoid(v.texcoord, _Tilt.xy, _Persp) * o.vertex.w;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Cue A — 뎁스 UV 패럴랙스(코어). dependent texture read 는 아래 _MainTex 샘플 1회뿐.
                float depth = tex2D(_DepthTex, i.uv).r;        // raw [0,1], 부호는 산식 안에서 힌지 뺄셈 후 적용
                float2 off  = DepthParallaxOffset(_Tilt.xy, depth, _DepthCenter, _Amplitude, _DepthSign);
                fixed4 col  = tex2D(_MainTex, i.uv + off) * i.color;                        // dependent read 1회

                // Cue C — 하이라이트 스윕. length(_Tilt) 게이트로 rest 에서 0. _Time 항 없음.
                col.rgb += DepthParallaxHighlight(i.uv, _Tilt.xy, _HiWidth, _HiStrength);

                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return col;
            }
            ENDCG
        }
    }
}
