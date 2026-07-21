# Season Gimmick — "집에 가도 되나요?" (Clock-Out) Spec

**상태**: 완료 2026-07-20, **룰1 재설계 2026-07-21**(unit 8) — 강제 퇴근(10초 타이머) 폐기 → **사망 시 사직서 드랍**. 세 번째 시즌 기믹. 기존 프레임(`BattleConfig.gimmickPool` + `GimmickData`)과 기존 시스템(사망 경로, SkyFall×TileAoe 투사체 메테오, Pickup 아키타입) 위에 조립한다. 인계: [7_handoff_summary.md](7_handoff_summary.md).
**unit 8 재설계 배경**: 배치한 유닛이 10초 뒤 플레이어 의사와 무관하게 사라지는 게 감성상 불합리하다는 평가(사용자). 강제 퇴근·퇴근 코스트 환급·running 타이머 인프라를 전부 걷어내고, defender 가 자연 사망할 때만 사직서를 남기도록 바꿨다. 상세 [8_death_drop_rework.md](8_death_drop_rework.md).

## 목표

시즌 기믹 "집에 가도 되나요?" 를 end-to-end 플레이 가능하게 한다. 두 룰:

1. **사망 → 사직서**(unit 8 재설계): 배치된 아군 유닛이 (원인 불문) **사망**하면 그 배치 타일에 **사직서**를 드랍한다. (강제 퇴근 10초 타이머는 폐기 — 배치 유닛이 강제로 사라지는 감성 문제.)
2. **사직서 5장 → 메테오 3발**: 맵에 사직서가 `resignationThreshold`(5)장 모이면 **5장을 소모**하고, 맵의 이동(Walk) 타일 임의 3곳에 **메테오를 순차 낙하**시킨다(적에게만 피해). (사용자 결정: 5장 소모·재누적 / 적으로만.)

## 검증 질문 (이 spec 이 답해야 할 것)

- ClockOut 기믹 매치에서, 배치 유닛이 **전투 중 사망**하면 그 타일에 사직서가 남는가? (가만히 둔 유닛은 사라지지 않는가?)
- 사직서가 5장 되면 5장이 사라지고 Walk 타일 3곳에 메테오가 순차 낙하해 **적만** 때리는가?
- gimmick=null / 다른 기믹 매치에서 이 시스템들이 완전히 비활성(무변화)인가?
- 결정론: 같은 matchSeed → 같은 메테오 착탄 셀 시퀀스인가?

## 재사용 지도 (신규 최소화)

| 조각 | 재사용 | 신규 |
|---|---|---|
| 기믹 프레임 | `BattleConfig.gimmickPool` · `GimmickData` · `CreateGimmickConfigIfActive` self-gate | `ClockOutGimmickData` · `ClockOutGimmickConfig` |
| 사망 관측 | `DeadTag`(Units) — DamageApplication/HealthDeath 가 부착, UnitLifecycle 이 파괴. 그 사이에서 관측 | `ResignationDropSystem`(Effects, unit 8) |
| 사직서 | `Pickup` 동형 아키타입 + poll-reconcile 뷰 | `Resignation`(Effects) + presenter |
| 메테오 | `BattleBridge.SpawnProjectile(new ProjectileSpawnRequest{ SkyFall×TileAoe … }, Entity.Null)` bridge-cast. **최소 템플릿** = content-1 OnDeath 폭발(BattleBridge, `SpawnProjectile(...,Entity.Null)` 셀 타겟 SkyFall×TileAoe). 보스 메테오(`BossPeriodicTriggerSystem`)는 Combat-carrier 변형. clockout 은 `targetFaction=Enemy`(기본; Defender 는 보스만) | 없음(cast만 재사용) |
| 결정론 셀 선택 | `PickupSpawnState` rng + Walk 후보 수집 전례 | — |

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_gimmick_data_and_config.md` | `ClockOutGimmickData` SO + `ClockOutGimmickConfig` + BattleBridge 주입 branch |
| 1 | `1_resignation_archetype.md` | `Resignation`(Effects) 아키타입 + presenter + BattleBridge reconcile (수동 스폰 뷰 검증) |
| 2 | `2_clockout_and_quit.md` | ~~`ClockOutTimer` 강제 퇴근~~ — **unit 8 로 폐기**(강제 퇴근 제거). 사망 경로 배경 참고용 |
| 3 | `3_resignation_threshold.md` | 사직서 ≥5 → 5장 소모 + `MeteorBarrageRequestsSingleton` enqueue (Effects) |
| 4 | `4_meteor_barrage_cast.md` | BattleBridge drain → 결정론 Walk 셀 3개 → SkyFall×TileAoe 3발 순차 cast (Enemy) |
| 5 | `5_scene_wiring_play_verify.md` | gimmickPool 등록 + 씬 wiring + Play 통합 검증 |
| 6 | `6_clockout_cost_refund.md` | ~~퇴근 코스트 환급~~ — **unit 8 로 폐기**(환급 제거) |
| 7 | `7_handoff_summary.md` | 인계 지도 (unit 0~6 시점) |
| 8 | `8_death_drop_rework.md` | **룰1 재설계**: 강제 퇴근 폐기 → 사망 시 사직서 드랍 (`ResignationDropSystem`) |

## Feature-Wide 계약

- **기믹 소스 = `BattleConfig.gimmickPool`** (기존 프레임). `ClockOutGimmickData : GimmickData`. 활성화 seam = `BattleBridge.CreateGimmickConfigIfActive` → `ClockOutGimmickConfig` 싱글턴 주입. 모든 시스템 `RequireForUpdate<ClockOutGimmickConfig>` self-gate. config 부재 = 완전 비활성(클린 플레이).
- **사직서 드랍 = 사망 시(unit 8 재설계)**. `ResignationDropSystem`(Effects)이 `DeadTag+DefenderUnitTag` defender 를 UnitLifecycle 파괴 직전에 관측(`[UpdateAfter(DamageApplication/HealthDeath)]`, `[UpdateBefore(UnitLifecycle)]`) → `DefenderTile.cell`(RO 읽기)에 사직서 스폰. **강제 퇴근/치명 데미지 없음** — defender 는 정상 전투 사망 시에만 사직서를 남긴다. 사망은 전투 중에만 나므로 running-gate 불필요.
- **사직서 = Effects 신규 아키타입**, 소비형 아님(유닛이 줍지 않음). 전역 임계로만 소모. 맵 위 존재(poll-reconcile 뷰).
- **사직서 임계 = 번아웃 Consume 전례**. 살아있는 사직서 ≥ `resignationThreshold` 시 그 수만큼(5장) destroy + barrage 요청 enqueue 후 재누적.
- **메테오 = 기존 투사체 재사용**. Effects→Bridge 신규 NativeQueue `MeteorBarrageRequestsSingleton`. BattleBridge drain → 결정론 rng 로 Walk 셀 3개(중복 회피) → `SpawnProjectile`(SkyFall×TileAoe, `targetFaction=Enemy`, owner=Null) 3발. "순차"는 `flightTime`(warning) 스태거로 착탄 시차. **Combat 투사체 시스템 코드 불변**(cast 프리미티브만 호출).
- **결정론**: 사직서/메테오 셀 선택은 seed 파생 `Unity.Mathematics.Random` (`MatchSeed.Derive*` 미러). Date/UnityEngine.Random 금지.
- **모든 수치 SO**: `resignationThreshold`(5)/`meteorCount`(3)/메테오 damage·tileRange·warningSec·stagger·ProjectileData ref — 전부 `ClockOutGimmickData`. 하드코딩 금지. (unit 8 로 `clockOutSeconds`/`clockOutCostRefund` 는 제거됨.)
- **맥락 경계**: 사직서 드랍·카운터·barrage요청 = Effects 소유. 사망 관측은 `DeadTag`/`DefenderTile`(Units 소유) **읽기만**. 메테오 cast = BattleBridge(Mono gateway, bridge-cast). **새 ModifierOrigin/StatusFxKind 불필요**(사직서는 월드 오브젝트, 버프 아님).

## 파이프라인 커버리지 — 사직서 (신규 아키타입, Pickup 표 대조)

| 정거장 | 계획 |
|---|---|
| 데이터 SO | `ClockOutGimmickData`(임계·수치·사직서 뷰 프리팹) |
| 스폰 진입점 | `ResignationDropSystem`(Effects, unit 8) — defender 사망 시 ECB 스폰(UnitLifecycle 파괴 직전) |
| ECS 컴포넌트(Effects) | `Resignation { cell }` (수명=전역 임계 소모 시 destroy) |
| 시뮬 시스템 | `ResignationDropSystem`(사망→스폰) · `ResignationThresholdSystem`(임계 소모+barrage 요청) |
| 이벤트 큐 | 신규 `MeteorBarrageRequestsSingleton`(Effects→Bridge). 사직서 뷰는 poll-reconcile(채널 0) |
| View | `ResignationPresenter`(플레이스홀더) + BattleBridge `ReconcileResignationViews` |
| 상태 연출 | N/A — 사직서는 월드 오브젝트(상태FX 아님). 사망=기존 사망 연출 재사용 |

## 파이프라인 커버리지 — 메테오

N/A(재사용) — SkyFall×TileAoe 투사체 파이프라인(projectile-trajectory-payload)을 그대로 소비. 신규 정거장 없음. cast 진입점만 `BattleBridge.SpawnProjectile` 재사용(bridge-cast, owner=Null).

## 비목표

- 사직서를 유닛이 줍는 상호작용(레드불과 달리 소비형 아님).
- 사망 전용 스코어/킬 규칙(기존 사망 경로 재사용).
- 메테오 밸런스 정밀 튜닝(SO 초기값으로 진행, 플레이 후 조정).
- 감정효과·다른 기믹·기믹 로테이션 메타.
- 사직서 정식 아트 / 사망·메테오 전용 VFX(플레이스홀더로 진행).

## 후속 후보

- 사직서/메테오 정식 아트 + VFX.
- 사직서 누적 카운터 UI(5까지 진행 표시).
- 메테오 "순차"를 착탄 스태거 대신 프레임 스케줄러로 강화(경고 텔레그래프 개별 노출).
- effect-trigger-unification 재검토: 이 기믹으로 시즌 기믹이 3종이 됐다(파킹 착수 압력↑). 단 사망-드랍/월드-스폰/barrage 는 trigger→effect rule 프레임 밖(파킹 문서 판정과 동일) — 통합 대상은 아님.
