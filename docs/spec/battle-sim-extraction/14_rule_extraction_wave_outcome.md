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
