# 27. Place Seam Reduction (Light)

## 목적

audit V-007 재평가에서 확인된 잔존 결함:

- Place slab 이 여전히 **독립된 흰 조각들**로 읽힘. slab 사이 녹색 seam 이 보드 전체 grid 감을 강조. Env / Walk 가 연결감을 얻은 것과 달리 Place 만 tile-per-cell 로 분리.

본 spec 은 **코드 구조를 건드리지 않고** theme param + variant asset 재튜닝으로 seam 강조를 완화한다 (경량 a 방식). 결과가 부족하면 `28` (region mesh 리팩터) 로 전환.

## 전제

- `24`, `25`, `26` 완료.
- audit V-007 재평가 결과 screenshot (`Assets/Screenshots/audit/20260424_26/seed12345_game_full.png`) 에서 Place slab seam 이 주 잔존 시각 문제로 확인됨.

## 변경 대상

- `Assets/_Project/Map/Theme/forest/forest.asset` — `tileTopScale`, `placeEdgeOpacity`, `placeEdgeThickness`, variant 참조 재튜닝
- (선택) `Assets/_Project/Art/Theme/forest/tile_place_variant_*.png` — variant 톤 harmonize. 디자이너 재제작이 필요하면 본 spec 에서는 명도/채도 차이만 간단히 조절, 고품질 재제작은 별도
- `Assets/_Project/Scripts/Data/MapThemeData.cs` — 필요 시 기본값만. 새 필드 추가 금지.

## 조정 포인트

### 1. `tileTopScale` 상향
- 현재 추정값 0.86 (rev1 튜닝 기준)
- **0.95~0.98** 로 조정. Place top quad 가 셀 크기에 가까워져 slab 사이 녹색 seam 이 얇아짐.
- 너무 1.0 에 붙이면 인접 slab 끼리 z-fighting 가능 → 0.98 이 안전 상한.

### 2. `placeEdgeOpacity` / `placeEdgeThickness` 재튜닝
- 현재 값: opacity 0.38, thickness 0.10.
- edge fringe 가 seam 을 오히려 강조하는 효과가 있으면 **opacity 0.20~0.30** 로 낮추기.
- thickness 는 유지 (0.10).
- 반대로 edge 가 fade 로 묶는 효과를 내면 유지.

### 3. `placeTileVariants` 톤 harmonize
- 현재 variant a~d 의 명도/채도 차이가 크면 slab 간 seam 이 더 도드라짐.
- 디자이너 재제작 없이 해결 가능한 범위:
  - variant 를 4종 → 2~3종으로 줄여 harmony 확보 (한두 장 theme 에서 뺌)
  - 혹은 `RuntimeMaterialFactory` 에서 tint 조정
- 본 spec 에서는 forest.asset 의 variant 참조만 재선정. 고품질 재제작은 별도.

### 4. Env fringe 침범 확인
- `11` 에서 Env region 간 blend fringe 가 들어간 상태. 이 fringe 가 Place 경계에 침범해 Place 외곽 톤을 흐리지 않는지 grep + 육안 확인.
- 침범하고 있으면 `MapView.BuildEnvironmentRegionSurface` 에서 region.cellCount 조건 또는 zone 경계 셀 제외 로직 확인 (수정 범위 내면 OK).

## 구현 가이드

1. Inspector 에서 `forest.asset` 조정 후 Play 확인 — 시각 튜닝 loop.
2. `tileTopScale` 0.95 로 시작 → 실제 화면 보고 0.96~0.98 범위에서 미세 조정.
3. `placeEdgeOpacity` 0.25 로 낮춰 seam 강조 완화.
4. variant 중 가장 튀는 1장을 빼고 재캡처. 효과 없으면 복원.
5. audit seed 12345 로 재캡처 → `Assets/Screenshots/audit/20260424_27/seed12345_game_full.png` 저장.
6. `VISUAL_AUDIT.md` 에 V-007 의 현재 상태 업데이트 (Mid → Low 가능).

## 완료 기준

- Play 재캡처에서 Place 영역이 **묶인 plate 영역** 으로 더 읽힘 (개별 slab grid 인상 완화).
- 인접 slab 사이 녹색 seam 이 눈에 띄지 않음 또는 현저히 감소.
- sorting 회귀 0 (26 의 결과 유지).
- Unity console error 0.
- `VISUAL_AUDIT.md` 의 V-007 상태가 현재 audit 결과로 갱신됨.

## 주의

- **region mesh 리팩터 금지**. param + asset 재선정만. 구조 변경이 필요해 보이면 `28` 로 전환.
- variant asset 재제작은 본 spec 범위 밖. 디자인 판단이 필요하면 별도 asset pass.
- `tileTopScale` 을 너무 올리면 Walk overlay 와 Place top 이 z-fighting 가능. Play 에서 flicker 확인.
- audit 재캡처는 **같은 seed / 같은 해상도 / 같은 카메라 프레이밍** 유지. 비교 가능성 필수.

## 다음 단계 분기

audit 재평가 결과:
- V-007 Low/해소 → 다음은 `17` 재작업(Poisson 제대로) 또는 palette refinement
- V-007 여전히 Mid/High → `28` (region mesh 리팩터) 로 전환

확인 일자: 2026-04-24 / 커밋 해시: 26b7b5d
