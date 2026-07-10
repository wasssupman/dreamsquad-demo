Shader "Wassup/UI/BackgroundDissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _NoiseTex ("Dissolve Noise", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Range(0.5, 8)) = 2
        _Dissolve ("Progress", Range(0, 1)) = 0
        // 전환 모드: 0=노이즈 디졸브, 1=원형 확산, 2=수평 스윕, 3=크로스페이드
        _Mode ("Mode", Float) = 1
        // 1 이면 공간 필드를 뒤집어 파면 진행 방향을 반전(중심에서 밖으로 퍼짐).
        _Invert ("Invert Field", Float) = 0
        _Center ("Radial Center (UV)", Vector) = (0.5, 0.35, 0, 0)
        _MaxRadius ("Radial Max Radius", Float) = 1.2
        _Aspect ("Rect Aspect (w/h)", Float) = 1.7778
        _NoiseAmount ("Front Noise Wobble", Range(0, 0.5)) = 0.12
        _EdgeWidth ("Front Band Width", Range(0, 0.3)) = 0.08
        _EdgeColor ("Front Band Color", Color) = (1, 0.78, 0.42, 0.85)
        _EdgeEmission ("Front Band Emission", Range(0, 4)) = 1.5
        _TintColor ("Golden Tint Color", Color) = (1, 0.72, 0.38, 1)
        _TintStrength ("Golden Tint Strength", Range(0, 1)) = 0

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
            sampler2D _NoiseTex;
            fixed4 _Color;
            float _NoiseScale;
            float _Dissolve;
            float _Mode;
            float _Invert;
            float4 _Center;
            float _MaxRadius;
            float _Aspect;
            float _NoiseAmount;
            float _EdgeWidth;
            fixed4 _EdgeColor;
            float _EdgeEmission;
            fixed4 _TintColor;
            float _TintStrength;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, i.uv) * i.color;
                float noise = tex2D(_NoiseTex, i.uv * _NoiseScale).r;
                float progress = _Dissolve;

                float frontDist; // 파면으로부터의 거리 (>0 = 아직 보이는 쪽)
                float alphaFactor;

                if (_Mode < 2.5) // 0/1/2: 공간 필드 기반 cut 계열
                {
                    float field;
                    if (_Mode < 0.5)
                    {
                        field = noise; // 노이즈 디졸브
                    }
                    else if (_Mode < 1.5)
                    {
                        // 캐릭터 중심 원형 확산 (aspect 보정으로 진원 유지)
                        float2 d = i.uv - _Center.xy;
                        d.x *= _Aspect;
                        field = saturate(length(d) / max(_MaxRadius, 0.0001))
                              + (noise - 0.5) * _NoiseAmount;
                    }
                    else
                    {
                        field = i.uv.x + (noise - 0.5) * _NoiseAmount; // 수평 스윕
                    }

                    // 필드 반전: 파면이 중심→밖으로 진행(반전 없으면 밖→중심).
                    if (_Invert > 0.5) field = 1.0 - field;

                    // progress 0→아무것도 안 사라짐, 1→전부 사라짐 보장 리매핑
                    float margin = _EdgeWidth + _NoiseAmount + 0.02;
                    float threshold = progress * (1.0 + 2.0 * margin) - margin;
                    frontDist = field - threshold;
                    alphaFactor = smoothstep(0.0, 0.012, frontDist);
                }
                else // 3: 크로스페이드 (파면 없음)
                {
                    frontDist = 1.0;
                    alphaFactor = 1.0 - progress;
                }

                // 파면 밴드 글로우 (cut 계열 전용, 보이는 쪽 가장자리)
                float band = (1.0 - smoothstep(0.0, _EdgeWidth, abs(frontDist))) * _EdgeColor.a;
                color.rgb = lerp(color.rgb, _EdgeColor.rgb * _EdgeEmission, band);

                // 골든아워 전체 틴트: 진행 중간에 최대 (sin 커브)
                float midCurve = sin(saturate(progress) * 3.14159);
                color.rgb = lerp(color.rgb, _TintColor.rgb, midCurve * _TintStrength);

                color.a *= alphaFactor;
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return color;
            }
            ENDCG
        }
    }
}
