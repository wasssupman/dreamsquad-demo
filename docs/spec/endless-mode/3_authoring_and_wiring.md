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
4. **기믹 등장** (2026-07-25 B안): 엔드리스도 `GameManager.AssignGimmick`(모든 진입 경로 공통)으로
   매치 기믹이 그대로 배정된다 — 제거하지 않는 게 의도. 스트레스 점수는 기믹/카드가
   `_leakAllowancePenalty` 를 건드리면 그만큼 반영된다.

## 완료 기준

- `.asset` + `.meta` 짝 커밋 (경로지정 add 시 .meta 누락 금지).
- 에디터에서 `DevMapOverride.Endless` 켜고 Play → `deck 'Deck_Endless'` 로드 로그.
- BattleBridge.endlessEncounter 슬롯 채워짐 확인.

✅ 확인 2026-07-25 — Deck_Endless(battleMode=Endless, waveSeed 20260807, waveCount 30,
fixedWaveIntervalSec 10, timer 180, defeatGoalReachedCount 100) 생성·설정. BattleScene BattleBridge
endlessEncounter=(MapDocument_Serpent, Deck_Endless) 배선(수술적 hunk 커밋 — 무관한 씬 편집 제외).
DevMapOverridePanel 에 ENDLESS 스텝 슬롯(코드, 새 GO 없음). 부팅 실검증은 unit 4 PlayMode.
커밋 해시는 handoff(unit 5) 참조.
