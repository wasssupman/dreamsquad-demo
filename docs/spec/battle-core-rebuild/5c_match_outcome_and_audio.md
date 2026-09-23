# 5c — 판 종료 · 결과 화면 · 제출 · 사운드 · 모드 진입 (조각 B · 3/3)

## 목적

**판이 끝나면 결과가 보이고 제출되고, 전투에 소리가 난다.** 이 둘은 지금 브리지 안에만 있어(`BattleBridge.cs:7223~7277` 제출·결과, `:4840·5453·7797` 사운드 3건) 어느 unit 도 안 받으면 unit 8 브리지 삭제와 함께 사라진다. 이 unit 이 끝나면 **사용자 플레이 1차**.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 판 종료 | `BattleCoreUnity/CoreMatchOutcomePresenter.cs`: `MatchEnded` 구독 → `IMatchGoal.BuildOutcome` 의 `MatchOutcome`(복사본) 을 `ResultScreen` 에 넘긴다. **`submitsReport && allowSubmit` 일 때만** `TournamentMatchReporter.ReportResult` 호출 — 시그니처 무변(v1 서버 무변, 계약 13). `bridge-fields.md` 의 `resultScreen`(25행)·`scoreRules`(26행)의 새 주인 = 여기. `sortDirection` 은 `MatchOutcome` 이 나른다 |
| 사운드 | `BattleCoreUnity/CoreBattleAudio.cs`: `AttackResolved`·`ProjectileSpawned`·`Placed` 구독 → `SoundManager`(제약 5 의 의도된 예외) 호출. **클립은 뷰 데이터 SO 가 defIndex 로 나른다** — 코어 정의표는 엔진 타입을 못 든다(계약 4). 뷰 코드가 클립을 고르지 않는다 |
| 모드 진입 3단 | `BattleDriver.Begin(ModeSelection)`: 테스트 모드 강제 > 로비 지정 > 기본 모드 SO. 로비 UI 는 범위 밖 — dev 토글로 새 씬 진입 |
| 테스트 | 새 PlayMode lane: 3분 완주 → `ResultScreen` 표시 1회 · `allowSubmit=false` 모드에서 `ReportResult` 호출 0 |

## 구현

1. 종료 후 드라이버는 틱 0(계약 5). 여운(죽는 애니·숫자)은 뷰 소관(X1) — 코어는 이미 끝났다.
2. 결과 화면은 `MatchOutcome` 만 읽는다 — `ScoreLedger` 를 되묻지 않는다(계약 7).
3. 사운드 호출은 사건당 1회, 뷰 풀과 같은 `ViewOrder` 방출을 탄다(유닛 동기 뒤).

## 이식 제외 — 사용자 플레이 1차에서 「아직 안 보이는 것」

| 안 보이는 것 | 켜지는 unit |
|---|---|
| 드림캐쳐 카드·손패·각성 | unit 7 |
| 상태이상 FX·지속 피해·실드 부여 연출·오라·빔 | unit 6(사건) + 뷰 풀 동시 |
| 기믹(사직서·열기/피로·레드불) · 보스 도약 연출 · 분열 | unit 6·7 |
| 해저드·장판·픽업 | unit 6 |
| `GamePhase.Tally` 합산 연출 | 이미 은퇴(X19) |

## 완료 기준

- [ ] 결과 화면 1회 · 제출 게이트 테스트 초록.
- [ ] 전투 사운드 3종이 새 씬에서 난다(공격·발사·배치).
- [ ] `bridge-methods.md` 미정 중 결과·제출·사운드 호출 몫 닫힘. **조각 B 종료 시 잔량을 README 상태 라인에 숫자로**(목표: 브리지 본체 105 중 뷰·입력·HUD·결과·사운드 몫 전부).
- [ ] **사용자 플레이 1차의 질문은 「배치·이동·전투·점수·종료의 손맛이 옛 씬과 같은가」다.** 카드·상태 FX·기믹의 부재는 위 표로 미리 고지하고 판정에서 뺀다 — 그 질문을 통째로 던지면 답이 항상 「아니다」가 돼 진짜 차이가 묻힌다.
- [ ] `core-reviewer` APPROVE → **조각 B 를 main 에 머지**(리뷰 뒤 · 푸시는 승인제).
