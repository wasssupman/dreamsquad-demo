# Unit 11 — 6-Section Layout 으로 goal/spawn 배치 재정의

## 목적

기존 "goal 중앙 ±2, spawn 4분면 코너 zone" 정책을 폐기하고, 맵을 **6 section** 으로 분할해 1 section 을 goal, 나머지 5 section 중 N(2~4) 개에 spawn 을 배치하는 구조로 바꾼다. 시각·전술 다양성을 늘리고 goal 위치 자체에 variation 을 준다.

## 변경 대상

- 수정: `Assets/_Project/Scripts/Data/MapGrid/GoalSpawnPlacer.cs` — Pick 알고리즘 전체 교체.
- 수정: `Assets/_Project/Tests/EditMode/MapGrid/GoalSpawnPlacerTests.cs` — 중앙 goal 가정 테스트 교체.
- 수정: `docs/spec/map-grid-generation/README.md` — 공통 원칙의 goal/spawn bullet 갱신.

## 정책

- **Layout**: aspect-ratio adaptive — `W ≥ H` → 3 cols × 2 rows. `W < H` → 2 cols × 3 rows. 결과: 항상 6 section.
- **Section 인덱싱**: row-major. `section = row * cols + col`. 마지막 col/row 는 그리드 나머지 셀 흡수.
- **Section anchor**: 각 section 의 corner 중 맵 boundary 와 닿는 점.
  - corner section (4): map corner.
  - edge section (2): section 의 outer edge 의 셀-기준 midpoint.
- **Anchor zone**: section 내부 + anchor 로부터 Chebyshev ≤ `cornerZoneRadius`. 기존 SO 필드 재사용.
- **Goal**: 6 section 중 seed-random 1개. goal cell = 그 section 의 anchor zone 내 uniform random.
- **Spawn**: 남은 5 section 중 spawn count N(2~4) 개를 seed-shuffle 선택. 각 spawn = 해당 section anchor zone 내 distance 룰 만족하는 셀 (spawn↔goal ≥ EffectiveSpawnToGoalMinManhattan, spawn↔spawn ≥ SpawnToSpawnMinManhattan).
- N=1 차단 유지.

## API 시그니처 (변경 없음)

`GoalSpawnPlacer.Pick(ref Random, int2 gridSize, MapGridGenerationSettings, Allocator)` → `GoalSpawnResult`. 외부 호출자 (`MapGridGenerator`, 테스트) 영향 없음.

내부에 helper 추가:
- `GetLayout(int2 gridSize) → int2 (cols, rows)`
- `GetSectionAnchor(int sectionIdx, int2 layout, int2 gridSize) → int2`
- `GetSectionBounds(int sectionIdx, int2 layout, int2 gridSize) → (xmin, xmax, ymin, ymax)`
- `TryPickInZone(...)` — anchor zone 안의 candidate cell 셔플 후 distance 룰 통과 첫 셀

## 완료 기준

- [ ] 컴파일 0 ERROR.
- [ ] 기존 EditMode 회귀 0 (단, `Pick_GoalWithinChebyshev2OfCenter` 는 제거하고 `Pick_GoalInChosenSection` 으로 교체).
- [ ] 3 preset × 50 seed sweep 통과 (성공률 ≥ 90%).
- [ ] PlayMode: MapGrid + 임의 preset → goal 이 매번 다른 section 에서 나오는지 시각 확인 (스크린샷 2~3장).
- [ ] 확인 일자 + 커밋 해시 (구현 후 채움):
