# 0. 맵 소스 sync 버그 수정 + fixedMapSeed 스위치

## 목적

씬은 `mapSource = MapGrid` 로 저장돼 있는데 실전은 구형 Legacy 생성기(1행/9행 고정 H자)가 돌던 원인을 제거하고, "매판 랜덤 기능은 유지하되 기본은 고정 시드" 요구를 스위치 하나로 만족시킨다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs`
- `Assets/_Project/Scripts/Core/DraftController.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

- **버그 원인**: `MapSettingsPanelView` 의 코드 기본값 `_selectedMapSource = Legacy` 가 `Initialize → PushAllToController` 로 매판 bridge 에 push 되어 씬 authoring(MapGrid)을 덮어썼다.
- **수정**: 패널 초기 소스를 씬 BattleBridge 값에서 sync (unit 2 에서 전 필드 hydrate 로 일반화됨).
- **fixedMapSeed**: `BattleBridge` 직렬화 필드. 비0 이면 `BuildMapForBattle` 의 맵 시드가 이 값으로 고정(매판 동일 맵), 0 이면 기존 `DeriveMapSeed(matchSeed)` 매판 랜덤. 웨이브/기믹/픽업 시드 스트림은 무영향(맵만 분리 고정).

## 완료 기준

- [x] compile 0 errors
- [x] Play 진입 시 MapGrid 생성기 가동 확인 (6섹션 앵커 구조 맵)
- [x] 같은 시드 → 같은 맵, `fixedMapSeed=0` → 매판 랜덤 복귀 확인

확인 2026-07-19 — 커밋 `acff0abc`
