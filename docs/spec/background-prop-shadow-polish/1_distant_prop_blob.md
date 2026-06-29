# 1 — 원경 프랍 블롭 그림자

## 목적

외곽 링 원경 프랍은 현재 그림자가 없다(`InstantiateRingProps` 에 블롭 부착 누락). 근경과 동일한 접지 블롭을 부착해 떠 보이지 않게 한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `InstantiateRingProps` 인스턴스 생성 직후 `AttachPropBlob(inst, prop)` 호출.
- `docs/spec/tilemap-world-surround/4_distant_props.md` — "원경 그림자 OFF" → "원경도 접지 블롭 ON (background-prop-shadow-polish unit 1)" 으로 계약 갱신.

## 구현

- 라인 ~372 (`MapView.DisablePropDebugMarkers(inst);` 인접)에 근경과 대칭으로 `AttachPropBlob(inst, prop);` 추가.
- 별도 거리 dimming 없음 — 동일 블롭 경로 재사용. 블롭 size 는 `prop.visualScale` 기반이라 작은 원경 프랍은 자연히 작은 블롭.
- 주석의 "원경이라 그림자 OFF 기본" 문구 갱신.

## 완료 기준

- compile 0. Play → 외곽 링 원경 프랍 발밑에 블롭 그림자가 보인다(근경과 일관).
- 사용자 육안 통과. 통과 시 확인 일자 + 커밋 해시 추가.

확인: 2026-06-29 사용자 육안 통과 · 커밋 ee10b86. 링 나무 94/94 블롭 부착 확인. 스크린샷 `Assets/Screenshots/prop_shadow_far_forest.png`.
