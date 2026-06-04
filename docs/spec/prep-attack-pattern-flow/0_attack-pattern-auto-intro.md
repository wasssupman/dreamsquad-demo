# 0 — 공격패턴 자동 인트로 + 자동 진행

## 목적

Squad 전투 진입 시 공격패턴(`WavePatternStripView`)을 자동으로 펼쳤다가 약 1초 dwell 후 접고, **START 대기 없이** 곧바로 `gameManager.RequestPlacement()` 를 호출해 다음 페이즈(드캐 3중1 → 배치)로 자동 진행한다. 기존 MAP SETUP 의 타이틀/START 게이트는 제거한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs`

(`WavePatternStripView.cs` / `MapSettingsPanelView.cs` 는 변경하지 않는다 — 공개 API 조합만 사용.)

## 구현

`SquadPrepView` 를 "화면 + START" 구조에서 "Canvas 호스트 + 자동 인트로 + 자동 진행" 으로 재작성.

- 제거: `_panel`, "MAP SETUP" 타이틀, START 버튼, `OnStartClicked`, `font` SerializeField.
- `BuildCanvas` → `EnsureCanvas` (Canvas/Scaler/Raycaster 만 보장, `sortingOrder=8`).
- `OnMapSetupRequested`:
  - `mapSettings.Initialize(draftController)` + `SetActive(true)` (자체 토글, 패널은 기본 접힘).
  - strip: `gameObject.SetActive(true)` → `RebuildFromDeck()` → `SnapHidden()` → `SetToggleEnabled(true)` → 인트로 코루틴 시작.
  - strip 미배선 시(headless) 즉시 `AdvanceToPlacement()`.

코루틴:

```csharp
[SerializeField] private float introDwellSec = 1f;
private bool _advanced;

private IEnumerator PlayIntro()
{
    wavePatternStrip.Unroll();
    // Unroll 의 카드 stagger 애니메이션은 1초 이상 걸린다. 완료(Shown) 전에
    // 진행하면 공격패턴이 떠 있는 채로 드캐가 뜬다 → 완료까지 대기.
    while (wavePatternStrip.CurrentState == WavePatternStripView.State.Unrolling)
        yield return null;

    yield return new WaitForSecondsRealtime(introDwellSec);   // 1초 유지

    if (wavePatternStrip.CurrentState == WavePatternStripView.State.Shown)
    {
        wavePatternStrip.Roll();
        while (wavePatternStrip.CurrentState == WavePatternStripView.State.Rolling)
            yield return null;                                // 퇴장 완료까지 대기
    }
    _introRoutine = null;
    AdvanceToPlacement();
}

private void AdvanceToPlacement()
{
    if (_advanced) return;          // 중복 진행 방지
    _advanced = true;
    if (gameManager != null) gameManager.RequestPlacement();
}
```

세부 (load-bearing):

- **Unroll 완료 대기 필수**: `Unroll()` 은 헤더 drop + 카드 stagger(10장 기준 ~1.3s)로 즉시 `Shown` 이 되지 않는다. 호출 직후부터 1초만 세면 그 시점 state 가 아직 `Unrolling` 이라 Roll 을 건너뛰고 즉시 진행 → **공격패턴 위에 드캐가 바로 뜨는 버그**. 따라서 `Unrolling` 이 끝날 때까지 `yield return null` 로 대기한 뒤 dwell 을 시작한다.
- **timeScale==1 유지**: `RequestPlacement()` 호출 전까지 timeScale 은 1 이라 Unroll/Roll 이 정상 재생된다. Roll 퇴장이 `Hidden` 으로 끝난 뒤 진행 → 드캐 모달(timeScale=0)이 깨끗한 화면에서 뜬다.
- dwell 중 사용자가 토글로 닫았으면(`CurrentState != Shown`) 자동 Roll 을 건너뛰고 바로 진행.
- 진입 1회당 `RequestPlacement` 1회 (`_advanced` 가드). `RequestPlacement → BeginPlacementPhase → SetPhase(Placement) → DreamcatcherController` 가 첫 드캐를 띄움(기존 경로).

## 완료 기준

- compile: CS 에러 0 (UnityMCP `read_console`).
- Play (Squad 모드 진입): 공격패턴이 자동으로 펼쳐졌다가 약 1초 뒤 접히고, **버튼 조작 없이** 곧바로 드캐 3중1 선택으로 넘어간다.
- 드캐 선택 후 배치로 이어진다(기존 흐름 유지).
- ✅ 2026-06-04 Play 확인 통과 (사용자): 펼침 → ~1초 유지 → 사라짐 → 드캐 → 배치. 커밋: `9a9fa09`
