# 4 — 오써링: 엔드리스 덱 · ScoreRules · 풀 엔트리

## 목적

무한 모드를 구동할 **데이터 에셋**을 만들고 풀에 엔트리로 등록한다. 씬의 BattleBridge 에
엔드리스 ScoreRules 를 배선한다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/Decks/Deck_Endless.asset` (+.meta)
- 신규 `Assets/_Project/Data/Config/ScoreRules_Endless.asset` (+.meta)
- `Assets/_Project/Data/Maps/MapDocumentPool.asset` (엔트리 추가 → index 6, 7번째)
- `Assets/_Project/Scenes/BattleScene.unity` (BattleBridge.endlessScoreRules 배선)

## 구현

1. **`Deck_Endless`** (AttackDeck):
   - `battleMode = Endless`
   - `waveSeed` = 비0 유니크 (예 `20260807`) — 결정론 규칙 준수
   - `minWaveCount = maxWaveCount = 30`
   - `fixedWaveIntervalSec = 10`
   - `timerDurationSec = 180`
   - `defeatGoalReachedCount` = 무제한이면 `0`, 개수 제한이면 양수 (초기값은 무제한 `0` 권장)
   - `stressScoreBudget` = 예 `20` (스트레스 점수 예산)
   - `attackUnitPool` / `bossUnit` / `bossWaveInterval` / escort = 기존 덱 값 재사용
   - `intraWaveSpacingSec`, `minUnitsPerWave`/`maxUnitsPerWave` = 기존 재사용(밸런싱은 후속)
2. **`ScoreRules_Endless`** (ScoreRulesData): `timeScorePerSecond = 0`, `stressScorePerPoint` = 튜닝값.
3. **풀 엔트리**: 기존 맵 문서 하나 재사용(예 `MapDocument_Serpent`) + `Deck_Endless` 로
   `MapDocumentPool.entries` 에 7번째(index 6) 추가. **신규 맵 오써링 불필요.**
4. **씬 배선**: BattleScene 의 BattleBridge 컴포넌트 `endlessScoreRules` 슬롯에 `ScoreRules_Endless`
   할당 (unity-feature-wiring 스킬).

## 완료 기준

- `.asset` + `.meta` 짝 커밋 (경로지정 add 시 .meta 누락 금지).
- 에디터에서 `DevMapOverride` index=6 강제 → `Battle started with ... deck 'Deck_Endless'` 로그.
- 동작(간격/점수/당기기) 검증은 unit 6 스모크에서.
