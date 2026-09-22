# 매치 규칙·경제·시간·프레젠테이션 계약 — 키워드 census

> READ-ONLY 조사. repo `/Users/sy/dev/wassup` @ `main` (HEAD `9461d6ae`), 2026-09-22.
> 모든 포인터는 grep/read 로 실존 확인했다. 확인 못 한 이력은 「이력 미발견」으로 표기.

---

## 1. 키워드 census (61행)

| 키워드 | 한 줄 정의 (게임 언어) | 현행 구현 포인터 | 판정 | 애매하면: 결정 이력 포인터 + 미결 요지 |
|---|---|---|---|---|
| `GamePhase` 7값 | 판의 국면 — 없음·뽑기·배치·전투·결과·집계·기믹 | `Core/GameManager.cs:24` | 확정 | 값 순서 ≠ 시간 순서(Result=4 < Tally=5 인데 시간은 Tally→Result). `Data/CameraDirectionConfig.cs:194` `breathPhases` 가 정수로 직렬화 → **append-only**(빼거나 끼우면 저장된 정수 의미가 밀린다) |
| `SetPhase` / `PhaseChanged` | 국면 전이의 유일 창구, 동기 발화 | `GameManager.SetPhase` | 확정 | `Result` 진입이 `RecordMatchPlayed` 를 겸한다(호출처 2 중 하나) |
| 매치 생애 | 시드→기믹→맵→배치→전투→마감→집계→철거 | `GameManager.Start` → `BattleBridge.BeginPlacement`/`StartBattle`/`EndMatch`/`StopBattle` | 확정 | |
| `EnsureMatchSeed` | 판당 시드 1개. 0 아니면 고정 재현 | `GameManager.EnsureMatchSeed`, `Core/MatchSeed.cs` | 확정 | 파생 **6계열**(Map·Wave·Visual·Pickup·Gimmick·Meteor), salt 로 decorrelated. `GenerateRandom()` 만 비결정론(진입점 1회) |
| `AssignGimmick` | 판당 기믹 1개 배정(없을 수 있음) | `GameManager.AssignGimmick`, `Core/GimmickSelection.cs` | 확정 | 같은 matchSeed → 같은 기믹. `gimmickEnabled` 는 현재 **false 확정**(match-intro-phase-toggles 계약 9, 코드 0줄 변경) |
| 진입 모드 3종 | 테스트모드 > 스쿼드 > draft 폴백 | `GameManager.Start` / `StartTestModeMatch` / `StartSquadMatch` | **애매** | draft 폴백은 로비 `LoadoutGate` **우회 경로**로만 도달한다고 코드가 스스로 로그를 남긴다(`GameManager.cs` 「게이트 우회 진입」). 존치 여부 결정 이력 미발견 |
| `PrepareDraftMap` | 뽑기 화면 뒤에 판을 미리 지어둠 | `BattleBridge.cs:2021` | 확정 | `BeginPlacement` 에 미생성 폴백 빌드가 있다(테스트·직접 StartBattle 용) |
| `BeginPlacement` | 배치 창을 연다 + **매치 경계 리셋 30여 항목** | `BattleBridge.cs:1528` | 확정 | 리셋 항목이 한 함수에 몰려 있고 `StopBattle`·`StartBattle` 에 부분 중복 |
| 배치 페이즈 창 | 길이는 설정값. 끄면 3초 카운트다운 + 입력 전면 차단 | `UI/PlacementPhaseView.BeginPlacementPhase`, `Data/BattleConfig.placementPhaseEnabled`/`autoStartCountdownSeconds` | 확정 | match-intro-phase-toggles 계약 1(3초를 코드에 박지 않는다) |
| 배치 진입 묶음은 두 경로 공통 | **길이가 0이어도 신호는 항상 발화** | 같은 함수 — `SetPhase(Placement)`·`CostRuntime.ResetToStart`·`CooldownRuntime.ResetAll`·`bridge.BeginPlacement()`·`PlacementReady` | 확정 | 계약 3. 페이즈를 건너뛰면 `DefenderSelector.OnPhaseChanged` 가 슬롯을 못 만들어 **전투 내내 트레이가 빈다** |
| 종료 경로 단일 | 자동 시작도 `FinishPlacement()` 로 합류 | `PlacementPhaseView.FinishPlacement` | 확정 | 계약 4. 두 번째 경로가 생기면 코스트 리젠·페이즈 전이 중 하나를 빠뜨린다 |
| 배치 중에도 sim 이 돈다 | `StartBattle` 전에 유닛을 놓고 스킬이 터진다 | `TickBattleFrame` 의 `_running` 앞 구간(`PushBattleTimeScaleToEcs`·적 dim 페이드·`DrainDefenderActivatedEvents`) | 확정 | 「배치 페이즈에 배치한 스킬은 낭비된다」가 사양(README 후속 후보 「배치 페이즈 발동 정책」) |
| 배치 잔상 3종 폐기 | 전투 시작 순간 옛 신호가 일제히 터지는 것 차단 | `StartBattle` — `DestroyEntitiesByType<ProjectileRequestCarrier>()` · `_shieldGrantedEventQueue.Clear()` · `_detectionEventQueue.Clear()` | 확정 | 실측 캐리어 3개가 낡은 좌표로 터졌던 사고가 근거 |
| 3분 고정 타이머 | 판 길이. 저작 플랜이면 플랜 값(0 = 무한) | `_timerDuration` (`StartBattle`), `BattleBridge.CheckTimer:7173` | 확정 | 저작 모드는 `plan.timerDurationSec`, seed/legacy 는 `deck.timerDurationSec` |
| `EndMatch` 호출처 **정확히 3** | `complete`(만료) · `stress_full`(마음 붕괴) · `submitted`(유저 제출) | `BattleBridge.cs:7181` / `:7072` / `:7161`, 정의 `:7246` | 확정 | three-minute-kill-race 계약(「EndMatch 를 새로 부르면 패배 부활」)을 heart-stress-axis 가 **의도적으로 뒤집어** 3으로 재고정(2026-08-23 사용자 결정, `docs/spec/README.md` 의 kill-race 그룹 머리말) |
| 「패배 없음」 | 승패 표기 없음. 단 **끝날 수는 있다** | `Core/MatchTally.cs` `Outcome` 주석 | **애매** | CLAUDE.md/메모리 요약의 「패배 제거」는 현행과 다르다. `stress_full` = 남은 시간 전량 몰수. heart-stress-axis README 「질 수는 없지만 끝날 수는 있다」 |
| 종료 통로 2 vs 라벨 3 | 게임 규칙상 통로는 2개, `submitted` 는 절차 밖 탈출구 | `MatchTally.Outcome` 주석 (사용자 결정 2026-08-23) | 확정 | UI·문구에서 제출을 「게임을 끝내는 방법」으로 승격 금지 |
| `SubmitMatch` / 60초 잠금 | 유저만 판을 조기 종료. 페널티 없음 | `BattleBridge.SubmitUnlockSec = 60f`, `CanSubmit`, `SubmitMatch:7158` | 확정 | 시계는 `_battleClock`(메뉴 연 시간 미포함). backlog: 「제출 개방 인지」[S]·「조기 제출의 동기」[S] 미결 |
| `BuildTally` / `MatchTally` | 판 성적 조립의 유일 지점 | `Core/MatchTally.cs`, `BattleBridge.cs:7327` | 확정 | 아키텍처 무참조 순수 값(UnityEngine/Entities 미참조) |
| 점수 = 킬 수 생값 | 1킬 = 1점, 예외 없음(보스·분열체 포함) | `MatchTally.Kills`/`Total`/`SubmissionScore`, `ReadFinalTally:3190` | 확정 | `killScore` 티어 가중 축 은퇴 · `ScoreRulesData` 폐기 · 시간/스트레스 배점 폐기(battle-score-formula → three-minute-kill-race unit 1) |
| 제출값 무가공 | 화면 숫자와 서버 값이 완전히 같다 | `MatchTally.SubmissionScore`, `ReportMatchResult:7216` | 확정 | 남은 안정도를 값에 실어 동점을 가르던 인코딩은 폐기 |
| `ReportMatchResult` | 제출이 화면보다 **앞** | `BattleBridge.cs:7216` | 확정 | score-tally-sequence 계약 3 — 화면 기다리다 앱이 죽으면 기록이 사라진다 |
| `GamePhase.Tally` | 전투 HUD 게이팅용 중간 박자 | `EndMatch` → `SetPhase(Tally)`, 소비처 `UI/ScoreHudView.cs:860`·`UI/Tutorial/FirstRunTutorialController.cs:164` | **애매** | **합산 연출은 은퇴**(`EndMatch:7242` 주석)인데 `docs/spec/score-tally-sequence/README.md` 는 4.0초 3축 시퀀스를 아직 정본처럼 서술. `TallySequence`/`ScoreTally` 심볼 grep 0건 → 문서 stale |
| `coreBurstHoldSec` | 마음이 터진 판에만 붙는 슬로우 박자 | `HoldThenShowResult:7282`, `TimeManager.Request(Battle, coreBurstTimeScale, priority:100)` | 확정 | 대기는 `WaitForSecondsRealtime`(unscaled) — 스케일 시간으로 재면 박자가 배로 늘어난다 |
| `PlayCoreBurst` | 붕괴 연출을 규칙에서 떼어낸 자리 | `BattleBridge.cs:7308` | 확정 | 원래 유출 배수구 안에만 있어서 배수구를 안 부르자 연출까지 죽었던 사고의 수정 |
| 종료 후 sim 이 계속 돈다 | `_running=false` 는 **브리지 프레임만** 멈춘다 | `TickBattleFrame` early-return | **애매** | score-tally-sequence 계약 6이 근거로 든 `PushBattleRunningToEcs`/`BattleRunning` 은 **레포 전체 grep 0건**. ECS `BattleSimGroup` 은 결과 화면 동안에도 전진한다 |
| `RecordMatchPlayed` | 「히스토리에 남는 판」 카운터 | `GameManager.RecordMatchPlayed` | 확정 | 호출처 2(Result 전이 · `MenuPopup.OnExit`), 판당 래치. Test Mode 도 센다(경험 신호) |
| `StopBattle` / `TeardownCurrentBattle` | 판 철거. 큐·쿼리·맵·뷰 반납 | `BattleBridge.cs:1982` / `:780` | 확정 | |
| `OnRestartRequested` | 같은 씬 재시작 | `BattleBridge.cs:737` | **애매** | dormant. 되살리면 `_matchRecorded` 래치 리셋 필요(GameManager 주석이 명시) |
| 유출 `_goalReachedCount` | 골에 도달해 사라진 적 수 | `EffectiveLeakLimit:6375`, `RemainingLeakAllowance:6717`, `LeakSiegingEnemy:7118` | **애매** | 현재 **아무것도 판정하지 않는다**(HUD 미표기). `OpenBreachedCellsForLeak:7080` 은 **호출처 0 인 휴면 코드**(heart-stress-axis unit 0 이 명시적으로 남겨둠) |
| `Leaks` (결과 집계) | **돌격형이 마음 치고 산화한 수** | `_rusherArrivalCount` → `MatchTally.Leaks` | 확정 | ⚠ 뜻이 바뀌었다. 구 의미(부서진 마음으로 적이 흘러듦)는 첫 붕괴에 판이 끝나 **구조적으로 발생 불가**. 화면 라벨을 「유출」로 쓰면 거짓말(MatchTally 주석) |
| 몽마의 계약 선불 | 유출 허용치를 미리 깎아 이득을 산다 | `TryPayLeakAllowance:6723`, `_leakAllowancePenalty` | **애매** | 한계 표기 자체가 사라져 **완전히 공짜**. `docs/spec/README.md` backlog 「몽마의 계약 코스트 재지정」[M] (three-minute-kill-race) |
| 마음 `Health` = 정본 | 스트레스는 별도 리소스가 아니라 체력의 표시 반전 | `Core/StressMath.cs` (`Max = 100f`, `FromHealth`, `IsFull`) | 확정 | heart-stress-axis 핵심 구조. 「100」은 표시 정규화이지 HP 최대치가 아니다. 실 HP = `AttackDeck.goalStabilityMax`(라이브 1500) |
| 스트레스 100 = 종료 | 첫 마음 붕괴가 곧 판의 끝 | `SyncGoalStability:7070` → `EndMatch("stress_full")` | 확정 | 골 개수 **무관**하게 「첫」 붕괴에서 끝난다(`StructureSpawnAndBreachTests` 2타워 단언이 고정) |
| 킬 회복 | 잡을수록 마음이 회복 | `EnqueueGoalHeal:6851`, `AttackDeck.killHealPerAwakening` | 확정 | 회복량 = `awakeningReward` 재사용 × 배율(명제 7, 새 저작 필드 없음). 저울은 `killHealPerAwakening` 하나 — HP 는 시계일 뿐 |
| 마음 방패(본능) | 방어 본능이 살아있는 동안 마음이 표적에서 제외 | `CoreShielded` 토글, `SyncGoalStability:7020` | 확정 | ⚠ 구조 변경이라 **`Update` 단계여야 안전**(LateUpdate 이동 또는 sim 중 두 번째 호출처 금지 — `EntityTypeHandle invalidated` 실측 사고) |
| 마음 공유 체력 (unit 12) | 마음 N개 · 저수지 하나 | `docs/spec/heart-stress-axis/12_shared_heart_pool.md` | **애매** | **작성됨 2026-08-25 · 착수 전**. 명제 10(마음 1개) 뒤집기. 마음 엔티티에서 `Health` 를 **떼어** 싱글턴으로 이사(미러 금지) · 회복 **1회만** 적용 · 심박 위상을 루프 밖으로 · `MapDocument` 체력 오버라이드. `wide-board-content` 가 선행 요구, `wide-board-camera` unit 7 이 이 계약에 기댐 |
| 적 마음 (`EnemyCore`) | 부숴도 판이 안 끝난다. 사격이 멎을 뿐 | `_enemyCoreCurrent` 미러, `SyncGoalStability` | **애매** | backlog 「적 마음(공성 맵)의 새 역할」[M] — 점수원/연출 미정 (three-minute-kill-race) |
| `CostRuntime` | 배치 자원. 배틀 도메인 시계로 재생 | `Core/CostRuntime.cs` | **애매** | **재생 스위치를 UI가 소유**(`PlacementPhaseView.FinishPlacement` → `BeginRegen`). 스크립트 진입은 그 뷰를 안 지나 코스트가 0에 멎고 배치가 전부 `InsufficientCost` 로 거부 — `SimHarnessRunner.StartMatch:196` 이 UI 역할을 대행 중(harness≠live 갭) |
| 코스트 배율(드림스톤) | `CostRate` 스톤만 재생 배율로 라우팅 | `CostRuntime.SetRegenRateMultiplier`, `GameManager.ResolveCostRateMultiplier` | 확정 | 매치 진입 2곳(`StartSquadMatch`·`StartTestModeMatch`)만 호출 가능. `ResetToStart`/`Configure` 는 배율을 절대 건드리지 않는다 |
| `PlacementCooldownRuntime` | 유닛 타입별 재배치 대기(사망 쿨타임 포함) | `Core/PlacementCooldownRuntime.cs` | 확정 | 0 = inert(등록조차 안 함). 배치 페이즈 진입마다 `ResetAll` |
| `SkillRuntime` | 액티브 스킬 쿨다운 | `Core/SkillRuntime.cs` | 확정 | `BeginPlacement` 에서 `ResetAll` |
| 트레이 = `defenderPool` 슬롯 | 배치 가능한 유닛 슬롯 | `SetDefenderPool:2613`, `DeployedCountOf:7519` | 확정 | 타입별 판 상한(`LimitReached`)이 존재 — 하네스가 슬롯마다 다른 유닛을 놓는 이유 |
| 각성 게이지 · 12장 덱 | 킬/사망이 각성을 주고 손패가 돈다 | `Core/Dreamcatcher/DreamcatcherHandController.cs` (`GainAwakening`, `AwakeningOverflowed`) | 확정 | 저장 덱 10 + 공용 Active 2 = 12, 시드는 매치 시드. **퇴근은 회수만, 각성 없음**(각성은 처치/사망 보상) |
| `WavePatternGenerator` 단일 RNG | 웨이브 편성 전체가 시드 하나의 소비 순서 | `Data/WavePatternGenerator.cs:106-108` | 확정 | **rng 소비 순서가 계약**이다. 보스 1종이면 rng 미소비 가드(`:262`), 램프 `NextFloat` 1콜 = 기존 `NextInt` 1콜 등 byte-identical 유지 장치가 곳곳에 |
| 플랜 우선순위 | 테스트모드 플랜 > 맵 인카운터 > 시드 생성 > 레거시 스폰 | `TryInitializeGeneratedWaves:2102` | 확정 | 테스트 모드가 이기는 이유 = 「지금 이 플랜을 보겠다」는 명시 지시 |
| `waveSeed` 결정론 | 비0 = 고정(같은 맵 같은 웨이브), 0 = matchSeed 파생 | 같은 함수 + `MatchSeed.DeriveWaveSeed` | 확정 | |
| 웨이브 케이던스 | **전멸 OR 상한 경과** (시각 그리드는 명목값) | `QueueDueWaves:2177`, `MaxWaveIntervalSec` | 확정 | 저작 플랜은 예외(저작 `durationSec` 타임라인이 정본). 웨이브 1은 무조건 시작 시 발사 |
| `NoQueuedAttackersRemain` | 전멸 판정. **전용 쿼리** | `BattleBridge.cs:7202` (`_aliveNormalAttackersQuery`) | 확정 | ⚠ 공용 `_aliveAttackersQuery`(소비처 11)에 필터 걸면 보너스 적이 광역기·배치 스킬에서 통째로 사라진다 |
| 드레인 순서 계약 | `DrainEnemyKilled` → `QueueDueWaves` | `TickBattleFrame:3128` | 확정 | 뒤집으면 분열 자식이 태어나기 전에 전멸 판정이 참 → 「엘리트 죽이면 판이 빨라지는」 역인센티브 |
| 보스 케이던스 | N웨이브마다 보스 1(선봉) + 호위 [min,max] | `WavePatternGenerator.cs:250-305` (`bossWaveInterval`·`bossEscortMin/Max`) | 확정 | 랜덤 루프 **뒤 후처리**. 호위는 블록 컨셉의 성질·위상을 입되 `countMul` 은 적용 안 함(이중 스케일 방지) |
| 보스 경보 | 보스 판별 단일 지점에서 구동 | `BakeNightmareMechanics:10108-10121` → `_bossWarning?.Show()` | 확정 | `SpawnUnit` 재판정 금지(로직 이중화·이중 발화). 재진입 코얼레스는 뷰 담당 |
| 당김 2층 | 규칙층 `TryPullNextWave` / 기제층 `ForceNextWave` | `BattleBridge.cs:2524` / `:2535`, `PullAllowed:2517` | 확정 | 상한 = 덱 `maxPullsPerClear`(`_pullsSinceClear`), **전멸로만 회복**(상한 경과는 회복 아님). 저작 플랜은 `PullCapApplies=false` 로 면제 |
| `ForceNextWave` 는 판 동력 | PlayMode 스모크가 이걸로 판을 굴린다 | 같은 함수 주석 | 확정 | no-op 으로 만들면 `TallyFlowTest`·`MovementIntegritySmokeTest` 가 타임아웃 |
| 보너스 웨이브 | 별도 큐·타임라인·포탈. 본류와 코드 경로 무공유 | `Bridge/BattleBridge.BonusWave.cs` | 확정 | 트리거 = 일반 킬 N(회수) AND 스트레스 ≤ 임계(창), 회수는 창이 닫혀도 쌓임. **래치**(`_bonusOfferLatched`)라 문턱에서 안 떨린다. 포탈 배정 `i % portalCount`, 동시 1벌 |
| `TickBonusWave` 위치 | 펌프가 `TickBattleFrame` **안**이어야 한다 | 같은 파일 | 확정 | `Update` 직하에 두면 하네스(`StepOneTick`)와 라이브가 갈린다 |
| `SpawnLeadInSec` | 웨이브 트리거 ~ 첫 적 등장 사이 리드인 | `_wavePlan.spawnLeadInSec`, `QueueWave:2578` | 확정 | ⚠ 트리거 그리드/`_waveTimeShift` 산식에 **절대 섞지 말 것**(당김 연타마다 누적 왜곡) |
| `_spawnSpreadCounter` | 같은 셀 겹침을 푸는 측면 오프셋 | `ComputeSpawnLateralOffset:1135`, `SpawnSpread.LaneFraction` | **애매** | **가변 상태**라 「N번째 스폰 위치」를 알려면 앞의 N-1기를 재생해야 한다. follow-up(battle-sim-extraction README): `LaneFraction(spawnOrdinal, …)` 파생 전환 + `wrapShift = (ordinal/laneCount) × ε` |
| 분열 자식 | 죽은 부모 자리에서 즉시 태어남 | `SpawnSplitChildren:11051` | 확정 | 레인·경로 모두 -1(부모 투어 미상속). ECB 아닌 직접 `AddComponent` 라 같은 프레임에 쿼리에 들어온다 |
| 강제 웨이브 로그 | `wave_forced` / `wave_started` / `bonus_pull` | `Logger.RecordWaveEvent` (`ForceNextWave`·`QueueWave`·`TryBonusPull`) | 확정 | |
| `TimeManager` | 도메인 스코프 시간 리스. 승자 = priority desc, 동률 scale asc | `Core/TimeControl/TimeManager.cs`, 도메인 2(`Battle`·`Interaction`) | 확정 | 의도된 예외 싱글턴(제약 5). `UnityEngine.Time.timeScale` 은 항상 1 |
| `BattleTimeScale` + `BattleScaledRateManager` | 브리지가 매 프레임 써서 ECS 그룹 dt를 스케일 | `Battle/BattleScaledRateManager.cs`, `PushBattleTimeScaleToEcs:3787` | 확정 | scale ≤ 0 = `PushTime` 안 함 = 그룹 멤버 전부 미실행(유휴 tick 0). 로컬 `_elapsedTime` 누산(정지 후 재개 점프 방지) |
| 슬로모 = 잡고 있는 동안 | 카드·유닛 드래그 중 판이 느려진다 | `UI/Dreamcatcher/DreamcatcherHandView.cs:597`(priority 50) · `UI/DefenderDragPlacementController.cs:360`(0) · `UI/DefenderRelocationController.cs:128`(0) · `UI/Dreamcatcher/DcInspectController.cs:532` | **애매** | 「슬로모는 뷰 전용 — 결정론 불변」은 **거짓**이다. 같은 `ScaleOf(Battle)` 가 `_battleClock`(`TimeManager.DeltaTime`)과 ECS 그룹 dt를 **둘 다** 스케일한다. 참인 것은 *「같은 dt 열이면 같은 결과」* 뿐. M1 후속 후보에 `pause/slow-mo gameplay 시계 정책` 으로 올라 있음 |
| 일시정지 | 메뉴 = Battle 도메인 scale 0 | `UI/MenuPopup.cs:78` (priority 100) | 확정 | 튜토리얼 freeze 도 같은 형태(`FirstRunTutorialController.cs:737`) |
| `_battleClock` | 웨이브·스폰·타이머·제출 해금의 유일 시계 | `TickBattleFrame:3120` | 확정 | `Time.time` 쓰면 메뉴 연 시간까지 센다 |
| `SimHarnessClock` / `StepOneTick` | 고정 스텝 하네스. 「얼마나」와 「언제 한 번」을 둘 다 준다 | `Core/TimeControl/SimHarnessClock.cs`, `BattleBridge.cs:3367` | 확정 | 순서 = **런타임 3개 tick(CostRuntime·CooldownRuntime·SkillRuntime) → 브리지 프레임 → ECS 1스텝**. `Update` 와 상호배타(`SimHarnessClock.Active` 게이트). `Time.captureDeltaTime` 도 고정 |
| `MatchConfigSnapshot` / `configHash` | 판의 「조건」을 불변 텍스트로 물질화 + SHA-256 16자 | `Core/MatchConfigSnapshot.cs`, `CollectMatchConfig:3216` | 확정 | **아트 참조 제외**(Sprite/Material/Prefab/Texture/AudioClip/Shader), 데이터 SO 는 리플렉션으로 통째 접기, 필드 **이름순**. 수집 실패해도 판을 막지 않는다(해시를 비우고 진행) |
| `LegacyTraceV0` / 관측 탭 | 드레인 지점에서 받아 적는 회귀 기준선 | `Core/Trace/LegacyTraceV0.cs`, `Core/Trace/LegacyTraceRecorder.cs`, `BattleBridge.cs` 내 `Ev` 호출 22건 | 확정 | **채널 22 · 탭 22** — 스펙 문서의 「19」는 stale(Detection=20·DefenderActivated=21·DefenderAiState=22 가 append 됨). 고정 폭 레코드 `tick·channel·a·b·i·f`, **Entity 미탑재**, 저장 전 직렬화 왕복 게이트 |
| 골든 코퍼스 | seed 시나리오별 기준선 | `Editor/Battle/SimHarnessRunner.Corpus`, `Assets/_Project/Tests/Golden/*.trace.txt`, `docs/spec/battle-sim-extraction/golden-corpus.md` | **애매** | 선언 **9종**(basic·long_boss·seed_b·seed_c·no_defense·summoner·restart·force_wave·**wide_body**), 베이크 **8종** — `wide_body` 는 「선행 결함 해소 뒤」로 보류(`7_corpus_wide_body_scenario.md`) |
| 코퍼스가 머신 상태를 상속 | 방어 덱이 개발자 `profile.json` 스쿼드를 따라간다 | `SimHarnessRunner.ApplyScenarioPool:209` (`_sceneDefaultPool` 캡처가 `PrepareDraftMap` 뒤) | **애매** | 2026-09-07 발견, **미해결**. 처방 3단계(전 시나리오 덱 명시 + 암묵 상속을 셋업 실패로 / 조건 지문을 골든에 함께 굽기 / 전량 클린 재베이크) 미구현. 같은 계열 전력: `DevMapOverride` PlayerPrefs(09-04 핀) · 시트 in-memory 값 · meta GUID 깨짐. **핀은 아는 축만 막는다** |
| LoginAutoImport 차단 | 하네스 중 시트 임포트가 SO 를 덮지 않게 | `UI/Outgame/LoginAutoImport.cs:76` | 확정 | one-shot `_done` 을 **소비하지 않는다**(소비하면 라이브 갱신이 조용히 사라짐). `LoginAutoImportTests.HarnessActive_SkipsImport_AndKeepsTheOneShotUnspent` |
| parity 기준 | 정수·이벤트 시퀀스 = exact / 연속값 = 1e-3 격자 | `4_legacy_trace_golden.md`, `TraceEvent.Quantize` | 확정 | 판독 순서: `DiffAgainst` 가 **`configHash` 불일치를 가장 먼저** 보고(드리프트를 회귀로 오진 방지) |
| 동률 예외 5종 | parity 실패로 치지 않되 발생 시 로그 | 같은 문서 | 확정 | KillAttribution 등량 · Aggro FIFO 축출 · Cc/Stat 병합 동키 · Stack/Dot 병합 동키 · HazardSingleton 셀 순회. (`HazardCast` 최근접은 unit 1 tie-break 로 해소돼 제외) |
| `SimEntityId` | 매치 내 비재사용 안정 ID. 타겟팅 동률·RNG seed·이벤트의 유일 축 | `Battle/Units/SimEntityId`, `BattleBridge.AttachSimEntityId:866`, `SimIdOf:3199` | 확정 | `Entity.Index/Version` 사용 금지(unit 1 이후). 싱글턴 승격은 M1 로 반환(공유 카운터가 ID 열을 밀어 골든 전건 발산) |
| 토너먼트 attempt | play 발행 창구는 로비 게이트 **하나** | `Core/Api/TournamentMatchReporter.BeginMatchFromLobby:58` / `BeginMatch:88` | 확정 | `BeginMatch` 는 **adopt-only** — 테스트/에디터 직접 Play 는 실서버 엔트리를 만들지 않는다(unit 8). 락 에러(`cannot wait`)는 pending 이 있으면 1회 재시도 |
| 서버 seed → 맵 | 서버 시드가 맵 풀 인덱스를 결정 | `TournamentMatchReporter.HasTournamentSeed`/`TournamentSeed` | 확정 | 부재(게스트·in-flight·실패) 시 index 0 폴백 |
| `deckInfo` / `AbandonMatch` | 반입 덱 스냅샷 · 나가기는 0점 마감 | `PersistMatchDeck:176`, `AbandonMatch:262`, `GameManager.PersistTournamentDeckSnapshot` | 확정 | 나가기도 **히스토리에 남는다**(그래서 `RecordMatchPlayed` 호출처가 둘) |
| 튜토리얼 판 | 저작 웨이브 + 첫 손패, 토너먼트 미제출 | `GameManager.StartSquadMatch` 의 `isFirstRunTutorial` 분기 | 확정 | 콘텐츠는 `tutorial-content-teardown`(76038c26)으로 전량 제거, **도구만 잔존**. 억제 setter 는 **무조건** 호출(if 안에 두면 다음 판이 true 물려받음) |
| 채널 31 수명 소유 | 31개 `NativeQueue` 의 생성·부착·반납이 전부 브리지 | `EnsureQueriesAndQueues:1679`, `DisposeEcsInfrastructureNativeContainers:960` | 확정 | `Allocator.Persistent` + 싱글턴 엔티티에 부착 |
| LateUpdate 순서 계약 | 도약 2채널 드레인 → `MirrorLiftKnobs` → `SyncMonoUnitViews` → … → `SyncProjectileViews` | `BattleBridge.LateUpdate:3409` | 확정 | 도약 드레인이 뷰 동기 **앞**이어야 1프레임 팝이 없다. `MirrorLiftKnobs` 는 매 프레임(1회 스냅샷 금지 — 인스펙터 튜닝 비대칭) |
| `UnitView` 3백엔드 | Spine / Sprite / Quad, 선택은 `TrySpawn` 한 곳 | `Presentation/UnitView.cs`(추상 베이스), `SpineUnitPool.TrySpawn` | 확정 | 인터페이스면 Unity fake-null 문제 → 추상 베이스로 |
| `BoardSpace.ToView` | sim 좌표 → 뷰 좌표. **sim-Y 를 버린다** | `Core/BoardSpace.cs` | 확정 | 평면 tilemap 보드라 높이는 뷰 공간 오프셋으로 따로 넘긴다 |
| 뷰 등록부 in bridge | `_defenderByTile`(판 위 유닛의 유일 진실) 외 10여 개 | `BattleBridge.cs:280-326` | 확정 | `_defenderByTile` 은 **유닛당 1엔트리**(대표 셀 키), 셀→유닛은 `_defenderCellOwner`. 등록/해제는 `OccupyDefenderFootprint`/`ReleaseDefenderFootprint` 두 함수만 |
| `LegacyTraceRecorder.Ev` 로깅 우선 | 브리지 드레인은 **첫 줄에서** 트레이스를 기록 | 22개 드레인 | 확정 | CLAUDE.md 「로깅은 마지막이 아니라 첫 축」 |
| battle-log-v2 | 배틀 JSON 로그(별개 축, 트레이스 아님) | `docs/spec/battle-log-v2/`, `Logging.BattleLogger` | **애매** | 상태가 아직 `in progress 2026-07-07`. Follow-up: `kills.unit_type` 가 빈 필드(이벤트가 위치만 나름) · 해저드 로그 볼륨 · `score_events` vs `result.score` 통일 |
| 테스트 lane 3 | 코어 26초 / 에셋 5초 / PlayMode 8분 | `docs/reference/test-procedure.md:13-15` | 확정 | EditMode 두 lane 은 기지 실패 없음(빨강 = 회귀). PlayMode 기준선 2026-09-21 = **59 실패** |

---

## 2. 채널 31 분류표

`(a)` sim→매치 규칙 · `(b)` sim→뷰 전용 · `(c)` sim→브리지 실행 요청 · `(d)` sim 내부.
**31개 큐의 생성·소유·반납은 전부 `BattleBridge.EnsureQueriesAndQueues` / `DisposeEcsInfrastructureNativeContainers`.**
값 스냅샷: `○` 전부 값 · `◐` 값 + Entity 혼재 · `✕` Entity 핸들 중심.

| 채널 | 방향 | 생산자 | 소비자 | 분류 | 값 스냅샷? |
|---|---|---|---|---|---|
| `EnemyKilledEventsSingleton` | Units→Bridge | `DamageApplicationSystem` | `DrainEnemyKilledEvents:5359` | a | ◐ pos·`awakeningReward` 는 값, `entity` 는 **등록부 키 전용**(역참조 금지, 드레인 시점엔 파괴됨) |
| `GoalReachedEventsSingleton` | Units→Bridge | `UnitLifecycleSystem` | `DrainGoalEvents:6731` | a | ✕ `Entity` + `canSiege` |
| `DefenderDeathEventsSingleton` | Units→Bridge | `UnitLifecycleSystem` | `DrainDefenderDeathEvents:4149` | a | ○ `int2 cell` 만 |
| `DefenderActivatedEventsSingleton` | Units→Bridge | `DeploymentActivationSystem` | `DrainDefenderActivatedEvents:7848` | a | ✕ Entity. ⚠ **`_running` 게이트 앞**에서 드레인(배치는 StartBattle 전에도 일어남) |
| `GoalCollapsedEventsSingleton` | Units→Bridge | `UnitLifecycleSystem` | `DrainGoalCollapsedEvents:9596` | b (연출·로그 전용) | ○ |
| `HazardDestroyedEventsSingleton` | Units→Bridge | `UnitLifecycleSystem` | `DrainHazardDestroyedEvents:9565` | a/b (뷰 회수) | ○ |
| `UnitAttackVisualEventsSingleton` | Combat→Bridge | `AttackSystem` · `HazardCastSystem` | `DrainUnitAttackVisualEvents:4755` | b | ✕ `attacker` Entity + targetWorld·animPeriod |
| `ProjectileHitEventsSingleton` | Combat→Bridge | `ProjectileHitSystem` | `DrainProjectileHitEvents:5095` | b | ◐ |
| `DamageNumberEventsSingleton` | Units→Bridge | `DamageApplicationSystem` | `DrainDamageNumberEvents:5192` | b | ◐ ⚠ 스포너 null 이면 큐를 `Clear()` 하고 나간다 |
| `HealAppliedEventsSingleton` | Units→Bridge | `DamageApplicationSystem` | `DrainHealAppliedEvents:5139` | b | ○ 위치+양 |
| `ShieldGrantedEventsSingleton` | Effects→Bridge | `SkillDispatchSystem` | `DrainShieldGrantedEvents:5156` | b | ○ 위치만. ⚠ 같은 null-Clear 함정 |
| `KnockupVisualEventsSingleton` | Combat→Bridge | `AttackSystem` · `SkillDispatchSystem` | `DrainKnockupVisualEvents:4545` | b | ✕ 대상 Entity + duration |
| `DcTriggerFiredEventsSingleton` | Combat→Bridge | `AttackSystem` | `DrainDcTriggerFiredEvents:4484` | b | ✕ host |
| `DetectionEventsSingleton` | Combat→Bridge | `DetectionSystem` | `DrainDetectionEvents:5178` | b | ○ **SimId 축**(`enemySimId`·`targetSimId`·`enemyPos`). ⚠ `targetSimId` 는 트레이스 전용 — 화면이 그 대상을 가리키면 안 됨 |
| `BossLeapVisualEventsSingleton` | Combat→Bridge | `SkillDispatchSystem` | `BattleBridge.BossLeap.cs:113` (LateUpdate) | b | ◐ |
| `UltimateLeapVisualEventsSingleton` | Combat→Bridge | `UltimateLeapSystem` · `SkillDispatchSystem` | `BattleBridge.UltimateLeap.cs:100` (LateUpdate) | b | ◐ `kind` Ascend/Descend 2종 |
| `AttackOutputLogEventsSingleton` | Combat→Bridge | `AttackSystem` | `DrainAttackOutputLogEvents:4992` | b (로그) | ◐ |
| `HazardRuntimeEventsSingleton` | Effects→Bridge | `DotApplySystem` · `ZoneApplySystem` | `DrainHazardRuntimeEvents:9445` | b (로그) | ◐ |
| `ShieldBreakEventsSingleton` | Units→Bridge | `DamageApplicationSystem` | `DrainShieldBreakEvents:4557` | **c** (브리지가 자기중심 폭발·주변 수면 실행) | ◐ host + tileRange + magnitude |
| `HazardSpawnRequestsSingleton` | Effects/Combat→Bridge | `HazardCastSystem` · `ProjectileHitSystem` · `SkillDispatchSystem` | `DrainHazardSpawnRequests:9467` | c | ◐ |
| `MeteorBarrageRequestsSingleton` | Effects→Bridge | `ResignationThresholdSystem` | `DrainMeteorBarrageRequests:5484` | c | ○ meteorCount |
| `CastEventsSingleton` | Effects→Combat | `HazardCastSystem` | `AttackSystem:238` | d | ✕ — `HazardCastSystem` 이 `[UpdateBefore(AttackSystem)]` 로 같은 프레임 소비 보장 |
| `ThreatHitEventsSingleton` | Combat→Combat | `AttackSystem` · `ProjectileHitSystem` | `HealthThresholdSystem:70` | d | ✕ victim/attacker |
| `BlinkRequestEventsSingleton` | Combat→Movement | `HealthThresholdSystem` · `UltimateLeapSystem` · `SkillDispatchSystem` | `BlinkApplySystem:31` | d | ◐ |
| `AggroAcquireEventsSingleton` | Combat→Effects | `AttackSystem` · `SkillDispatchSystem` | `AggroStateSystem:138` | d | ✕ |
| `CcClearRequestsSingleton` | Units→Effects | `DamageApplicationSystem` | `CcClearSystem:26` | d (wake-on-hit) | ✕ |
| `EnemyCcEventsSingleton` | 다수→Effects | `AttackSystem` · `ProjectileHitSystem` · `SkillDispatchSystem` · `ZoneApplySystem` · `StackModifierTickSystem` | `CcApplySystem:29` | d | ✕ |
| `DotApplyEventsSingleton` | 다수→Effects | `SkillDispatchSystem` · `ZoneApplySystem` · `StackModifierTickSystem` | `DotApplySystem:26` | d | ✕ 병합 키 = `(DotOrigin, DotElement)` 2축 |
| `StatModifierApplyEventsSingleton` | 다수 **+ Bridge**→Effects | 시스템 9곳 + `BattleBridge.EnqueueStatModifier:5959`·`:5978`·`ApplyEffectTileIfAny:9102` | `ModifierApplySystem:36` | d | ✕ |
| `StackModifierApplyEventsSingleton` | 다수→Effects | `AttackSystem` · `ProjectileHitSystem` · `SkillDispatchSystem` · `FatigueAccrualSystem` | `ModifierApplySystem:39` | d | ✕ |
| `SkillFiredEventsSingleton` | 감지자 다수 **+ Bridge**→Skills | `AttackSystem` · `BossPeriodicTriggerSystem` · `HealthThresholdSystem` · `DamageApplicationSystem` · `UnitLifecycleSystem` · `HazardCastSystem` + `BattleBridge.cs:3030`·`:4387` | `SkillDispatchSystem:199` (seam 7종, `budget = queue.Count` 스냅샷) | d | ◐ **자리·피해·반경·층·진영·방향은 값 스냅샷**, `Caster`/`Target` 은 Entity |

**캐리어 엔티티 채널 2개** (큐가 아니라 임시 엔티티 — 위 31에 불포함, 둘 다 분류 **c**):

| 채널 | 방향 | 생산자 | 소비자 | 비고 |
|---|---|---|---|---|
| `ProjectileSpawnRequest` + `ProjectileRequestCarrier` | Combat/Skills→Bridge | 발사 arm·스킬 concrete | `DrainProjectileSpawnRequests:5434` | `StartBattle` 이 배치 페이즈 잔여분을 파괴 |
| `PatrolSpawnRequest` + `PatrolRequestCarrier` | Combat→Bridge | 소환사 arm | `DrainPatrolSpawnRequests:9010` | `patrolDataIndex` 로 managed SO 레지스트리 조회 |

---

## 3. Battle/Bridge 밖 Entities 참조 — 무엇에 Entity 를 쓰나

실측 **런타임 24파일 + Editor 3파일**. (프롬프트의 「32파일」과 다르다 — 현재 `grep -rl "Unity.Entities"` 가 Battle/Bridge 제외 28건을 내는데 그중 4건은 *「Unity.Entities 참조 금지」* 주석만 있는 **오탐**이다: `Data/Abilities/DefenderAbilityData.cs` · `Data/Abilities/UnitSkillAbility.cs` · `Data/Dreamcatcher/DcMechanic.cs` · `Skills/SkillEntityId.cs`. 테스트 어셈블리는 별도로 100+ 파일이 참조한다.)

| 파일 | 용도 (1줄) | `SimEntityId` 로 대체 가능? |
|---|---|---|
| `Presentation/UnitView.cs` | 추상 베이스가 `abstract Entity Entity` 를 노출 | ○ 키 타입 교체 |
| `Presentation/SpineUnitView.cs` | Spine 백엔드가 자기 키 보관 | ○ |
| `Presentation/SpriteUnitView.cs` | Sprite 백엔드 키 | ○ |
| `Presentation/QuadUnitView.cs` | Quad 백엔드 키 (`Configure(Entity, …)`) | ○ |
| `Presentation/SpineUnitPool.cs` | `Dictionary<Entity, UnitView>` 풀 + `TrySpawn` 이 백엔드 선택 | ○ |
| `Presentation/QuadUnitViewPool.cs` | 같은 형태의 Quad 풀 | ○ |
| `Presentation/ProjectileViewPool.cs` | 활성 투사체 뷰 키 · `CopyActiveEntities` · 임팩트 소켓 조회 | ○ |
| `Presentation/BeamPresenter.cs` | 빔 세션 키 + `ViewPosResolver(Entity, …)` delegate + source/target | ○ |
| `Presentation/DcAuraVisualPool.cs` | 부착 오라 host 키 + `Func<Entity, Transform>` 앵커 resolver | ○ |
| `Presentation/DcIconStripSpawner.cs` | host별 부착 카드 목록 · 활성 스트립 뷰 맵 | ○ |
| `Presentation/EnemyHitBarSpawner.cs` | 피격 바 활성 맵 | ○ |
| `Presentation/EnemyHitBarView.cs` | 뷰 인스턴스가 자기 host 보관 | ○ |
| `Presentation/StatusFxSpawner.cs` | `(Entity, StatusFxKind)` 복합 키 | ○ |
| `Presentation/StatusFxView.cs` | 뷰가 자기 host 보관 | ○ |
| `Presentation/UnitOverheadUiLayer.cs` | 통합 오버헤드 맵 + host별 카드 목록 | ○ |
| `Core/Dreamcatcher/DreamcatcherHandController.cs` | `_attachedTo` 부착 등록부 + 사망/퇴근/취소 콜백 시그니처 | ○ |
| `UI/DefenderDragPlacementController.cs` | 배치 되돌리기 창의 `_undoEntity` | ○ |
| `UI/DefenderRelocationController.cs` | 재배치 대상 · 비행 중 단일 슬롯(`_activeFlightEntity`) | ○ |
| `UI/DefenderSelector.cs` | 콜백 시그니처만(값 미사용, `_` 로 버림) | ○ 시그니처 교체로 끝 |
| `UI/Dreamcatcher/DcInspectController.cs` | 선택 유닛 + 부착 카드 열람 + `TryPick` | ○ |
| `UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` | 드롭 대상 hover 유닛 + 화면 Rect 버퍼 | ○ |
| `UI/Dreamcatcher/DreamcatcherFocusPresenter.cs` | 락온 대상 · 틴트 적용 유닛 · Rect 버퍼 | ○ |
| `UI/Dreamcatcher/DreamcatcherHandView.cs` | `SelectionTarget` · 카드 비행 목적지 host | ○ |
| `UI/Tutorial/FirstRunTutorialController.cs` | 튜토리얼 host 해석(`TryResolveHost`) | ○ (콘텐츠는 철거됨, 도구만 잔존) |
| `Editor/Battle/SimHarnessRunner.cs` | 상태 지문용 월드 직접 접근(제약 1의 Editor 전용 예외) | ○ |
| `Editor/Battle/SimOrderDumpMenu.cs` | 유효 시스템 총순서 덤프 | ○ |
| `Editor/Battle/DetectionProbeMenu.cs` | 감지 반경 프로브 | ○ |

**요약**: 24개 전부 *「엔티티를 키로 쓰는 사전/리스트」* 또는 *「콜백 시그니처」* 다. 역참조(`EntityManager` 조회)는 하나도 없다 — 전부 `BattleBridge` 가 번역해 넘긴다. 따라서 **`SimEntityId` 로의 기계적 치환이 24/24 가능**하다.

---

## 4. ECS 고유라 새 설계에서 개념 자체가 사라지는 것

- **`NativeQueue` 싱글턴 + 브리지 수명 소유** — 큐 생성·`Dispose`·`_xQueueCreated` 플래그·`EnsureQueriesAndQueues` 멱등 가드가 통째로 소멸. 남는 것은 *「이 사건이 어느 seam 을 지나는가」* 라는 분류학뿐(D5 자기철회 2회의 결론: 채널 목록은 세리머니가 아니라 **이벤트 프로토콜 분류학의 초안**).
- **`EntityQuery` 재생성/누수 방어** — `SyncMonoUnitViews:3814` 의 `NullReferenceException` 캐치 + 쿼리 3개 동시 반납·재생성 같은 코드는 개념째 소멸.
- **`BattleScaledRateManager` / `IRateManager` / `PushTime`·`PopTime` / `BattleTimeScale` 싱글턴 왕복** — 그룹 rate 제어를 순수 sim 의 틱 루프가 직접 갖는다. Entities internal 할당자 swap 미접근이라는 알려진 한계도 함께 소멸.
- **구조 변경(`AddComponent`/`RemoveComponent`)의 프레임 순서 의존** — `CoreShielded` 토글이 *「Update 단계여야 안전」* 인 제약, `ObjectDisposedException: EntityTypeHandle invalidated` 류가 전부 소멸.
- **`Entity.Null` / fake-null / 파괴 후 값 비교** — `EnemyKilledEvent.entity` 를 「역참조 금지 등록부 키」로 쓰는 관용구가 `SimEntityId` 로 자연 해소.
- **`SimEntityId` 컴포넌트 부착 행위 자체** — 새 sim 에서는 ID 가 엔티티의 1급 정체성이라 「붙인다」가 없다.
- **Burst 제약이 만든 인덱스 레지스트리 6종** — `_projectileDataByIndex` · `_zoneHazardRegistry` · `_patrolUnitRegistry` · `_blockingHazardSoRegistry` · `_skillVfxPrefabs` · `_stackThresholds` 는 managed SO 를 unmanaged 구조체에 못 실어서 생긴 것이다. 존재 이유가 사라진다.
- **`ECB` / `RequireForUpdate` / 시스템 순서 어트리뷰트** — 틱 파이프라인이 명시 호출 순서가 되면 캡처된 총순서(unit 0)가 코드 그 자체가 된다.

---

## 5. 코드에만 박혀 있고 문서에 없는 규칙 (rebuild 가 놓치기 쉬운 것)

1. **`_running=false` 가 sim 을 멈추지 않는다.** score-tally-sequence 계약 6이 근거로 든 `PushBattleRunningToEcs`/`BattleRunning` 은 **레포 전체 grep 0건**. 결과 화면 동안에도 ECS 그룹은 전진한다. 리빌드가 「원래 멈췄다」로 가정하면 동작이 바뀐다.
2. **`TickBattleFrame` 의 드레인 순서 자체가 계약이다.** 최소 3건이 순서 의존:
   `DrainEnemyKilled` → `QueueDueWaves`(분열 자식이 전멸 판정 앞에 태어나야 함) ·
   `SyncGoalStability` → `TickBonusPullOffer`(한 프레임 묵은 스트레스로 판정하면 문턱에서 떨림) ·
   `DrainGoalEvents` → `SyncGoalStability`(붕괴 프레임에 배수구가 안 열리는 근거).
3. **`LateUpdate` 순서 계약 3건** — 도약 2채널 드레인이 `SyncMonoUnitViews` **앞**이어야 1프레임 팝이 없다. `MirrorLiftKnobs` 는 매 프레임(맵 빌드 1회 스냅샷 금지 — 같이 도입된 노브 8개와 비대칭이 됨).
4. **`_defenderByTile` 은 유닛당 1엔트리**(대표 셀 키)이고 **「엔트리 수 = 기수」가 불변식**이다. 셀→유닛 해석은 `_defenderCellOwner` 를 거치며, 등록/해제 함수는 2개뿐(`OccupyDefenderFootprint`/`ReleaseDefenderFootprint`).
5. **`DrainShieldGranted`/`DrainDamageNumber` 는 스포너가 null 이면 큐를 `Clear()` 하고 나간다.** 뷰를 떼면 그 채널의 골든이 **조용히 빈다**. 탭이 프레젠테이션 배선 뒤에 있다는 구조적 결함(4_legacy_trace_golden.md 가 M1 에서 끊으라고 지시).
6. **`_bonusPullSuppressed` 는 `ResetBonusWaveState` 에서 지우지 않는다** — 판 시작 **전** 외부 주입이라, 리셋에 넣으면 `GameManager` 가 켠 억제가 판 시작에 지워진다. 다른 모든 보너스 상태와 규칙이 다르다.
7. **`MatchConfig` 수집 실패는 판을 막지 않는다** — `try/catch` 로 해시를 비우고 진행. 빈 해시가 골든 쪽 신호. `_running = true` **앞**이라 막으면 판이 시작되지 않는다.
8. **매치 경계 리셋이 3곳에 부분 중복**(`BeginPlacement` · `StartBattle` · `StopBattle`). `ResetBonusWaveState` 가 앞의 둘 **양쪽**에 있어야 한다는 주석이 그 증거이고, 리빌드에서 한 곳으로 접어야 할 부채.
9. **트레이스 채널 번호는 append-only.** 재사용하면 옛 골든이 다른 사건으로 읽힌다(`TraceChannel` 주석 3곳).
10. **`ScheduledWaveTime` 과 `SpawnLeadInSec` 을 섞으면** 당김 연타마다 리드인이 누적 왜곡된다. 리드인은 **스폰 base 에만** 더한다.
11. **`MaxWaveIntervalSec` 폴백이 0 이면 전 웨이브가 한 프레임에 쏟아진다** — 덱이 0일 때 플랜 명목 interval, 그것도 0이면 20f 하드 폴백.
12. **`_aliveAttackersQuery` 에 필터를 걸지 말 것** — 소비처 11곳(슬로우·토네이도·메테오 사전집계, 배치 스킬 대상 수집, 전방 투사체, 밀쳐냄, 골 근접 경보)이 공유한다. 전멸 판정만 전용 쿼리를 쓴다.
13. **보스 판별은 `BakeNightmareMechanics` 한 곳**이고 경보도 거기서 쏜다. `SpawnUnit` 에서 재판정하면 이중 발화.
14. **`TryBonusPull` 의 크레딧 소비는 `+= killThreshold`(한 회분)**이지 `= _normalKillCount` 가 아니다. 후자면 스트레스에 막혀 쌓인 초과 크레딧이 통째로 증발한다.
15. **`EndMatch` 의 `stress_full` 분기만 연출 박자를 갖는다**(`coreBurstHoldSec`). 만료·제출은 터지는 것이 없어 즉시 결과 화면. 「종료 사유 표기」가 아니라 「사건이 있을 때만 그 연출이 나간다」.
16. **`GamePhase` 가 `CameraDirectionConfig.asset` 에 정수로 직렬화**된다(`breathPhases`). enum 에서 값을 빼거나 끼우면 저장된 카메라 설정의 의미가 밀린다.

---

## 6. 이 영역에서 rebuild 가 결정해야 할 열린 질문 (7)

1. **슬로모의 정체를 무엇으로 재정의할 것인가.** 지금은 dt 배율이라 고정 틱과 양립하지 않는다(`_battleClock` 과 ECS dt 를 **둘 다** 같은 스케일이 민다). 「틱 발행률 조절」(결정론 유지, 판 길이 불변) 대 「뷰 보간만 늦춤」(sim 완전 불변) 중 하나를 골라야 하고 게임 느낌이 다르다. battle-sim-extraction README 의 M1 후속 후보 `pause/slow-mo gameplay 시계 정책` 이 이 질문이다.
2. **판이 끝난 뒤 sim 을 멈출 것인가.** 현행은 안 멈춘다(문서는 멈춘다고 적혀 있다). 멈추면 결과 화면의 「전장 여운」이 정지 화면이 되고, 안 멈추면 집계 뒤에도 적이 계속 움직인다. 어느 쪽이든 명시적 결정이 필요하다.
3. **`GamePhase.Tally` 를 유지할 것인가.** 합산 연출은 은퇴했고 남은 일은 HUD 게이팅 + 마음 붕괴 박자뿐이다. 유지하면 append-only 부채가 남고, 없애면 `CameraDirectionConfig.asset` 의 직렬화 정수가 밀린다. 같이 결정할 것: `score-tally-sequence/README.md` 를 현행에 맞춰 갱신할지 은퇴 표기할지.
4. **코스트 재생 스위치의 소유자를 누구로 할 것인가.** 현재 UI(`PlacementPhaseView`)가 갖고 있어 하네스·테스트가 매번 대행한다(harness≠live 갭의 유일한 알려진 사례). 매치 세션이 소유하면 갭이 구조적으로 닫힌다.
5. **`_spawnSpreadCounter` 를 `spawnOrdinal` 파생으로 바꿀 것인가.** 바꾸면 값은 같되 「앞을 재생하지 않고 즉시 파생」이 된다. 스냅샷 부분 재시뮬·late-join·리플레이 점프의 전제 조건이고, 동시 다개체 겹침(서브레인 3 초과 시 4기째부터 동일점)도 함께 해소된다.
6. **마음을 N개 공유 체력으로 열 것인가**(heart-stress-axis unit 12). 열면 `Health` 정본이 마음 엔티티에서 싱글턴으로 **이사**하고(미러 금지), 회복 1회 적용·심박 위상 루프 밖 이동·`goals > 1` 경고 제거가 따라온다. `wide-board-content` 가 이걸 요구하고 `wide-board-camera` unit 7 이 기대고 있다. 리빌드 시점이 이사하기 가장 싼 자리다.
7. **죽은 축 3개 — 유출 · 몽마의 계약 · 적 마음 — 를 되살릴지 걷어낼지.** 셋 다 「카운터는 도는데 아무것도 판정하지 않는」 상태이고, `OpenBreachedCellsForLeak` 는 아예 호출처 0 인 휴면 코드다. 리빌드가 이 상태를 그대로 승계하면 새 sim 이 태어나면서부터 죽은 개념 3개를 안고 간다.

---

## 7. 부록 — 수치 정정표 (프롬프트 전제 vs 실측)

| 프롬프트 전제 | 실측 | 근거 |
|---|---|---|
| 관측 탭 19개 | **22개** (채널 enum 22 · `Ev` 호출 22) | `Core/Trace/LegacyTraceV0.cs:21-51`, `grep -c LegacyTraceRecorder.Ev BattleBridge.cs` = 22 |
| 골든 코퍼스 9종 | **선언 9 / 베이크 8** (`wide_body` 보류) | `SimHarnessRunner.Corpus`, `Assets/_Project/Tests/Golden/` 8파일 |
| Battle/Bridge 밖 Entities 32파일 | **런타임 24 + Editor 3** (+오탐 4) | `grep -rl "Unity.Entities"` 후 주석 전용 4건 제외 |
| 채널 31개 | **31개 정확** (+ 캐리어 엔티티 채널 2) | `grep "struct .*EventsSingleton\|struct .*RequestsSingleton"` = 31 |
| `EndMatch` 호출처 3 | **3개 정확** | `BattleBridge.cs:7072 / 7161 / 7181` |
| `MatchSeed` 파생 6 | **6개 정확** | `Core/MatchSeed.cs` — Map·Wave·Visual·Pickup·Gimmick·Meteor |
