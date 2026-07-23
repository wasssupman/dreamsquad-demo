# 4. MapGrid 절차 생성 폴백 체인 삭제

## 목적

`MapGridBattleAdapter.Build` 는 usable authored doc 이면 `ToGeneratedMap` 을, 아니면 `MapGridGenerator.Generate`(절차)를 탄다. 풀 5장이 전부 authored 라 절차 분기는 실게임에서 한 번도 안 돈다. 이 폴백을 hard-fail 로 바꾸고 생성 체인 전체를 삭제한다. (painter+authored 로 대체된 **직전 절차 생성** 세대.)

## 변경 대상

- 수정: `Assets/_Project/Scripts/Data/MapGrid/MapGridBattleAdapter.cs` — 절차 폴백 제거, unusable doc → 명확한 예외(hard-fail)
- 수정: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `mapGridSettings`(:38) 필드 + adapter 인자 정리, **그리고 이에 의존하는 `SetGoalEdgeOnly`(:1783)/`CurrentGoalEdgeOnly`(:1789) 동반 제거**(유닛 2 에서 미룬 쌍 — 리뷰: 이 둘이 mapGridSettings 참조라 필드 제거와 같은 유닛에서 빼야 컴파일)
- 삭제(전체, `Data/MapGrid/`): `MapGridGenerator.cs`·`IncrementalPathBuilder.cs`·`PathRouter.cs`·`PathBuildResult.cs`·`GoalSpawnPlacer.cs`·`GoalSpawnResult.cs`·`CellClassifier.cs`·`MapGridValidator.cs`·`MapGridIndex.cs`·`MapGridPreset.cs`·`MapGridGenerationSettings.cs` (+meta)
- 삭제: `Assets/_Project/Editor/MapGrid/MapGridDebugWindow.cs` + **빈 `Editor/MapGrid/` 폴더+meta**(리뷰 m8)
- 삭제(에셋): `Assets/_Project/Data/Maps/MapGridGenerationSettings_Default.asset`(실경로 주의)
- 판정: `MapGenerationFailedException.cs` — hard-fail 신호로 재사용(유지) 또는 표준 예외로 대체 후 삭제.

## 테스트 (리뷰 반영 — 폴더 통삭 금지, 파일별로)

`Tests/EditMode/MapGrid/` 실 파일 9종:
- **삭제(7)**: `MapGridGeneratorTests`·`IncrementalPathBuilderTests`·`GoalSpawnPlacerTests`·`MapGridValidatorTests`·`MapGridIntegrationTests`·`MapGridSeedSweepTests`·`MapGridGenerationSettingsTests`
- **유지(1)**: `MapDocumentRoundTripTests.cs` — keep-set `MapDocument`+`ToGeneratedMap`/`WriteToDocument` 회귀. 삭제 타입 0 참조(리뷰 CONFIRM) → **보존**
- **재작성(1)**: `MapGridBattleAdapterTests.cs` — 5개 테스트 전부 구 시그니처(`Build(seed,settings,doc,int2?)`/절차 폴백/MinGridDimension)라 무효. 신 시그니처용 슬림 2케이스로 교체: usable-doc → `ToGeneratedMap` 동등, unusable-doc → throw. (안 하면 단순화 adapter 무테스트)

## 구현

1. **adapter 단순화**: `Build(int, MapGridGenerationSettings, MapDocument, int2?)` → `Build(MapDocument doc)`. usable 이면 `MapDocumentBuilder.ToGeneratedMap`, 아니면 `throw`(명확 메시지). `PickGridSize`/`ClampGridSize`/`MinGridDimension` 제거(painter 무의존 확인, MinGridDimension 유일 비-체인 소비처는 유닛 1 삭제된 MapSettingsPanelView).
2. **BattleBridge**: adapter 호출부(≈916)를 새 시그니처로, `mapGridSettings` 필드+씬 참조 + GoalEdgeOnly 쌍 제거.
3. 체인 11 `.cs` + meta 삭제(참조: `MapGridIndex`/`CellClassifier`/`MapGridValidator` 는 체인+디버그창+삭제테스트에서만 — 리뷰 CONFIRM). 디버그창·해당 테스트 함께 삭제하면 무참조.
4. `MapGridGenerationSettings_Default.asset` 삭제, 씬 필드 정리(격리 편집). 빈 Editor/MapGrid 폴더 제거.

## 계약

- **usable doc → 동작 100% 불변**(여전히 `ToGeneratedMap`). 바뀌는 건 unusable 시: 조용한 절차 폴백 → hard-fail.
- keep: `MapGridBattleAdapter`(단순화)·`MapDocumentBuilder`·`MapDocument`·`MapDocumentPool`·`MapPoolSelect`·`MapConnectivity`·`GeneratedMap`·`MapTileType`·`MapDocumentRoundTripTests`.

## 완료 기준

- [x] adapter=doc 전용(`Build(MapDocument)`, unusable → `MapGenerationFailedException` hard-fail — 예외 타입도 메시지 ctor 로 단순화·재사용), 체인 11종 + 디버그창 + MapGrid 테스트 7종 삭제
- [x] `MapDocumentRoundTripTests` 유지, `MapGridBattleAdapterTests` 신 시그니처로 재작성(usable=ToGeneratedMap 동등 / unusable=throw, 2케이스 통과)
- [x] `mapGridSettings` 필드/에셋 + GoalEdgeOnly 쌍 제거, 씬 재저장 델타 1줄(mapGridSettings 고아 필드만) 검증, GUID 잔여 참조 0, 빈 Editor/MapGrid 폴더 제거. BattleBridgeDraftMapTests 의 유닛 2 임시 settings 주입도 제거
- [x] compile 0 error, EditMode green — connectivity 가드도 무조건 검사로 단순화(usable 통과 후엔 validator-backed 경로가 존재하지 않음)
- [ ] (사용자) 스쿼드 Play — 5장 풀 맵 로테이션·렌더·pathing 정상(usable 경로 무변화 실증)

확인 2026-07-23 — EditMode 1250 중 1248 green(0 fail). 사용자 Play 확인 대기.
