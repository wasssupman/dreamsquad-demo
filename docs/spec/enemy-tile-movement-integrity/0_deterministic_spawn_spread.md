# 0 — 결정론 스폰 분산 (③)

## 목적

스폰 측면 오프셋의 RNG(`_spawnSpreadRng`)를 제거하고 결정론 저불일치 수열로 대체한다. 시뮬레이션의 구조적 결정성(같은 입력 → byte-identical 거동)을 확보하되, 시각적 분산(한 점 겹침 방지)은 유지한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/SpawnSpread.cs` — `DeterministicFraction(int index, float spreadFraction, float topScale)` 추가. 헤더 코멘트 "랜덤" → "결정론".
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_spawnSpreadRng`(Random) 제거 → `_spawnSpreadCounter`(int). `ComputeSpawnLateralOffset` 가 `DeterministicFraction(_spawnSpreadCounter++, …)` 호출. 맵 빌드 시 `_spawnSpreadCounter = 0` 리셋(기존 RNG 리셋 지점).
- `Assets/_Project/Tests/EditMode/SpawnSpreadTests.cs` — `DeterministicFraction` 회귀 5종 추가.

## 구현

- 분율 = `lerp(min, max, frac(index · φ⁻¹))`, φ⁻¹ = 0.61803398875 (golden-ratio Weyl 저불일치 수열). `range = FractionRange(spreadFraction, topScale)`.
- RNG 없음 → 같은 index 항상 같은 분율. 연속 index 는 t-공간에서 0.382/0.618 만큼 떨어져 한 점 겹침을 줄인다.
- `index` 는 스폰 순번(맵 빌드마다 0 리셋). 셀은 perp 축 방향만 결정(기존과 동일).
- `|offset| < 0.5·tile` 불변식은 `LateralOffset` clamp 로 유지(기존).

## 완료 기준

- compile 0 에러.
- EditMode `SpawnSpreadTests` 전체 green (신규 5종 포함): 결정론(같은 index 동일값) / 범위 내([min,max]) / topScale 준수 / 연속 index 분리(>0.05) / index 0 = min.
- 기존 `FractionRange`/`Perpendicular`/`LateralOffset` 테스트 무회귀.
- Play 스폰 시 적이 셀 내 분산되며 한 점 적층 없음(육안, unit 3 통합 검증에서 확인).
