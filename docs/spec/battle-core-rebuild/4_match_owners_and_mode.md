# 4 — 매치 담당자 + 매치 모드 (조각 A 마지막)

## 목적

판을 **규칙으로** 돌린다: 배치 판정·코스트·웨이브·마음·점수·시계·손패 자원·기믹 호스트가 각자 상태와 규칙과 자기 틱 단계를 갖고, 매치 모드(SO)가 그들의 파라미터와 목표 concrete 를 고른다. 이 unit 이 끝나면 조각 A 의 검증 질문 「헤드리스로 3분 판 완주」에 **진짜 웨이브·코스트·마음**으로 답한다. 정본 = `ledgers/rules.md` 매치·프레젠테이션 절(필수 15) · `ledgers/rule-holders.md`(133행 — 브리지 밖 규칙의 귀속) · `match-mode-design.md` · `census-match-presentation.md`.

## 변경 대상

| 항목 | 경로 |
|---|---|
| 담당자 | `BattleCore/Owners/`: `MatchClock`(unit 1 확장) · `PlacementService` · `CostLedger` · `WaveScheduler` · `HeartMeter` · `ScoreLedger` · `HandDeck` · `GimmickHost`(빈 호스트 — 바인딩은 unit 7) |
| 모드 | `BattleCore/Match/ModeDef.cs`(확장) · `BattleCore/Goals/{IMatchGoal, MatchGoalContext, MatchOutcome, KillScoreTimedGoal, WaveClearGoal, TimeAttackGoal}.cs` |
| salvage(순수) | `Data/WavePatternGenerator.cs`(UnityEngine 참조 제거 — `Mathf`→`math`, SO 입력은 plain `WaveDeckDef` 로) · `Core/StressMath.cs` · `Core/GimmickSelection.cs` · `Core/MatchTally.cs`(→ `MatchOutcome`) · `Data/FootprintMath.cs` · `Battle/Movement/SpawnSpread.cs`(unit 2) |
| Unity 층 | `Data/MatchModeData.cs`(SO, `Assets/_Project/Data/Modes/MatchMode_KillScore3Min.asset`) · `MatchDefinitionBuilder` 확장(모드·덱·플랜·맵 풀·기믹 풀 → `ModeDef`·`WaveDeckDef`·`WavePlanDef`·`GimmickDef[]`) · 모드 선택 우선순위 3단(테스트 모드 강제 > 로비/서버 > 기본 모드) — **빌더가 모드를 읽는 유일한 지점** |
| 이벤트 | `WaveQueued/Started` · `EnemySpawned` · `BonusOffered/Pulled` · `CostChanged` · `Placed/Retired/PlacementRejected`(receipt 와 별개, 뷰용) · `HeartChanged{stress}` · `HeartCollapsed` · `ScoreChanged` · `MatchEnded{reason, outcome}` |
| 테스트 | `Tests/EditModeCore/Match*` — `WavePatternGenerator*`(옛 EditMode 테스트 복사, byte-identical RNG 소비 순서 검증) · `StressMath*` · `Placement*`(마스크·footprint·순서) · `Cost*` · `Goal*`(3 concrete) · 모드 유효성(`EditModeAssets` lane: `targetWaves` ↔ 덱 `maxWaveCount`, 저작 플랜 × `clockKind`). 골든 `kill_race_3min`(진짜 웨이브) · `wave_clear_8` · `time_attack_8` |

## 구현

1. **PlacementService.** 판정 순서 = 페이즈 → 공간(footprint 전 칸 `(셀 층 & 유닛 층) != 0`, M29 다칸) → 유닛 유효 → 풀 → 보드 상한 → 코스트(「구조 > 자원」). 점유 쌍(occupied + cellOwner) 항상 함께. 자석 스냅 row-major first-win. 배치·사망·퇴근 쿨타임(퇴근 = 사망 비율, `Clamp01` 이 방어선). `maxOnBoard` 는 타입 키 덮어쓰기 대신 `max(remaining, new)`(census 후속 후보 반영 — 라이브 전원 1 이라 무변). 퇴근은 `Dead` 를 안 켜고 `Destroy` — 각성·사직서·작별 선물이 배제 코드 0 으로 안 일어난다. 배치 페이즈: `Deploying` 한 단계(비행은 뷰 시간, 착지 커맨드 `LandDefender`), 활성화 = 모션 길이(`DeployMotionSeconds`) 배틀 시간. 효과 타일 1회(회수 없음, 재배치 재무장 없음 — 가드 분리).
2. **CostLedger.** 시작 10·상한 15·재생 1/s·재생 배율(드림스톤 `CostRate`)은 `CostConfig` 정의표. **재생 시작 = 배치 페이즈 종료 틱**(X24 — 담당자 소유, UI 갭 소멸). `TryPay` 는 `PlacementService` 판정 마지막 단계. 메타 intent `GainCost` 즉시 반영.
3. **WaveScheduler.** `WavePatternGenerator` salvage — **RNG 소비 순서 byte-identical**(보스 1종이면 미소비 가드 · 램프 `NextFloat` 1콜 등). 플랜 우선순위(테스트 플랜 > 저작 인카운터 > 시드 생성). 케이던스 = 전멸 OR 상한 경과(저작 플랜은 타임라인). 웨이브 1 즉시. 리드인은 스폰 기준시각에만(X10). 간격 폴백 사슬(덱 → 플랜 → 20)(X11). 보스 케이던스 후처리 + 경보는 **스폰 시 한 곳**(X13). 당김 2층(규칙 `TryPull` / 기제 `ForceNext`), 상한 = 덱 `maxPullsPerClear`, 전멸로만 회복, 저작 플랜 면제. 보너스: 킬 N AND 스트레스 ≤ 임계, 래치, 크레딧 한 회분 차감(X14), 억제 플래그는 판 경계 리셋 밖(X6), 포탈 `i % portalCount`. 전멸 판정은 **자기 술어**(보너스 적 제외, 분열 자식은 처치 이벤트 구독이 웨이브 예약 **앞**이라 먼저 태어난다 — X2). 스폰 흩뿌림 `SpawnSpread`. 살아 있는 공격자 목록엔 필터 없음(X12).
4. **HeartMeter.** 마음 체력을 **담당자가 든다**(마음 거점 개체는 위치·피격 대상만, 체력 미러 금지 — X29 이사 비용 0). 스트레스 = `StressMath.FromHealth`(표시 정규화 100). 골 도달 구독: 돌격형 = `stabilityDamage` 즉시, 공성형 = 거점 피해가 여기로. 킬 회복 = `awakeningReward × killHealPerAwakening`(SO 원값). 본능 생존 중 `CoreShielded`(마음 후보 제외). **첫 붕괴 = `EndMatch(stress_full)`**(X18). 구독 순서: 골 이벤트 → 안정도 → 보너스 제안(X2).
5. **ScoreLedger.** 킬 = 1점 생값(보스·분열체 포함). `UnitSlain` 구독. 제출값 무가공.
6. **MatchClock.** 타이머(`ModeDef.clockKind` FixedLimit/CountUp) · 배치 페이즈 창(길이 0 이어도 진입 신호 발화 — 트레이 구성) · 종료 통로 3(`complete`·`stress_full`·`submitted`) **정확히 3** · 판 경계 리셋 **한 곳**(X8) · `EndMatch` 뒤 틱 0 · 붕괴 종료만 연출 박자(X15 — 뷰가 `reason` 으로 판단).
7. **HandDeck.** rule-holders 의 `DreamcatcherHandController` 19행 이관: 큐 12 = 저장 10 + 공용 액티브 2, Fisher-Yates 1회(매치 시드 raw), 손패 = 큐 앞 N, 각성 게이지(킬 보상·사망 보상·퇴근 0·액티브 비용·초과 소멸·시간 충전 없음), 회수(사망·퇴근 → 뒤 / 인수인계 집합 연산 → 다른 카드 앞 / 액티브 → 뒤 / 실패 부착 무변), 부착 상한 3(`CountAttachedTo`). **각성 손패의 실시간 계약(D23)은 뷰 애니메이션 시간** — 규칙은 틱. 부착 효과 자체는 unit 7.
8. **GimmickHost.** 시즌 게이트(`ModeDef.gimmickPool` × `DeriveGimmickSeed`)로 이번 판 기믹 1개 선택, 바인딩 부착은 unit 7. 빈 호스트라도 판 시작 이벤트에 `GimmickAssigned` 를 낸다(리빌 페이즈용).
9. **매치 모드.** `MatchModeData` SO 스키마 = 별첨 표 그대로(append-only). `ModeDef` plain. `IMatchGoal` concrete 3: `KillScoreTimedGoal`(만료→`complete` · 점수 = 킬 · 붕괴 = 남은 시간 몰수, 같은 잣대) · `WaveClearGoal`(마지막 웨이브 dispatch + 전멸 → `complete`, 붕괴 = **패배** 라벨) · `TimeAttackGoal`(위 신호 + 경과 시간 점수, 오름차순). `MatchGoalContext` = 담당자 읽기 모델 + `EndMatch` 하나. 담당자 안에 `if (mode…)` 0. v1 제출은 `KillScoreTimed` 만(`submitsReport`).
10. **재시작·쿨다운 시계(사용자 답 대기 — 기본값으로 진행).** 판 안 재시작 = **없음**(`BattleMatch` 는 새로 조립, C7·S6·K5 계약 안 옮김). 액티브 쿨다운 = **판의 시계**(`HandDeck` 이 틱으로 잰다 — 슬로모 중 느려짐). 답이 다르면 이 두 줄만 바뀐다.

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 판 경계 리셋 3곳 중복 | 한 곳(`MatchClock`) | 보류(X8) |
| 종료 후 전투 계속 | 계약 5 | 보류(X1) |
| 코스트 재생 스위치 UI 소유 | 담당자 소유 | 필수 규칙의 **소유 이전**(X24) |
| 유출 카운터·`OpenBreachedCellsForLeak`·몽마의 계약·적 마음 판정·뽑기 폴백·재시작 경로 | 제거 확정 | 제거(X17·X20~X23) |
| `GamePhase.Tally` 합산 연출 | 이미 은퇴. enum 값은 카메라 에셋 정수라 유지(X16) | 보류(X19) |
| `_spawnSpreadCounter` 가변 상태 | 현행 의미 유지, 순번 파생은 후속 후보 | 보류(X25) |
| 타이머 소유자 2곳 | `ModeDef.durationSec` 하나(덱 값은 조각 E 까지 폴백) | 보류 |

## 완료 기준

- [ ] 헤드리스 초록. `WavePatternGenerator` RNG 소비 순서 테스트 byte-identical(옛 테스트 복사).
- [ ] 골든 `kill_race_3min`: 라이브 덱(`Deck_Duel`)·맵 스냅샷·기본 모드로 3분 완주, 종료 사유 `complete`, 킬 > 0, 결정론 2회 동일. `wave_clear_8`·`time_attack_8`: 8웨이브 클리어 종료, 마음 붕괴 시나리오에서 `MatchOutcome.kind = Defeat`.
- [ ] 모드 유효성 테스트(Assets lane) 빨강 케이스 확인(`targetWaves 12` vs 덱 `maxWaveCount 10`).
- [ ] 담당자 안 `mode` 분기 0(grep) · `EndMatch` 호출처 = `MatchClock` 만료·`HeartMeter` 붕괴·목표·제출 커맨드 **4곳 정확히**(통로는 3, 목표는 `complete` 를 공유).
- [ ] rule-holders 133행 중 `HandDeck`·`CostLedger`·`PlacementService`·`MatchClock`·`ScoreLedger`·`WaveScheduler`·`HeartMeter` 귀속 56행이 코드 포인터로 매핑(표 하단). 미정 2 는 사용자 답으로 닫음.
- [ ] 옛 코퍼스 `basic`·`long_boss`·`force_wave` 와 거시 지표 대조표(참고).
- [ ] `core-reviewer` APPROVE.
