# 0 — 바닥 그림자 receive 셰이더 + 머티리얼

## 목적

타일맵 바닥이 방향광 그림자를 받게 한다. **라이팅 없이 그림자 감쇠만** 곱해 플랫 색 유지.

## 변경 대상

- 신규: `Assets/_Project/Shaders/Tile_ShadowReceive.shader`
- 신규(에셋): `Assets/_Project/Art/TileShadowReceive.mat` (위 셰이더)
- 수정: `Assets/_Project/Scripts/Core/TilemapMapView.cs` — groundTilemap 에 머티리얼 적용 + `receiveShadows=true`
  (serialized 머티리얼 필드 또는 ConfigureGrid 에서 할당)

## 구현

셰이더 `Wassup/Tile_ShadowReceive` — **URP Sprite-Unlit 패턴 기반**:
- `Tags { RenderType=Transparent? }` — 타일은 불투명이면 Opaque/AlphaClip. 스프라이트 호환 위해
  `[PerRendererData] _MainTex`, vertex color(`COLOR`) 입력, `_BaseColor`(또는 _Color).
- `LightMode=UniversalForward` + 그림자 지시자:
  ```
  #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
  #pragma multi_compile _ _SHADOWS_SOFT
  #include ".../ShaderLibrary/Lighting.hlsl"
  ```
- vert: 월드 위치 → `GetShadowCoord(GetVertexPositionInputs(...))` → frag 로 전달.
- frag: `half s = MainLightRealtimeShadow(shadowCoord);` → `col.rgb *= lerp(_ShadowTint, 1, s)` 또는
  `col.rgb *= s` (그림자=어둡게). 알파는 스프라이트 알파. `_ShadowStrength` 로 세기 조절(데이터).
- 라이팅/NdotL **없음** — 그림자 영역만 어두워짐(플랫 유지).

머티리얼 `TileShadowReceive.mat`: 위 셰이더, `_ShadowStrength` 기본값.

TilemapMapView: groundTilemap.GetComponent<TilemapRenderer>() 에 `receiveShadows=true` +
`material = TileShadowReceive`. overlayTilemap 은 선택(마커는 그림자 안 받아도 됨).

> 핵심 검증 순서: ① 머티리얼만 바꿔 **타일이 정상 렌더되는지**(텍스처/색) 먼저 확인 → ② 그 다음 그림자 보임 확인.
> 스프라이트 텍스처가 안 잡히면 `_MainTex` 바인딩/PerRendererData 문제 → URP Sprite-Unlit 원본 구조에 맞춘다.

## 완료 기준

- compile/셰이더 에러 없음. 타일이 이전과 동일한 색/텍스처로 렌더(그림자 추가 전 회귀 없음).
- 씬에 임시 그림자 캐스터(아무 3D 큐브)를 두면 바닥에 그림자가 보임(다음 unit 의 빌보드 캐스터 전 PoC).
- 바닥이 방향광으로 음영지지 않음(플랫 유지) — 그림자 영역만 어둡다.
