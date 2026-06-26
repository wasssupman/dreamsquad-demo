# 4 — 원경 프랍 (외곽 링)

## 목적

외곽 터레인 링 셀에 원경 프랍을 저밀도로 흩뿌려 보드를 자연 환경(숲/바위 경계)으로 감싼다.
원경이라 그림자 OFF, 작은 프랍(꽃)은 제외.

## 변경 대상

- `Assets/_Project/Scripts/Data/PropData.cs` — `excludeFromDistantRing`, `distantRingWeight`
- `Assets/_Project/Scripts/Data/MapThemeData.cs` — `ringPropDensity`, `ringPropFalloffPerCell`
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `InstantiateRingProps` + `RingWeight`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 링 프랍 호출

## 구현

- `InstantiateRingProps(theme, playableSize, seed, castShadows, densityScale=1)`: 링 셀 순회, VisualPlan(sim) 밖이라
  `BackgroundPropPlacer` 대신 **별도 경량 scatter**.
- 셀마다 `density * falloff` 확률로 배치. `falloff = clamp01(1 - ringPropFalloffPerCell*(ringDist-1))` → 바깥일수록 성김.
- 가중치 선택은 **`RingWeight(p)` 누적 롤**: `distantRingWeight>=0` 이면 그것, 아니면 `placementWeight`.
  → **보드 위 분포는 그대로 두고 링에서만 별도 분포**(침엽수림처럼 나무 압도·돌 가끔). **`excludeFromDistantRing` 프랍(꽃)은 제외**.
- 위치는 `CellCenterToWorld`(grid 권위). 그림자: BattleBridge 가 `castShadows=false` 로 호출(원경 OFF). 시드 결정적.
- 시즌 백드롭 EdgeProps(12 앵커)와 공존(별개 레이어).

## 데이터 (forest 적용값 — 침엽수림 backdrop)

- `ringPropDensity=0.55`(빽빽), `ringPropFalloffPerCell=0.04`(링 전체가 숲).
- `distantRingWeight`: tree=14, rock_s/m/l=1 → 링의 ~82% 나무. 꽃 3종 `excludeFromDistantRing=true`.

## 완료 기준 (검증 2026-06-26)

- compile 0. 링 위 원경 프랍 ~230개(나무 위주), 빽빽한 침엽수림이 보드를 감쌈, 그림자 없음.
- 꽃은 링에 없음. 보드 안 근경 프랍과 자연스럽게 이어짐. 스크린샷 확인 완료.
