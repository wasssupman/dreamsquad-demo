# 2 — BattleBridge 모드 인지 (진입·간격·누수·점수·리포트)

## 목적

BattleBridge 가 `battleMode` 를 인지해 무한 모드를 구동한다. **모든 모드 분기를 여기 한 곳에** 모은다
(계약 1). `ScoreMath` 순수함수·스케줄러·`ForceNextWave` 는 **건드리지 않는다.**

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
  - 맵/덱 선택 블록 (라인 ~869-897)
  - `DrainGoalEvents` (라인 ~3910)
  - `CalculateBattleScore` (라인 ~4032)
  - `ReportMatchResult` (라인 ~3973)

## 구현

1. **엔드리스 진입 (공용 풀 아님 — critic MAJOR#2)**:
   - `[SerializeField] private MapDocumentPool.Entry endlessEncounter;` (map+deck 한 쌍).
   - dev 토글: `DevMapOverride` 에 `Endless` bool 추가(또는 미러 static) — 패널에서 켜기.
   - 선택 블록 **맨 앞**(라인 869 이전)에 분기:
     ```csharp
     if (DevMapOverride.Endless && endlessEncounter.deck != null) {
         activeDoc = endlessEncounter.document; _resolvedDeck = endlessEncounter.deck;
     } else { /* 기존 풀 선택 그대로 */ }
     ```
   - **`mapPool` 은 손대지 않는다** → count 불변 → `MapPoolSelect` 무손 → 토너먼트/디버그 선택
     byte-identical (회귀 0). 제외 필터·"엔드리스 풀끝" 규약 불필요.
   - `IsEndless => _resolvedDeck != null && _resolvedDeck.battleMode == BattleMode.Endless`.
2. **고정 간격**: `WavePatternGenerator.Generate(ActiveDeck, seed)` 가 unit 1 에서 `fixedWaveIntervalSec`
   를 전달하므로 추가 분기 없음(데이터 구동).
3. **누수 게이트 (무제한-only v1)** — `DrainGoalEvents`:
   ```csharp
   bool defeatEnabled = !IsEndless;              // 엔드리스는 누수로 죽지 않음
   if (defeatEnabled && !_resultShown && _goalReachedCount >= EffectiveLeakLimit()) { /* 기존 패배 */ }
   ```
   `_goalReachedCount` 증가·`RefreshLeakHud()` 는 그대로(스트레스 점수 반영).
4. **시간축 0** — `CalculateBattleScore` 한 줄(critic 권고, CheckVictory 는 유지 → 조기클리어 OK):
   ```csharp
   int remainingMs = IsEndless ? 0 : Mathf.RoundToInt(RemainingBattleSeconds() * 1000f);
   ```
   `scoreRules`(메인 것) 재사용 — 별도 엔드리스 ScoreRules 없음. `stressLimit=defeatGoalReachedCount`
   그대로(분기 없음).
5. **토너먼트 리포트 스킵** — `ReportMatchResult` 진입부 `if (IsEndless) { /* 로그 */ return; }`.

## 완료 기준

- 컴파일 통과. **메인 모드 완전 불변** — `IsEndless=false` 라 모든 분기가 기존 경로.
- `DevMapOverride.Endless` 켜면 `Deck_Endless` 로드, 10초 간격 플랜, 누수해도 안 죽음, 결과 시간점수 0,
  토너먼트 리포트 미발생 (로그 확인). 실검증은 unit 4.
- **`mapPool.Count` 불변 확인** — 토너먼트 시드→맵 매핑 회귀 없음.

✅ 확인 2026-07-25 — MCP 강제 리컴파일 에러 0 + EditMode 전체 1295/1295 통과(0 실패, 2 skip=기존
[Ignore], 회귀 0). 런타임 진입/누수/시간0 실검증은 unit 4. 커밋 해시는 handoff(unit 5) 참조.
