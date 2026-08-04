# M1 salvage 판정표 — conform / adapt / rewrite / discard

> unit 10 산출물 · 2026-08-04. "설계는 백지, 실행은 스트랭글러"(ADR D6)의 실행 절반.
> 입력: 청사진 ①②③ + 전수 인벤토리 2편(컴포넌트 97+21 · 게이트 44 · 쓰기 지도 44행).
> 11+ 구현 unit 분해가 이 표의 등급 집계를 직접 인용한다.

## 등급 정의

| 등급 | 뜻 | 이식 비용 |
|---|---|---|
| **conform** | 그대로 옮긴다 — 이미 아키텍처 중립(plain 값 입출력) 또는 순수 계산 | 파일 이동 + using 정리 |
| **adapt** | 로직은 살리고 껍데기를 바꾼다 — ECS 관용구(쿼리·ECB·lookup)를 컬렉션 순회로 | 몸체 재작성, 규칙 보존 |
| **rewrite** | 규칙을 다시 표현한다 — 형태가 ECS 에 종속돼 직역이 무의미 | 계약만 승계 |
| **discard** | 신 sim 에 대응물이 없다 — ECS 아티팩트 또는 소비자 0 | 삭제 |

---

## 1. 시스템 44 판정

순서는 order-capture(청사진 ③ P1~P12). 근거 열은 인벤토리 실측을 가리킨다.

| # | 시스템 | 등급 | 근거 |
|---|---|---|---|
| 1 | LastRun | adapt | 단일 컴포넌트 tick + `IncomingDamage` append. ECB 는 컴포넌트 제거뿐 |
| 2 | HazardLifetime | **rewrite** | 매 틱 `NativeParallelMultiHashMap` 전체 재빌드가 본질 — 신 sim 은 셀→효과 인덱스를 증분 유지하는 편이 자연스럽다(동률 예외 ⑥의 뿌리도 이 컨테이너) |
| 3 | AllyBuffField | adapt | "매 틱 재발행 = 회수" 시맨틱 보존(청사진 ③ §6). 채널 → 직접 적용 |
| 4 | BossPeriodicTrigger | adapt | 슬롯 tick + 캐리어 생성 → 발사 요청 큐잉으로 |
| 5 | ZoneApply | adapt | 순수 생산자(컴포넌트 쓰기 0) — 3채널 enqueue 를 직접 호출로 |
| 6 | ObstacleLifetime | adapt | `blockedCells` 재빌드 + 2루프(부재로 아키타입 분기 — 인벤토리 B-1) |
| 7 | DefenderField | conform | multi-source BFS = 순수 그래프 계산. 입출력이 배열 |
| 8 | AggroState | adapt | capacity FIFO 동률(예외 ②)·orphan 해제 OR 게이트 보존 |
| 9 | ModifierApply | **rewrite** | 존재 이유가 "큐 드레인 + ECB/EntityManager 혼용 회피"(인벤토리 C-5) — 채널 소멸 시 병합 함수로 접힌다. 병합키·refresh=max 계약만 승계 |
| 10 | CcApply | adapt | `CcEffectMerge` 는 conform, 드레인 껍데기만 adapt. **보스 면역 단일 게이트 지점 보존** |
| 11 | HealthDeath | adapt | 사망 마킹 안전망 — 릴레이 불변식(청사진 ③ §3) |
| 12 | LethalTimer | conform | 타이머 감산 + 플래그 |
| 13 | TauntAttackGrant | **rewrite** | 전부 ECB(구조 변경만) — 신 sim 에선 "공격 수단 부여/회수"가 상태 전이 1줄 |
| 14 | EnemyAiState | conform | FSM 전이 판정 — 이미 값 계산 |
| 15 | DotApply | adapt | 부여·틱·감쇠 3단 + `(origin,element)` 병합키 보존 |
| 16 | PatrolField | conform | 방향 굽기 = 순수 계산 |
| 17 | Movement | adapt | 하강·포탈·trim 로직 보존(`MovementCellTrim` 등 순수 헬퍼는 conform) |
| 18 | HazardCast | adapt | 쿨다운 + 최근접(unit 1 simId 동률) + Cast 같은-틱 계약 |
| 19 | ShieldCast | adapt | `IncomingShield` append 만 — 병합은 #34 |
| 20 | ResignationThreshold | adapt | 임계 소모 + barrage 요청 |
| 21 | HeatAccrual | adapt | lazy-attach 2-pass 를 1-pass 로 접기(청사진 ② §5) |
| 22 | PickupSpawn | adapt | **RNG write-back 필수**(§5) |
| 23 | PickupConsume | adapt | 소비 락(`LastRun` 보유 = 거절) 보존 |
| 24 | HitFlash | **discard** | 자기 `LocalTransform.Scale` 만 쓰는 **뷰 연출**(스케일 펄스 0.15s) — sim 소비자 0 실측. ⚠ 생산자는 sim(`ProjectileHitSystem:273-280·396-403` 이 태그 Add/Set) → **sim 은 피격 사실(`DamageApplied`)만 내고 펄스는 뷰가 소유**. 태그·시스템은 폐기, `originalScale` 은 뷰 로컬 상태 |
| 25 | EffectTick | adapt | 캐리어 TTL — 캐리어가 객체가 되면 수명 필드로 |
| 26 | ProjectileMove | adapt | 궤적 전진(`MovementKind` 분기) — 궤적 수식 자체는 conform 후보 |
| 27 | ProjectileHit | adapt | payload 디스패치 — 규칙 밀도 최고(573줄). 분해는 이식 unit 소관 |
| 28 | FatigueAccrual | adapt | lazy-attach 접기 |
| 29 | StatModifierTick | conform | 슬롯 감산·만료 |
| 30 | ModifierStatsAggregate | conform | `(base+Σadd)·Πmul` + 클램프 = 순수 산식(`ModifierMath` 는 이미 순수) |
| 31 | MaxHealthScale | adapt | `Health.ScaleMax` 순수함수 + 2-pass 접기 |
| 32 | StackModifierTick | adapt | 엣지 임계 교차 감지 보존. **`GetStackThresholds` 의존 역전이 선행**(§4) |
| 33 | Attack | adapt | 최대 클러스터(1,600줄) — 타겟팅 랭킹 4종은 이미 conform(순수), 루프 몸체 adapt |
| 34 | DamageApplication | adapt | 인박스 3종 드레인 + 실드 병합 + 킬 귀속(동률 ①) + 마킹 |
| 35 | ResignationDrop | adapt | 사망 창 관찰(릴레이 §3) |
| 36 | PatrolLifecycle | adapt | 소환사 3중 판정 → SimId 등록부 질의로 번역(청사진 ② §2) |
| 37 | CcClear | adapt | wake-on-hit 소비 |
| 38 | ProjectileEmitter | adapt | `EmitterInstance` tick + ShotOrder → 요청. `ShotOrder`·`PatternSpec` 은 conform |
| 39 | DreamCocoon | adapt | 3단 판정 순서 계약 보존 |
| 40 | CcDecay | adapt | IJobEntity `.Run()` → 단순 루프 |
| 41 | UnitLifecycle | **rewrite** | "유일한 파괴자 + 4루프 상호배제"는 ECS 삭제 지연의 산물. 신 sim 은 객체 제거가 즉시라 파괴 전 이벤트 베이크만 계약으로 승계 |
| 42 | HealthThreshold | adapt | 임계 평가 + blink 요청. ThreatHit 드레인은 §3 참조 |
| 43 | UltimateLeap | adapt | 3단 시퀀스(예고 시간 = sim 소유) |
| 44 | BlinkApply | **rewrite** | 채널 소비자 1줄 — 위치 대입이 Movement 소유라는 경계 표현. 채널 소멸 시 함수 호출로 흡수 |

**집계**: conform 7 · adapt 31 · rewrite 5 · discard 1.
→ 규칙 밀도의 대부분이 adapt(31/44)이고, 순수 계산 유틸(랭킹 4종·`ModifierMath`·`TileAoe`·`GridMath`·
`ShotOrder`·`CcEffectMerge`·`DotEffectMerge`·`KillAttribution`·`AggroPolicy`·`HeatMath`·`VolleyMath`
등 제약-10 산물)은 **시스템 판정과 별개로 전부 conform** — 이 자산이 이식 비용을 크게 낮춘다.

## 2. 채널 27 판정

| 그룹 | 채널 | 등급 | 처분 |
|---|---|---|---|
| 내부 phase (9) | AggroHit·Cast·ThreatHit·BlinkRequest·EnemyCc·DotApply·CcClear·StatModifierApply·StackModifierApply | **discard**(컨테이너) | 큐는 사라지고 phase 간 직접 전달로. **단 26쌍의 같은-틱/1틱-지연 계약은 보존**(청사진 ③ §2) — 컨테이너 폐기 ≠ 타이밍 폐기 |
| 출력 semantic (12) | EnemyKilled·GoalReached·DefenderDeath·ShieldBreak·HazardSpawnRequest·HazardDestroyed·HazardRuntime·MeteorBarrageRequest·DcTriggerFired·AttackOutputLog·ProjectileHit·HealApplied | **adapt** | 세션 이벤트로 승격(청사진 ① §4). 4채널(HazardSpawn·MeteorBarrage·ShieldBreak·DefenderDeath)은 **규칙 실행 위임 → 결과 사실로 격하**가 동반 |
| 출력 projection (6) | DamageNumber·ShieldGranted·UnitAttackVisual·KnockupVisual·BossLeapVisual·UltimateLeapVisual | **adapt** | presentation projection 으로. `DamageNumber` 는 semantic `DamageApplied` 신설 후 파생 |

`ThreatHit` 특기: 소비자(#42)는 있으나 **누적값 `ThreatEntry` 의 하류 소비자가 0**(boss-jjangssen
unit 4 가 blink 정책 교체) → 채널·버퍼·`ThreatTable.Leader` 3종이 **discard 후보**. 되살릴 계획이
없으면 이식 대상에서 빼는 것이 정직하다(판단은 이식 unit 에서 사용자 확인).

## 3. Bridge 클러스터 판정

성격 5분류: **sim 규칙**(→ sim lib 이식 · M1-4 "1급 작업") · **뷰**(→ 프레젠테이션 잔류) ·
**세션 인프라**(→ IMatchSession 구현체) · **디버그**(→ 퇴거) · **배선**(→ MatchConfig 물질화).
멤버 실측(본체 공개·비공개 메서드 전수 스캔) 기반 16 클러스터:

| 클러스터 | 대표 멤버(실측 라인) | 성격 | 판정 |
|---|---|---|---|
| 매치 수명 | `BeginPlacement:1246` · `StartBattle:1317` · `StopBattle:1689` · `TeardownCurrentBattle:544` · `OnRestartRequested:501` · `EnterPlacementOrGift:491` | 세션 인프라 | **rewrite** → 세션 수명(청사진 ① §1 Create/Dispose). 페이즈 전이는 프레젠테이션 |
| 웨이브 스케줄·스폰 | `ScheduledWaveTime:1859` · `QueueDueWaves:1867` · `QueueWave:1985` · `ForceNextWave:1944` · `SpawnUnit` · `TryInitializeGeneratedWaves:1806` · `TryGetSpawnAlertForecast:1894` | **sim 규칙** | **adapt** → sim 이식 1급. 예보는 읽기 모델(캐시 참조 노출 금지) |
| 승패·타이머·점수 | `CheckTimer:5045` · `CheckVictory:5067` · `RemainingBattleSeconds:5043` · `CalculateBattleScore:5167` · `EffectiveLeakLimit:4971` · `RemainingLeakAllowance:4985` · `TryPayLeakAllowance:4991` · `DrainGoalEvents:5000` | **sim 규칙** | **adapt** → sim 이식 1급. 유출 허용치는 통화(청사진 ① §10-2). `ScoreMath` 자체는 conform |
| 결과·집계 연출 | `ReportMatchResult:5110` · `BeginTally:5134` · `FinishTally:5152` · `RefreshLeakHud:4980` | 뷰 | **adapt** → `MatchEnded` 이벤트 소비자로 |
| 배치 규칙 | `SpatialPlacementCheck:5207`(static 순수) · `CanPlaceDefenderAt:5217` · `TryBeginDefenderDeployment:5303` · `ActivateDeployedDefender:5334/5341` · `TriggerDeploymentOnPlaceSkill:5355` · `PlaceDefender:5192`·`PlaceDefenderAs:5283`(은퇴) | **sim 규칙** | **adapt**(순수 체크는 conform, 은퇴 2경로는 **discard**) |
| 재배치 | `Relocation.cs` 전체(216줄) — `RelocationCheck:21`(순수) · `TryBeginDefenderRelocation:82` · `FinishDefenderRelocation:189` | **sim 규칙** | **adapt** + 비행 상태 sim 화(청사진 ① §2) |
| 배치 시 효과 | `ApplyOnPlaceEffect:4359` · `ApplyForwardOnPlaceProjectile:4526` · `RecomputeSynergyFor:4583` · `NeutralizeActiveSynergy:4644` · `EnqueueStatModifier*:4666~4716` | **sim 규칙** | **adapt** → 시너지·on-place 가 규칙. enqueue 헬퍼는 채널 소멸로 흡수 |
| 스킬 캐스트 | `CastSkillAtTile:2034` · `CastPortal:2083` · `ApplySlowField:2348` · `ApplyTornado:2375` · `ApplyMeteor:2418` · `ApplyPortal:2470` · `SpawnAllyBuffZone:2121` | **sim 규칙** | **adapt** → PlayCard Active 변종의 실행부. `ApplySlowField` 는 스냅샷 잔재(백로그 "감속장을 캐리어로") |
| 드림캐쳐 | `Dreamcatcher.cs`(972줄) — `ApplyDreamcatcherCard:96` · `ApplyDreamcatcherCardToUnit:243` · `ApplyBountyMark:757` · `RevokeDreamcatcherEffects` · `WouldDreamcatcherCardApply:734` | **sim 규칙** | **rewrite** → 원자 트랜잭션화(청사진 ① §2)로 5단계·롤백 구조 자체가 바뀐다. `DcApplicability` 는 conform |
| 맵·필드 빌드 | `BuildMapForBattle:971` · `BuildFlowField:785` · `BuildPickupSpawnState:887` · `PrepareDraftMap:1725` · `RebuildDraftMap:1789` · Teardown 3종 | 배선 | **adapt** → 맵 생성 결과는 `MatchConfig` 물질화(unit 3 기성), 필드는 sim 내부 재구축(청사진 ② §4) |
| ECS 인프라 | `EnsureQueriesAndQueues:1411`(280줄) · `DestroyEcsInfrastructureEntities:645` · `DisposeEcsInfrastructureNativeContainers:690` · `DisposeCachedQueries:723` · `HasLiveEntityManager:609` · `AttachSimEntityId:4109` | 세션 인프라 | **discard**(SimEntityId 발급만 sim 으로 이관) — 큐 27개 생성·해제가 채널 소멸과 함께 전부 사라진다. **`_em.` 305 중 최대 밀도 구역** |
| 이벤트 드레인 | `Drain*` 18종(`:3263`~`:4030`) + `AdvanceBattleFrame:2520` | 세션 인프라 | **rewrite** → `LegacyMatchSessionAdapter`(유일 drain 소유자, M1-4)가 흡수 후 세션 이벤트로 |
| 뷰 sync | `SyncMonoUnitViews:3013` · `SyncPatrolViews:3157` · `SyncProjectileViews:2699` · `Reconcile{StatusFx:2734,PickupViews:2889,ResignationViews:2947}` · `GatherOverheadStacks:3207` · `EvaluateEnemyHealthTint:3247` · `MirrorLiftKnobs:281` | 뷰 | **adapt** → 읽기 모델(Transform·상태이상 — 청사진 ① §6 C2) 소비자로 재작성 |
| 좌표·픽·프리뷰 서비스 | `TryScreenToCell:4729`·`Strict:4746` · `TryPickNearestEnemy:4765` · `TryPickDefenderAtScreen:4837` · `TryGetUnitScreenRect:4877` · `EnumerateDefenderScreenRects:4861` · `Set/ClearPlacementRange`·`AimGuide`·`SkillAim*`(`:5259`~`:5726`) · `PlayDeploymentPresentation:5731` + 코루틴 2 | 뷰 | **conform**(좌표 변환은 순수) / 프리뷰 페인팅은 그대로 잔류. ⚠ `TryPickNearestEnemy` 의 Entity.Index 동률은 커맨드 구성 측(청사진 ① 예외 ③) |
| 하네스·트레이스 | `BeginHarness:2607` · `StepOneTick:2644` · `EndHarness:2627` · `GetHarnessDigestCounts:2676` · `LegacyTrace.cs`(330줄) | 세션 인프라 | **rewrite** → 신 sim 드라이버가 대체(하네스 게이트·RateManager 소멸). 트레이스 tap 은 A/B 비교기로 이관 |
| 디버그·Dev | `DebugSpawnPatrolAt:6194` · `DebugTryGetPatrolAnchorCell:6210` · `DebugWorldToCell:2237`·`Fractional:2245` · `DebugSpawn{Obstacle,Hazard,BlockingHazard}At` · `DebugRelocateFirstDefender` | 디버그 | **discard** → 선행 머지 3(§4) |

**집계**: sim 규칙 8 · 뷰 4 · 세션 인프라 4(중 1 discard) · 배선 1 · 디버그 1 = 16 클러스터.
등급: conform 1 · adapt 9 · rewrite 4 · discard 2.

**모듈 단위 총계**: 시스템 44 + 채널 27 + Bridge 16 = **87건**(설계 정본의 "~60건" 추정보다 큼 —
채널을 개별 계수했기 때문. 채널 27 을 3그룹으로 접으면 63건으로 추정과 일치).

## 4. 선행 머지 3건 (적출 전에 Bridge 를 가볍게)

| # | 작업 | 현 상태(실측) | 목적 |
|---|---|---|---|
| 1 | 비주얼 statics 분리 | `BattleBridge.cs:260-298` 의 static 프레젠테이션 상수 **21개 실측**(CharacterVisualScale·BlobShadow*·WalkAnim*·Lift* 등)을 뷰 7종이 읽는다 | Bridge 를 sim/뷰로 가르기 전에 뷰-only 표면을 떼어낸다. 청사진 ① §6 "계약 밖" 판정과 일치 |
| 2 | `GetStackThresholds` 의존 역전 | 정의 `BattleBridge.cs:6852`(static, `_stackThresholds` managed Dictionary 조회 — 주석이 "Called by StackModifierTickSystem (managed context — not Burst)" 로 결합을 자체 문서화) ← 호출 `StackModifierTickSystem.cs:90` **1곳**. sim 시스템의 유일한 프로덕션 Bridge 결합(나머지 Battle→Bridge 참조 5개는 디버그 메뉴) | asmdef 의존 방향 불변식(제약 1 후계)의 **마지막 위반**. 역전 방향 = 임계 규칙을 `MatchConfig` 물질화분으로 주입(청사진 ② config-singleton 규칙과 동형) → sim 이 Bridge 를 모른다 |
| 3 | DebugMenu 퇴거 | Battle/ 5파일(`BlockingHazard/Fatigue/Hazard/Obstacle DebugMenu`·`PatrolDebugMenu`) + Bridge 의 `Debug*` 멤버군 | sim 폴더에서 MonoBehaviour 를 제거 → salvage 판정 노이즈 소거 |

Bridge 파셜 실측(2026-08-04): 본체 `BattleBridge.cs` 6,878 + Dreamcatcher 972 + LegacyTrace 330 +
BossLeap 242 + Relocation 216 + UltimateLeap 147 + UnitStats 77 = **8,862줄 / 7파일**.

## 5. 테스트 판정

실측(2026-08-04): 테스트 파일 **262** 중 Entities 참조 **74**, 그중 `new World(` 조립 **38**,
Entities 미참조 **188**.
- 조립 38 → 설계 정본 M1-5 방침대로 "**어서션만 salvage, 골격 재작성**"(rewrite).
- Entities 참조하나 조립 안 함 36 → **adapt**(lookup·EntityQuery 헬퍼를 컬렉션 접근으로).
- 미참조 188 → **conform**. 현 EditMode 1,888건의 절대다수가 여기 있고, **sim lib 이식 후에도
  그대로 통과해야 한다** — 이 188파일이 이식의 실질 안전망이다(제약 10 순수 함수 자산의 배당).
`BattleScaledRateManagerTests` 는 RateManager 소멸과 함께 **discard**(하네스 게이트 테스트는
StepOneTick 이 신 sim 드라이버로 대체될 때 재작성).

## 6. 11+ unit 분해 초안 (이 표에서 도출)

1. **선행 머지 3건**(§4) — 각각 독립 커밋, 행동 변화 0
2. `IMatchSession` 파사드 + `LegacyMatchSessionAdapter`(유일 drain 소유자) — 구 sim 위에서
3. 소비자 재배선(82파일) — 읽기 모델·이벤트 구독으로 전환, 구 sim 유지
4. Bridge 매치 규칙 적출 ①: 웨이브·승패·타이머 → sim 후보 모듈
5. Bridge 매치 규칙 적출 ②: 배치 규칙 + **통화 5종**(청사진 ① §10-2)
6. Bridge 매치 규칙 적출 ③: 드림캐쳐 파셜 + 덱 소유권 + 카드 원자 트랜잭션
7. sim lib 골격(asmdef, UnityEngine 참조 = 컴파일 에러) + conform 유틸 이주
8. 맥락별 이식 4단계(Units → Movement → Effects → Combat 역순 의존 기준, 각 단계 테스트 포팅)
9. pause/slow-mo gameplay 시계 정책(§10-4 — 통화 누적 rate 동반)
10. 커맨드로그 이중 기록 개시
11. A/B parity + 성능 게이트(ARM64 IL2CPP p95/p99·GC) + 스왑 + RTT 150ms 수용 리뷰

각 항목의 완료 기준·순서 의존은 착수 시 개별 unit 문서로 분해한다(지금 쓰면 추측 스펙).
