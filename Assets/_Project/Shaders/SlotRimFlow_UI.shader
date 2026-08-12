// defender-board-limit 1 — 트레이 셀 "출전 중" 테두리 순환 이펙트.
//
// UICordShine 골격(UI/Default 기반 → 스텐실·마스크 클립 호환)에 두 계산만 얹는다:
//   ① 둥근 사각 **테두리 마스크** — SDF 로 셰이더가 직접 그린다. 그래서 UiRoundedSprite 로
//      테두리 텍스처를 굽지 않고 Image.sprite 없이(흰 1x1) 쓸 수 있다.
//   ② 셀 중심 기준 **각도를 도는 밴드** — 꼬리는 각거리 감쇠. 이것이 "휘몰아침".
//
// uv0 만 읽는다 — Canvas.additionalShaderChannels 를 건드릴 필요가 없다.
// 쿨타임 림 글로우(제자리 호흡 = 밝기만)와 **움직임 문법**으로 구분된다.
Shader "Wassup/UI/SlotRimFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _RimColor ("Rim Color", Color) = (0.55, 0.9, 1, 1)
        _Aspect ("Cell Aspect (w/h)", Float) = 1.3
        _Radius ("Corner Radius (half-height units)", Range(0, 1)) = 0.28
        _RimThickness ("Rim Thickness (half-height units)", Range(0.01, 0.5)) = 0.08
        _Speed ("Flow Speed (loops/sec)", Float) = 0.35
        _Bands ("Band Count", Range(1, 6)) = 2
        _Tail ("Tail Length (0..1 of segment)", Range(0.05, 1)) = 0.55
        _Strength ("Band Strength", Range(0, 3)) = 1.2
        _BaseAlpha ("Base Ring Alpha", Range(0, 1)) = 0.22

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

            fixed4 _Color;
            float4 _ClipRect;

            fixed4 _RimColor;
            float _Aspect;
            float _Radius;
            float _RimThickness;
            float _Speed;
            float _Bands;
            float _Tail;
            float _Strength;
            float _BaseAlpha;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 둥근 사각 SDF. 반환 <0 = 내부. 좌표계는 "half-height 단위" —
            // y 는 -1..1, x 는 -aspect..aspect 라 두께/반경이 두 축에서 같은 픽셀로 보인다.
            float RoundedBoxSDF(float2 q, float2 halfExtents, float r)
            {
                float2 d = abs(q) - (halfExtents - r);
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float aspect = max(_Aspect, 0.01);
                float2 q = (IN.texcoord - 0.5) * 2.0;
                q.x *= aspect;

                // ① 테두리 마스크: 경계 안쪽 _RimThickness 만큼의 띠.
                float sdf = RoundedBoxSDF(q, float2(aspect, 1.0), _Radius);
                float inside = -sdf;                       // >0 = 내부, 값 = 경계로부터의 거리
                float aa = fwidth(inside) + 1e-4;
                float ring = smoothstep(0.0, aa, inside)
                           * smoothstep(_RimThickness, _RimThickness - aa, inside);

                // ② 도는 밴드: 중심 기준 각도를 시간으로 밀고, 각 구간 머리에서 꼬리로 감쇠.
                float ang = atan2(q.y, q.x) * 0.15915494 + 0.5;   // 1/(2pi), 0..1
                float u = frac(frac(ang - _Time.y * _Speed) * max(_Bands, 1.0));
                float flow = saturate(1.0 - u / max(_Tail, 1e-3));
                flow *= flow;

                half4 col = _RimColor * IN.color;
                col.a *= saturate(ring) * saturate(_BaseAlpha + flow * _Strength);

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
