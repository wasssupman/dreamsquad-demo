# 0 — 골 안정도

## 목적

골에 **부서질 수 있는 체력**을 준다. 적이 골을 뚫으면 그 적의 티어만큼 안정도가 깎이고,
0이 되면 판이 끝난다. 스트레스 한계 패배는 제거하고 스트레스는 집계 지표로만 남긴다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `stabilityDamage`
- `Assets/_Project/Scripts/Data/AttackDeck.cs` — `goalStabilityMax`
- `Assets/_Project/Data/Enemies/*.asset` (11종 + 보스 2종) — 티어별 피해값 저작
- `Assets/_Project/Scripts/Data/Decks/Deck_*.asset` (7개, **`Deck_Endless` 포함**) — 최대 안정도
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DrainGoalEvents`, 패배 판정, 읽기 API
- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — 스트레스 배지를 개수만 표시
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.BattleHud.cs` — 패배 규칙 문구
- `Assets/_Project/Tests/EditMode/ScoreHudStressSeamTests.cs` · `Tests/PlayMode/IncubusPactTest.cs`

## 구현

**1. 안정도 상태** — 브리지가 소유하는 값이다(ECS 컴포넌트·시스템 신설 없음). 판 시작 시
`_stability = ActiveDeck.goalStabilityMax`, 티어다운·`BeginPlacement` 에서 리셋. 읽기 전용
프로퍼티 2개(`GoalStabilityCurrent`/`GoalStabilityMax`)를 노출한다 — unit 1(바)과 unit 3
(tie-break)이 이 창구만 쓴다.

**2. 유출 피해** — `DrainGoalEvents`(`BattleBridge.cs:4643`)에서 처리한다. 이미 있는
`_enemyTypeByEntity`(`:250`, 스폰 시 등록 `:6644`)로 유출한 적의 SO 를 찾아
`stabilityDamage` 만큼 깎는다. 같은 루프에서:

- 기존 뷰 despawn(`:4648-4649`)·표식 회수(`:4653`)·`_goalReachedCount++` 는 **그대로 둔다**
  (적은 지금처럼 골에서 사라진다 — 지속 공격은 `goal-tower-siege` 의 몫).
- `_enemyTypeByEntity` 에서 엔트리를 제거한다. 지금은 킬 경로(`:3618`)에서만 지워서 유출한
  적이 등록부에 남는다.
- SO 조회 실패(맵에 없는 엔티티)는 피해 1 로 폴백하고 경고 1회. 조용히 0 으로 넘기면 유출이
  무해해진다.

**3. 패배 전환** — `_goalReachedCount >= leakLimit` 패배 블록을 제거하고 `_stability <= 0` 으로
바꾼다. 기존 패배 처리(`SetResult("defeat")` → `BeginTally(win:false, ...)`)는 재사용한다.
**엔드리스도 이 패배를 받는다** — `IsEndless` 게이트를 두지 않는다(무한 모드에 끝이 생긴다).
`endless-mode` README 계약 4("누수로 죽지 않음")를 이 결정으로 갱신한다.

**4. 스트레스 배지** — `SetLeakStatus(current, limit, showLimit)` 를 전 모드에서
`showLimit: false` 로 부른다. 분모(`/10`)와 위기색은 한계가 패배와 무관해진 순간 거짓말이
되고, 엔드리스가 쓰던 개수 전용 표시(`ScoreHudView.cs:157-158`)가 이미 그 모양이다.
`EffectiveLeakLimit()`·`TryPayLeakAllowance`(몽마의 계약)는 **건드리지 않는다** — 계약 카드의
코스트 재지정은 README 후속 후보다.

**5. 튜토리얼 문구 — 편집 불요(실측 확인)** — `BattleHud.cs:27` 의 `"스트레스가 {0}이 되면
패배합니다."` 는 `:144` 의 `if (scoreHud.ShowsStressLimit && scoreHud.StressLimit > 0)` 가드
안에 있다. `showLimit: false` 로 바뀌면 이 줄이 **자동으로 생략**된다(엔드리스를 위해 원저자가
넣은 가드가 그대로 작동). 남는 `"악몽을 막아 스트레스 관리하세요!"` 는 여전히 참이다.
사용자 작성 문구를 임의로 바꾸지 않는다. 안정도 바를 가리키는 새 안내는 unit 1 이후의
선택 사항이다.

**6. 저작값** — 적 asset 은 **12종**(일반 10 + 보스 2)이다. 체력 기준 티어(실측 health):

| 티어 | 자산 | `stabilityDamage` |
|---|---|---|
| 보스 | Boss_Jjangssen(950) · Boss_Nightmare(1000) | 5 |
| 엘리트 | Tanker(100) · Vanguard(120) | 2 |
| 일반 | Basic(60) · Rootcaster(45) · Kindler(45) · Debuffer(40) · Needler(35) · Sniper(30) · Swift(30) · Runner(20) | 1 |

`goalStabilityMax`: 9개 덱 전부 20(일반 20기 또는 보스 4기를 흘리면 패배). 판정 기준은 완료
기준에 둔다. 코드 기본값(1 / 20)에 의존하지 않고 asset 에 명시 저작한다.

## 완료 기준

- [ ] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러/경고 0
- [ ] Play: 적 1기 유출 → 안정도가 정확히 그 적의 `stabilityDamage` 만큼 감소
- [ ] Play: 안정도 0 → 즉시 패배. 스트레스가 10에 닿아도 패배하지 않는다
- [ ] Play: 스트레스 배지가 분모·위기색 없이 개수만 표시한다
- [ ] 튜닝 판정: 방어를 거의 하지 않으면 3분 전에 0이 되고, 정상 플레이면 절반 이상 남는다
      (덱 1개에서 2회 측정해 값 보고)
- [ ] 적 asset 12종 **전부** `stabilityDamage` 저작 확인 — 기본값에 의존하면 티어가 안 보인다
- [ ] EditMode: `ScoreHudStressSeamTests` 통과 — 뷰 seam(showLimit 양방향)은 불변이고 호출자만
      false 로 바뀐다. 클래스 주석의 "엔드리스 전용" 서술만 갱신
- [ ] PlayMode: `IncubusPactTest` 통과 — `TryPayLeakAllowance`/`RemainingLeakAllowance` 만
      건드리므로 무영향이어야 한다(패배 게이트 제거의 부작용 확인용)
