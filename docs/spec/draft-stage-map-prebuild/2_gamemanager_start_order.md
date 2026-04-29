# GameManager.Start 호출 순서 변경

**작업 구분**: 2

## 목적

`GameManager.Start()` 가 `BeginDraft()` 직전에 `battleBridge.PrepareDraftMap()` 를 호출해, draft UI 가 표시되기 전에 맵이 빌드되도록 한다.

## 변경 대상

- Modify: `Assets/_Project/Scripts/Core/GameManager.cs`

## 구현

현재 `GameManager.Start()` (line 82~95):

```csharp
private void Start()
{
    if (draftController != null)
    {
        SetPhase(GamePhase.Draft);
        draftController.BeginDraft();
    }
    else if (battleBridge != null)
    {
        Debug.LogWarning("[GameManager] draftController unset; starting battle with inspector defenderPool fallback.");
        SetPhase(GamePhase.Battle);
        battleBridge.StartBattle();
    }
}
```

변경 후:

```csharp
private void Start()
{
    if (draftController != null)
    {
        // Build the map BEFORE entering Draft so the user sees the final
        // playfield behind the card fan. RebuildDraftMap fires from
        // DraftController.SetMapGenerationOptions for option toggles.
        if (battleBridge != null) battleBridge.PrepareDraftMap();

        SetPhase(GamePhase.Draft);
        draftController.BeginDraft();
    }
    else if (battleBridge != null)
    {
        Debug.LogWarning("[GameManager] draftController unset; starting battle with inspector defenderPool fallback.");
        SetPhase(GamePhase.Battle);
        battleBridge.StartBattle();
    }
}
```

## 호출 순서 보장

- `GameManager.Awake` → World 자동 생성 (Entities 패키지 default).
- `GameManager.OnEnable` → logger.StartSession.
- `BattleBridge.Awake` → field validation (line 125~130).
- `BattleBridge.Start` → ResultScreen subscribe (line 132~139).
- `GameManager.Start` → ★ `PrepareDraftMap` → BeginDraft (DraftStarted 이벤트 emit).
- DraftView.Awake/OnEnable → DraftController.DraftStarted 구독 (이미 동작 중).

`PrepareDraftMap` 이 `World.DefaultGameObjectInjectionWorld` 를 가져오는데, Awake 에서 이미 생성됐으므로 정상. race 가 발생하면 Unit 0 의 `DeferredPrepareDraftMap` coroutine 이 1프레임 yield 후 재시도.

## 단위 테스트 (EditMode)

Unit 4 에서 통합 — `GameManager.Start` 시뮬레이션 후 `bridge._generatedMap.IsCreated == true`.

## 완료 기준

- 컴파일 성공.
- `GameManager.Start` 가 `BeginDraft` 직전 `PrepareDraftMap` 호출.
- `draftController == null` 폴백 분기 (직접 battle 시작) 는 변경 없음 — `StartBattle` 내부의 `BeginPlacement` 폴백이 맵 빌드.
- 콘솔 에러/경고 0.

검증: 2026-04-30 — 컴파일 통과. PlayMode V1 (맵 시각 표시) 검증은 Unit 5. 커밋 `3d3cb28`.
