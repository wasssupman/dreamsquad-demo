# EditMode 단위 테스트

**작업 구분**: 4

## 목적

본 spec 의 핵심 계약을 EditMode 테스트로 회귀 게이트 형성. PlayMode 시각 검증은 Unit 5 가 담당.

## 변경 대상

- Add: `Assets/_Project/Tests/EditMode/BattleBridgeDraftMapTests.cs`
- Add: `Assets/_Project/Tests/EditMode/DraftControllerMapRebuildTests.cs`

## 구현

### BattleBridgeDraftMapTests.cs

테스트 케이스:

| # | 이름 | 검증 |
|---|---|---|
| 1 | `PrepareDraftMap_FirstCall_BuildsMap` | `PrepareDraftMap` 호출 후 `bridge.HasGeneratedMap == true` |
| 2 | `RebuildDraftMap_DisposesOldAndCreatesNew` | `PrepareDraftMap` → 첫 NativeArray 참조 캡처 → `RebuildDraftMap` → 새 NativeArray 참조 (이전과 다름), 둘 다 IsCreated 검증 시퀀스 |
| 3 | `BeginPlacement_AfterPrepare_DoesNotRebuild` | `PrepareDraftMap` 후 NativeArray 참조 캡처 → `BeginPlacement` → 같은 참조 유지 (= BuildMapForBattle 미호출 증명) |
| 4 | `BeginPlacement_WithoutPrepare_FallbackBuilds` | `PrepareDraftMap` 호출 안한 채 `BeginPlacement` → `HasGeneratedMap == true` |
| 5 | `RebuildDraftMap_50Iterations_NoEntityLeak` | `PrepareDraftMap` → `RebuildDraftMap` 50회 반복 → `_em.UniversalQuery` entity 카운트 변동 ≤ 기준 (단순 단조 증가 X) |
| 6 | `RebuildDraftMap_50Iterations_NoMapViewChildLeak` | mapView.transform.childCount 가 한정 범위 (4 root: Tiles + Obstacles + BackgroundProps + Goal) — 누적 X |

테스트 fixture: `[SetUp]` 에서 BattleBridge GameObject + 의존 (deck, map, mapView, mapTheme, mapSettings) prefab 또는 ScriptableObject 의 minimal 인스턴스 구성. 기존 `Assets/_Project/Tests/EditMode/` 의 다른 BattleBridge 테스트 (예: `SpawnBlockingHazardTests.cs`) 의 fixture 패턴 재사용.

### DraftControllerMapRebuildTests.cs

| # | 이름 | 검증 |
|---|---|---|
| 1 | `SetMapGenerationOptions_TriggersBridgeRebuild` | mock bridge 의 `RebuildDraftMap` 호출 카운트 == 1 |
| 2 | `SetMapPathShape_TriggersBridgeRebuild` | 동일 |
| 3 | `BeginDraft_DoesNotTriggerRebuild` | BeginDraft 자체는 RebuildDraftMap 을 호출하지 않음 (책임 분리: GameManager.Start = PrepareDraftMap, OnRedraftRequested = PrepareDraftMap) |
| 4 | `OnRedraftRequested_RebuildsMap` | OnRedraftRequested 시뮬레이션 후 `bridge.HasGeneratedMap == true` (TeardownCurrentBattle 후 PrepareDraftMap 재호출 검증) — BattleBridge 측 테스트로 분류 가능 |

mock bridge: 본 테스트만을 위한 가벼운 wrapper (인터페이스 추출하지 말고, BattleBridge 의 일부 동작을 stub 으로 대체하는 가짜 컴포넌트). 또는 테스트 전용 카운터 필드 추가.

(BattleBridge 가 인터페이스 추출에 적합하지 않으면 — 현재 클래스가 너무 크고 단일 — 카운터 hook 또는 partial class 로 테스트 전용 노출.)

### 의존 / 충돌 점검

- ECS World 가 EditMode 테스트에서 사용 가능한지 확인. `World.DefaultGameObjectInjectionWorld` 가 EditMode 에서는 자동 생성 안되는 경우 있음 — `new World("Test")` 명시 생성 후 dispose 패턴 사용.
- 기존 `SpawnBlockingHazardTests.cs` 등이 동일 패턴을 쓰고 있다면 그대로 재사용.

## 완료 기준

- 컴파일 성공.
- `BattleBridgeDraftMapTests` 6 케이스 모두 통과.
- `DraftControllerMapRebuildTests` 4 케이스 모두 통과.
- 기존 EditMode 테스트 회귀 0 (전체 통과 카운트가 신규 추가분만큼만 증가).
- 콘솔 에러 0.
