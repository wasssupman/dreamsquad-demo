# 2 — 클리어 시 다음 웨이브 어필

## 목적

`bridge.NextWaveClearReady`가 false→true가 되는 순간을 축하하면서, 플레이어가 누르거나 다음
웨이브가 자동 호출될 때까지 “여기를 눌러 진행”이라는 비언어적 신호를 반복한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/NextWaveDock.cs`
- `Assets/_Project/Scenes/BattleScene.unity` (연출 수치 배선)

## 구현

View 내부 상태는 `Normal / ClearReady / Disabled` 세 시각 상태로 제한한다.

- **진입 한방**: 버튼이 약 1.1배로 hop한 뒤 squash 복원, 골드 림 flash, 이중 화살표가 짧게
  앞으로 밀린다. false→true edge에서 한 번만 실행한다.
- **반복 어필**: 약 1.5초 주기로 (1) 작은 통통 바운스, (2) 화면 중앙 쪽 3~5° 넛지 후 복원,
  (3) 버튼 뒤 hollow pulse ring 2개를 stagger 확산·fade한다.
- 리듬은 `AwakeningGaugeView.AttentionRoutine`과 `JarFigurePile.Hop()`을 참고하되,
  피규어 물리나 드림캐쳐 View에 의존하지 않는다. `NextWaveDock` 자체 RectTransform/Tween만 쓴다.
- TMP 숫자와 타이머는 움직이지 않는다. 버튼 face·화살표·ring만 움직여 정보 판독을 보존한다.
- `Time.unscaledDeltaTime`/PrimeTween unscaled를 사용한다. pointer press가 들어오면 attention tween을
  양보하고, release 뒤에도 clear 상태면 다음 주기부터 재개한다.
- 클릭, 자동 QueueWave, `NextWaveHasNext=false`, panel hide/disable, battle 종료 시 coroutine/tween을
  중지하고 scale/rotation/color/alpha를 원상복구한다.
- 주기·scale·lean·ring 색/크기는 `[SerializeField] private`로 둔다. 상시 SFX·화면 전체 flash·
  손가락 포인터·`클리어!` 텍스트는 추가하지 않는다.

## 완료 기준

- clear 진입마다 한방 연출은 정확히 1회, 루프는 clear 상태 동안만 반복한다.
- 리드인/웨이브 진행 중에는 강조가 나타나지 않는다.
- 버튼 클릭 또는 자동 웨이브 호출 프레임에 강조가 종료되고 잔여 ring/tween이 남지 않는다.
- press/release와 attention이 겹쳐 scale이 누적되거나 비정상 각도로 고정되지 않는다.
- 슬로모/일시정지 표현 중에도 UI 리듬은 안정적이며 타이머 숫자 판독을 방해하지 않는다.

검증 2026-07-26 — targeted PlayMode 1/1 및 clear-ready 캡처 확인 — commit `663ad01c`.
