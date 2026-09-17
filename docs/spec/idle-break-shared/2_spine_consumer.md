# 2 · `SpineUnitView` 가 `IdleBreakCycle` 을 소비한다

## 목적

Spine 뷰의 idle 변형(루프 이어붙임)을 은퇴시키고 스프라이트와 같은 쉼/컷 규칙을 탄다. 소환사가 첫 대상(unit 1 데이터).

## 변경 대상

- 수정 `Presentation/SpineUnitView.cs` — 아래 블록만. 다른 멤버 무변.

## 구현

**은퇴**: `AdvanceIdleVariant` · `HookIdleVariantCycle` · `OnIdleVariantComplete` · `_currentIdleVariant`/`_idleVariantIndex` ·
`ResolveLocomotionAnimation` 의 `_currentIdleVariant` 분기 · `PlayIdleLooping` 의 첫 추첨.

**신설** — `IdleBreakCycle _idle;` + 두 헬퍼:
- `PlayBaseLoop()` — 기본 idle 루프 엔트리(`SetAnimation(0, idleName, true)` + `MixDuration = LocoMixDuration`). 이미 돌고 있으면 재시작하지 않는다(이름 비교).
  `Loop == true` 라 `IsLocomotionLoopPlaying`·원샷 게이트(unit 10 계약 4·5) 그대로.
- `PlayBreak(int i)` — `name = ResolveAnimation(SpineIdleBreaks[i])`; `state.SetAnimation(0, name, true)` (**loop:true**, TimeScale 1, 크로스페이드) ·
  `_currentBreak = name`. 길이 = `Animation.Duration`(트랙이 `_skeleton.timeScale` 로 같이 느려지므로 배틀 시간 기준 한 바퀴).

**진입 지점** — `ResolveLocomotionAnimation()` 의 정지 자리: `walk`(이동 중) > `_loopOverride` > **컷 순환** > idle.
순환 중 「desired 이름」은 `_idle.Looping ? idle : _currentBreak` 로 답해 `UpdateLocomotionAnimation`/`RefreshLocomotionIfLooping` 의
이름 비교가 순환을 되돌리지 않게 한다. 순환 시작(= `BeginLoop` + `PlayBaseLoop`)은 `PlayIdleLooping`(스폰)과 원샷 복귀 큐가 아니라
**원샷 완주 후 로코모션 재개 시점**에 한다 — Spine 은 원샷 뒤 `AddAnimation(loco, loop)` 큐로 복귀하므로, 큐된 loco 엔트리의
`Start` 콜백(또는 `UpdateLocomotionAnimation` 의 첫 루프 감지)에서 `BeginLoop` 한다(루프는 이미 큐로 돌아와 있다). **트랙 0 에 원샷이 있으면 Tick 하지 않는다**
(`!current.Loop` 게이트 — 계약 4).

**Tick** — `UpdatePosition` 안(`AdvanceHop` 옆)에서 `_idle.Tick(Time.deltaTime * _battleScale)`; 전이면 위 소비 형태.
`SetLoopOverride` 가 걸리면 `_idle.Stop()`, `ClearLoopOverride`/원샷 완주 뒤 로코모션 재개 시 다시 `BeginLoop`.
`SpineIdleBreaks` 가 비면 순환 없음 = 현행 idle 단일 루프(무회귀).

## 완료 기준

- 소환사(CH1): 스폰 직후 `idle` 루프 → 1~3초 → `idle2`/`idle3` 중 하나 한 바퀴(크로스페이드) → 다시 `idle` 루프. 소환물 생존 중엔 `attack2` 루프(오버라이드 우선),
  상실 시 `attack3` 원샷 후 다시 기본 루프+타이머부터.
- `PatrolDefenderPlayTest` 무수정 초록(오버라이드 단언은 그대로 성립해야 한다).
- `idleBreaks` 가 빈 유닛(나머지 전부) 애니 거동 무변 — 이쑤시개 Spine 복귀본 등으로 육안.
- EditMode 전체 초록.
