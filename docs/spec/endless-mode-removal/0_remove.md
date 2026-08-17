# 0 — 엔드리스 제거 (진입 경로 · 저작 자산 · 모드 축)

## 목적

엔드리스가 남긴 것 셋을 한 번에 걷는다: **모드 축**(`BattleMode`), **진입 경로**
(`DevMapOverride.Endless` + dev 패널 슬롯 + `endlessEncounter`), **저작 자산**(`Deck_Endless`).

## 변경 대상

- **삭제** `Scripts/Data/BattleMode.cs` — 값이 `Main` 하나만 남으면 enum 이 무의미하다
- `Scripts/Data/AttackDeck.cs` — `battleMode` 필드
- `Scripts/Bridge/BattleBridge.cs` — `IsEndless` · `endlessEncounter`(SerializeField) ·
  맵 소스 분기 · `ReportMatchResult` 가드 · 시작 로그의 `endless=` 꼬리표
- `Scripts/Core/DevMapOverride.cs` — `Endless` 프로퍼티 · PlayerPrefs 키
- `Scripts/UI/DevMapOverridePanel.cs` — 스텝 사이클의 ENDLESS 슬롯 · 라벨
- **삭제** `Scripts/Data/Decks/Deck_Endless.asset`
- **삭제** 테스트 2개: `EndlessScoreTests` · `EndlessModeSmokeTest`
- 덱 목록에서 `Deck_Endless` 제외: `DragonBreathAuthoringTests` ·
  `SlimeSplitAuthoringTests` · `WaveConceptAuthoringTests` · `WaveConceptBossTests` ·
  `WaveKillBudgetPinTests`
- `DevMapOverride.Endless` 세이브/복원 제거: `WaypointRoutingLiveTest` ·
  `MapDocumentPoolDevEntriesTests`
- 주석 귀속 정리: `ScoreHudView`(누수 배지 2곳) · `GameManager`(matchesPlayed)

## 구현

**1. `BattleMode` enum 통째 삭제.** `Main` 하나만 남는 enum 은 «모드가 있다» 는 거짓말을
계속한다. `AttackDeck.battleMode` 도 같이 사라지고, 덱 에셋의 `battleMode:` 키는 직렬화
필드가 없어져 다음 저장에 흘려진다(`killScore` 선례).

**2. 맵 소스 분기 제거 — 풀 선택은 손대지 않는다.** 엔드리스는 `mapPool` 을 **건드리지 않는
별도 선행 분기**였다. 그 분기만 빼면 되고 인덱스 계산은 한 줄도 안 바뀐다 → 랜덤/토너먼트
맵 배정이 **byte-identical** 로 남는다.

**3. dev 패널 스텝 사이클에서 슬롯 하나가 빠진다.** `total = steppable + 1`(ENDLESS 자리)
이 `total = steppable` 이 된다. 맵 인덱스 강제와 dev 슬롯은 **그대로 남는다** — 엔드리스와
별개 기능이다.

**4. `ReportMatchResult` 의 엔드리스 가드 제거.** 이제 모든 판이 토너먼트에 올라간다.
그것이 「엔드리스 = 토너먼트에 안 올라가는 덱」이었다는 사실의 정확한 반대다.

**5. `ScoreHudView` 의 `showLimit` 두 갈래는 남긴다.** 「죽는 한계가 없으니 분모 숨김」은
`three-minute-kill-race` unit 2 이후 **모든 판**의 규칙이 됐고, `ScoreHudStressSeamTests`
가 두 갈래를 다 검증한다. 주석의 «endless-mode» 귀속만 고친다.

## 완료 기준

- [x] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러 0
- [x] 코드베이스에 `BattleMode`·`IsEndless`·`endlessEncounter`·`DevMapOverride.Endless`
      참조가 0건
- [x] `Deck_Endless.asset` 부재. 덱 목록을 도는 저작 테스트 5개가 그것을 안 찾는다
- [x] **EditMode 2446/2446 완주 · 신규 실패 0건** (사전 실패 5건은
      `MultiGoalPoolSeparationTests` 4 + `WhirlpotAuthoringTests` — 직전 실행과 동일)
- [x] PlayMode `TallyFlowTest`(6.9s)·`GoalStabilityTest`(21.9s) 초록
- [ ] **Play 육안 미확인**: dev 패널 스텝이 ENDLESS 없이 풀→dev 슬롯만 돈다

> 덱 에셋 12종의 `battleMode:` 키는 남아 있다 — 직렬화 필드가 사라져 Unity 가 다음 저장에
> 흘린다(`killScore` 선례). 12개를 지금 만지면 diff 만 커지고 병행 세션 충돌 면적이 넓어진다.
