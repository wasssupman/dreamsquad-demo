# 14 — Bridge 규칙 적출 ① 웨이브·승패·타이머·점수

## 목적

Bridge 상주 매치 규칙 적출의 첫 묶음(설계 정본 M1-4 "1급 작업"). 이 규칙들이 Bridge 에 남으면
sim lib 이 반쪽이 된다 — **판이 언제 끝나고 누가 이겼는지를 sim 이 몰라서** 서버권위(M3)도 리플레이
검증도 성립하지 않는다. 아직 신 sim 은 없으므로 **규칙을 sim 후보 모듈로 옮기고 Bridge 는 호출자**가
된다(unit 17 이 그 모듈을 asmdef 로 격리).

## 변경 대상

salvage 판정표 §3 의 sim 규칙 클러스터 3개:

- **웨이브 스케줄·스폰**: `ScheduledWaveTime` · `QueueDueWaves` · `QueueWave` · `ForceNextWave`
  (`_waveTimeShift` 누적 재기준 — **비멱등 계약 보존**) · `TryInitializeGeneratedWaves` ·
  pending 스폰 게이트 루프
- **승패·타이머**: `CheckTimer` · `CheckVictory` · `RemainingBattleSeconds` · `NoQueuedAttackersRemain` ·
  `RefreshNextWaveClearReady`
- **점수·유출**: `CalculateBattleScore`(`ScoreMath` 는 이미 순수 = conform) · `EffectiveLeakLimit` ·
  `RemainingLeakAllowance` · `TryPayLeakAllowance`(**비가역 선불** — 몽마의 계약) · `_goalReachedCount`
  · `_killScoreTotal` · `_leakAllowancePenalty` · 스트레스 누적
- 신규 `Assets/_Project/Scripts/Sim/Match/` (모듈 위치는 unit 17 이 확정) — 위 규칙의 새 집
- `BattleBridge` — 위 멤버 제거 후 세션/모듈 호출로 대체. `_battleClock` 은 **unit 19(시계 정책)**
  까지 Bridge 잔류(여기서 옮기면 하네스·라이브 이중 구동 계약이 깨진다)

## 구현

- **읽기 모델의 신설 카운터를 여기서 채운다**(unit 12 가 미지원으로 남긴 것): 점수·유출·
  `effectiveLeakLimit`·스트레스. 청사진 ① §6 실측대로 이들은 현재 **읽기면이 없어** 뷰가 독립 누적
  중이므로, 규칙이 sim 으로 오면 `ScoreHudView.AddScore` 미러링과 튜토리얼의 `scoreHud.StressLimit`
  역폴링이 **같은 소스로 접힌다**.
- `MatchEnded{outcome, score4}` 의 실제 발행 지점이 여기다 — outcome 4종(victory/victory_timeout/
  defeat/aborted, 청사진 ① §1)을 이 모듈이 결정한다.
- **웨이브 예보**(`TryGetSpawnAlertForecast`)는 규칙 파생이므로 함께 옮기고, 읽기 모델은 복사본을 준다.

## 완료 기준

- compile 0 · EditMode 회귀 0 · **골든 7종 byte diff 0**(규칙 이동이 결과를 바꾸지 않았음의 증인.
  `forced_wave`·`normal` 시나리오가 웨이브/타이머 경로를 직접 덮는다).
- 읽기 모델에서 점수·유출·스트레스가 **실제 값**으로 서빙되고, `ScoreHudView` 가 자체 누적이 아니라
  그 값을 그린다(PlayMode 스모크로 HUD 수치 일치 확인).
- Bridge 에서 위 3클러스터 멤버 0(grep) — 남은 것은 호출과 뷰 통지뿐.

## 구현 결과 (2026-08-05)

### 만든 것

`Assets/_Project/Scripts/Sim/Match/` — `Wassup.Runtime` asmdef 안이지만 이 unit 이 만든 세 타입은
**`UnityEngine` 을 직접 `using` 하지 않는다**. 그 선이 unit 17 asmdef 분리의 출발점이다.

> ⚠ **정정 (2026-08-05, 리뷰 양측 H1)**: 위 문장은 unit 14 시점에는 폴더 전체에 대해 참이었지만
> **unit 15-B 의 `MatchPlacementRules` 가 `Vector2Int` 때문에 `using UnityEngine` 을 들여왔다.**
> 이제 폴더 단위 주장은 거짓이고, 무참조인 것은 `MatchOutcomeRules`·`MatchOutcomeNames`·
> `MatchWaveSchedule` 세 개다(`MatchWaveSchedule` 은 `Wassup.Data` 를 참조하지만 `UnityEngine` 을
> 직접 쓰지는 않는다). 드리프트 재발을 막는 게이트는 `SimEngineIndependenceTests` 가 소유한다 —
> asmdef 가 없는 동안 강제 수단은 그것뿐이다.

| 타입 | 소유 상태 | 성격 |
|---|---|---|
| `MatchWaveSchedule` | 플랜·인덱스·`_waveTimeShift`·대기열·예고·클리어 래치 | 스케줄 규칙 |
| `MatchOutcomeRules` | 유출·선불차감·킬점수·결과 래치·제한시간 | 승패·점수 규칙 |
| `MatchOutcomeNames` | — | enum → 기존 로그 문자열 |

두 모듈 모두 **부작용이 없다**: 로그·HUD·연출·엔티티 생성을 하나도 하지 않고 판정만 돌려준다.
새로 큐잉된 웨이브는 `QueuedWaveNotice` 로 나가고 서술(`RecordWaveEvent`·`Debug.Log`)은 Bridge 가 한다.

### 설계 결정 3개

1. **`MatchOutcome` enum 을 신설하지 않았다.** unit 12 가 세션 계약에 이미 정의해 뒀고
   (`Wassup.Core.Session.MatchOutcome`, 4종 동일), 규칙이 그 어휘로 결과를 내야 세션 이벤트·
   커맨드로그·리플레이가 한 어휘를 쓴다. 처음엔 모르고 중복 정의했다가 `CS0104` 로 드러났다.
2. **`TryInitializeGeneratedWaves` 는 규칙이 아니라 로더라서 Bridge 에 남겼다** —
   `ResolveAndInitializeWavePlan()` 으로 개명. 하는 일이 SO 해석(작성 플랜 → seed → legacy
   fall-through)과 로거 통지뿐이고, 고른 **결과**(`GeneratedWavePlan`)만 모듈에 넘긴다. 규칙 안으로
   SO 를 끌고 들어가면 sim lib 이 에셋 계층을 물고 온다. 이 함수가 unit 18 의 데이터 seam 이다.
3. **`ConcludeMatch` 로 종료 3종을 한 경로로 접었다.** 버팀 승리가 `BeginTally` 에 리터럴 `0f` 를
   넘기던 분기는 제거했다 — 그 판정 조건이 `clock >= duration` 이라 `RemainingBattleSeconds` 가
   정확히 0 을 주므로 **같은 값**이다.

### 부수 소득 — 테스트가 씬을 버렸다

규칙이 plain 객체가 되면서 웨이브 테스트 4종(20건)이 재작성됐다:

- `WaveForceRescheduleTests` · `WaveSpawnLeadInTests` — `BattleBridge`·`GameObject`·리플렉션 **전부
  제거**. private 필드 3개를 주입하고 private 메서드를 리플렉션으로 부르던 픽스처가 그냥 `new` 다.
- `SpawnAlertForecastTests` — `laneCount` 가 plain 인자가 되어 **레인 수만을 위해 만들던
  `NativeArray` 맵 2개(+Dispose)가 사라졌다.** `_running` 게이트만 Bridge 소유라 그 1건은 남겼다.
- `NextWaveClearReadyTests` — **Bridge 에 남는다.** 검증 대상이 "대기열(모듈) + 생존 적(ECS 질의)의
  합집합" 이라 두 소유자가 만나는 지점이 곧 Bridge 다. 대신 상태 주입을 `_waveSchedule` 의 공개
  API 로 바꿔 픽스처가 규칙을 우회하지 않게 했고, 인덱스 주입 대신 실제 큐잉으로 도달시킨다.

### 남긴 것 (B2 — 다음 작업 단위)

`ScoreHudView.SetLeakStatus` push→pull 역전, `ScoreTallyView.Play`, `ResultScreen.ShowVictory/
ShowDefeat` 는 옮기지 않았다. 킬 점수는 이 unit 에서 정본이 뒤집혔지만(아래), 유출 배지·연출·결과
화면은 **뷰 소유권** 문제라 한 묶음으로 다루는 것이 맞다(`13_consumer_rewiring.md` B2).

**킬 점수 정본 역전(이 unit 에서 완료)**: `ScoreHudView` 가 `SyncScoreFromSession()` 으로 매 프레임
`ReadModel.ScoreKill` 을 따라간다. `AddScore` 누적을 지우지 않고 **덮어쓰기**로 둔 이유가 둘 —
① 세션 없는 경로(EditMode 픽스처·툴 씬)에서는 누적이 유일한 값이다, ② 두 값이 어긋나면 정본이
이겨 조용히 갈리는 대신 즉시 수렴한다. **Battle 구간에서만** 동기화한다(`Tally`→`MatchPhase.Ended`):
연출은 시간·스트레스 축을 킬 점수 위에 더해 올리므로 계속 동기화하면 합산이 안 보인다.
