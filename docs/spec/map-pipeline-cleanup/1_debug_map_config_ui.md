# 1. 디버그 맵 설정 UI + 맵-config 표면 제거

## 목적

legacy 맵 브랜치를 런타임에 살려두는 통로는 디버그 패널 `MapSettingsPanelView`(→ `DraftController` → BattleBridge)다. 이 패널·plumbing 을 제거해 이후 BattleBridge legacy API 를 무참조로 만든다.

> **리뷰 정정(2026-07-23)**: `MapSettingsPanelView` 는 `DraftController` 만 쓰는 게 아니다. `DraftView.cs`·`SquadPrepView.cs` 가 이 타입을 **`[SerializeField]` 필드로 들고 메서드를 호출**한다 → 타입 삭제 시 **즉시 컴파일 붕괴**(BLOCKER). 이 두 파일을 패널 삭제보다 **먼저** 정리해야 한다. 특히 `SquadPrepView` 는 라이브 아웃게임 스쿼드-준비 뷰다.

## 변경 대상 (순서 중요 — consumer 먼저)

1. 수정: `Assets/_Project/Scripts/UI/Draft/DraftView.cs` — `mapSettings` 필드(:24) + 호출부(:166/:174 `SetActive`, :188 `Initialize`) 제거
2. 수정: `Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs` — `mapSettings` 필드(:21) + 호출부(:44–48 `Initialize`/`SetActive`) 제거
3. 수정: `Assets/_Project/Scripts/Core/DraftController.cs` — 맵-config 전달 표면 제거(아래 목록)
4. 삭제: `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs` (+meta)
5. 수정: `Assets/_Project/Scenes/BattleScene.unity` — 패널 컴포넌트/오브젝트 + DraftView·SquadPrepView 의 `mapSettings` 바인딩(:555, :1127) 정리 (SquadPrepView 가 다른 씬에도 있으면 그 씬도)
6. 삭제/축소(무참조화되면): `Tests/EditMode/DraftControllerMapRebuildTests.cs`, `BattleBridgeDraftMapTests.cs` — legacy map-config/manual 경로만 검증하는지 확인 후 통삭제, 아니면 해당 케이스만

## DraftController 제거 표면 (리뷰 반영 — 전량)

- `SetMapSource` / `SetMapGridGridSize`(실명 `SetMapGridGridSizeOverride` 계열) / `SetGoalEdgeOnly` / `SetMapGenerationOptions` / `SyncMapStateFromBridge`
- getter: `SelectedMapSource` / `SelectedMapGridGridSize` / `SelectedMapGenerationOptions` / `BridgeGoalEdgeOnly`
- **누락됐던 것(추가)**: `SelectedMapPathShape`(:45), `SetMapPathShape(MapPathShape)`(:174), 그리고 **draft-confirm 내부 push** `TryConfirm()` 의 `battleBridge.SetMapGenerationOptions(SelectedMapGenerationOptions)`(:132)
- drafting 본연(유닛 드래프트/카드) 로직은 **불변**. 위 심볼들의 패널·삭제테스트 외 호출처 0 확인됨 → 안전 제거.

## 계약

- DraftController 에서 지우는 것은 **맵 설정 전달부만**.
- 이 유닛 후 BattleBridge 의 `SetMapSource`/`SetMapGridGridSizeOverride`/`SetGoalEdgeOnly`/`SetMapPathShape` 는 **완전 무참조**, `SetMapGenerationOptions` 는 GameManager(:261/:330)만 남는다(유닛 2 가 제거).

## 완료 기준

- [x] DraftView·SquadPrepView 에서 `mapSettings` 필드+호출 제거(SquadPrepView 는 무참조가 된 `draftController` 필드도 동반 제거), `MapSettingsPanelView.cs` 삭제
- [x] DraftController 맵-config 표면(:45/:132/:174 포함) 제거, drafting 로직 무변화 (using Unity.Mathematics/Wassup.Data.MapGrid 도 무참조화로 제거)
- [x] BattleScene 패널 와이어링 제거(다른 씬 참조 0 확인), missing-script/reference 경고 0 — **기존 잔해 2건(MapView·DraftView 의 사망 컴포넌트, 73a2efd1·e824ed4a 에서 스크립트만 삭제된 것)도 같은 저장에서 정리**
- [x] compile 0 error, EditMode green(삭제한 draft-map 테스트 제외)
- [x] (사용자) 스쿼드 매치 Play — 맵/배치 정상, 디버그 패널 부재 무영향 (확인 2026-07-23)

확인 2026-07-23 — EditMode 1298 중 1296 green(0 fail, DraftControllerMapRebuildTests 3케이스 삭제 반영). 씬 편집은 fresh-load(clean) 상태에서 수행·저장 델타 전수 검토 — 유일한 무관 델타는 AwakeningGaugeView 필드 마이그레이션 1줄 스왑(죽은 seedSplats 제거+attentionLean 기본값, 행동 무영향)으로 수용. `BattleBridgeDraftMapTests` 는 DraftController 표면이 아닌 legacy 필드(map/useProcedural) 픽스처라 **유닛 2로 이월**(현재 green 유지).
