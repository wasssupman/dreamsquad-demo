# 10 — 마음이 터지는 한 박자

## 목적

**마음이 무너지는 순간을 보드에서 사건으로 만든다.** 지금은 바가 꽉 차는 그 프레임에 결과
화면이 덮어써서, 플레이어는 «터졌다» 를 한 번도 못 본다.

⚠ **결과 화면에 종료 사유를 표기하지 않는다.** 리본이 `"결과"` 고정인 것은 **현시점의 의도된
결정**이다(사용자, 2026-08-24). 3분 만료와 파열이 화면상 같아 보이는 것은 버그가 아니다 —
`tally.Outcome` 이 로그에만 실리는 현행을 그대로 둔다. 이 unit 은 **보드 연출만** 다룬다.

## 지금 왜 안 보이나 — 원인 둘

**1. 붕괴 연출이 유출 배수구에 딸려 죽었다.** `SpawnGoalCollapse`(VFX)와 `MarkGoalCollapsed`
(프랍 그을림 + 주저앉음)은 `OpenGoalCellAfterBreach` **안에만** 있다. 그 함수는 유출 배수구
(`OpenBreachedCellsForLeak`)의 일부이고, unit 0 이 「누수가 없다」를 만들려고 배수구를 **일부러
안 부르기로** 했다. 규칙을 끊었더니 **연출이 같이 끊긴** 것이다 — 본능이 무너질 때는 같은 VFX 가
나가는데 마음만 안 나가는 이유가 이것이다.

**2. 결과 화면이 같은 프레임에 뜬다.** `EndMatch` 가 `SetPhase(Tally)` → `SetPhase(Result)` →
`resultScreen.Show(tally)` 를 **연달아 동기 호출**한다. 그래서 붕괴 VFX 를 지금 자리에서
그냥 쏘면 1프레임 보이고 화면에 덮인다 = 사실상 안 보인다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 붕괴 연출 분리 + 마감 지연

## 구현

**1. 연출을 규칙에서 떼어낸다.** 붕괴 프레임에 셀별로 직접 부른다:
`vfxSpawner.SpawnGoalCollapse(위치)` + `tilemapMapView.MarkGoalCollapsed(셀)`.
배수구(`OpenBreachedCellsForLeak`)는 **계속 안 부른다** — 그것이 「누수가 없다」의 실체다.
연출과 규칙을 같은 함수에 묶어 둔 것이 애초의 결함이었다.

**2. 화면만 늦춘다.** `EndMatch` 의 순서를 이렇게 가른다:

| 즉시 | 지연 |
|---|---|
| `_resultShown` · `_running=false` (심 정지) | `SetPhase(Result)` |
| `BuildTally` · 로그 · `ReportMatchResult`(서버 제출) | `resultScreen.Show(tally)` |
| `SetPhase(Tally)` | |

**제출이 표시보다 앞이라는 계약을 지킨다** — 화면을 기다리다 앱이 죽어도 기록은 이미 갔다.
지연은 `GamePhase.Tally` 가 잡는다. 새 페이즈를 만들지 않는 이유: Tally 는 원래 「전투종료 →
Tally → 결과화면」의 중간 박자 자리였고, HUD 게이팅이 **이미 Tally 를 안다**(`ScoreHudView` 가
그 페이즈에서 점수 패널을 유지한다).

**3. 그 박자 동안 시간을 늦춘다.** `TimeManager.Request(TimeDomain.Battle, …)` 로 슬로우하고
`using`/`Dispose` 로 반납한다. `Time.timeScale` 은 금지(도메인 시간제어 규율).
대기는 **unscaled**(`WaitForSecondsRealtime`)다 — 스케일된 시간으로 기다리면 슬로우가
대기 자체를 늘려 박자가 배로 길어진다.

**4. 터지는 판에만 적용한다.** 3분 만료·유저 제출은 터지는 것이 없으므로 **현행 즉시**다.
이건 「종료 사유 표기」가 아니라 **사건이 있을 때만 그 사건의 연출이 나가는 것**이다.

## 완료 기준

- [x] 마음이 0 이 되면 그 자리에 붕괴 VFX 가 뜨고 프랍이 그을리며 주저앉는다
- [x] 그 박자가 눈에 보인 **뒤에** 결과 화면이 뜬다
- [x] 박자 동안 화면이 느려진다 · 끝나면 시간 배율이 원복된다(리스 누수 없음)
- [x] 3분 만료·제출은 지연 없이 현행대로 결과 화면
- [x] 결과 리본은 여전히 `"결과"` — 종료 사유를 말하지 않는다(의도)
- [x] 서버 제출이 화면보다 먼저 나간다(순서 계약 유지 — 코드 순서로 보장)
- [x] EditMode — 2647 실행, 이 변경 발 신규 실패 0

**확인 2026-08-24** — 커밋 `2e6165be`. 사용자 Play 확인 통과.
프로그램 실측: 붕괴 프레임 `phase=Tally` · 배율 0.30 (결과 화면 안 뜸) → 박자 후
`phase=Result` · 배율 **1.00**(리스 누수 없음) · 골 프랍 scale 0.30(`MarkGoalCollapsed` 1회) ·
`goalCollapse` 폴백 경고 없음(실제 VFX) · `EndMatch("complete")` 즉시 Result ·
`StopBattle` 이 코루틴 정리 + 배율 원복.
