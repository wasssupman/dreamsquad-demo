# 0 — 공격패턴 자동 인트로

## 목적

Squad MAP SETUP 진입 시 공격패턴(`WavePatternStripView`)을 자동으로 펼쳤다가 약 1초 dwell 후 자동으로 접는다. 기존의 "FadeIn 후 계속 표시" 를 대체한다. 맵 설정 패널·START 버튼은 그대로 둔다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs`

(`WavePatternStripView.cs` 는 변경하지 않는다. 공개 API 조합만 사용.)

## 구현

`SquadPrepView.OnMapSetupRequested()` 의 strip 처리 블록을 자동 인트로로 교체한다.

기존:

```csharp
wavePatternStrip.gameObject.SetActive(true);
wavePatternStrip.RebuildFromDeck();
wavePatternStrip.SnapHidden(); // rest positions, then soft fade in
wavePatternStrip.FadeIn();
wavePatternStrip.SetToggleEnabled(true);
```

변경:

```csharp
wavePatternStrip.gameObject.SetActive(true);
wavePatternStrip.RebuildFromDeck();
wavePatternStrip.SnapHidden();
wavePatternStrip.SetToggleEnabled(true);
StartCoroutine(PlayIntro());   // Unroll → dwell → Roll
```

코루틴:

```csharp
[SerializeField] private float introDwellSec = 1f;
private Coroutine _introRoutine;

private System.Collections.IEnumerator PlayIntro()
{
    wavePatternStrip.Unroll();                 // dramatic drop + card fade
    yield return new WaitForSecondsRealtime(introDwellSec);
    if (wavePatternStrip.CurrentState == WavePatternStripView.State.Shown)
        wavePatternStrip.Roll();               // auto-hide → SnapHidden(Hidden)
    _introRoutine = null;
}
```

세부:

- `WaitForSecondsRealtime` 사용 (MAP SETUP 단계는 `timeScale==1` 이지만 일관성/안전 차원). dwell 은 Unroll 애니메이션 **완료 이후** 1초가 아니라 호출 시점부터 측정해도 무방하나, 자연스러움을 위해 Unroll 의 체감 길이를 고려해 `introDwellSec` 기본 1f 로 두고 실기 확인 후 튜닝한다.
- dwell 종료 시 사용자가 토글로 이미 닫았거나(Hidden) 다시 펼치는 중이면 자동 Roll 을 건너뛴다 → `CurrentState == Shown` 가드.
- 조기 종료(선택): strip 의 `OnDwellInterrupt` 에 Roll 을 연결하면 배경 탭으로 인트로를 즉시 닫을 수 있다. 필수는 아님 — 추가 시 `OnEnable/OnDisable` 에서 구독/해제.
- `RebuildFromDeck()` 직후 `SnapHidden()` 으로 rest 위치를 잡은 뒤 `Unroll()` 이 시작 위치를 재설정하므로 순서 유지.
- 매치 재진입 시 중복 코루틴 방지: 이미 `_introRoutine` 이 살아 있으면 `StopCoroutine` 후 재시작.

## 완료 기준

- compile: Unity 콘솔 에러 0 (UnityMCP `read_console`).
- Play (Squad 모드 진입): MAP SETUP 화면에서 공격패턴이 자동으로 펼쳐졌다가 약 1초 뒤 자동으로 접힌다. 맵 설정 패널과 START 버튼은 그대로 조작 가능.
- 자동 접힘 후 "!" 토글 버튼으로 다시 펼치고 접을 수 있다 (MAP SETUP 단계 내에서).
- ✅ 2026-06-04 Play 확인 통과 (사용자). 커밋: `2e3a819`
