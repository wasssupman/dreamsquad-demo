# 3 — 오써링 + 씬 배선

## 목적

무한 모드를 구동할 **덱 에셋**을 만들고, BattleBridge 의 `endlessEncounter` 와 dev 토글을 배선한다.
**`ScoreRules_Endless` 에셋·풀 엔트리는 만들지 않는다**(critic 반영: 메인 scoreRules 재사용, 공용 풀 미사용).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Data/Decks/Deck_Endless.asset` (+.meta)
- `Assets/_Project/Scenes/BattleScene.unity` (BattleBridge.endlessEncounter 배선)
- (unit 2 의 dev 토글 UI) `DevMapOverridePanel` 에 Endless 토글 추가

## 구현

1. **`Deck_Endless`** (AttackDeck):
   - `battleMode = Endless`
   - `waveSeed` = 비0 유니크 (예 `20260807`)
   - `minWaveCount = maxWaveCount = 30`
   - `fixedWaveIntervalSec = 10`
   - `timerDurationSec = 180` — **반드시 >0**(엔드리스는 타이머로 종료. 0 이면 종료자 없음)
   - `defeatGoalReachedCount` = **높게**(예 `100`) — 패배엔 안 쓰이고(무제한) **스트레스 점수 예산**으로만
     쓰인다. 180초 내 도달 불가능한 값이라야 누수 페널티가 saturate 되지 않음(critic MAJOR#3, README §).
   - `attackUnitPool`/`bossUnit`/`bossWaveInterval`/escort/`intraWaveSpacingSec`/`min·maxUnitsPerWave`
     = 기존 덱 값 재사용(밸런싱은 후속).
2. **`endlessEncounter` 배선**: BattleScene 의 BattleBridge 에 `endlessEncounter =
   (기존 맵 문서 1개, Deck_Endless)` 할당. **신규 맵 오써링 불필요** — 기존 맵 재사용.
3. **dev 토글**: `DevMapOverridePanel` 에 "Endless" 체크박스 추가 → `DevMapOverride.Endless` 세팅.
4. **가정 명시**: 엔드리스는 **기믹 없음**(gimmickPool 미적용) 전제 → `_leakAllowancePenalty=0` →
   스트레스 = 순수 누수 수. 배선 시 엔드리스 경로에 기믹이 안 붙는지 확인.

## 완료 기준

- `.asset` + `.meta` 짝 커밋 (경로지정 add 시 .meta 누락 금지).
- 에디터에서 `DevMapOverride.Endless` 켜고 Play → `deck 'Deck_Endless'` 로드 로그.
- BattleBridge.endlessEncounter 슬롯 채워짐 확인.
