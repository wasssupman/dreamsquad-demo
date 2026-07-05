# 3 — BattleBridge 통합: singleton write + 웨이브/타이머 Battle 클럭 (BLOCKER)

## 목적

두 가지: (1) `TimeManager.ScaleOf(Battle)` 을 매 프레임 `BattleTimeScale` singleton 으로 흘려보낸다. (2) **critic BLOCKER** — BattleBridge 의 웨이브 스폰과 매치 타이머가 `Time.time`(실시간, timeScale 불변)에 묶여 있어 정지·슬로우모가 안 먹는다. 이를 Battle-스케일 클럭으로 교체한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

## 구현

1. **singleton 부팅 + 매 프레임 write** (Update 초반):
   - 부팅 시 `BattleTimeScale{Value=1}` singleton 엔티티 생성(없으면).
   - 매 프레임 `Value = TimeManager.Instance.ScaleOf(TimeDomain.Battle)`.

2. **Battle-스케일 클럭 누산기** (BLOCKER 핵심):
   - 필드 `double _battleClock` 추가. Update 에서 `_battleClock += TimeManager.Instance.DeltaTime(TimeDomain.Battle);` (= `unscaledDeltaTime * ScaleOf(Battle)`).
   - `Time.time - _startTime` 로 진행하던 **load-bearing 경로**를 `_battleClock` 로 교체:
     - 웨이브 스케줄: `QueueDueWaves(t)` 의 `t` (critic `:1704–1705`)
     - 유닛 스폰 스케줄: `entry.triggerTimeSec` 비교 (critic `:1708`)
     - 매치 타이머: `CheckTimer` 의 `Time.time - _startTime < _timerDuration` (critic `:2516–2520`) 및 `TimerRemaining` (`:2514`)
   - `_startTime`/`_battleClock` 은 매치 시작 시 리셋.
   - **cosmetic 타임스탬프는 실시간 유지 가능** (이벤트/로그 `:1366/:1392/:1433/:3300…`) — 게임 로직 아님.

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] Play 정지(scale 0): 웨이브 스폰 **정지**, `TimerRemaining` **동결**, 재개 시 이어감.
- [ ] Play 슬로우모(scale 0.2): 웨이브 간격·타이머가 전투와 **같은 5x 느린 페이스**(desync 없음).
- [ ] scale 1 기본: 기존 웨이브/타이머 동작과 회귀 없음.

## 주의

- ECS 경계 유지: TimeManager(Mono) → BattleBridge → BattleTimeScale singleton 단방향. RateManager 가 TimeManager 직접 참조 금지.
- `_battleClock` 는 double 로 누산해 장시간 정밀도 확보.
