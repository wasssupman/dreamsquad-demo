# 4 — 매치 담당자 + 매치 모드 (조각 A 마지막)

## 목적

판을 **규칙으로** 돌린다: 배치 판정·코스트·웨이브·마음·점수·시계·손패 자원·기믹 호스트가 각자 상태와 규칙과 자기 틱 단계를 갖고, 매치 모드(SO)가 그들의 파라미터와 목표 concrete 를 고른다. 이 unit 이 끝나면 조각 A 의 검증 질문 「헤드리스로 3분 판 완주」에 **진짜 웨이브·코스트·마음**으로 답한다. 정본 = `ledgers/rules.md` 매치·프레젠테이션 절(필수 15) · `ledgers/rule-holders.md`(133행 — 브리지 밖 규칙의 귀속) · `match-mode-design.md` · `census-match-presentation.md`.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 담당자 | `BattleCore/Owners/`: `MatchClock`(unit 1 확장) · `PlacementService` · `CostLedger` · `WaveScheduler` · `HeartMeter` · `ScoreLedger` · `HandDeck` · `GimmickHost`(빈 호스트 — 바인딩은 unit 7) |
| 거점 | `World/BattleWorld.SpawnStructure` · `World/EffectEligibility.cs`(F3 술어 3) · `Match/MatchDefinition.cs`(`StructureDef`) · `Map/MapSnapshot.cs`(`StructureSize` · `StructureSpot.DefIndex` · `CloseReservedPlacement`) · `Owners/HeartMeter`(마음 타워) · `Phases/FieldPrepPhase.Begin`(저작 거점) |
| 모드 | `BattleCore/Match/ModeDef.cs`(확장) · `BattleCore/Goals/{IMatchGoal, MatchGoalContext, MatchOutcome, KillScoreTimedGoal, WaveClearGoal, TimeAttackGoal}.cs` |
| salvage(순수) | `Data/WavePatternGenerator.cs`(UnityEngine 참조 제거 — `Mathf`→`math`, SO 입력은 plain `WaveDeckDef` 로) · `Core/StressMath.cs` · `Core/GimmickSelection.cs` · `Core/MatchTally.cs`(→ `MatchOutcome`) · `Data/FootprintMath.cs` · `Battle/Movement/SpawnSpread.cs`(unit 2) |
| Unity 층 | `Data/MatchModeData.cs`(SO, `Assets/_Project/Data/Modes/MatchMode_KillScore3Min.asset`) · `MatchDefinitionBuilder` 확장(모드·덱·플랜·맵 풀·기믹 풀 → `ModeDef`·`WaveDeckDef`·`WavePlanDef`·`GimmickDef[]`) · 모드 선택 우선순위 3단(테스트 모드 강제 > 로비/서버 > 기본 모드) — **빌더가 모드를 읽는 유일한 지점**. ⚠ unit 5 의 드라이버는 `Build(…, structures: 스테이지 저작 목록)` 을 **반드시 넘긴다** — 격자 투영(`GeneratedMap.structures`)에는 셀과 진영밖에 없어 스탯이 없고, 안 넘기면 저작 거점이 **한 기도 안 선다**(조용히 기본 스탯으로 세우지 않는 것이 그 선택이다) |
| 이벤트 | `WaveQueued/Started` · `EnemySpawned` · `BonusOffered/Pulled` · `CostChanged` · `Placed/Retired/PlacementRejected`(receipt 와 별개, 뷰용) · `HeartChanged{stress}` · `HeartCollapsed` · `ScoreChanged` · `MatchEnded{reason, outcome}` |
| 테스트 | `Tests/EditModeCore/Match*` — `WavePatternGenerator*`(옛 EditMode 테스트 복사, byte-identical RNG 소비 순서 검증) · `StressMath*` · `Placement*`(마스크·footprint·순서) · `Cost*` · `Goal*`(3 concrete) · 모드 유효성(`EditModeAssets` lane: `targetWaves` ↔ 덱 `maxWaveCount`, 저작 플랜 × `clockKind`). 골든 `kill_race_3min`(진짜 웨이브) · `wave_clear_8` · `time_attack_8` |

## 구현

0. **거점 스폰.** 저작 거점(본능 · 적 마음)은 `FieldPrepPhase.Begin` 이 세운다 — **장(場)의 가구**라 이동이 읽는 목적지·장애물과 같은 층이기 때문이다. 방어 마음만 `HeartMeter` 가 갖는다(체력이 담당자에게 있어서다). 순서는 **본능 → 마음**이고 그것이 방패(「본능이 살아 있나」)의 초기값이 첫 틱 전에 서는 근거다. 스탯은 `MatchDefinition.Structures`(정의표), 자리는 `MapSnapshot.Structures`(셀·진영·점유·표 인덱스) — 같은 SO 를 여러 자리에 찍는 것이 저작의 기본형이라 값을 자리마다 복제하지 않는다. 거점은 통행을 **안 막고**(점유만) 배치판에서는 자기 footprint 가 닫힌다(`CloseReservedPlacement` — 스폰·골 칸도 같은 함수가 닫는다, 옛 `CloseCellLayers`). 무너진 거점은 그 틱부터 목적지 후보에서 빠진다(안 빼면 적이 잔해를 향해 계속 걷는다).
1. **PlacementService.** 판정 순서 = 페이즈 → 공간(footprint 전 칸 `(셀 층 & 유닛 층) != 0`, M29 다칸) → 유닛 유효 → 풀 → 보드 상한 → 코스트(「구조 > 자원」). 점유 쌍(occupied + cellOwner) 항상 함께. 자석 스냅 row-major first-win. 배치·사망·퇴근 쿨타임(퇴근 = 사망 비율, `Clamp01` 이 방어선). `maxOnBoard` 는 타입 키 덮어쓰기 대신 `max(remaining, new)`(census 후속 후보 반영 — 라이브 전원 1 이라 무변). 퇴근은 `Dead` 를 안 켜고 `Destroy` — 각성·사직서·작별 선물이 배제 코드 0 으로 안 일어난다. 배치 페이즈: `Deploying` 한 단계(비행은 뷰 시간, 착지 커맨드 `LandDefender`), 활성화 = 모션 길이(`DeployMotionSeconds`) 배틀 시간. 효과 타일 1회(회수 없음, 재배치 재무장 없음 — 가드 분리).
2. **CostLedger.** 시작 10·상한 15·재생 1/s·재생 배율(드림스톤 `CostRate`)은 `CostConfig` 정의표. **재생 시작 = 배치 페이즈 종료 틱**(X24 — 담당자 소유, UI 갭 소멸). `TryPay` 는 `PlacementService` 판정 마지막 단계. 메타 intent `GainCost` 즉시 반영.
3. **WaveScheduler.** `WavePatternGenerator` salvage — **RNG 소비 순서 byte-identical**(보스 1종이면 미소비 가드 · 램프 `NextFloat` 1콜 등). 플랜 우선순위(테스트 플랜 > 저작 인카운터 > 시드 생성). 케이던스 = 전멸 OR 상한 경과(저작 플랜은 타임라인). 웨이브 1 즉시. 리드인은 스폰 기준시각에만(X10). 간격 폴백 사슬(덱 → 플랜 → 20)(X11). 보스 케이던스 후처리 + 경보는 **스폰 시 한 곳**(X13). 당김 2층(규칙 `TryPull` / 기제 `ForceNext`), 상한 = 덱 `maxPullsPerClear`, 전멸로만 회복, 저작 플랜 면제. 보너스: 킬 N AND 스트레스 ≤ 임계, 래치, 크레딧 한 회분 차감(X14), 억제 플래그는 판 경계 리셋 밖(X6), 포탈 `i % portalCount`. 전멸 판정은 **자기 술어**(보너스 적 제외, 분열 자식은 처치 이벤트 구독이 웨이브 예약 **앞**이라 먼저 태어난다 — X2). 스폰 흩뿌림 `SpawnSpread`. 살아 있는 공격자 목록엔 필터 없음(X12).
4. **HeartMeter.** 마음 체력을 **담당자가 든다**(마음 거점 개체는 위치·피격 대상만, 체력 미러 금지 — X29 이사 비용 0). 스트레스 = `StressMath.FromHealth`(표시 정규화 100). 골 도달 구독: 돌격형 = `stabilityDamage` 즉시, 공성형 = 거점 피해가 여기로. 킬 회복 = `awakeningReward × killHealPerAwakening`(SO 원값). 본능 생존 중 `CoreShielded`(마음 후보 제외). **첫 붕괴 = `EndMatch(stress_full)`**(X18). 구독 순서: 골 이벤트 → 안정도 → 보너스 제안(X2).
   - **마음 타워를 이 담당자가 세운다**(`Begin`) — 골 하나당 하나, 1×1 점유, 몸 = 내접원 0.5. 체력 0(=덱 미저작)이면 **안 세운다**(옛 `goalStabilityMax > 0` 게이트).
   - 그래서 담당자 단계가 생겼다(`ITickPhase`, 담당자 중 **맨 앞**). 두 가지만 한다: ① 방패 관찰(`Unit.Untargetable` 갱신) ② **타워 인박스 드레인**. 피해 단계는 `Unit.HealthExternal` 개체를 통째로 건너뛰므로(인박스도 안 비운다) 드레인의 주인이 곧 체력의 주인이다.
   - 공성 피해의 경로: 적 공격 → 타워 인박스 → `DrainTowers` → `DamageApplied`(비율의 분모가 **마음의 최대치**) + `HeartChanged`. 타워는 `Dead` 가 안 켜지므로 `UnitSlain`·`UnitDestroyed` 가 나지 않는다.
5. **ScoreLedger.** 킬 = 1점 생값(보스·분열체 포함). `UnitSlain` 구독. 제출값 무가공.
6. **MatchClock.** 타이머(`ModeDef.clockKind` FixedLimit/CountUp) · 배치 페이즈 창(길이 0 이어도 진입 신호 발화 — 트레이 구성) · 종료 통로 3(`complete`·`stress_full`·`submitted`) **정확히 3** · 판 경계 리셋 **한 곳**(X8) · `EndMatch` 뒤 틱 0 · 붕괴 종료만 연출 박자(X15 — 뷰가 `reason` 으로 판단).
7. **HandDeck.** rule-holders 의 `DreamcatcherHandController` 19행 이관: 큐 12 = 저장 10 + 공용 액티브 2, Fisher-Yates 1회(매치 시드 raw), 손패 = 큐 앞 N, 각성 게이지(킬 보상·사망 보상·퇴근 0·액티브 비용·초과 소멸·시간 충전 없음), 회수(사망·퇴근 → 뒤 / 인수인계 집합 연산 → 다른 카드 앞 / 액티브 → 뒤 / 실패 부착 무변), 부착 상한 3(`CountAttachedTo`). **각성 손패의 실시간 계약(D23)은 뷰 애니메이션 시간** — 규칙은 틱. 부착 효과 자체는 unit 7.
8. **GimmickHost.** 시즌 게이트(`ModeDef.gimmickPool` × `DeriveGimmickSeed`)로 이번 판 기믹 1개 선택, 바인딩 부착은 unit 7. 빈 호스트라도 판 시작 이벤트에 `GimmickAssigned` 를 낸다(리빌 페이즈용).
9. **매치 모드.** `MatchModeData` SO 스키마 = 별첨 표 그대로(append-only). `ModeDef` plain. `IMatchGoal` concrete 3: `KillScoreTimedGoal`(만료→`complete` · 점수 = 킬 · 붕괴 = 남은 시간 몰수, 같은 잣대) · `WaveClearGoal`(마지막 웨이브 dispatch + 전멸 → `complete`, 붕괴 = **패배** 라벨) · `TimeAttackGoal`(위 신호 + 경과 시간 점수, 오름차순). `MatchGoalContext` = 담당자 읽기 모델 + `EndMatch` 하나. 담당자 안에 `if (mode…)` 0. v1 제출은 `KillScoreTimed` 만(`submitsReport`).
10. **재시작·쿨다운 시계(사용자 답 대기 — 기본값으로 진행).** 판 안 재시작 = **없음**(`BattleMatch` 는 새로 조립, C7·S6·K5 계약 안 옮김). 액티브 쿨다운 = **판의 시계**(`HandDeck` 이 틱으로 잰다 — 슬로모 중 느려짐). 답이 다르면 이 두 줄만 바뀐다.

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 판 경계 리셋 3곳 중복 | 담당자마다 자기 `Begin` — 「판 경계」를 부르는 한 함수를 만들지 않았다 | 보류(X8) 해소 |
| 종료 후 전투 계속 | 계약 5 — `BattleMatch.Tick()` 이 종료 뒤 no-op | 보류(X1) |
| 코스트 재생 스위치 UI 소유 | `CostLedger` 가 `PlacementPhaseChanged` 를 구독해 스스로 켠다 | 필수 규칙의 **소유 이전**(X24) |
| 유출 카운터·`OpenBreachedCellsForLeak`·몽마의 계약·적 마음 판정·뽑기 폴백·재시작 경로 | 제거 확정 | 제거(X17·X20~X23) |
| `GamePhase.Tally` 합산 연출 | 코어에 판 **밖** 국면이 없다(`MatchPhase` 는 배치·전투 둘). 카메라 에셋 정수는 뷰 층의 `GamePhase` 가 계속 진다 | 보류(X19·X16) |
| `_spawnSpreadCounter` 가변 상태 | 현행 의미 유지(`BattleWorld.SpawnOrdinal`), 순번 파생은 후속 후보 | 보류(X25) |
| 타이머 소유자 2곳 | `ModeDef.MatchSeconds` 하나. 덱의 `timerDurationSec` 은 **생성기 안**에서만 명목 그리드로 쓰이고 판 길이를 정하지 않는다 | 해소 |
| 보스 폴백(`bossPool` 비면 `bossUnit` 단일) | 코어 덱 정의표에 단일 보스 칸이 **없다**(표현이 하나뿐) — 접는 자리를 SO 를 아는 쪽(`MatchDefinitionBuilder.ToDeckDef`)으로 옮겼다 | 소유 이전 |
| 손패 셔플의 `System.Random` | 계약 5 가 금지(플랫폼·런타임 버전 의존) — xorshift 로 바꿔 **같은 시드의 순열이 라이브와 다르다**. 규칙(한 곳·판 시드·1회)은 보존 | 의도만 이식 |
| 액티브 쿨다운의 벽시계 | 판의 시계로 옮겼다(spec 구현 10 의 기본값). 답이 다르면 `HandDeck.Run` 하나가 바뀐다 | **사용자 답 대기** |
| 판 안 재시작 | 없음. `BattleMatch` 를 새로 조립한다 — C7·S6·K5 계약을 옮기지 않았다 | **사용자 답 대기** |
| 카드 효과(부착·시전의 실행) | unit 7. 커맨드는 **성사**되고 자원이 움직이되 효과 자리는 진단 통로로 말하고 지나간다(조용한 무동작 금지) | unit 7 |
| 거점 개체의 **임의 footprint** | 크기를 **진영이 정한다**(마음 1×1 · 본능 3×3) — SO 가 알 수 없는 구조라 저작으로 못 바꾼다. `StructureSize` 가 그 두 상수와 몸 반경 파생을 한 곳에 든다 | 보류(E28) |
| 적 마음(`EnemyCore`) 의 «판을 끝내는» 축 | 세우기는 하되(저작이 있으면) 부숴도 판이 안 끝난다 — X23 제거 확정. 옛 `_enemyCoreMax` 미러는 안 옮겼다 | 제거(X23) |
| 골별 안정도 · 골 붕괴 이벤트 | 새 코어의 마음은 **저수지 하나**다(X29). 골이 여럿이어도 체력은 한 벌이고 첫 붕괴가 판을 끝내므로 「이 골이 무너졌다」가 별도 사건이 아니다 | 소유 이전 |
| 스탯·스택 면역의 **집행** | 술어(`EffectEligibility.AcceptsModifier`)는 섰고 행동불능 축은 `BattleWorld.RequestCc` 한 문에서 실제로 막는다. 스탯·스택은 **생산자가 unit 6 에 생길 때** 같은 술어를 지난다 | unit 6 |
| 거점 뷰(프랍·그림자·체력 바) | `StructureData.viewPrefab`·`viewScale` 은 아트라 정의표에 들어올 수 없다 — 뷰 층이 `UnitSpawned`(`Kind = Structure`)를 구독해 자기 풀에서 만든다 | unit 5 |
| 효과 타일의 **효과** | 뽑기·1회 소비 가드만 이식(`PlacementService`). 적용은 unit 6 | unit 6 |
| 코드 기본값을 「기획」으로 읽기 | **기준은 라이브 에셋이다.** 골든과 모드 SO 는 `BattleConfig.asset`(배치 입력 off · 카운트다운 3) · `DefaultCostConfig.asset`(10/10/0.35/창 30) · `AwakeningConfig.asset`(게이지 20/100 · 손패 **4** · 부착 3) · `DeckRuleConfig_Default.asset`(덱 10 · Squad **무제한**)에서 값을 가져온다. C# 필드 기본값(손패 5 · Squad ≤2 · 코스트 30/15/20)과 **다르다** | 정정 |
| `MatchModeData.squadCardMax` | 안 만들었다 — 덱 규칙은 `DeckRuleConfig` 가 이미 소유하고 라이브가 **무제한**이다. 모드가 그 값을 복제하면 두 곳이 갈린다 | 소유 이전 |

## 완료 기준

- [x] 헤드리스 초록(**359 통과 / 0 실패**). RNG 소비 순서는 **오라클 테스트**가 증언한다 — 옛 EditMode 테스트는 SO 를 만들어 돌려 헤드리스에서 컴파일되지 않으므로, 그 테스트가 지키던 «소비 차례»를 `WaveGeneratorTests` 가 손으로 재현해 대조한다(웨이브 수 → 종 A → 종 B → 지터 → 배분 → 보스 후처리).
- [x] 골든 `kill_race_3min`: 고정구 덱(`Deck_Duel` 손잡이 복제)·맵 스냅샷·**라이브 에셋 모드**로 3분 완주 — 종료 사유 `complete`, 틱 10980(배치 창 180 = 3초 카운트다운 + 전투 10800), 전투 시계 180.000, 킬 126 / 점수 125(하나는 방어유닛 사망이라 처치가 아니다), 결정론 2회 동일. `wave_clear_8`·`time_attack_8`: 틱 3649 · 57.817초에 8웨이브 클리어. `heart_collapse`: `stress_full` · `MatchOutcome.kind = Defeat` · 놓침 4.
- [x] **거점 스폰 이후 골든 재베이크**(11종). 이벤트 줄이 **한 바이트도 안 바뀐 것 6종**(`empty_board`·`spawn_destroy`·`march_to_goal`·`detour_obstacle`·`detect_and_chase`·`kill_race_basic`) — 이 고정구들은 마음을 저작하지 않아 타워가 서지 않는다. 나머지 4종은 **틱 0 의 타워 스폰 1건만** 늘었고 틱 수·킬·점수·놓침·종료 사유가 전부 같다. `configHash` 는 11종 전부 바뀐다(예약 칸 폐쇄 + `struct` 줄 형식 + `[structure]` 절 + 「마음 기본값 = 없음」).
- [x] 새 골든 `siege_instinct_fall` — **다른 어느 골든도 안 지나는 구간**을 증언한다(그쪽은 방어유닛이 다 잡아 유출이 0 이다): 본능이 먼저 서서 마음을 표적에서 빼고 → 본능이 무너지고(틱 1130) → 마음 타워가 조준 후보가 되어(첫 피격 틱 1280) → `stress_full` 로 끝난다(틱 1988). 처치 1 / 점수 0 이 「거점은 적이 아니다」의 표면이다.
- [x] 모드 유효성 테스트 빨강 케이스 확인(`targetWaves 12` vs 덱 `maxWaveCount 10` · 저작 플랜 × `clockKind` · 손패 > 덱). ⚠ **Assets lane 이 아니라 코어 lane**에 있다 — 검증기(`ModeValidation`)가 코어에 있어야 헤드리스와 에셋이 **같은 자**를 쓴다.
- [x] 담당자 안 `mode` 분기 0(`CoreArchitectureTests` — `MatchClock` 만 예외이고 그것이 시계 정책의 소유자다) · `EndMatch` 호출처 **4곳 정확히**.
- [x] rule-holders 56행 매핑(아래 표).
- [ ] 옛 코퍼스 `basic`·`long_boss`·`force_wave` 와 거시 지표 대조표(참고) — 옛 러너가 Unity 에디터를 요구해 이 세션에서 못 돌렸다.
- [x] 리뷰 F1 — `UnitSlain`·`GoalReached` 가 정의표 인덱스를 **값으로** 싣는다(`CoreEvent.DefIndex`). `HandDeck.OnSlain`·`HeartMeter.OnSlain/OnGoalReached` 의 드레인 시점 재질의 3곳 제거. 트레이스 포맷은 무변(여섯 칸에 안 실린다) — 그래서 이 항목만으로는 골든이 안 바뀐다.
- [x] 리뷰 F2 — `WaveScheduler.StepSpawns` 의 `RemoveAt(i); i--` → **앞으로 접는 한 번 순회**(읽기·쓰기 커서). 스왑 팝은 남은 항목의 순서를 흔들어 같은 시드가 다른 판이 되고, 역순 순회는 한 틱에 여러 마리가 나올 때 웨이브 시작 신호를 그 웨이브의 마지막 적에게 붙인다. 발행 차례 무변 = 골든 무변.
- [ ] `core-reviewer` APPROVE.
- [ ] **Unity 층 미검증(에디터 열릴 때)**: `MatchModeData` SO 의 인스펙터 표시 · `MatchMode_KillScore3Min.asset` 의 역직렬화 · `MatchDefinitionBuilder.Build(mode, …)` 의 실제 SO 입력. 컴파일은 **헤드리스 Unity 층 검사 lane**(`BattleCoreUnity.Check.csproj`)에서 초록이고, 그 lane 에 이 모드 SO 파일을 명시로 넣었다.

---

## rule-holders 귀속 56행 → 코드 포인터

`ledgers/rule-holders.md` 에서 이 unit 의 담당자 7 에게 귀속된 행 전부다. 「이관」 = 규칙이 그대로
왔다, 「소유 이전」 = 규칙은 같은데 주인이 바뀌었다, 「보류」 = 자리만 만들고 내용은 뒤 unit.

| 행 | 새 자리 | 비고 |
|---|---|---|
| D1 덱을 배치 진입마다 새로 구성 | `HandDeck.Begin` | 판 경계는 담당자 자기 `Begin` |
| D2 덱 = 저장 10 + 공용 액티브 2, 셔플 1회·판 시드 | `HandDeck.Begin` | 난수원만 xorshift(이식 제외 표) |
| D5 온보딩 첫 손패 고정 | `HandDeck.Begin(pinnedFront)` | 저작 목록은 정의표의 몫 |
| D6 각성 상한 초과분 소멸 + 손실 고지 | `HandDeck.Gain` · `OverflowLost` | |
| D7 각성은 처치·사망의 보상(퇴근 0) | `HandDeck.OnSlain` | 퇴근은 `Retired` 만 구독(게이지 미접촉) |
| D8 숙주가 떠나면 카드 전부 큐 맨 뒤로 | `HandDeck.Recover` | |
| D9 퇴근 + 「인수인계」 → 나머지가 앞으로 | `HandDeck.Recover(retired: true)` | 선언 카드 자신은 맨 뒤 |
| D10 「인수인계」 판정이 두 곳에 있으면 안 된다 | `CardDef.DeclaresRetireRecall` | 저작 한 칸이 곧 판정(중복 2 해소) |
| D11 부착 결과 규약 3종 | `HandDeck.TryAttach` receipt | 실패·성공만 남고 「회수 불필요」는 숙주 소멸로 자동 |
| D12 먼저 적용하고 값을 나중에 | `HandDeck.TryAttach` 순서 ①②③ | |
| D13 부착 상한 3 | `HandDeck.CanAttachMore` · `CountAttachedTo` | |
| D14 적 표식에는 그 상한을 안 쓴다 | 보류 | 표식은 unit 7(적 부착 경로 자체가 없다) |
| D15 쓸 수 있는 조건 = 손패 + 각성 | `HandDeck.TryAttach`/`TryCast` | 값은 카드가 정한다(`CardDef.Cost`) |
| D16 액티브↔부착 경로 배타 | `RejectReason.WrongCardKind` | |
| D17 액티브는 뒤로 재활용, 부착은 이탈 | `HandDeck.TryCast` / `TryAttach` | |
| D18 지불은 0 밑으로 안 내려간다 | `HandDeck.Spend` | |
| D20 부착 목록은 부착 번호 오름차순 | `HandDeck.CompareByAttachSeq` | |
| D23 각성 손패는 실시간 | 뷰(unit 5) | 규칙은 틱 — 이 담당자는 시간을 안 쓴다 |
| D24 부착 순서 자체가 기능 | `HandDeck._attachSeq` | |
| K1 기록이 없으면 준비된 것 | `HandDeck.IsReady` | |
| K2 시전 성사 → 쿨다운 재충전 | `HandDeck.TryCast` | **확인과 커밋이 한 함수**(호출부 책임이 사라졌다) |
| K3 쿨다운 감소는 일괄 | `HandDeck.ReduceAllCooldowns` | |
| K5 판 경계에서 전량 소거 | `HandDeck.Begin` | |
| C1 시작값 → 상한, 초당 재생(배율) | `CostLedger.Begin` · `Run` | |
| C2 재생 스위치 3단 | `CostLedger.OnPhase` | **소유 이전**(X24) |
| C3 시작값 클램프·상한 최소 1·속도 음수 불가 | `CostLedger.Begin` | |
| C4 모자라면 거부 | `CostLedger.CanAfford` · `TryPay` | |
| C5 화면은 내림, 판정은 실수 | `CostLedger.CurrentInt` | |
| C6 환급은 상한을 안 넘는다 | `CostLedger.Gain` | |
| C7 재생 배율은 초기화가 안 건드린다 | `MatchDefinition.CostRateMultiplier` | setter 가 없다 — 구조로 막았다 |
| C8 코스트는 배치를 막는 판 상태 | `CostLedger` 가 틱 단계 | `Update` 가 통째로 사라졌다(중복 10) |
| C9 판의 시계를 따른다 | 같은 자리 | 틱 발행률로 자동 성립 |
| G10 돌 코스트 배율은 판 진입에만 | `MatchDefinitionBuilder.Build(costRateMultiplier)` | 호출처 0(중복 4 해소) |
| L1 놓으면 그 종류에 대기 | `PlacementService.StartCooldown(Place)` | |
| L2 0 = 없는 것과 같다 | 같은 함수 + `StepCooldowns` 조기 반환 | |
| L3 놓을 수 있나 = 남은 시간 0 | `PlacementService.IsReady` | |
| L4 배치 진입·판 정리에서 전부 지움 | `PlacementService.Begin` | |
| L5 배치 취소는 **그 배치가 건 대기만** | `PlacementService.ClearPlaceCooldown` | 출처를 키에 넣어 **근사가 사라졌다** |
| L6 대기는 판의 시계 | `PlacementService.Run` | 초가 아니라 **틱**으로 센다(실측 드리프트) |
| P5 모자라면 배치 시도 자체 거부 | `PlacementService.Judge` → `InsufficientCost` | 입력이 미리 거르지 않는다(중복 3 해소) |
| P6 성사 뒤에 깎는다 | `PlacementService.TryPlace` 순서 | |
| G1 한 번에 한 국면, 재진입 무시 | `MatchClock.Phase` · `FinishPlacement` | |
| G20 씬이 꺼지면 전투를 멈춘다 | 뷰(unit 5) | 코어는 판 수명을 `BattleMatch` 로 갖는다 |
| Y1 결과 라벨 셋, 승패 자리 없음 | `MatchEndReason` 3 | 승패는 **목표**가 붙인다(`OutcomeKind`) |
| Y2 통로는 둘, 제출은 절차 밖 | `MatchClock` 통로 3 · `ModeDef.AllowSubmit` | 제출 어휘는 그 모드에만 |
| Y10 성적 조립 지점은 하나 | `IMatchGoal.BuildOutcome` | |
| Y11 성적은 아키텍처를 모르는 순수 값 | `MatchOutcome` | |
| Y3 1킬 = 1점, 예외 없음 | `ScoreLedger.OnSlain` | |
| Y4 흘려보낸 적은 점수에 없다 | 같은 자리 | 그쪽은 처치 사건을 안 낸다 |
| Y5 제출값은 총점 그대로 | `ScoreLedger.SubmissionScore` | |
| Y6 음수 처치는 0 | `MatchOutcome` 생성자 | 더하기만 하므로 발생 경로도 없다 |
| G12 보너스 억제는 조건 **밖**에서 | `WaveScheduler.BonusPullSuppressed` | `Begin` 이 안 지운다(X6) |
| G24 일시정지 웨이브 브리핑 | `WaveScheduler` 읽기 모델(`WaveReached`·`WaveCount`) | 다시 만들지 않고 **읽는다** |
| Y7 도달 웨이브 = 마지막 큐잉 번호 | `WaveScheduler.WaveReached` | |
| Y8 마음의 남은 안정도·최대치 | `HeartMeter.Health` · `MaxHealth` | |
| Y9 「놓쳤다」 = 돌격형 산화 수 | `HeartMeter.Leaks` | 화면에 「유출」이라 쓰면 거짓말 |

**미정 2** 는 이 unit 에서 닫히지 않았다 — 둘 다 「플레이어가 겪는 규칙」이라 기본값으로
구현하고 이식 제외 표에 **사용자 답 대기**로 적었다(판 안 재시작 없음 · 액티브 쿨다운은 판의 시계).

## `rules.md` 매치·프레젠테이션 필수 15 → 코드 포인터

| # | 새 자리 |
|---|---|
| X2 한 틱 안 순서 3건 | `EventOrder`(한 파일에 전순서) + 틱 단계 순서. 「골 이벤트 → 안정도 → 보너스 제안」은 `HeartMeter` 가 내는 `HeartChanged` 를 `WaveScheduler` 가 구독하는 **연쇄**로 성립한다 |
| X3 도약 2채널을 뷰 갱신 앞에 | 뷰(unit 5) — 코어는 사건을 순서대로 낸다 |
| X5 뷰가 없어도 채널이 빈다 | `EventBus` — 구독자 수와 무관하게 발행된다 |
| X6 보너스 억제는 판 경계 리셋 **밖** | `WaveScheduler.BonusPullSuppressed`(프로퍼티, `Begin` 미접촉) |
| X7 해시 수집 실패해도 판은 시작 | `MatchDefinition.ConfigHash` 는 값일 뿐 게이트가 아니다 |
| X9 관측 채널 번호 append-only | `CoreTraceChannel` 19~31 추가(재사용 0) |
| X10 리드인은 스폰 기준시각에만 | `WaveScheduler.Dispatch` 의 `spawnBase` |
| X11 간격 폴백 사슬 | `WaveScheduler.Interval`(덱 → 플랜 → 20) |
| X12 살아 있는 공격자 목록에 필터 금지 | `WaveScheduler.FieldClear` 만 자기 술어(보너스 제외) |
| X13 보스 판별·경보 한 곳 | `PlannedWave.IsBoss`(생성기가 굽는다) → `WaveStarted` 1회 |
| X14 보너스 크레딧은 한 회분씩 | `WaveScheduler.TryPullBonus` 의 `_bonusConsumed += KillThreshold` |
| X15 붕괴 종료만 연출 박자 | `MatchClock.EndHasPresentationBeat` |
| X16 게임 단계 enum 은 카메라 에셋 정수 | 코어에 판 밖 국면이 없다 — 뷰 층 `GamePhase` 가 계속 진다 |
| X18 「패배 없음」은 거짓 | `HeartMeter.Damage` → `EndMatch(StressFull)` |
| X24 코스트 재생은 배치 종료에 시작 | `CostLedger.OnPhase` |
| E19 누가 무엇을 받을 수 있나는 스폰 시점에 못 박힌다 | `BattleWorld.SpawnStructure`(거점은 이동·감지·순찰 부분을 안 받는다) + `EffectEligibility` |
| E28 거점 크기는 진영이 정한다 | `MapSnapshot.StructureSize` — 상수 둘과 몸 반경 파생이 한 곳 |
| F3 거점은 상태이상·모디파이어 전면 면역 | `EffectEligibility` 술어 3 + `BattleWorld.RequestCc` 단일 문. 회복만 열려 있다 |
| C15 거점에 타입 기반 특별 취급이 없다 | 타겟팅 경로 무변 — 거점은 후보 목록에 **그냥 들어간다**(`BuildCandidates` 에 종류 분기 0) |
| M18 거점 고르기의 동률은 칸 사전순 | `StructureChoice.IsBefore` 무변. 무너진 거점 제외는 **진영 비트를 0 으로** 두어 서명을 안 바꿨다(예고선이 같은 함수를 쓴다) |
