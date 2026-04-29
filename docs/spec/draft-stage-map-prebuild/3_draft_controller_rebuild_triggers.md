# DraftController Rebuild 트리거

**작업 구분**: 3

## 목적

MAP SETTINGS 옵션 변경 시 `BattleBridge.RebuildDraftMap()` 를 호출하도록 `DraftController.SetMapGenerationOptions()` 에 트리거 추가. Redraft 진입 시에도 같은 옵션 + 새 seed 로 새 맵을 그리도록 `BeginDraft()` 분기 보강.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Core/DraftController.cs` — SetMapGenerationOptions / SetMapPathShape 트리거
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `OnRedraftRequested` 에 `PrepareDraftMap` 호출 추가, `HasGeneratedMap` getter 추가

## 구현

### SetMapGenerationOptions 트리거

현재 (line 140~143):

```csharp
public void SetMapGenerationOptions(MapGenerationOptions options)
{
    SelectedMapGenerationOptions = options.Normalized();
}
```

변경 후:

```csharp
public void SetMapGenerationOptions(MapGenerationOptions options)
{
    SelectedMapGenerationOptions = options.Normalized();
    // Phase: draft-stage-map-prebuild — push new options to the bridge so
    // the playfield behind the card fan reflects the user's choice
    // immediately. Bridge handles cleanup + flow field rebuild.
    if (battleBridge != null) battleBridge.SetMapGenerationOptions(SelectedMapGenerationOptions);
    if (battleBridge != null) battleBridge.RebuildDraftMap();
}
```

`SetMapPathShape` (line 133~138) 도 동일 패턴 — 끝에 `battleBridge?.SetMapGenerationOptions(SelectedMapGenerationOptions); battleBridge?.RebuildDraftMap();` 추가.

(현재 `TryConfirm` 안에서 `battleBridge.SetMapGenerationOptions(...)` 를 한 번 호출하는데, 옵션이 토글마다 bridge 로 push 되면 `TryConfirm` 의 호출은 idempotent — 그대로 유지하거나 제거 가능. 본 unit 은 그대로 유지.)

### Redraft path 보강 — `OnRedraftRequested` 수정 (BattleBridge)

**중요**: 현재 `BattleBridge.OnRedraftRequested()` (line 181~) 는 `TeardownCurrentBattle()` 호출 후 `draftController.BeginDraft()` 를 호출한다. `TeardownCurrentBattle` 안에서 `TeardownGeneratedMap()` 까지 부르므로 (line ~321), **BeginDraft 시점에 맵은 이미 destroy 된 상태**. 즉 Redraft 후 draft UI 가 떠도 맵 배경이 빈다.

해결: `OnRedraftRequested` 에서 `TeardownCurrentBattle()` 후 `BeginDraft` 직전에 `PrepareDraftMap()` 을 다시 호출. `GameManager.Start` 와 동일한 패턴.

```csharp
// BattleBridge.OnRedraftRequested — 변경
private void OnRedraftRequested()
{
    if (draftController == null) { ... 기존 폴백 ... return; }

    var logger = GameManager.Instance?.Logger;
    if (logger != null) { logger.EndSession(); logger.StartSession(); }

    if (_world != null) TeardownCurrentBattle();
    if (resultScreen != null) resultScreen.Hide();

    // ★ 추가 — TeardownCurrentBattle 이 맵을 destroy 했으니 재빌드.
    PrepareDraftMap();

    draftController.BeginDraft();
}
```

이로 인해 `DraftController.BeginDraft` 안에서는 별도의 RebuildDraftMap 호출 불필요 — 맵은 이미 PrepareDraftMap 으로 새로 빌드된 상태. **BeginDraft 변경 없음**.

### HasGeneratedMap getter

`BattleBridge.HasGeneratedMap` 는 본 unit 의 `MapSettingsPanelView` 호출 흐름에서 직접 필요하지 않지만, EditMode 테스트에서 `_generatedMap.IsCreated` 검증용으로 노출:

```csharp
// In BattleBridge.cs:
public bool HasGeneratedMap => _generatedMap.IsCreated;
```

### 동작 시나리오

| 시점 | 호출 | 결과 |
|---|---|---|
| 게임 시작 | `GameManager.Start` → `PrepareDraftMap` | 첫 빌드 |
| 옵션 토글 | `MapSettingsPanelView` → `SetMapGenerationOptions` → `RebuildDraftMap` | 재빌드 |
| Confirm | `TryConfirm` → `DraftConfirmed` → `BeginPlacement` | skip (이미 빌드됨) |
| Redraft 누름 | `OnRedraftRequested` → `TeardownCurrentBattle` → `PrepareDraftMap` → `BeginDraft` | 재빌드 |
| Restart 누름 | `OnRestartRequested` → `TeardownCurrentBattle` → `BeginPlacementPhase` → BeginPlacement 폴백 빌드 | 재빌드 (맵 dispose 됐으므로 폴백) |

## 단위 테스트 (EditMode)

Unit 4 에서 통합:
- `DraftController.SetMapGenerationOptions(...)` 호출 시 `battleBridge.RebuildDraftMap` 호출 확인 (mock bridge 또는 카운터).
- `OnRedraftRequested` 시뮬레이션 후 `bridge.HasGeneratedMap == true` (PrepareDraftMap 이 재빌드).

## 완료 기준

- 컴파일 성공.
- `SetMapGenerationOptions` / `SetMapPathShape` 가 `RebuildDraftMap` 호출.
- `BattleBridge.OnRedraftRequested` 가 `TeardownCurrentBattle` 후 `PrepareDraftMap` 호출 (BeginDraft 진입 시 맵 존재 보장).
- `BattleBridge.HasGeneratedMap` public getter 존재.
- `DraftController.BeginDraft` 변경 없음 (Redraft 처리는 OnRedraftRequested 가 책임).
- 콘솔 에러/경고 0.
