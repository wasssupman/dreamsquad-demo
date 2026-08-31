Shader "Wassup/PlacementRangeRing"
{
    // distance-based-range unit 5 — 공격 사거리의 **윤곽**.
    //
    // ⚠ **이 셰이더는 근사가 아니라 판정식 그 자체를 그린다.**
    //     sim:    |max(|Δ| − halfExtent, 0)| ≤ range + 0.5 + 대상반경
    //     여기:   sdRoundedBox(p, _HalfExtent, _Range) ≤ 0
    // 호출부가 `_Range` 에 **이미 0.5 를 더해서** 넣는다(`SkillMath.SelfBodyRadiusTiles`) —
    // 그 0.5 는 「한 칸의 몸 반지름」이고 rev 2 에서 뺄셈에서 덧셈으로 옮겨왔다.
    // 둘은 **같은 식**이다 — 둥근사각 SDF 의 정의가 `length(max(|p|−b, 0)) − r` 이기 때문이다.
    // 그래서 `_HalfExtent` 와 `_Range` 는 저작 값이 아니라 **판정 입력의 복사본**이고,
    // 호출부(TilemapMapView)가 사거리 술어에 넣는 값을 그대로 넣는다.
    //
    // ⚠ **`_Range` 하나짜리 원 셰이더로 만들지 말 것**(사용자 결정 2026-08-31). 오늘 전 유닛이
    // 1×1 이라 `_HalfExtent = (0, 0)` 이고 그래서 **진짜 원**이 그려지지만, 다칸 유닛이 들어오면
    // 반폭 `(w−1)/2` 가 들어가 **사각 변이 살아난다**(그게 그 유닛의 몸이다). 파라미터가
    // 판정식과 1:1 이면 저작만 바뀌고 여기는 그대로다.
    //
    // ⚠ **`_Range` 는 「점 대상」 기준이다.** 술어는 대상의 `bodyRadius` 도 더하는데(unit 3),
    // 호출부(링)는 그걸 모른다 — 링은 「이 자리에서 어디까지」를 말하고 대상별 몸은
    // 대상 마크(unit 7)가 말한다. `bodyRadius` 가 저작되면 마크가 링 밖에 뜰 수 있고 그건 의도다.
    //
    // 단위는 **타일**이다(1 = 한 칸). 쿼드는 `_QuadCells` 칸 정사각형이고 uv 0..1 → p ∈ [-Q/2, Q/2].
    //
    // ⚠ 다크 라이너가 필수다 — 밝은 배치 칸/보도 위에서 밝은 선만 있으면 사라진다
    // (아웃라인 스프라이트에 라이너가 구워져 있는 것과 같은 이유). 여기선 SDF 라 공짜다.
    //
    // ⚠ **URP Decal 금지**(모바일에서 렌더러 피처가 패스를 늘린다). 그냥 SpriteRenderer + 이 머티리얼.
    // ⚠ 런타임 생성 쿼드가 쓰므로 반드시 **.mat 에셋**으로 참조 — `Shader.Find` 는 빌드 스트리핑에
    //    걸린다(2026-07-15 출시 사고).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color       ("Ring Color", Color) = (0.55, 1.0, 0.25, 0.95)
        _LinerColor  ("Dark Liner Color", Color) = (0.05, 0.12, 0.02, 0.75)
        // 판정 입력의 복사본 — 호출부가 사거리 술어에 넣는 값과 **같아야 한다**.
        _HalfExtent  ("Half Extent (tiles, xy) — 1×1 이면 0", Vector) = (0, 0, 0, 0)
        _Range       ("Range (tiles)", Float) = 3
        _FillAlpha   ("Interior Fill Alpha", Range(0, 1)) = 0.12
        _Thickness   ("Ring Thickness (tiles)", Range(0.01, 0.4)) = 0.09
        _LinerWidth  ("Liner Width (tiles)", Range(0.0, 0.2)) = 0.05
        _Feather     ("Edge Feather (tiles)", Range(0.002, 0.08)) = 0.018
        _QuadCells   ("Quad Size (tiles) - C# 동기", Float) = 16
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

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            half4 _Color;
            half4 _LinerColor;
            float4 _HalfExtent;
            float _Range;
            float _FillAlpha;
            float _Thickness;
            float _LinerWidth;
            float _Feather;
            float _QuadCells;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // 둥근사각 SDF. **이것이 판정식이다** — `length(max(|p| − b, 0)) − r`.
            // 내부(음수) 항 `min(max(q.x,q.y), 0)` 은 상자 안쪽 거리를 채워 SDF 를 연속으로 만든다.
            // 경계(=0)는 상자 안쪽 항과 무관하므로 sim 과 정확히 같은 곡선이다.
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // uv 0..1 → 타일 단위 좌표(중심 원점)
                float2 p = (IN.uv - 0.5) * _QuadCells;
                float d = sdRoundedBox(p, _HalfExtent.xy, _Range);

                float half_t = _Thickness * 0.5;
                // 선: |d| 가 두께 절반 안. feather 로 부드럽게.
                float ring = 1.0 - smoothstep(half_t - _Feather, half_t + _Feather, abs(d));
                // 라이너: 선 **바깥**에만 깐다. 안쪽에 깔면 채움(옅은 라임)을 더럽힌다.
                float linerOuter = half_t + _LinerWidth;
                float liner = (1.0 - smoothstep(linerOuter - _Feather, linerOuter + _Feather, abs(d)))
                              * step(0.0, d);

                // 내부 채움 — **선과 같은 곡선이 경계다.** 칸으로 칠하면 실루엣이 계단이 되고
                // 「직선 변 + 깎인 모서리」로 읽힌다(사용자 지적 2026-08-31). 여기서 칠하면
                // 채움과 선이 **정의상 같은 모양**이라 삐져나옴도 구조적으로 사라진다.
                float fill = 1.0 - smoothstep(-_Feather, _Feather, d);

                // 라이너를 먼저 깔고 그 위에 채움, 그 위에 선 — 밝은 바닥에서도 선이 살아남는다.
                half4 c = _LinerColor;
                c.a *= liner;
                c.rgb = lerp(c.rgb, _Color.rgb, max(fill, ring));
                c.a = max(c.a, max(_Color.a * ring, _FillAlpha * fill));
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
