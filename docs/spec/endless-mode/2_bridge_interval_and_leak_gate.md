# 2 — BattleBridge: 고정간격 연결 + 누수 게이트

## 목적

BattleBridge 가 `ActiveDeck.battleMode` / 관련 데이터를 읽어 (a) 고정간격 생성 경로를 잇고,
(b) 누수 한계를 **무제한 또는 개수**로 게이트한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - 생성부: `TryInitializeGeneratedWaves` 부근 (라인 ~1587-1597)
  - 패배 게이트: `DrainGoalEvents` (라인 ~3908-3921)

## 구현

1. **고정간격 연결**: `WavePatternGenerator.Generate(ActiveDeck, waveSeed)` 가 unit 1 에서
   `deck.fixedWaveIntervalSec` 를 이미 전달하므로, Bridge 는 추가 작업 없이 엔드리스 덱이면 자동으로
   고정간격 플랜을 받는다. (별도 분기 불필요 — 데이터 구동.)
2. **누수/패배 게이트** (`DrainGoalEvents`):
   ```csharp
   int leakLimit = EffectiveLeakLimit();
   bool defeatEnabled = ActiveDeck != null && ActiveDeck.defeatGoalReachedCount > 0;
   if (defeatEnabled && !_resultShown && _goalReachedCount >= leakLimit) { /* 기존 패배 */ }
   ```
   - `defeatGoalReachedCount <= 0`(무제한)이면 패배 트리거 스킵 — 계속 플레이.
   - `_goalReachedCount` 증가·`RefreshLeakHud()` 는 무제한이어도 그대로(점수/HUD 반영).
3. 무제한일 때 HUD 표기(`RemainingLeakAllowance` 등)가 음수/이상값으로 깨지지 않는지 확인 —
   필요 시 무제한 표시(∞) 가드. (표시 세부는 최소한으로.)

## 완료 기준

- 컴파일 통과.
- **메인 덱 동작 불변**: `defeatGoalReachedCount>0` 이라 패배 게이트가 기존과 동일하게 작동.
- 엔드리스 덱(`defeatGoalReachedCount<=0`)은 누수해도 패배하지 않고 3분 풀타임 진행.
- 엔드리스 덱이 10초 간격 플랜을 받는지 로그로 확인(`waves.Count`, 첫 웨이브 간격).
