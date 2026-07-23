# 5. Handoff Summary — map-pipeline-cleanup

## Commit

- unit 0 `65d7c7c0` orphan 에셋 4종 · unit 1 `8b41fd04` 디버그 맵설정 UI(-744줄) · unit 2 `83505375` BattleBridge legacy 소스 · unit 3 `78809293` pre-MapGrid 생성기(-1,044줄) · unit 4 `b7d570c6` MapGrid 절차 폴백 체인(-2,616줄)
- 유닛 3·4 는 재커밋본(구 eb0834c3/5503a8c3 은 병행 세션 무관 파일이 스테이징에 쓸려 들어가 대체 — 내용 동일, 오염만 제거)

## Implemented

- **맵 생산 경로가 하나로 줄었다**: `mapPool → MapGridBattleAdapter.Build(doc) → ToGeneratedMap`. legacy 두 세대(pre-MapGrid 절차 + MapGrid 절차 폴백)와 그걸 살려두던 디버그 UI 전부 제거.
- **hard-fail 전환**: unusable 문서 → `MapGenerationFailedException`(메시지 ctor 로 단순화) → LogError + 판 정지. 조용한 절차 폴백 은퇴.
- **가드 pool 화**: `ActiveDeck==null || map==null` 3곳 → `mapPool` 가드. `SpatialPlacementCheck` 의 param shadow `map` 은 불변.
- **keep-set 무손상**: `BuildFallbackLinear`(connectivity 실패 안전망, goals 세팅 포함)·`DesignateDeco`(데코 미지정 문서맵 커빙)·`MapConnectivity`·`MapDocument(Builder/Pool)`·`MapPoolSelect`(토너먼트 시드 3분기) 전부 그대로.
- 씬: 패널 GO 제거 + 기존 missing-script 잔해 2건 정리 + 고아 직렬 필드 8종 드롭(전부 fresh-load 격리 편집·diff 전수 검증).

## Key Files

- `Scripts/Bridge/BattleBridge.cs` — BuildMapForBattle(≈850) 단일 경로·Fallback 상수·pool 가드
- `Scripts/Data/MapGrid/MapGridBattleAdapter.cs`(doc 전용)·`MapGenerationFailedException.cs`(메시지 ctor)
- `Scripts/Data/BattleMapBuilder.cs`(FallbackLinear only)·`ObstaclePlacer.cs`(DesignateDeco only)
- `Tests/EditMode/BattleBridgeDraftMapTests.cs`(라이브 풀 픽스처 5케이스)·`MapGrid/MapGridBattleAdapterTests.cs`(재작성 2케이스)

## Verified

- 유닛별 EditMode green: 1299→1297(u1) → 1297(u2) → 1288(u3) → **1248(u4)**, 전 유닛 0 fail. compile 0·콘솔 missing-reference 0·씬 diff 전수 검증.
- 삭제 전 GUID 전수 재확인 프로토콜 준수. 유일 잔여 참조는 `Assets/_Recovery/`(git 미추적 크래시 스냅샷) — 라이브 소비자 0 판정.
- **사용자 Play 미확인**: 스쿼드 매치 — 5장 풀 로테이션·렌더·pathing·점수 정상(usable 경로 무변화 실증).

## Notes (되돌리면 안 됨)

- **Fallback 상수(20×10/ver1/lane2)는 제거 시점 라이브 값과 동일** — FallbackLinear 동작 보존용. 바꾸면 connectivity 실패 시 안전망 모양이 달라진다.
- **hard-fail 은 사양**: unusable 문서에서 절차 생성으로 되돌리지 말 것. painter 가 bake 시 usable 을 강제한다 — unusable = authoring 버그.
- `RebuildDraftMap` 은 런타임 호출처 0 이지만 맵 라이프사이클 API 로 유지(BattleBridgeDraftMapTests 가 계약 커버). 카운터만 제거됨.
- manual-map-authoring 의 "mapDocument 최우선" 노브는 이 정리로 물리적으로 소멸(이미 stale — 풀이 항상 이겼음). 맵 강제 = `fixedMapSeed`(밸런싱 레퍼런스).
- `Assets/_Recovery/` 는 이 스펙이 안 건드림(미추적 사용자 영역) — 정리는 사용자 판단.

## Follow-up

- 사용자 Play: 스쿼드 매치 수판 — 로테이션(토너먼트 시드 3번 맵)·배치·pathing·점수 무회귀.
- README 후속 후보 계승: `season_S2_desert` 시즌 폐기 판단(별도 product 결정), DesignateDeco 슬림 테스트 신설.
- `MapPainterWindow` 는 체인 무의존이라 무영향(정밀 검증 완료) — bake 워크플로우 그대로.
