# 5 — 모바일 예산 + 그림자 정책 + 카메라 합성

## 목적

모바일 비용을 제어하고, 그림자 정책(근경 cast / 원경 off / 모바일 전부 off)을 확정한다. 카메라/배경
합성(링 + 다크 페이드)이 프레임 안에 자연스러운지 검증.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `mobilePropBudgetScale` + 프랍 호출 배율
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `InstantiateRingProps(... densityScale)`

## 구현

- **그림자 정책 (기존 배선으로 이미 성립, 확정)**:
  - 근경(보드) 프랍: `InstantiateBackgroundProps(..., UseRealShadows)`. `UseRealShadows = useRealShadows && !isMobilePlatform`.
  - 원경(링) 프랍: 항상 `castShadows=false`.
  - → 데스크톱: 근경 cast / 원경 off. **모바일: 전부 off**(블롭 폴백은 유닛만 해당).
  - 프랍 그림자 머티리얼: `URP/Unlit + _ALPHATEST_ON`(ShadowCaster 실루엣). `Prefabs/Props/test/mat/`.
- **모바일 프랍 예산**: `mobilePropBudgetScale`(serialized, 기본 0.5). 모바일에서만 적용:
  - 근경: `placements.GetRange(0, count*scale)` (앞쪽=중앙/가장자리 우선 보존, 필러 컷).
  - 원경: `InstantiateRingProps(..., ringScale)` → `density *= scale`.
  - 데스크톱/에디터(`isMobilePlatform=false`) 는 scale=1 → 무영향.
- **카메라/배경**: tilemap 모드는 시즌 스카이박스 미마운트(Legacy3D 전용) → 카메라 솔리드 다크 배경.
  링이 바깥으로 다크 페이드 → 배경에 자연 블렌딩. 수동 카메라(preset 비활성)로 보드 프레이밍 유지, farClip=100 충분.

## 완료 기준 (검증 2026-06-25)

- compile 0. 데스크톱 풀 카운트(근경 41 / 원경 58), 모바일 배율 0.5 → ~20/~29(로직 검증).
- 모바일 그림자 전부 off(UseRealShadows=false 경로). 링+페이드 프레임 내 합성 정상.

## 후속 후보

- tilemap 모드 시즌 스카이박스 마운트(현재 Legacy3D 전용 → 다크 페이드로 대체). 원경 horizon 강화 시.
- 실기기 프로파일링 후 `mobilePropBudgetScale`·`maxTilePropCount`·`ringPropDensity` 재튜닝.
- 프랍 그림자 strength/soft 튜닝(현재 tilemap-real-shadows 의 light/RP 설정에 종속).
