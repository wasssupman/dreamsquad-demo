Shader "Wassup/UI/DraftCardFoil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0, 1)) = 0.5
        _Tilt ("Tilt", Range(-1, 1)) = 0
        _Speed ("Speed", Range(0, 4)) = 1
        _HueShift ("Hue Shift", Range(0, 1)) = 0

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
            fixed4 _Color;
            float _Intensity;
            float _Tilt;
            float _Speed;
            float _HueShift;
            float4 _ClipRect;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 Hash22(float2 p)
            {
                float n = Hash21(p);
                return frac(float2(n, Hash21(p + n + 17.13)));
            }

            float CellValue(float2 p, out float cellId, out float edgeDistance)
            {
                float2 baseCell = floor(p);
                float2 local = frac(p);

                float closest = 10.0;
                float second = 10.0;
                cellId = 0.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 cell = baseCell + offset;
                        float2 siteDelta = offset + Hash22(cell) - local;
                        float distSq = dot(siteDelta, siteDelta);
                        if (distSq < closest)
                        {
                            second = closest;
                            closest = distSq;
                            cellId = Hash21(cell);
                        }
                        else if (distSq < second)
                        {
                            second = distSq;
                        }
                    }
                }

                float closestDist = sqrt(closest);
                float secondDist = sqrt(second);
                edgeDistance = secondDist - closestDist;
                return closestDist;
            }

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
                float2 uv = i.uv;
                float time = _Time.y * _Speed;
                float2 centered = uv - 0.5;

                float2 movingLight = normalize(float2(
                    sin(time * 0.82 + _HueShift * 6.28318) + _Tilt * 0.65,
                    cos(time * 0.63 + _HueShift * 3.14159) + 0.42));
                float lightSweep = dot(centered, movingLight);

                float2 grooveDirA = normalize(float2(0.86, 0.50));
                float2 grooveDirB = normalize(float2(-0.42, 0.91));
                float grooveA = 0.5 + 0.5 * sin(dot(uv, grooveDirA) * 860.0 + lightSweep * 8.0);
                float grooveB = 0.5 + 0.5 * sin(dot(uv, grooveDirB) * 640.0 - lightSweep * 7.0);
                float microGroove = pow(max(grooveA, grooveB), 18.0);

                float broadSheen = pow(saturate(1.0 - abs(lightSweep - sin(time * 0.25) * 0.22) * 2.6), 2.4);
                float softCloud = 0.5 + 0.5 * sin((uv.x * 3.1 + uv.y * 2.2 + time * 0.10 + _HueShift) * 6.28318);
                float foilBody = saturate(0.16 + broadSheen * 0.55 + microGroove * 0.16 + softCloud * 0.08);
                float diffractionPhase = lightSweep * 1.35 + microGroove * 0.18 + softCloud * 0.12 + _HueShift;
                float prism = frac(diffractionPhase + time * 0.06);
                float3 rainbow = 0.5 + 0.5 * sin(6.28318 * (prism + float3(0.00, 0.33, 0.66)));
                float3 filmBase = float3(0.88, 0.93, 1.0);

                float2 grainUv = uv * float2(130.0, 190.0);
                float grain = Hash21(floor(grainUv));
                float grainMask = smoothstep(0.88, 0.995, grain);
                float grainPrism = frac(grain + lightSweep * 0.36 + _HueShift);
                float3 prismGrain = 0.5 + 0.5 * sin(6.28318 * (grainPrism + float3(0.03, 0.37, 0.70)));

                float inset = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float rim = smoothstep(0.055, 0.020, inset) * smoothstep(0.004, 0.022, inset);
                float rimSweep = pow(saturate(0.5 + 0.5 * cos((uv.x * 1.8 - uv.y * 1.1 + time * 0.42 + _Tilt * 0.35) * 6.28318)), 2.6);
                float3 rimColor = lerp(float3(0.92, 0.96, 1.0), rainbow, 0.55 + rimSweep * 0.35);

                float highlight = broadSheen;
                float3 color = lerp(filmBase, rainbow, saturate(0.34 + broadSheen * 0.36 + microGroove * 0.12));
                color = lerp(color, prismGrain, grainMask * 0.18);
                color = lerp(color, rimColor, rim * (0.42 + rimSweep * 0.38));

                float edge = smoothstep(0.0, 0.16, uv.x) * smoothstep(1.0, 0.84, uv.x)
                           * smoothstep(0.0, 0.14, uv.y) * smoothstep(1.0, 0.86, uv.y);

                float alpha = (0.06 + foilBody * 0.28 + highlight * 0.18 + grainMask * 0.02 + rim * (0.10 + rimSweep * 0.08)) * _Intensity * edge;

                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                return fixed4(color * i.color.rgb, alpha * i.color.a);
            }
            ENDCG
        }
    }
}
