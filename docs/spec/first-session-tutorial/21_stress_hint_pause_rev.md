# 21 — 스트레스 안내를 정지+탭으로 (unit 19 rev)

## 목적

unit 19 는 스트레스 2줄을 **비차단·시간 경과**로 흘렸다. 전투가 도는 중에 3초씩 지나가므로
읽지 못한 채 사라질 수 있다. 사용자 결정(2026-08-01): **전투를 잠깐 정지하고 탭으로 넘긴다.**

unit 19 의 "비차단" 계약을 이 unit 이 **스트레스 스텝에 한해** 뒤집는다. 웨이브 스텝(unit 20)은
그대로 비차단이다 — `단, 준비가 되었을때!` 가 "지금 누르지 마"라는 뜻이라 정지시킬 이유가 없고,
한 판에 정지 구간이 하나만 생기는 것이 낫다.

## 왜 Placement 로 옮기지 않았나

먼저 검토한 안은 "클래스 안내 다음, 배치 단계에서" 였다. `ScoreHudView.OnPhaseChanged` 가
패널을 **Battle·Tally 에서만** 켜므로 Placement 에는 배지가 존재하지 않고, 포커스를 걸면
`FocusUi` 가 링을 조용히 끈다. 배지를 미리 드러내려면 가시성 판정을 뜯어야 해서
전투 HUD 의 표시 계약을 튜토리얼이 침범한다. 정지 방식은 그 침범 없이 같은 목적을 만든다.

## 변경 대상

- `Scripts/UI/Tutorial/TutorialGuidanceStyle.cs` — `stressHintDelaySeconds` · `stressHintFallbackSeconds`
- `Scripts/UI/Tutorial/TutorialGuidanceView.cs` — 위 둘 노출
- `Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — 스트레스 스텝 재작성 + 탭 소비 분기
- `docs/spec/first-session-tutorial/README.md` — 계약 갱신

## 구현

### 정지는 기존 1급 기구를 쓴다

`TimeManager`(`Wassup.Core.TimeControl`)는 **글로벌 `Time.timeScale` 을 절대 건드리지 않고**
도메인별 lease 로 동작한다. `Request(TimeDomain.Battle, 0f)` → `TimeLease.Dispose()`.

이 방식이 성립하는 근거(전부 기존 계약):

- **시뮬 정지**: `BattleSimGroup` 에 `BattleScaledRateManager` 가 붙어 한 지점에서 제어한다.
- **타이머·웨이브도 함께 멈춘다**: `_battleClock` 이 전투 도메인 스케일을 반영한 클럭이고
  (`BattleBridge.cs:266-267`) `TimerRemaining = _timerDuration − _battleClock` 이다. 스폰 게이트도
  같은 클럭을 쓴다(`:1815`). **즉 정지가 시간점수를 깎지 않는다.**
- **안내 자신은 안 멈춘다**: 튜토리얼은 전부 `Time.unscaledDeltaTime`(`WaitUnscaled`).
- **선례**: 손패가 이미 Battle 도메인에 0.3x 슬로모 lease 를 건다. UI 가 전투 시간을 잡는 것은
  확립된 패턴이다.
- **충돌 없음**: 승자 = `(priority desc, 동률이면 scale asc)` 라 `0` 이 `0.3` 을 이기고, 해제하면
  손패 슬로모로 정확히 복귀한다. priority 를 올릴 필요가 없다.

### 흐름

```
Battle 진입 → 배지 활성 대기 → stressHintDelaySeconds
  → Battle 도메인 0 lease 획득
  → 배지 포커스 + 한 문단(2줄) + 풀스크린 탭 캐처
  → 탭 또는 stressHintFallbackSeconds 만료 → lease 해제 · 캐처 해제
  → 웨이브 스텝(unit 20, 비차단 유지)
```

- 문구는 **한 문단 1탭**이다(사용자 결정). 두 줄을 한 번에 보여준다 — 클래스 안내(6줄 한 문단)와
  같은 리듬이고 첫 판의 강제 탭 수가 안 늘어난다.
- 둘째 줄 생략 조건은 unit 19 그대로: `ShowsStressLimit == false` **또는** `StressLimit <= 0`.
  생략되면 한 줄만 있는 문단이 된다(스텝 자체는 유지 — 스트레스 관리는 엔드리스에도 유효하다).

### lease 누수 = 전투 영구 정지 (이 unit 의 유일한 고위험)

`TimeManager.ResetAll()` 안전망은 **매치 경계**에만 있다. 판 도중 탭이 유실되면 그 판이 얼어붙는다.
클래스 안내가 같은 모양의 위험을 `classHintFallbackSeconds` 로 막은 선례를 그대로 따른다.

- **만료 폴백**(`stressHintFallbackSeconds`) — 탭이 안 와도 자동 해제 후 진행.
- lease 는 **필드 하나**가 소유하고 그 필드만 해제한다. 중복 획득 금지(획득 전 기존 lease 해제).
- 해제는 `StopBattleHudHint()` 가 소유한다 — 정리 경로 3곳(비-Battle 페이즈 · `OnDisable` ·
  체인 정상 종료)이 이미 그 함수로 수렴하므로 새 경로를 만들지 않는다.
- 탭 캐처(`SetTapToContinue`)도 같은 곳에서 끈다. `guidance.Hide()` 가 캐처를 함께 끄지만
  **명시적으로도 끈다** — 잔류하면 화면 전체가 먹통이다.

### 탭 소비자가 둘이 된다

`ContinueTapped` 는 지금 클래스 안내 전용이다. `OnContinueTapped` 첫 줄에 스트레스 대기 분기를
둔다. core 는 Battle 진입에서 이미 끝나 있어 기존 가드가 어차피 막지만, **순서를 명시**해 두
소비자가 서로를 가리지 않게 한다.

## 완료 기준

- 컴파일 오류 0 · EditMode 전체 통과 유지
- Play(첫 판 전투): 시작 후 `stressHintDelaySeconds` 뒤 **전투가 멈추고** 배지 링 + 한 문단 →
  탭 → **전투가 다시 흐른다** → 이어서 웨이브 2줄(비차단)
- **정지 중 남은시간이 줄지 않는다**(HUD 타이머 관측) · 적이 움직이지 않는다 · 웨이브가 안 나온다
- 탭하지 않고 방치 → `stressHintFallbackSeconds` 뒤 자동 해제, 전투 재개
- 정지 중 씬 이탈(메뉴·결과)에서도 다음 판 전투가 정상 속도다(lease 누수 없음)
- 손패를 열어 슬로모(0.3x)가 걸린 상태와 겹쳐도 해제 후 0.3x 로 복귀
