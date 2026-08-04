# M1 청사진 ① — IMatchSession 세션 계약

> unit 7 산출물 · 2026-08-04 · 실측 기반(트레이스 스키마·플레이어 동사·읽기면 3트랙 전수 조사).
> 백지 재도출 원칙(ADR D6): 기존 코드는 근거 자료이지 계약의 정본이 아니다. 단 **LegacyTraceV0
> 와의 대응은 §9 에 명시**한다 — parity 비교기가 두 세계를 이어야 하기 때문.

4구현이 이 표면 하나를 공유한다: `LocalSession`(인프로세스, RTT 노브) · `RemoteSession`(M3) ·
`ReplaySession`(M2, seek) · `GhostSession`(필터드 프로젝션). 스왑 = 구현체 교체 1곳.

## 1. 세션 수명

```
Create(MatchConfig config)            // configHash 확정 — 드래프트/시드/기믹 배정은 config 구성(커맨드 아님)
  → InstallSnapshot(snapshot)?        // 재접속·리플레이 seek 전용. 신규 매치는 생략
  → [ SendCommand* / Tick / OnTickEvents / ReadModel ]*
  → 종료: MatchEnded(outcome) 이벤트  // victory | victory_timeout | defeat | aborted
  → Dispose                           // 이탈(나가기) = 커맨드가 아니라 세션 파기. 현 MenuPopup.OnExit 실측과 일치
```

- `outcome` 은 현 `CaptureLegacyTraceResult` 3종 + **`aborted`**(비종결 종료 — 현 trace 의
  `incomplete`/`stopped` 폴백이 여기 대응한다. critic C-M6).
- **restart 는 세션 동사가 아니다** — 프로덕션에 재시도 UI 가 없고(실측), 하네스의
  `RestartHarnessMatch` 는 "같은 config 로 세션 재생성"이다. 계약상 restart = Dispose → Create(같은 config).
- **ReplaySession 의 도출**(critic M3): `InstallSnapshot(직전 키프레임 ≤ t)` + `Tick` 반복 —
  Tick 은 **기록된 이벤트의 방출 전용**이다(재시뮬 아님 — ADR D2). seek 을 위해 세션은
  **스냅샷 키프레임 인덱스**(간격은 M2 튜너블) 면을 가진다. 읽기 모델은 스냅샷+스트림에서
  재구성 가능한 필드만 Replay 에서 유효(§6 의 preflight 술어류는 Live 전용으로 표기).
- 페이즈(Gift·Gimmick·리빌)는 **연출**이다 — 플레이어 선택이 없음을 실측 확인. 세션은
  `Placement → Battle → Ended` 3상태만 소유하고, 연출 페이즈는 프레젠테이션이 소유한다.

## 2. SendCommand — 동사 7종 (실측 전수)

공통 봉투: `{ matchId, clientSeq(u32 단조), verb, payload }`. Entity 참조는 전부 `SimEntityId`.
SO 참조는 전부 **안정 id(string)** — 현 `DefenderUnitData` 참조 동일성 판정(`IndexOf(defenderPool, ...)`)은
id 판정으로 재정의한다.

| verb | payload | 원자성 규칙 (현 다단계의 sim 내 접기) |
|---|---|---|
| `DeployDefender` | `cell:int2, unitDefId` | 검증(공간 4종+풀+코스트+**쿨타임**) → 코스트 차감 → 스폰(pending) → on-place push 까지 한 틱. **쿨타임 시작도 sim** — 현재 UI(`DefenderSelector`)가 시작하는 것을 이관. `PlacementRejectReason` 에 `OnCooldown` 신설(현 enum 8종에 없는 실측 공백 — UI 게이트뿐이라 커맨드 우회 시 무시됨) |
| `SetDeployFacing` | `defender:SimId, facing:int2` | **활성화의 주체는 커맨드가 아니라 Deploy 가 예약한 `activationTick`**(= tick + delayTicks(unitDef) — 현재 뷰 코루틴이 소유하던 지연을 sim 시퀀스로. critic M5 로 역할 단일화). 이 커맨드는 방향 힌트만 싣고, `activationTick` 전 도착분을 병합·미도착 시 기본 +Y(현 조준 붕괴 폴백과 동일). 활성화 실행이 pending 해제 + on-place exactly-once(`_onPlaceTriggeredEntities` 계승)를 수행. 거절: `TooLate`(deadline 초과)·`UnknownEntity` 신설(현재는 조용한 no-op 뿐) |
| `RelocateDefender` | `from:int2, to:int2` | 점유 스왑 + **비행을 sim 상태로**(`RelocationFlight{ landTick }` — 현재 뷰 코루틴이 착지 시각 소유). 거절 = `RelocationCheck` 순수 함수 계승(`NoDefenderAtSource/SourceBusy/SameCell` + 공간 4종). 코스트 없음(기존 계약) |
| `PlayCard` (변종 4: `Attach` / `MarkEnemy` / `ActiveTile` / `ActivePortal`) | `cardInstanceId` + (`host:SimId` \| `enemy:SimId` \| `cell:int2` \| `entry:int2, exit:int2`) | **원자 트랜잭션**(설계 정본 §8 MAJOR): 검증 전부 선행(손패 보유·타입·게이지≥cost·유출허용치 선불 가능·부착 캡·적용성 preflight[`DuplicateState` 포함]·Active 쿨다운·포탈 entry≠exit) → 효과+게이지 지불+유출 선불+손패 소비를 **한 틱 안에서** 적용. 현 5단계(적용→지불 실패 시 revoke 롤백)의 롤백 경로 자체를 소거한다. `cardInstanceId` 는 덱/손패 상태가 sim 소유가 되는 전제(§5 통화·§10) — 현 `entryId` 는 Mono 덱 로컬이라 승격 불가 |
| `ForceNextWave` | — | **비멱등**(`_waveTimeShift` 누적 재기준 — 실측) → clientSeq 총순서 필수. 연타 허용은 기존 계약 유지. receipt 선례 있음 — 현행 receipt 는 3종(ForceNextWave + 하네스 전용 2종)이고 **배치·재배치·FinishPlacement·Pause 는 receipt 전무**(critic m1 — §9 에 명시) |
| `FinishPlacement` | — | **배치 카운트다운을 sim 이 소유** — 현재 뷰 타이머(`Time.deltaTime`)와 버튼이 같은 지점에 수렴하는데 타이머가 뷰 시계라 두 트리거가 갈릴 수 있다. sim: `placementDeadlineTick` 만료 시 자동 수락과 동일 경로. `PlacementPhasePolicy.CanFinish` 순수 함수 계승 |
| `Pause` / `Resume` | — | 유일하게 커맨드 자격이 있는 시간 제어(MenuPopup, scale 0·priority 100). **UI 제스처 슬로모 6종(드래그·이동·조준·인스펙트·손패·튜토리얼)은 커맨드가 될 수 없다** — Remote 에선 각 클라 손가락이 남의 sim 을 늘리는 구조. 처분(뷰 전용 시간 확장 vs sim 클럭 분리)은 **M1 "gameplay 시계 정책" unit 의 입력**으로 이관하고, 본 계약은 "sim 클럭을 바꾸는 것은 커맨드뿐"만 고정한다 |

## 3. CommandReceipt — typed 승격

현행: `channel="CommandReceipt", payload="command=...,accepted=..."` 문자열, 멱등성·전용 순번 없음(실측).

```
CommandReceipt {
  matchId, configHash,          // 골든 diff 1차 판독 축(기존 계약 계승)
  clientSeq,                    // 요청 에코
  accepted: bool,
  rejectReason: CommandReject,  // 아래 통합 enum
  acceptedTick,                 // 수락 시 실행 tick
  orderInTick,                  // 같은 tick 내 실행 순서(0부터) — 현 전역 sequence 를 tick 상대화
}
```

- **멱등성**: 같은 `clientSeq` 재전송 → 같은 receipt 재발급(재실행 금지).
- **순번**: 전송 채널은 순서 보장을 가정한다 — 비순서 채널이면 **세션이 재정렬 버퍼를 소유**하고,
  갭은 즉시 거절이 아니라 **보류 + 타임아웃 후 `SeqGap` 거절**(critic M4 — §8 jitter 와의 충돌 해소).
- **`CommandReject` 통합 enum**(계수 = None 제외 실멤버): `PlacementRejectReason` 은 실측 12멤버
  (None + 배치 8 + 재배치 3 `NoDefenderAtSource/SourceBusy/SameCell`) + `OnCooldown` 신설 ∪
  `DcRejectReason` 8(현재 **밖으로 새지 않고** `bool` 로 접혀 UI 가 preflight 미러로 재계산하는
  이중 구조를 소거. 단 `Unclassified` 는 배선 버그 센티넬이므로 와이어 사유가 아니라 세션 그룹
  `InternalError` 로 분리 — critic m3) ∪ 웨이브(`NoWaveLeft/NotRunning`) ∪
  세션(`SeqGap/UnknownVerb/PhaseClosed/TooLate/UnknownEntity/InternalError`). 접두 그룹핑으로 한 enum.

## 4. OnTickEvents — 이벤트 3분리와 채널 판정

이벤트 봉투: `{ eventSeq(매치 전역 단조 — 백로그 재개점·틱 내 총순서의 축, critic M1), tick, channel, payload }`.

내부 phase queue 9종(`AggroHit·Cast·ThreatHit·BlinkRequest·EnemyCc·DotApply·CcClear·StatModifierApply·StackModifierApply`)은
계약 밖(내부 전달 수단 — 현 `internalPhaseChannels` 제외 목록 계승). 어떤 기전으로 접히는지는
**unit 9(틱 파이프라인)의 소유**다(critic m4 — 여기서 선점하지 않는다).

판정 대상 = **현행 출력 18채널 ∪ genesis/이동 신설**(critic C1 — 현 18채널엔 스폰·배치·발사 사건이
없다: 스폰이 전부 Bridge 소유라 채널이 애초에 없었다. AMR 이 "상태 재구성 가능한 권위 기록"이려면
개체 발생과 권위 웨이포인트가 스트림에 있어야 한다):

| 신설 채널 | 판정 | 계약명 / 비고 |
|---|---|---|
| (신설) | semantic | `EnemySpawned{ enemy, unitDefId, spawnCell, waypoints }` — 웨이포인트는 발생 시 1회 + 변경 시 `WaypointUpdate` |
| (신설) | semantic | `DefenderDeployed{ defender, unitDefId, cell }` — Deploy receipt 의 수락 사실과 별개인 스폰 사실 |
| (신설) | semantic | `ProjectileSpawned{ projectile, dataIndex, shooter, target/impact }` |
| (신설) | semantic | `WaypointUpdate{ entity, waypoints }` — 권위 웨이포인트 + 코스메틱 클라 보간(설계 정본 §1). 판정 좌표는 각 사건이 동봉 |

현행 18채널 판정 — **semantic AMR**(상태 재구성에 필요한 게임 사실) vs **presentation projection**(파생 연출 신호):

| 현행 채널 | 판정 | 계약명 / 비고 |
|---|---|---|
| `EnemyKilled` | semantic | `EnemyKilled{ victim, killer, killScore, awakeningReward, pos }` — 킬버스트 필드는 후속 `HazardSpawned`/`DamageApplied` 로 정규화 검토 |
| `GoalReached` | semantic | `EnemyLeaked{ enemy }` 개명 — 유출 카운터·패배 판정의 근거 사실 |
| `DefenderDeath` | semantic | `DefenderDied{ defender, cell, onDeathAoe... }` — 3-arg tap 이 하던 entity 보강을 DTO 정식 필드로 |
| `ShieldBreak` | semantic | 파열 사실 + OnShieldBreak payload 실행 근거 |
| `HazardSpawnRequest` | semantic | `HazardSpawned` 개명(요청이 아니라 사실 시점으로) |
| `HazardDestroyed` / `HazardRuntime` | semantic | 장판 생멸·적용 사실(eventType 축 유지) |
| `MeteorBarrageRequest` | semantic | 기믹 임계 도달 사실 |
| `DcTriggerFired` | semantic | 카드 발동 사실(카드 계열 UI·아이콘 펄스의 근거) |
| `AttackOutputLog` | semantic | `AttackResolved` 개명 — 공격 산출(kind/magnitude/stat/stack) 사실 |
| `ProjectileHit` | semantic | 착탄 사실(payload 해석 완료 시점) |
| `DamageNumber` | **projection** | semantic 은 `DamageApplied{ target, amount, source }` 로 신설하고 hpRatio·표시 위치는 파생 |
| `HealApplied` | semantic 승격 필요 | 현 DTO 가 `pos+amount` 뿐 — **대상 SimId 가 없다**(실측). `HealApplied{ target, amount }` 로 재정의 |
| `ShieldGranted` | projection | 원샷 VFX 신호(pos 뿐). semantic 은 실드 슬롯 상태 변화로 충분 |
| `UnitAttackVisual` | projection | 공격 모션 신호(초당 5회 코얼레스 대상) |
| `KnockupVisual` | projection | 심 실체는 Stun — 띄우기 그림 전용(기존 채널 설계 의도 그대로) |
| `BossLeapVisual` / `UltimateLeapVisual` | projection | sim 은 Blink 로 종결 — 뷰 아치/예고 타이밍 전용 |

- **규칙 실행 위임 4채널의 이관**(critic M9): `HazardSpawnRequest`·`MeteorBarrageRequest`·
  `ShieldBreak`(payload AoE/수면을 Bridge 드레인이 실행)·`DefenderDeath`(타일 해제·시너지 재계산·
  onDeathAoe 실행)는 현재 "사실"이 아니라 **Bridge 에게 내리는 명령**이다. 신 sim 에서 규칙 실행은
  sim 내부로 이관하고 이벤트는 **결과 사실로 격하**한다(파생 피해·수면은 각자의 semantic 사건으로) —
  ADR D4(Bridge 해체)·D5 교훈("뷰는 게임 규칙을 소유하지 않는다").
- projection 은 semantic 스트림의 **파생**으로 재생산 가능해야 한다(리플레이 = 스트림 플레이백).
- **형상 변경 3등급**(critic M8 — "라벨만" 단일 규칙은 자기모순이었다):
  ① **라벨만**(스왑 시) — semantic/projection 분류 부여, 형상 불변.
  ② **AMR 필수 필드**(스왑과 동시 + 비교기 보정) — 상태 재구성에 없어선 안 되는 것:
  tick 귀속 통일(현행 16채널 `tick-1`/2채널 `tick` → 발생 tick), `HealApplied.target` 신설(현 DTO 는
  pos+amount 뿐 — 대상 없는 힐로는 재구성 검증 불성립), `DamageApplied` 신설, genesis/웨이포인트 신설.
  ③ **순수 개명**(parity 통과 후 별도 커밋) — `GoalReached→EnemyLeaked` 등 의미 불변 이름 정리.
- 이벤트에 실리는 연속값(`pos` 등)은 epsilon 축, 나머지는 exact(기존 parity 계약).

## 5. InstallSnapshot — day-1 스키마

```
Snapshot {
  snapshotTick, eventSeq, lastAcceptedCommandSeq, sessionEpoch, configHash,
  idAllocator,                       // 현 _simEntityIdCounter
  rng: { meteor, pickup, bomb, ... },// 서브스트림별 state (현 Part A 의 meteorRng 를 전 스트림으로 확장)
  clock: { battleClock, timerRemaining, placementDeadlineTick },
  waves: { nextWaveIndex, waveTimeShift, pendingSpawns[] },   // future wave 포함
  score: { goals, leakPenalty, killScore, stress },  // stress 는 현 canonical 에 없음 — 신설(critic m6)
  currencies: { cost, gauge, leakAllowance, unitCooldowns[], skillCooldowns[] },  // §10 이관 전제
  deck: { hand[], cycleState, attachments[] },
  entities: [ per-SimId 블록 ],      // ⚠ 범위 기준 = unit 8 대응표(96+21) 중 sim 상태 판정분 **전수**
                                     // (상한에서 차감하는 방식). 현 상태 해시 Part B(10+10)는 드리프트
                                     // digest 이지 재개 가능 상태가 아니다 — 하한으로 쓰지 말 것(critic C3)
  unkeyed: [ PickupSpawnState 등 ],  // 현 Part C 계승 — 스포너 상태. 땅의 픽업·포탈 링크·토네이도 등
                                     // per-instance 는 ID 승격 후 entities 로(§10-1)
  scheduledCommands[],               // 예약분(activationTick·relocation landTick 등)
}
```

- `sessionEpoch` = **세션 재생성 카운터**. epoch mismatch = 보유 백로그 무효 → 전량
  `InstallSnapshot` 재수신(critic M2 — Remote 재접속의 도출 근거).
- 재접속: `snapshot + eventSeq 이후 백로그` 를 **exactly-once** 로 재생(설계 정본 §8).
- 스냅샷 해시 = 현 `BuildLegacyFinalStateCanonical` 의 계승(정렬·포맷 규칙 동일 계열). cost 는
  **단일 표현으로 통일** — 현행은 tick 모델이 int(절삭)·해시가 float 로 이원화(실측 불일치).

## 6. 읽기 모델 — tick-스탬프드

원칙: 뷰는 **폴링을 세션 읽기 모델로, push 를 이벤트 구독으로** 대체한다. 실측 결론 3개가 형태를 정한다 —
점수/유출/스트레스는 현재 **읽기면이 없어**(전부 private + 뷰측 독립 누적) 신설이고, 코스트/쿨다운은
현재 sim 밖(Mono 런타임)이며, 기믹 진행은 관측 불가(신설 면).

| 그룹 | 필드 | 현행 대응 |
|---|---|---|
| 진행 | `tick, battleClock, phase(Placement/Battle/Ended), timerRemaining, placementRemaining` | 뷰 타이머·`RemainingBattleSeconds`(private) 승격 |
| 점수 | `score{kill,total?}, goals, effectiveLeakLimit, stressAccrued, stressLimit, outcome?` | **신설** — `SetLeakStatus`/`ShowVictory` push 와 튜토리얼의 `scoreHud.StressLimit` 역폴링이 전부 여기로 접힘 |
| 웨이브 | `nextWave{available,hasNext,number,clearReady}, spawnForecast(복사본)` | `NextWaveDock` 폴링 5종 + `TryGetSpawnAlertForecast`(현행 캐시 참조 노출 금지 — 실측 주석 계약) |
| 통화 | `cost{current,max}, gauge{current,max}, leakAllowance, cooldown(unitDefId)/cooldown(skillId)` | `CostRuntime`·`Gauge`·`RemainingLeakAllowance`·쿨타임 2종 — §10 sim 이관 전제. cost 는 결정론 대상이면 고정소수점 후보(현 float 비교 실측) |
| 손패 | `hand[], canUse(cardInstanceId), costOf, attachments[], attachBudget(host)` | `DreamcatcherHandController` 읽기면 — 덱 소유권 이동과 한 몸 |
| 개체 | `IsAlive(SimId)`, `Transform(SimId)`, `StatusReadout(SimId){cc[],dot[],shields[],modifiers[]}`, `StatReadout(SimId)`, `DefenderAt(cell)`, `CanPlaceAt(cell,unitDefId)`, `CanRelocate(from,to)` | **생존 질의 명시 신설** — 현행은 `TryGetUnitViewAnchor` 실패를 생존 프로브로 겸용(실측). **Transform·상태이상은 뷰 sync 최다 폴링 2축**(critic C2 — `LocalTransform` 15곳 + CcEffect/DotEffect/ShieldSlot 버퍼): 내부 9채널을 계약 밖으로 두는 대신 상태이상은 이 읽기면이 서빙한다(KnockupVisual 등 projection 의 파생 근거). preflight 술어(`CanPlaceAt` 등)는 커맨드 검증과 같은 함수 공유(이중 계산 소거), **Live 전용**(Replay 에선 무효 — §1) |
| 기믹 | `gimmickProgress?` | **자리만 예약** — 값 정의는 기믹 이식 unit 에서(현재 관측 불가면) |

계약 밖(세션이 서빙하지 않음): 좌표/픽/스크린 rect 공간 질의(뷰 서비스), static 프레젠테이션 상수
(SO 미러), `BattleRunning/HarnessTick/ConfigHash`(하네스·러너 전용 — UI 소비자 0 실측).

## 7. 고스트 프로젝션

`GhostSession` 이 semantic 스트림에서 필터: `{ tick, deployEvents(수락 receipt 파생: cell·unitDefId),
scoreMilestones, waveIndex }` — 웨이브 인덱스 정렬(설계 정본 §1). 추가 필드 금지(비교 화면의 정보 예산).

## 8. LocalSession RTT 주입 노브

`LocalSession(config, rtt: { commandDelayMs, eventDelayMs, jitterMs })` — SendCommand→receipt 와
이벤트 전달 양쪽에 지연 주입. 상설 가드 ③: 엔지니어링 도구가 아니라 **수용 기준**(RTT 150ms 에서
전 스킬·카드 디자인 리뷰 통과가 M1 게이트, 매트릭스 50/150/300ms+jitter 는 M2 확장).

## 9. LegacyTraceV0 대응표

| LegacyTraceV0 | 세션 계약 | 판정 |
|---|---|---|
| `header{version,configSchemaVersion,configHash,seed,tickRate,deckId,mapGoalCount}` | `SessionInfo` | 동일(필드 계승) |
| `header.scenario` | — | **제외** — 하네스 라벨(critic m7) |
| `header.bridgeDrainedChannels/internalPhaseChannels/channelPolicy` | §4 판정표 | 개명 — 18/9 구분이 semantic·projection/내부 로 재라벨 + genesis 신설분 추가 |
| `header.commandChannels` | §3 receipt 스트림 | 승격(critic m7) |
| `CommandReceipt`(문자열 payload, 전역 sequence, 현재 tick) | §3 typed struct | **승격** — clientSeq·멱등성·orderInTick 신설. ⚠ 현행 receipt 는 3동사뿐 — **배치·재배치·FinishPlacement·Pause 는 receipt 부재**라 그 4동사의 receipt parity 는 신 sim 쪽 신설 검증(critic m1) |
| `ticks[]`(14필드) | §6 읽기 모델 | 계승 + cost 표현 통일 + `bosses` 등 카운트는 디버그 축으로 유지. **phase 는 8종→3종 축소 = 명시적 행동 차이** — 비교기가 폴딩표(Gift/Gimmick/Placement→Placement, Battle→Battle, Result/Tally→Ended)를 소유(critic M7) |
| `events[]`(tick-1/tick 이원 귀속) | §4 | 발생-tick 통일 — **명시적 행동 차이**, A/B 비교기가 구 트레이스의 귀속 시프트를 보정 |
| `final{outcome,score4,stateHash,executedTicks}` | `MatchEnded` 이벤트 + 스냅샷 해시 | 계승 + `incomplete`/`stopped` 폴백 → `aborted` 매핑(critic M6) |
| `restart.json` 골든(teardown/re-arm 1 trace) | §1 Dispose→Create | **세션 2 / trace 1** — 비교기가 이어붙임(critic m5) |
| Entity→`sim:{id}` 정규화(미등록 throw) | SimEntityId 전면화 | 계승 — 단 pickup/사직서/필드 캐리어가 이벤트에 실리게 되면 **ID 부여 승격이 선행**돼야 함(현행 throw 함정 실측) |

## 10. 미결 — 다음 unit 의 입력

1. **ID 미부여 4종의 승격**(critic C3 — 1급): pickup·사직서 드랍·필드 캐리어(버프장판·토네이도·포탈)는
   per-instance sim 상태인데 SimEntityId 가 없어 스냅샷·이벤트 축에 태울 수 없다(현행 정규화는
   미등록 Entity 에 throw). unit 1 이 "unit 4 에서 재판단"으로 미룬 것을 스냅샷 요구가 확정시킨다 —
   카운터의 ECS 싱글턴화 포함.
2. **통화 5종의 sim 이관**: `CostRuntime`(float — 고정소수점화 검토)·각성 게이지·유출 허용치
   (`TryPayLeakAllowance` 비가역)·배치 쿨타임(`PlacementCooldownRuntime` — 거절 enum 공백의 뿌리)·
   스킬 쿨타임(`SkillRuntime`). 커맨드 검증이 sim 안에서 닫히기 위한 전제 — 이식 unit 분해 시 1급 항목.
3. **덱/손패 소유권**: `cardInstanceId` 가 성립하려면 `DreamcatcherCycleDeck`·`_attachedTo` 가 sim 상태여야
   한다. 선물 셔플(시드 파생)도 config 물질화 대상.
4. **시계 정책**: UI 슬로모 6종의 처분(§2 Pause 행). ⚠ 두 사실이 처분을 제약한다(critic M10):
   ① `CostRuntime`·`PlacementCooldownRuntime` 이 Battle 도메인 dt 로 tick 하므로 슬로모는 **지금
   코스트 회복·배치 쿨다운을 늦추고 있다** — "뷰 전용 격하"는 통화 누적 rate 변경 = 명시적 행동
   변경으로 다뤄야 한다(§10-2 와 한 몸). ② 튜토리얼 힌트는 slow-mo 가 아니라 `scale 0`(완전 정지)라
   뷰 전용으로 표현 불가 — `Pause` 커맨드로 승격이 맞다. 조준 lease 가 활성화 시각을 결정하는 현
   구조는 `SetDeployFacing` 의 sim 시퀀스화로 함께 풀린다.
5. **projection 채널의 순수 개명 시점**: §4 형상 변경 3등급의 ③ — parity 통과 후 별도 커밋(골든 보호).
