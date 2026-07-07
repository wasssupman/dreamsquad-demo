# 6 · 키링 홀로그램 스타일 (SF)

## 목적

unit 5 의 로프+골드 스타일을 SF 홀로그램으로 교체한다: 가산 발광 빔 줄 +
홀로 링, 시안→마젠타 그라데이션, 스캔라인, 플리커, 이동 펄스, 글리치.
로프 스타일 에셋은 보존(7ba9a285) — SO 슬롯 교체만으로 스타일 스위칭 가능.

## 변경 대상

- 신설: `Assets/_Project/Shaders/UICordHologram.shader`
- 신설: `Assets/_Project/Sprites/Keyring/keyring_cord_holo.png`, `keyring_ring_holo.png`
  (그레이스케일 빔/링 — 색은 셰이더 그라데이션이 입힘)
- 신설: `Assets/_Project/Art/KeyringCordHologram.mat`
- 수정: `LobbyKeyringSettings.cs` — `ringMaterial` 슬롯 추가 (고리도 발광 머티리얼)
- 수정: `LobbyKeyringDrag.cs` — BuildRig 에서 ringMaterial 적용 (1줄)
- 수정: `LobbyKeyringSettings.asset` — 홀로 스프라이트/머티리얼 할당

## 구현

- **셰이더**: UGUI 스텐실/클립 골격 + **가산 블렌드**(`Blend SrcAlpha One`) 발광.
  - 그라데이션: uv.y 로 `_ColorA`(시안)→`_ColorB`(마젠타) lerp
  - 스캔라인: `sin(uv.y·_ScanDensity − t·_ScanSpeed)` 감쇠 줄무늬
  - 플리커: 시간 해시 기반 전체 밝기 미세 떨림 (`_FlickerSpeed/Strength`)
  - 펄스: 줄을 따라 흐르는 밝은 밴드 (`_PulseSpeed/Width/Strength`)
  - 글리치: 행 해시가 임계 초과 시 해당 행 uv.x 어긋남 (`_GlitchAmount/Speed`)
  - `_Intensity` 전체 배율
- **텍스처**: 그레이스케일 — 줄 = 중심 코어 라인 + 소프트 글로우 폭 프로파일,
  링 = 얇은 코어 링 + 내외측 글로우 falloff. 색을 안 굽고 셰이더가 입혀
  팔레트 변경이 머티리얼 파라미터로 끝난다.
- **폴백 계약 유지**: ringMaterial 미할당 시 기본 UI 머티리얼 (기존과 동일).
- 에디터 일괄 셋업은 unit 5 와 같은 일회용 메뉴 스크립트 방식
  (execute_code 는 이 프로젝트에서 커맨드라인 한계로 불가 — handoff 참조).

## 완료 기준

- compile 클린, 콘솔 에러 0.
- 드래그 시 줄이 시안→마젠타 발광 빔 + 스캔라인/플리커/펄스/글리치,
  고리가 발광 홀로 링으로 보인다 (사용자 시각 확인).
- 로프 스타일로 되돌리기 = SO 슬롯 교체만으로 가능 (코드 무변경).

확인 2026-07-07 — 사용자 Play 통과 확인("좋다"). ropeLength 220 / cordAttachDrop 110
튜닝 확정. 커밋 `576f8047`.
