# 4 — 상/중/하 슬롯 → 중앙 ± 연속 랜덤

## 목적

unit 1/3 후속(사용자 관찰). 뚜렷한 상/중/하 3레인은 인위적으로 보인다 → **중앙 기준 ± 작은 연속 랜덤
변주**로 교체. "정렬된 3줄" 대신 "중앙에 모인 자연스러운 흩뜨림". 키 큰 캐릭터 상단 보정(`topScale`)은 유지.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/SpawnSpread.cs` — 슬롯 API(`SlotFraction`) 제거,
  `FractionRange` + 연속 `LateralOffset(frac,…)` 도입. `SpawnSpreadMode` enum 제거.
- `Assets/_Project/Scripts/Tests/EditMode/SpawnSpreadTests.cs` — 연속 API 회귀로 교체.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `spawnSpreadSlots`/`mode`/슬롯 커서/`NextSpawnSlot` 제거,
  스폰마다 `FractionRange` 에서 연속 랜덤 추출.

## 구현

- `FractionRange(fraction, topScale)` → `[−fraction, +fraction·topScale]` (둘 다 `MaxHalfFraction` clamp).
- `ComputeSpawnLateralOffset`: `frac = rng.NextFloat(range)`, `offset = LateralOffset(frac, tile, flow)`.
  `LateralOffset` 가 frac 을 `±0.49` 로 clamp → **셀 불변식(`|오프셋|<0.5·tile`) 보장**.
- config: `spawnSpreadEnabled` / `spawnSpreadFraction`(기본 0.2) / `spawnSpreadTopScale`(기본 0.5). `slots`·`mode` 삭제.
- 결정론: `_spawnSpreadRng`(map seed, 빌드마다 리셋) 유지. lateral 축 = `flow[spawnCell]` 수직(불변).

## 완료 기준

- compile 0 에러. EditMode `SpawnSpreadTests` green (범위/clamp/수직/셀 내).
- Play: 같은 lane 적이 중앙에 모이되 미세 변주로 안 겹치고, 뚜렷한 3레인 느낌이 없음 육안.

완료 확인 2026-06-29 — compile 0 / EditMode `SpawnSpreadTests` 10/10 / Play 측정(execute_code): 적 13마리 중
10마리가 spread 범위(avg z 0.175) 내 — 중앙±변주 정상. off-tile 이상치 3마리는 코너 엣지-허깅(±0.49)으로
판명(spread 상한 0.1·넉백 무관) → unit 5(lane-centering)에서 처리.
