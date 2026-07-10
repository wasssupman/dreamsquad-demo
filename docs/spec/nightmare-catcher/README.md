# Nightmare Catcher — 보스/적 능동 스킬 (드림캐쳐 프레임워크 편입)

> 상태: **완료 2026-07-10** (units 0~6 전량 구현·커밋·Play e2e 확인 + rev 3 실플레이 피드백. 인계: `7_handoff_summary.md`)
> 이력: rev 2 = `dreamcatcher-awakening-hand` 개편 정합 패스. rev 3 = 실플레이 피드백(blink 연출·VFX 렌더 함정·튜닝) + 범위 밖 발견(보스 누수 → `enemy-hunter-targeting` 분리).
>
> 선행 토대: `docs/spec/dreamcatcher-unit-trigger/` (trigger×payload 프레임워크), `docs/spec/dreamcatcher-content-1/` (트리거/페이로드 확장 선례), `docs/spec/dreamcatcher-awakening-hand/` (사용 방식 — 각성치·손패·Active 카드).

## 목표

"나이트매어캐쳐"는 **드림캐쳐의 서사적 대응물**이다 — 방어유닛에 붙는 드림캐쳐처럼, 적/보스에 붙어 능동 스킬로 발현된다. 구현상 별도 시스템이 아니라 **기존 드림캐쳐 `trigger × payload` 프레임워크에 편입**한다.

핵심 통찰: 드림캐쳐는 이미 진영·아키텍처 중립으로 설계됐다(`DcMechanic.cs` = Entities 무참조, `DcTriggerSlot` = 임의 엔티티 버퍼). 막혀 있는 건 **해석 계층의 `DefenderUnitTag` 게이트 5곳뿐**. "드림캐쳐는 무엇에든 붙는다 — 나무에도 붙되 스탯형은 조용히 no-op" 이 개념을 코드로 embodiment 한다.

이 spec 의 MVP 검증 대상은 새로 만드는 **유일한 두 세만틱**이다:

- **융단폭격** = `PeriodicTimer(10s)` × `AreaBarrage(반경 3, 10 데미지)` — 주기 트리거 (신규)
- **텔레포트** = `HealthThreshold(-10%마다)` × `SelfBlink(위협 리더 근처)` — 임계치 트리거 + 위치 페이로드 (신규)

기본공격(100)·채찍질(오라)은 기존 프리미티브 조합이라 **후속 후보**로 분리(스코프 엄수).

## 검증 질문

> 보스에 부착된 나이트매어캐쳐가 **10초 주기로** 임의 방어유닛 중심 3타일 AoE 폭격을 발사하는가? 최대체력이 **누적 10% 감소할 때마다** 자신에게 가장 많은 데미지를 입힌 방어유닛 근처로 순간이동하는가? 이 둘이 **기본 이동/공격과 동시에(직교)** 굴러가는가? 그리고 방어유닛의 기존 드림캐쳐/스탯 카드 경로는 **무회귀**인가?

## rev 2 정합 — awakening-hand 개편 (2026-07-10)

이 spec 은 `dreamcatcher-awakening-hand`(각성치+CR 순환 손패, 3중1/SkillBar 폐지) 병합 **이전**(888d420d)에 작성됐다. 개편 diff(`888d420d..HEAD`) 대조 결과:

- **로직 계약(units 0~3) 무손상.** 개편은 Mono/UI 계층(손패·게이지·bridge 부착 API)만 변경 — AttackSystem/ProjectileHitSystem/MovementSystem/`DcMechanic`/`DcTriggerSlot`/투사체 struct 전부 무변경(diff 0줄). enum append 좌표 유효.
- **무회귀 대상 표현 갱신**: 플레이어 Meteor 는 이제 SkillBar(dormant)가 아니라 **Active 카드**(`CastSkillAtTile`, `skillRuntime` 배선 해제 상태)로 캐스트된다. owner=Null 가드(N2)·진영 플래그 기본값=enemy(N3)는 그대로 유효 — 단 Active 카드로 Meteor 사용 빈도가 늘어 이 무회귀 표면을 더 자주 밟는다(unit 6 e2e 갱신).
- **각성 경제 자동 편입**: 적 스폰이 `AwakeningReward` 를 무조건 베이크하므로 **보스 처치도 각성치를 준다**(`AttackUnitData.awakeningReward` — 보스 값은 unit 6 authoring 결정). 역방향: **폭격이 방어유닛을 죽이면** 기존 사망 드레인이 각성 +4 와 부착 카드 회수(호스트 사망 → 큐 맨 뒤)를 자동 구동 — 신규 코드 0, unit 6 e2e 관찰 항목.
- **슬로모 상호작용**: 손패 열림 동안 Battle 도메인 0.3x — PeriodicTimer accumulator·SkyFall 텔레그래프도 시뮬 전체와 함께 감속(도메인 dt 설계의 의도된 결과 — "위기에 손패 열고 폭격을 늦춘다"는 정상 플레이).
- **배선 앵커 드리프트**: BattleBridge +161줄 등으로 라인 이동(스폰 베이크 :4022→:4166 등) — units 4~6 앵커 갱신됨(rev 2).

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_definition_layer.md` | 계약 | enum 확장 — `PeriodicTimer`/`HealthThreshold` 트리거 + `AreaBarrage`/`SelfBlink` 페이로드 + `DcTriggerSpec` 필드 (ECS 무참조, 컴파일만) |
| 1 | `1_boss_threat_table.md` | 계약 | 보스 전용 위협 테이블 — `(공격자, 누적피해)` 버퍼 + 위협 리더 조회 + **투사체 owner 귀속**(원거리 반영). 텔레포트 타겟 소스 |
| 2 | `2_barrage_mechanic.md` | 계약(로직) | 융단폭격 완결 정의 — 주기 accumulator + 결정론 진앙 선택 + SkyFall×TileAoe 재사용 |
| 3 | `3_teleport_mechanic.md` | 계약(로직) | 텔레포트 완결 정의 — 임계치 accumulator(반복·래치) + SelfBlink(Combat→Movement seam) |
| 4 | `4_faction_neutral_gates.md` | 배선 | BossTag + 신규 arm 태생적 중립(slot/threat 존재 게이트) + AreaBarrage 페이로드 진영 파라미터화. 기존 arm 게이트 개방은 지연(MVP 신규 트리거라 불요) |
| 5 | `5_enemy_bake_hook.md` | 배선 | 적/보스 스폰 시 BossTag+ThreatTable+DcTriggerSlot 베이크(병렬 경로) + arm 시스템 3개 등록 (`TauntAttackGrantSystem` 선례) |
| 6 | `6_boss_play_validation.md` | 검증 | 보스 authoring + Play e2e(주기 폭격·임계치 텔레포트·직교성·무회귀) + 렌즈 B + 파이프라인 커버리지 |

> **문서 작성 순서 ≠ 코드 커밋 순서.** 0~3(로직 정의)을 먼저 써서 렌즈 A 크리틱에 건다. 4~6(아키텍처 배선)은 로직 검증 후 작성하고, 게이트 회귀는 렌즈 B 로 유닛 4 코드 이후 별도 리뷰.

## Feature-wide 계약 (load-bearing)

1. **드림캐쳐 = 진영 무관 서사, 별도 시스템 아님.** 나이트매어캐쳐는 새 트리거/페이로드 enum + arm 으로 편입한다. 적 전용 병렬 시스템(트리거 arm 복제) 금지 — sync-debt 재생산(`EnemyAiStateSystem.HasFireTarget` 미러링 전례).
2. **2계층 불변.** 정의 계층(`DcMechanic.cs`)은 Entities/Battle 타입 무참조 유지. 새 enum 은 **끝에 append**(기존 카드 int 직렬화 보존).
3. **게이트는 완화, 라이프사이클은 병렬.** 발동 arm/부착 API 게이트를 `isDefender`→`hasSlot` 로 **완화**(strict superset — 슬롯은 명시 베이크로만 생기므로 defender 불변). 부착·정리 라이프사이클(`_activeDcEffects` 상속, `DestroyEntitiesByType<DefenderUnitTag>` teardown)은 **손대지 않고** 적 스폰에 별도 베이크 훅.
4. **복합 행동 = 직교 슬롯의 합, FSM 상태 증식 아님.** 폭격·텔레포트·기본공격은 각자 독립 accumulator/슬롯으로 틱한다. `AiState`(Marching/Engaging/Chasing/Standoff)는 이동/교전 게이트로만 유지. 스킬이 이동을 하드 중단하지 않으면 새 상태 0. (텔레포트=순간, 폭격=백그라운드 → FSM 무변경.)
5. **위협 테이블은 보스 전용.** 일반 적은 기존 nearest/aggro 정책 유지. `(공격자,누적피해)` 버퍼는 보스 엔티티에만. 귀속은 근접+원거리 모두 — 투사체에 owner 필드 추가로 원거리 반영(사용자 결정 2026-07-10). 이 게임은 방어유닛 대부분이 원거리라 원거리 미반영 시 텔레포트가 상시 '최근접' 폴백으로 붕괴(렌즈 A HIGH-2).
6. **스탯형 no-op degradation.** 스탯% 페이로드가 해당 스탯 컴포넌트 없는 대상(예: 나무)에 부착되면 조용히 아무것도 안 하고 끝난다(throw/warn 아님). 부착은 성립, 효과만 부재.
7. **결정론.** "임의 진앙" 등 분산은 seeded RNG 아닌 index 기반 결정론(round-robin/누적 카운터). `docs/reference/lessons/` 시뮬 결정론 원칙.
8. **페이로드 = 기존 프리미티브 재사용 우선, 단 "공짜 재사용" 아님.** 융단폭격=SkyFall×TileAoe(플레이어 Meteor 파이프라인) 재사용이나, 착탄 피해 풀이 적 진영(`AttackUnitTag`) 고정이라 **진영축 1개 추가**가 필요(verbatim 아님, 렌즈 A HIGH-1). SelfBlink 는 완전 신규 세만틱(위치 텔레포트 = Movement 소유 → Combat→Movement 이벤트 seam, 직접 position 쓰기 금지).
9. **degenerate 파라미터 가드는 정의 계층 계약.** `periodSeconds<=0`/`fraction<=0` 이면 트리거 순수함수가 **발동 안 함**(kind 디스패치가 아니라 함수 내부 가드). 안 하면 새 카드 값 누락 시 매 틱 스핀-발동(렌즈 A HIGH/MED-3).

## 파이프라인 커버리지

> 대조 대상: `docs/reference/object-pipeline-map.md` §투사체(융단폭격) + §적유닛(보스). **아키타입별 정거장 대조표는 `6_boss_play_validation.md` §파이프라인 커버리지에 확정.** 신규 채널 +2(`ThreatHitEventsSingleton`·`BlinkRequestEventsSingleton`), 신규 SO 타입 0, teardown 은 `AttackUnitTag` 적 경로 상속.

## 후속 후보 (스코프 밖)

- **(렌즈 B M1 잔여, 2026-07-10)** `ProjectileHitSystem` 의 기존 `StatModifierApplyEventsSingleton`/`StackModifierApplyEventsSingleton` 접근을 `TryGetSingleton`→`TryGetSingletonRW` 로 정렬 — 큐 변이 싱글턴의 RO 접근은 동작하나(참조 시맨틱) 의도 표기가 틀림. 이번 spec 은 신규 threat 싱글턴만 정렬(기존 코드 무접촉 원칙).
- **(최종 렌즈 B M1, 2026-07-10)** 투사체발 위협/데미지의 1프레임 지연 가능성 — `ProjectileHitSystem` 과 `DamageApplicationSystem`/`BossHealthThresholdSystem` 간 명시 순서 제약 없음(기존 성질, 60fps 비가시). 투사체 데미지만으로 경계를 넘는 프레임에서 blink 가 1프레임 늦을 수 있음. 문제화되면 `DamageApplicationSystem` 에 `[UpdateAfter(ProjectileHitSystem)]` 1줄.
- **라이브 웨이브 보스 편성 규칙** — 이번 e2e 는 테스트 플랜(WavePlan_BossTest) 배선. 생성형 웨이브(WaveA 풀)에 보스를 넣으면 무작위 다수 등장하므로, "N웨이브째 보스 1기" 같은 편성 규칙은 밸런스/product 결정과 함께 별도 작업.
- **보스 전용 아트/연출** — 현재 Tanker 외형 재사용(스케일 2.1) + Meteor 낙하 비주얼. 보스 실루엣·폭격 전용 VFX·blink 연출 고도화는 후속.
- **(rev 3 발견) GA ProjectileData 들의 `hitPrefab` 이 머즐(Muzzle) 프리팹을 가리킴** — `Projectile_ExplosiveBullet_GA`·`Projectile_RotatingSpheres03_GA` 등에서 확인(머즐 = 빔 2개 0.35초, 사실상 무연출). 인게임 히트 이펙트가 전반적으로 빈약했다면 이것. 실물은 `GA/Prefabs/Hits/vfx_Hit_*` — 전수 재배정은 별도 데이터 정비 작업.
- **폭격 피격 체감** — defender 피격은 데미지 팝업이 없는 기존 사양(DamageNumber enemy 전용 게이트). 폭격 맞는 순간의 체감 연출(팝업 진영 개방 or 피격 플래시)은 후속(실플레이 피드백 2026-07-10).

- **기본공격 100 / 채찍질(3타일 아군 이동속도 오라)** — 기존 프리미티브(`AttackOutput` / MoveSpeedMul Aura) 조합. 게이트 개방 후 데이터로 붙음. Aura 는 `modifier-framework-and-healer` 후속의 Aura defender 와 producer 공유.
- **게이트 완전 일반화(공통부 추출)** — 적 경로가 실제로 돌기 시작한 뒤, defender/enemy 공통 부착·정리 라이프사이클을 추출. 두 번째 사용처 확정 후.
- **보스 페이즈/캐스팅 상태** — 스킬이 이동을 하드 중단해야 할 때만 `AiState.Casting` 1개 추가. 현 MVP 는 불필요.
- **보스 어그로 저항/면역** — 현재 모든 적이 동일하게 `Aggroed` 대상(보스도 끌림 — 실플레이 확인 2026-07-10, **사용자 확정: 면역 시스템 후속 추가**). `BossTag` 가 이미 있어 어그로 부착 지점 게이트 1줄로 구현 가능.
- **위협 감쇠/타임아웃** — 누적피해 threat 의 시간 감쇠, off-target 해제 정책.
- **나이트매어캐쳐 authoring/부착 UX** — 적 데이터에서 스킬 선언 → 스폰 시 자동 베이크. 인게임 선택 UI 아님(드림캐쳐 UX 와 대칭이나 별도).
