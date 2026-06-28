# 3 — 비대칭 분산: 상단 슬롯 압축

> ⚠️ 슬롯 기반 구현(`SlotFraction`)은 **unit 4(중앙 ± 연속 랜덤)로 대체**됨. `topScale` 개념만 unit 4 에 계승. 이 문서는 중간 단계 기록.

## 목적

unit 1 후속(사용자 관찰). 캐릭터가 **키 큰 스프라이트**라 틸트 화면에서 상단(뒤쪽) 슬롯의 적이
너무 높이 떠 어색하다. 분산 폭(`fraction`) 자체가 아니라 **상단 슬롯만 중앙으로 당겨 낮춘다**(비대칭).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/SpawnSpread.cs` — `SlotFraction`/`LateralOffset` 에 `topScale` 추가.
- `Assets/_Project/Scripts/Tests/EditMode/SpawnSpreadTests.cs` — 시그니처 갱신 + 비대칭 회귀.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `spawnSpreadTopScale` config + 전달.

## 구현

- `SlotFraction(..., topScale)`: `f>0`(상단)일 때만 `*= saturate(topScale)`. 하단·중단 불변.
  압축은 항상 더 작아지므로 `|오프셋|<0.5타일` 불변식 유지.
- config `spawnSpreadTopScale`(0~1, 기본 0.5). `ComputeSpawnLateralOffset` 가 전달. 라이브 튜닝.
- **부호**: 상단 = `+perpendicular`(마지막 슬롯). Play 에서 줄어드는 게 반대(하단)면 한 줄 flip.

## 완료 기준

- compile 0 에러. EditMode `SpawnSpreadTests` green (비대칭/대칭 both).
- Play: 상단 슬롯 적이 중앙 쪽으로 낮아져 자연스러움 육안.

완료 확인: (대기)
