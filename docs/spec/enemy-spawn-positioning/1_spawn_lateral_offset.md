# 1 — 스폰 측면 분산 (셀 내 sub-cell 오프셋)

## 목적

목표 2. 스폰 시 같은 lane 적들이 한 점에 겹치지 않도록, **스폰 셀 안에서** 진행방향 수직으로
상/중/하 sub-cell 위치에 분산 배치한다. cardinal flow field 가 그 오프셋을 전방으로 보존 → 행진 중에도 나란히.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/SpawnSpread.cs` — 신설. 순수 수학 + `SpawnSpreadMode` enum.
- `Assets/_Project/Scripts/Tests/EditMode/SpawnSpreadTests.cs` — 신설. 순수 함수 회귀.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 스폰 위치에 측면 오프셋 적용 + 슬롯 배정/config/리셋.

## 구현

- **`SpawnSpread`(순수)**: `SlotFraction(i,count,frac)`∈`[−half,+half]`, `half=clamp(frac,0,0.49)`
  (**셀 불변식 강제**). `Perpendicular(flowDir)`=XZ 단위 수직(0→폴백). `LateralOffset(...)`=`perp·frac·tileSize` → `float3(x,0,z)`.
- **`BattleBridge`**: `spawnWorldPos`(셀 중심) `+= ComputeSpawnLateralOffset(spawnIndex, spawnCell)`.
  다운스트림(`LocalTransform`·view) 무변경 — **한 점만 보정**.
  - `flowDir` = `_flowFieldSingleton` 의 `flow[spawnCell]`(초기 진행방향, 토폴로지 유도; 좌측 스폰 하드코딩 안 함).
  - `NextSpawnSlot`: Sequential=lane별 round-robin 커서 / Random=map seed 결정론 RNG.
  - config: `enabled` / `slots(1~5)` / `fraction(0~0.49)` / `mode`. 슬롯 커서·RNG 는 `BuildFlowField` 직후 리셋.

## 완료 기준

- compile 0 에러. EditMode `SpawnSpreadTests` green (분율 대칭/클램프/수직/셀 내).
- Play: 같은 lane 다수 스폰 시 상/중/하로 갈라져 나오고, 직진 구간에서 간격 유지(겹침 해소) 육안.
- `|오프셋|<0.5타일` → 셀 단위 시스템(타겟팅/goal/cell-trim) 거동 불변.

완료 확인 2026-06-26 — compile 0 에러 / EditMode `SpawnSpreadTests` 8/8 pass / Play: 상·중·하 분산 유지 사용자 확인.
가장자리 슬롯의 경계 근접(스프라이트 걸침)은 `spawnSpreadFraction`(0~0.49) 로 사용자 튜닝 — `|오프셋|<0.5타일` 이라 셀 내 유지(버그 아님).
코너 정중앙 추적은 후속(비주얼 수직추적). 스폰 분산 기본값(slots=3·fraction=0.33·Sequential)은 코드 default; 씬 튜닝값은 사용자 소관.
