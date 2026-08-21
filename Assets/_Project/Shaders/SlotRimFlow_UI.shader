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
        _RimThickness ("Rim Thickness (half-height units)", Range(0.01, 0.5)) = 0.095
        _Speed ("Flow Speed (loops/sec)", Float) = 0.35
        _Bands ("Band Count", Range(1, 6)) = 2
        _Tail ("Tail Length (0..1 of segment)", Range(0.05, 1)) = 0.65
        _Head ("Head Rise (0..1 of segment)", Range(0.01, 0.5)) = 0.12
        _Strength ("Band Strength", Range(0, 4)) = 2.6
        _BaseAlpha ("Base Ring Alpha", Range(0, 1)) = 0.40
        _Glow ("Glow Spread (half-height units)", Range(0, 0.5)) = 0.14
        _GlowStrength ("Glow Strength", Range(0, 1.5)) = 0.65
        _GlowInner ("Inward Glow Scale", Range(0, 1)) = 0.5
        _CoreBoost ("Band Hot Core (white mix)", Range(0, 1)) = 0.75
        _Bleed ("Quad Bleed (half-height units)", Range(0, 0.5)) = 0.09

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
            float _Head;
            float _Strength;
            float _BaseAlpha;
            float _Glow;
            float _GlowStrength;
            float _GlowInner;
            float _CoreBoost;
            float _Bleed;

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
                //    quad 는 셀보다 _Bleed 만큼 크다(글로우가 셀 밖으로 번질 자리) — 상자를
                //    그만큼 줄여야 링이 여전히 **셀 가장자리**에 앉는다. 안 줄이면 링이 통째로
                //    바깥으로 밀려 포트레이트에서 뜬다.
                float2 ext = max(float2(aspect, 1.0) - _Bleed, 0.02);
                float sdf = RoundedBoxSDF(q, ext, _Radius);
                float inside = -sdf;                       // >0 = 내부, 값 = 경계로부터의 거리
                float aa = fwidth(inside) + 1e-4;
                float ring = smoothstep(0.0, aa, inside)
                           * smoothstep(_RimThickness, _RimThickness - aa, inside);

                // ② 글로우: 링 띠([0, _RimThickness])에서 **안팎 양쪽으로** 번지는 헤일로.
                //    하드 링 하나만 있을 때보다 훨씬 뜨겁게 읽힌다.
                //
                //    거리는 **하나**로 잰다(링 띠에서 얼마나 떨어졌나). 안팎을 각자 거리로 재면
                //    바깥 항이 셀 내부 전체에서 1 이 돼 칸이 통째로 물든다 — max(sdf, 0) 은
                //    "링 옆"이 아니라 "밖이 아님"이기 때문이다. 안팎 구분은 **계수**로만 한다:
                //
                //    (바깥 outerFade) quad 는 셀 밖으로 _Bleed 밖에 없어 지수 감쇠가 다 끝나기
                //      전에 잘린다 — 그러면 헤일로 바깥선이 **직사각형으로 딱 끊겨** 사각 판때기가
                //      한 장 더 붙은 것처럼 보인다. 여백 끝에서 0 이 되는 창으로 막는다.
                //    (안쪽 _GlowInner) 같은 세기로 들어오면 포트레이트를 덮는다. 절반으로 죽인다.
                float distFromBand = max(max(sdf, inside - _RimThickness), 0.0);
                float g = exp(-distFromBand / max(_Glow, 1e-4));
                float innerMask = saturate((inside - _RimThickness) / max(aa, 1e-5));
                float outerFade = saturate(1.0 - max(sdf, 0.0) / max(_Bleed, 1e-4));
                float glow = g * lerp(outerFade, _GlowInner, innerMask) * _GlowStrength;

                // ③ 도는 밴드: 중심 기준 각도를 시간으로 밀고, 각 구간 머리에서 꼬리로 감쇠.
                float ang = atan2(q.y, q.x) * 0.15915494 + 0.5;   // 1/(2pi), 0..1
                // 밴드 수는 **정수로 스냅**한다. 2.5 같은 값이면 각도 wrap 지점(ang 0↔1)에서
                // 마지막 밴드가 잘려 이음매가 보인다 — 슬라이더가 연속값이라 밟기 쉬운 함정이다.
                float bands = max(floor(_Bands), 1.0);
                float u = frac(frac(ang - _Time.y * _Speed) * bands);
                // u = 밴드 머리로부터의 거리(0 = 머리). 꼬리는 뒤로 감쇠하고, **머리 바로 앞
                // (u→1)에서는 짧게 상승**한다. 이 상승이 없으면 u 가 1→0 으로 감기는 자리에서
                // 밝기가 0→1 로 튀어 링을 가로지르는 **직선 절단**으로 보인다 — 글로우와 흰
                // 코어가 붙으면서 그 이음매가 눈에 띄게 됐다(원래도 있던 불연속).
                float fall = saturate(1.0 - u / max(_Tail, 1e-3)); fall *= fall;
                float rise = smoothstep(1.0 - _Head, 1.0, u); rise *= rise;
                float flow = max(fall, rise);
                // 밴드 머리의 좁고 뜨거운 코어. 알파는 아래 saturate 에서 1 에 물리므로 더 세게
                // 해도 안 밝아진다 — **색을 흰색 쪽으로 밀어** 밝기를 만든다(불꽃이 지나가는 머리).
                float coreFall = saturate(1.0 - u / max(_Tail * 0.3, 1e-3));
                float coreRise = smoothstep(1.0 - _Head * 0.6, 1.0, u);
                float core = max(coreFall, coreRise);
                core = core * core * core;

                half4 col = _RimColor * IN.color;
                col.rgb = lerp(col.rgb, half3(1.0, 1.0, 1.0), core * _CoreBoost);
                col.a *= saturate(ring + glow) * saturate(_BaseAlpha + flow * _Strength);

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
