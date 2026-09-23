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

## 고친 것 (기존 코어·Unity 층 변경)

| 무엇 | 왜 |
|---|---|
| `CoreEvent.AttackResolved` 가 `DefIndex` 를 싣는다 | 공격음은 **그 유닛의 저작**(`DefenderUnitData.attackSfxClip`)에서 온다. 줄 번호가 사건에 없으면 소리를 내는 쪽이 코어에 개체를 되물어야 하고, 공격자가 살아 있어 그 되묻기가 «성립은» 한다 — 그래서 위험하다. 한 번 허용하면 다음 사람이 소멸 사건에도 같은 모양을 쓴다(계약 7). `Spawned` 가 5a 에서 같은 이유로 실었다. **골든 무영향** — `DefIndex` 는 트레이스에 실리지 않는다 |
| `ViewOrder.Audio`(60) · `ViewOrder.Outcome`(70) 신설 | 소리는 **그림이 선 뒤**, 결과 화면은 **맨 뒤**. 결과가 먼저 덮으면 마지막 킬의 숫자와 죽는 모션이 화면 밑에 깔린다 |
| `BattleDriver.Begin(ModeSelection)` + `MatchEntryContext` | 3단 서열(`MatchDefinitionBuilder.ResolveMode`)은 unit 4 에 이미 있었지만 **호출처가 0** 이었다. 칸이 둘(강제·지정)인 이유는 세기가 달라서다 — 한 칸으로 접으면 그 서열이 «부르는 쪽 순서»로 옮겨간다. 씬 경계를 넘는 carry-in 은 `TestModeContext` 와 같은 모양이고 **1회 소비**다 |
| `CoreCameraFeed` 가 종료를 `GamePhase.Result` 로 민다 | 5b 가 그 자리에 「5c 에서 한 줄이 붙는다」고 적어 뒀다. ⚠ 디렉터는 `Result` 를 전투 상태로 접으므로(`ResolveState` — 결과는 전면 UI) **오늘 화면은 안 바뀐다.** 그래도 미는 이유는, 안 밀면 디렉터가 「아직 전투 중」이라고 믿고 그 거짓이 결과 레시피가 생기는 날 버그가 되기 때문이다 |
| 장부 오귀속 2건 정정 | 186 `ReleaseCoreBurstHold` 는 접두사 휴리스틱이 `PlacementService` 로 보냈는데 배치와 무관한 **결과 박자**의 리스 정리다. 188 `BuildTally` 는 `ScoreLedger` 가 아니라 `IMatchGoal.BuildOutcome` — 조립 지점은 목표이고 점수는 그 재료 하나다 |
| `MatchOutcome` → `MatchTally` **어댑터 한 줄** | 결과 화면 700줄(2컬럼 랭킹·대기 목록·서버 응답 보류)을 다시 쓰는 것은 이 unit 의 질문에 답하지 않는다. `MatchTally` 는 이미 「조립 지점 하나」라는 같은 계약의 값이고, `MatchOutcome` 이 그것의 후계다 — 화면이 후계를 직접 받는 것은 unit 8 |
| 씬: `SoundManager` · `ResultScreen` · `MatchOutcome` · `BattleAudio` 4 오브젝트 | `SoundManager` 의 클립·볼륨 19개는 **옛 `BattleScene` 블록에서 그대로 복사**했다(값을 지어내지 않는다 — 제약 6). `ResultScreen` 은 자기 캔버스를 스스로 세우므로(`UiCanvasSetup.Ensure`) 루트 오브젝트 하나면 된다 |

## 이식 제외 — 사용자 플레이 1차에서 「아직 안 보이는 것」

| 안 보이는 것 | 켜지는 unit |
|---|---|
| 드림캐쳐 카드·손패·각성 | unit 7 |
| 상태이상 FX·지속 피해·실드 부여 연출·오라·빔 | unit 6(사건) + 뷰 풀 동시 |
| 기믹(사직서·열기/피로·레드불) · 보스 도약 연출 · 분열 | unit 6·7 |
| 해저드·장판·픽업 | unit 6 |
| `GamePhase.Tally` 합산 연출 | 이미 은퇴(X19) |
| **전투 BGM** — `SoundManager` 의 BGM 자동 재생은 `GameManager.PhaseChanged` **구독**이고 새 씬에는 그 매니저가 없다. 클립은 씬에 배선돼 있으니 구동만 붙으면 된다 | unit 8(규칙 보유자 10 이사) |
| **제출 payload 의 덱 스냅샷** — 로거(`GameManager.Logger`)가 주인이다. 점수는 올라가고 덱만 빈다(서버가 경고 한 줄) | unit 8 |
| **결과 화면의 실제 랭킹** — 새 씬은 로비 게이트를 안 거쳐 참가 신청(attemptId)이 없다. 「참가자 찾는 중」 5칸으로 뜨는 것이 정상이다 | 로비 진입이 새 씬으로 옮겨질 때(unit 9) |
| **`WaveClear`·`TimeAttack` 결과 화면의 단위 표기** — 히어로 숫자가 「기」로 못박혀 있는데 그 둘의 점수는 웨이브·밀리초다. 오늘은 모드 SO 가 `KillScoreTimed` 하나뿐이라 닿지 않는다 | 모드 선택 UI 와 같이 |
| **공격음이 START 가 아니라 RESOLVE 에 난다** — 새 코어에는 공격 시작 시각 사건이 없다(모션 트리거도 `AttackResolved` 다). 옛 씬과 미묘하게 다른 박자일 수 있다 | 플레이에서 어색하면 unit 6 |
| **배치 연출 VFX·카메라 흔들기** | 5b 에서 이미 보류 · unit 6 |

## 완료 기준

- [x] 결과 화면 1회 · 제출 게이트 테스트 초록.
      `CoreMatchOutcomeTests` 5종 — 결과 1회(+ 종료 사건 1건 + **종료 뒤 틱 0**) · 게이트 열림/닫힘 ·
      3단 서열(강제 > 지정) · 배치음 1회.
      ⚠ **테스트의 판은 3분이 아니라 2초다.** 시계 길이는 모드 저작이고, 이 lane 이 증언하는 것은
      **사슬이 이어지는가**(만료 → `MatchEnded` → 성적 → 게이트 → 화면)이지 3분이 3분인가가 아니다.
      서버는 안 두드린다 — 이 판들은 참가 신청이 없어 리포터가 통보를 **생략**한다(그쪽의 정상 경로).
- [x] 전투 사운드 3종이 새 씬에서 난다(공격·발사·배치).
      씬에 `SoundManager`(클립 12 · 볼륨 17 옛 씬 값 그대로) + `CoreBattleAudio`. 배치음은 테스트가
      「사건당 한 번」으로 못박고, 셋 다 **Play 실계수**로 확인했다 — 45초 판에서 배치 5 · 공격 11 ·
      발사 8. ⚠ 방어유닛을 **적 근처에 놓아야** 공격·발사가 0 이 아니다. 첫 시도에서 뒷줄에 놓았더니
      셋 중 배치만 울렸고, 잡은 것은 전부 거점이었다 — 거점은 소리를 내지 않는다(옛 거동 그대로).
      ⚠ **계수기가 늘었다고 소리가 난 것은 아니다** — 클립이 없으면 `PlayAttack` 이 무음으로 흘린다.
      그래서 판 위에 **가디언 하나만** 놓고 다시 쟀다(공격 4회). 로스터 8 중 `attackSfxClip` 이
      있는 유닛은 가디언뿐이고(나머지는 투사체 유닛 — 옛 씬도 무음), 배치 보이스는 8/8 이다.
- [x] `bridge-methods.md` 미정 중 결과·제출·사운드 호출 몫 닫힘. **64 → 59**(5행: `SetMatchSeed`·
      `FindDefenderData`·`ReportMatchResult`·`ShowResult`·`HoldThenShowResult`). `check_ledgers.py` exit 0.
      조각 B 전체로는 **128 → 98 → 64 → 59**.
- [x] Play 육안 — 두 판 모두 결과 화면에 도달(30초 판 4기 · 45초 판 11기 · 랭킹은 「참가자 찾는 중」
      5칸) · 콘솔 에러·경고 **0**.
      ⚠ **결과 화면 뒤로 전투 HUD 가 계속 보인다.** 옛 씬은 `GamePhase.Tally/Result` 로 HUD 를
      게이팅했는데 새 HUD 는 페이즈를 안 읽는다(드라이버 읽기 모델만 본다). 결과 패널이 화면
      가운데를 덮으므로 판독은 되지만 옛 화면과 다르다 — 사용자 플레이에서 거슬리면 5c 후속.

- [ ] **사용자 플레이 1차의 질문은 「배치·이동·전투·점수·종료의 손맛이 옛 씬과 같은가」다.** 카드·상태 FX·기믹의 부재는 위 표로 미리 고지하고 판정에서 뺀다 — 그 질문을 통째로 던지면 답이 항상 「아니다」가 돼 진짜 차이가 묻힌다.
- [x] `core-reviewer` APPROVE(2026-09-23 · 위반 0). 리뷰어의 MEDIUM 「`using Wassup.Battle.Units` 잔류」는 **오판** — `Faction` 열거형이 그 네임스페이스에 살아(Skills 어셈블리) 코어 전체가 같은 using 을 쓴다; unit 8 에서 네임스페이스 이사 여부는 별도 판단. 리드 재검증: export 3종 · EditMode 371/371 · PlayMode 21/21. → **사용자 플레이 1차 통과 후 조각 B 를 main 에 머지**(푸시는 승인제).

### 새 씬에 들어가는 법 (dev 토글)

로비 UI 는 이 spec 밖이고, `BattleCoreScene` 은 **빌드 설정에 없다**(옛 `BattleScene` 이 그 자리다 —
unit 9 에서 교대한다). 그래서 진입은 에디터 메뉴다:

- `Wassup/BattleCore/씬 열고 플레이 (기본 모드)` — 드라이버 저작 모드(`MatchMode_KillScore3Min`, 3분).
- `Wassup/BattleCore/씬 열고 플레이 (선택한 모드 SO)` — 프로젝트 창에서 고른 `MatchModeData` 를
  **「로비 지정」 칸**에 넣고 연다. 로비가 설 때 그 UI 가 채울 칸이 이미 값으로 존재한다는 증명이다.
- `Wassup/BattleCore/씬 열기` — 플레이 없이 열기만.
