# 2 · 홀로그램 효과 HLSL include + 월드 셰이더/머티리얼

## 목적

`UICordHologram.shader` 의 효과(그라데이션/스캔라인/플리커/펄스/글리치)를 공유 include 로 추출하고, 월드 스페이스(LineRenderer/SpriteRenderer)용 URP unlit 가산 셰이더 + 머티리얼을 신설한다. UGUI 쪽 비주얼 무변경.

## 변경 대상

- 신설: `Assets/_Project/Shaders/KeyringHologramCommon.hlsl`
- 신설: `Assets/_Project/Shaders/WorldCordHologram.shader`
- 신설: `Assets/_Project/Art/KeyringCordHologramWorld.mat` (`_LengthAxis=1`, 줄용) — 고리가 `_LengthAxis=0` 을 요구하면 `KeyringRingHologramWorld.mat` 추가 (셰이더는 1개)
- 수정: `Assets/_Project/Shaders/UICordHologram.shader` — include 사용으로 재구성
- 수정: `Assets/_Project/Data/Config/KeyringStyleHologram.asset` — 월드 머티리얼 슬롯 할당

## 구현

- **include 계약 (계약 6)**: self-contained 순수 `float` 함수만 — `fixed`/`half` 금지(URP HLSLPROGRAM 에 `fixed` 미정의), `_Time` 비참조(시간은 `t` 파라미터로 주입), UnityCG/URP 헤더 include 금지. CGPROGRAM(UGUI)·HLSLPROGRAM(URP) 양쪽에서 컴파일되어야 한다.
- UGUI 셰이더의 `fixed`→`float` 정밀도 변화는 "비주얼 무변경" 의 **허용 예외** (모바일 실기기에서 육안 차이 없음 전제).
- **월드 셰이더**: URP unlit, `Blend SrcAlpha One`(가산) + ZWrite Off + 투명 큐. `CBUFFER_START(UnityPerMaterial)` 로 SRP Batcher 호환. 프로퍼티는 UI 셰이더와 동일 세트(_ColorA/_ColorB/_Scan*/_Flicker*/_Pulse*/_Glitch*/_Intensity) + `_MainTex`(홀로 빔 텍스처 — wrap=Clamp 전제).
- **uv 전치**: UI 는 uv.y = 줄 길이 방향(세로 스트레치 Image), LineRenderer 는 textureMode 기본(Stretch)에서 uv.x = 길이 방향 — 월드 셰이더는 전치를 하드코딩하지 않고 `_LengthAxis` 프로퍼티(0=uv.y 길이, 1=uv.x 길이)로 파라미터화한다. 글리치의 "행 어긋남" 오프셋 축도 함께 따라간다. 줄(LR)=1, 고리(SpriteRenderer, UI 와 같은 세로 그라데이션 유지)=0 — 필요 시 머티리얼 2개(cord/ring)로 분리하되 셰이더는 1개.
- vertex color 는 양쪽 모두 곱해짐 — rig 쪽에서 white 강제(계약 7, unit 3).
- **가산 washout 확인을 이 unit 에서**: 밝은 배경(전투 보드 톤) 1장 오프스크린 렌더로 시인성 판정 — 문제면 블렌드 모드(예: premultiplied)나 _Intensity 를 여기서 확정. unit 3 으로 미루면 재작업 위험.

## 완료 기준

- compile 클린(양 셰이더), 콘솔 에러 0.
- **same-frame A/B**: 구 UGUI 셰이더 사본(임시 유지)과 include 재구성판을 같은 프레임에 나란히 오프스크린 렌더 → diff 로 무변경 확인 (`_Time` 동일 → 결정론). 확인 후 사본 삭제.
- 월드 머티리얼을 임시 쿼드/LineRenderer 에 입힌 오프스크린 렌더 1장 — 그라데이션/스캔라인이 길이 방향으로 흐르는지(축 전치 검증) + 밝은 배경 washout 판정.
