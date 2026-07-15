# 2 — UGUI 뎁스 패럴랙스 셰이더

## 목적

3중 큐(뎁스 UV 패럴랙스 · 클립공간 사다리꼴 · 하이라이트 스윕)를 얹은 UGUI 프래그먼트 셰이더.
모든 큐가 `_Tilt` 게이트 → rest 완전 no-op.

## 변경 대상

- New: `Assets/_Project/Modules/DepthParallax/Shaders/DepthParallax_UI.shader`
  (Shader 이름: `"Wassup/UI/DepthParallax"`)
- New: `Assets/_Project/Modules/DepthParallax/Shaders/DepthParallax_Default.mat`

## 구현

- **스캐폴드는 `DraftCardFoil_UI.shader` / `CardCrumple_UI.shader` 를 그대로 복제**: `[PerRendererData]
  _MainTex`, 6개 `_Stencil*`/`_ColorMask` 프로퍼티 + `Stencil{}` 블록, `Tags{Queue=Transparent,
  IgnoreProjector, RenderType=Transparent, PreviewType=Plane, CanUseSpriteAtlas}`, `Cull Off/Lighting
  Off/ZWrite Off/ZTest [unity_GUIZTestMode]/Blend SrcAlpha OneMinusSrcAlpha/ColorMask [_ColorMask]`,
  `float4 _ClipRect` + `col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect)`, worldPosition vert→frag.
  `CGPROGRAM`+`UnityCG.cginc`/`UnityUI.cginc`(URP HLSL 아님).
- **프로퍼티 추가**: `_DepthTex`(2D, "gray"), `_Tilt`(Vector, (0,0,0,0)), `_Amplitude`, `_DepthCenter`,
  `_DepthSign`, `_Persp`, `_HiStrength`, `_HiWidth`.
- **Cue A — 뎁스 패럴랙스(frag, 코어)**:
  ```
  float depth = tex2D(_DepthTex, i.uv).r;                              // raw [0,1], 부호 미적용
  float2 off  = _Tilt.xy * (depth - _DepthCenter) * _Amplitude * _DepthSign; // 힌지 항 전체에 부호
  fixed4 col  = tex2D(_MainTex, i.uv + off) * i.color;                 // dependent read 1회
  ```
  **`_DepthSign` 은 `(depth-_DepthCenter)` 뺄셈 *후* 힌지 항 전체에 곱한다.** raw 에 먼저 곱하면
  힌지 평면이 `[0,1]` 밖으로 밀려 near/far 반대 이동이 붕괴 → 극성 반전이 깨진다(중심 피벗 불변식 상실).
  unit 1 `UvOffset` 도 동일 순서(depthSign 인자 마지막)로 계약 일치.
- **Cue B — 클립공간 사다리꼴(vert, ortho 에서 회전감)**: 코너 부호를 UV0 에서 유도.
  ```
  float2 orig = v.texcoord*2-1;
  float2 p = orig;
  p.y *= 1 - _Persp*_Tilt.x*orig.x;
  p.x *= 1 - _Persp*_Tilt.y*p.y;
  o.vertex.xy += (p - orig) * o.vertex.w; // (p-orig)이 이미 _Persp·_Tilt 스케일 — 재곱 금지
  ```
  (RectTransform half-size 불필요. `_Persp≈0.03~0.08`. quad-local[-1,1]→클립 정확 스케일은
  구현 시 확정 — 위 `*w` 는 근사이며 unit 3 no-op diff 로 검증하며 조정. rest 에서 `_Tilt=0`→`p=orig`→delta 0.)
- **Cue C — 하이라이트 스윕(frag, `length(_Tilt)` 게이트)**: 틸트축 방향 밴드
  `spec = exp(-sqr((band-mag)/_HiWidth)) * _HiStrength * mag; col.rgb += spec;`. **`_Time` sheen 금지.**
- **rest no-op**: `_Tilt=0` → off=0, 사다리꼴 항=0, spec=0(`*mag`). 최종 UV 클램프 금지(오프셋만 필요시 클램프).
- 기본 머티리얼은 이 셰이더 + SO 기본값과 정합하는 초기값.

## 완료 기준

- 컴파일/셰이더 임포트 클린(`read_console`), 셰이더 variant 에러 없음.
- 에디터에서 머티리얼에 임의 스프라이트+뎁스 물려 `_Tilt` 를 흔들면 패럴랙스/사다리꼴/하이라이트
  육안 확인. `_Tilt=0` 이면 평범한 스프라이트.
- dependent texture read 가 1회(코드 리뷰로 확인 — `tex2D(_MainTex, uv+off)` 단일).
