# Season Gimmick — "집에 가도 되나요?" (Clock-Out) Spec

**상태**: 완료 2026-07-20 — units 0~6 + 메테오 데미지 150. Play 통합검증 사용자 통과(퇴근→사직서→메테오 + 코스트 환급). 인계: [7_handoff_summary.md](7_handoff_summary.md). 세 번째 시즌 기믹. 기존 프레임(`BattleConfig.gimmickPool` + `GimmickData`)과 기존 시스템(사망 경로, SkyFall×TileAoe 투사체 메테오, Pickup 아키타입, FatigueAccrual lazy-attach) 위에 조립한다.
**재리뷰 2026-07-20**: 현 HEAD(투사체/공격 리워크 병합 후) 기준 재검증 — units 0~3 병합 후 compile 클린(CS 에러 0), 메테오 cast(unit 4) 계획 유지. 리워크는 **가법적**이라 SkyFall×TileAoe·death 경로·IncomingDamage 불변(DirectionalLinear/PathHit 는 별도 trajectory/payload 축).

## 목표

시즌 기믹 "집에 가도 되나요?" 를 end-to-end 플레이 가능하게 한다. 두 룰:

1. **퇴근 → 사직서**: 전투 시작 후 배치된 아군 유닛은 `clockOutSeconds`(10s)가 지나면 배치 타일에 **사직서**를 스폰하고 **퇴근(사망)** 한다. (사용자 결정: 기존 defender 사망 경로 그대로 재사용 / 타이머는 running 후에만.)
2. **사직서 5장 → 메테오 3발**: 맵에 사직서가 `resignationThreshold`(5)장 모이면 **5장을 소모**하고, 맵의 이동(Walk) 타일 임의 3곳에 **메테오를 순차 낙하**시킨다(적에게만 피해). (사용자 결정: 5장 소모·재누적 / 적으로만.)

## 검증 질문 (이 spec 이 답해야 할 것)

- ClockOut 기믹 매치에서, 전투 시작 후 배치 유닛이 10초에 퇴근하고 그 타일에 사직서가 남는가?
- 사직서가 5장 되면 5장이 사라지고 Walk 타일 3곳에 메테오가 순차 낙하해 **적만** 때리는가?
- gimmick=null / 다른 기믹 매치에서 이 시스템들이 완전히 비활성(무변화)인가?
- 결정론: 같은 matchSeed → 같은 메테오 착탄 셀 시퀀스인가?

## 재사용 지도 (신규 최소화)

| 조각 | 재사용 | 신규 |
|---|---|---|
| 기믹 프레임 | `BattleConfig.gimmickPool` · `GimmickData` · `CreateGimmickConfigIfActive` self-gate | `ClockOutGimmickData` · `ClockOutGimmickConfig` |
| 배치 타이머 | `FatigueAccrualSystem` lazy-attach 패턴 | `ClockOutTimer`(Effects) |
| 퇴근(사망) | `IncomingDamage`(Effects→Units) → DamageApplication → DeadTag → UnitLifecycle → `DefenderDeathEvent` (LastRun crash 전례) | — |
| 사직서 | `Pickup` 동형 아키타입 + poll-reconcile 뷰 | `Resignation`(Effects) + presenter |
| 메테오 | `BattleBridge.SpawnProjectile(new ProjectileSpawnRequest{ SkyFall×TileAoe … }, Entity.Null)` bridge-cast. **최소 템플릿** = content-1 OnDeath 폭발(BattleBridge, `SpawnProjectile(...,Entity.Null)` 셀 타겟 SkyFall×TileAoe). 보스 메테오(`BossPeriodicTriggerSystem`)는 Combat-carrier 변형. clockout 은 `targetFaction=Enemy`(기본; Defender 는 보스만) | 없음(cast만 재사용) |
| 결정론 셀 선택 | `PickupSpawnState` rng + Walk 후보 수집 전례 | — |

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_gimmick_data_and_config.md` | `ClockOutGimmickData` SO + `ClockOutGimmickConfig` + BattleBridge 주입 branch |
| 1 | `1_resignation_archetype.md` | `Resignation`(Effects) 아키타입 + presenter + BattleBridge reconcile (수동 스폰 뷰 검증) |
| 2 | `2_clockout_and_quit.md` | `ClockOutTimer` running-defender lazy-attach → 만료 시 사직서 스폰 + 치명 IncomingDamage 퇴근 |
| 3 | `3_resignation_threshold.md` | 사직서 ≥5 → 5장 소모 + `MeteorBarrageRequestsSingleton` enqueue (Effects) |
| 4 | `4_meteor_barrage_cast.md` | BattleBridge drain → 결정론 Walk 셀 3개 → SkyFall×TileAoe 3발 순차 cast (Enemy) |
| 5 | `5_scene_wiring_play_verify.md` | gimmickPool 등록 + 씬 wiring + Play 통합 검증(검증 질문 4개) |
| 6 | `6_clockout_cost_refund.md` | (추가) 퇴근 1회당 코스트 환급 — 기존 `CostRuntime.AddCost` 패스 |
| 7 | `7_handoff_summary.md` | 인계 지도 |

## Feature-Wide 계약

- **기믹 소스 = `BattleConfig.gimmickPool`** (기존 프레임). `ClockOutGimmickData : GimmickData`. 활성화 seam = `BattleBridge.CreateGimmickConfigIfActive` → `ClockOutGimmickConfig` 싱글턴 주입. 모든 시스템 `RequireForUpdate<ClockOutGimmickConfig>` self-gate. config 부재 = 완전 비활성(클린 플레이).
- **퇴근 = running-only, 기존 사망 경로 재사용**. `ClockOutTimer`(Effects)를 running defender 에 lazy-attach(FatigueAccrual 패턴, 배치 페이즈 미가동). 만료 시 Effects 시스템이 (a) `DefenderTile.cell`(읽기)에 사직서 스폰, (b) 치명 `IncomingDamage`(대량, source=Null 킬 미귀속) append. Health 쓰기·사망은 Units 소유 그대로.
- **사직서 = Effects 신규 아키타입**, 소비형 아님(유닛이 줍지 않음). 전역 임계로만 소모. 맵 위 존재(poll-reconcile 뷰).
- **사직서 임계 = 번아웃 Consume 전례**. 살아있는 사직서 ≥ `resignationThreshold` 시 그 수만큼(5장) destroy + barrage 요청 enqueue 후 재누적.
- **메테오 = 기존 투사체 재사용**. Effects→Bridge 신규 NativeQueue `MeteorBarrageRequestsSingleton`. BattleBridge drain → 결정론 rng 로 Walk 셀 3개(중복 회피) → `SpawnProjectile`(SkyFall×TileAoe, `targetFaction=Enemy`, owner=Null) 3발. "순차"는 `flightTime`(warning) 스태거로 착탄 시차. **Combat 투사체 시스템 코드 불변**(cast 프리미티브만 호출).
- **결정론**: 사직서/메테오 셀 선택은 seed 파생 `Unity.Mathematics.Random` (`MatchSeed.Derive*` 미러). Date/UnityEngine.Random 금지.
- **모든 수치 SO**: `clockOutSeconds`(10)/`resignationThreshold`(5)/`meteorCount`(3)/메테오 damage·tileRange·warningSec·stagger·ProjectileData ref — 전부 `ClockOutGimmickData`. 하드코딩 금지(퇴근 치명 데미지 sentinel 대량값만 예외, 주석 명시).
- **맥락 경계**: 타이머·사직서·카운터·barrage요청 = Effects 소유. 사망 = IncomingDamage(Effects→Units). 메테오 cast = BattleBridge(Mono gateway, bridge-cast). **새 ModifierOrigin/StatusFxKind 불필요**(퇴근=사망, 버프 아님).

## 파이프라인 커버리지 — 사직서 (신규 아키타입, Pickup 표 대조)

| 정거장 | 계획 |
|---|---|
| 데이터 SO | `ClockOutGimmickData`(임계·수치·사직서 뷰 프리팹) |
| 스폰 진입점 | `ClockOutSystem`(Effects) — 퇴근 타이머 만료 시 ECB 스폰 |
| ECS 컴포넌트(Effects) | `Resignation { cell }` (수명=전역 임계 소모 시 destroy) |
| 시뮬 시스템 | `ClockOutSystem`(타이머+스폰+퇴근) · `ResignationThresholdSystem`(임계 소모+barrage 요청) |
| 이벤트 큐 | 신규 `MeteorBarrageRequestsSingleton`(Effects→Bridge). 사직서 뷰는 poll-reconcile(채널 0) |
| View | `ResignationPresenter`(플레이스홀더) + BattleBridge `ReconcileResignationViews` |
| 상태 연출 | N/A — 사직서는 월드 오브젝트(상태FX 아님). 퇴근=기존 사망 연출 재사용 |

## 파이프라인 커버리지 — 메테오

N/A(재사용) — SkyFall×TileAoe 투사체 파이프라인(projectile-trajectory-payload)을 그대로 소비. 신규 정거장 없음. cast 진입점만 `BattleBridge.SpawnProjectile` 재사용(bridge-cast, owner=Null).

## 비목표

- 사직서를 유닛이 줍는 상호작용(레드불과 달리 소비형 아님).
- 퇴근 전용 스코어/킬 규칙(기존 사망 재사용 — 사용자 결정).
- 메테오 밸런스 정밀 튜닝(SO 초기값으로 진행, 플레이 후 조정).
- 감정효과·다른 기믹·기믹 로테이션 메타.
- 사직서 정식 아트 / 퇴근·메테오 전용 VFX(플레이스홀더로 진행).

## 후속 후보

- 사직서/퇴근/메테오 정식 아트 + VFX.
- 사직서 누적 카운터 UI(5까지 진행 표시).
- 메테오 "순차"를 착탄 스태거 대신 프레임 스케줄러로 강화(경고 텔레그래프 개별 노출).
- effect-trigger-unification 재검토: 이 기믹으로 시즌 기믹이 3종이 됐다(파킹 착수 압력↑). 단 퇴근-타이머/월드-스폰/barrage 는 trigger→effect rule 프레임 밖(파킹 문서 판정과 동일) — 통합 대상은 아님.
