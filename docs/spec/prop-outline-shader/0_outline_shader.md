# 0 — Prop Outline Shader (Sprite + Lit toggle + inner-stroke)

## 목적

모든 배경 프랍(SpriteRenderer 빌보드)에 드롭인 가능한 단일 스프라이트 셰이더. 텍스처는 SpriteRenderer 가 공급(`_MainTex`). 각 프랍의 현 룩(평면/라이팅)을 유지하면서 실루엣 안쪽 가장자리에 외곽선 스트로크를 그린다.

## 변경 대상

- `Assets/_Project/Shaders/Prop_Outline_Sprite.shader` (신규).

## 구현

셰이더명 `Wassup/Prop Outline (Sprite)`. 단일 `UniversalForward` 패스.

**Properties**
- `[MainTexture] _MainTex` (2D), `[MainColor] _BaseColor` (Color, 흰색).
- `_Cutoff` (0..1, 0.5) — 실루엣 판정 임계.
- `[Toggle(_LIT_ON)] _Lit` (0) — on 시 Blinn-Phong.
- `[Enum(Off,0,On,1)] _ZWrite` (1) — 컷아웃 깊이 정렬 보존.
- `[Toggle(_OUTLINE_ON)] _OutlineEnabled` (1), `_OutlineColor` (어두운 톤), `_OutlineWidth` (0..0.2, 0.05, **상대 두께**).

**렌더 상태**: Tags `RenderType=Transparent`, `Queue=Transparent`, URP. `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite [_ZWrite]`, `Cull Off`.

**Vertex**: OS→WS→HClip. uv(`TRANSFORM_TEX`), 정점 COLOR, positionWS, normalWS(스프라이트 노멀 없으면 (0,0,-1) 폴백), viewDirWS, fog.

**Fragment**:
1. `half4 tex = SAMPLE(_MainTex,uv) * IN.color * _BaseColor; half a = tex.a;`
2. `baseRGB = tex.rgb;` `#ifdef _LIT_ON` → `UniversalFragmentBlinnPhong`(albedo=tex.rgb, spec=0)로 교체.
3. **내부 스트로크(alpha erosion)**: `#ifdef _OUTLINE_ON` 이고 `a >= _Cutoff` 면, 8방향 오프셋 알파의 **min**. `minA < _Cutoff`(이웃에 배경 있음=가장자리) → strokeMask=1.
   - 오프셋: `t = _MainTex_TexelSize.xy * (_OutlineWidth * minDim)`, `minDim=min(w,h)` (해상도 독립, 양축 동일 텍셀).
4. 합성: `rgb = lerp(baseRGB, _OutlineColor.rgb, strokeMask); rgb = MixFog(rgb, fog);` 반환 `half4(rgb, a)` (알파는 아트 그대로 → 소프트 엣지 보존).

**includes/pragmas**: `Core.hlsl`, `Lighting.hlsl`. 라이팅/포그/instancing multi_compile. shader_feature_local_fragment `_LIT_ON`, `_OUTLINE_ON`. CBUFFER `UnityPerMaterial`(`_MainTex_ST`,`_BaseColor`,`_Cutoff`,`_OutlineColor`,`_OutlineWidth`). `_MainTex_TexelSize` 는 CBUFFER 밖. `_ZWrite` 는 렌더상태 전용(HLSL 미선언).

## 계약

- 텍스처는 항상 `_MainTex`(SpriteRenderer) → 전 프랍 드롭인.
- 외곽선 = 내부 가장자리 스트로크(아트 위, 배경 무관 또렷). 반투명 내부(페인트 그림자)는 스트로크 안 함 → 발밑 링 없음.
- 두께는 해상도 독립 상대값. `_ZWrite` On 으로 컷아웃 정렬 보존.
- `_LIT_ON` off=평면, on=Simple Lit 룩.

## 완료 기준

- `ShaderUtil.ShaderHasError` false, console 0. 셰이더 `Wassup/Prop Outline (Sprite)` 등록.
- (unit 1) 적용 후 Play 스크린샷: 가장자리 스트로크가 배경 무관하게 보이고 발밑 링 없음, 베이스 룩 회귀 없음.

확인: 2026-06-29 셰이더 컴파일 OK (내부 스트로크 버전). 시각 확정 대기.
