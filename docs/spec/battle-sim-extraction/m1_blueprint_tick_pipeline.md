# M1 청사진 ③ — 틱 파이프라인 순서도

> unit 9 산출물 · 2026-08-04 · **정본은 order-capture.md 의 44개 유효 총순서다** — 설계 스케치와
> 어긋나는 지점은 전부 캡처를 따랐다(feature-wide 계약 6). 재배치 결정 0 — 순수 박제.
> 이 문서와 다르게 구현하면 골든(LegacyTraceV0 7종)이 깨진다.

## 0. 스텝 합성 (하네스 계약 계승)

```
P0  커맨드 반입(스케줄 tick) → SkillRuntime Tick → Bridge 프레임(시계·웨이브/스폰·전틱 이벤트 drain)
P1~P12  BattleSimGroup 1회 (아래 44 시스템, 캡처 순서)
P13 post-sim 도약 드레인 → 읽기 모델 스탬프
```

신 sim 의 `Sim.Tick` 은 P0 의 Bridge 몫(웨이브 스케줄·스폰 게이트)을 흡수한다(M1-4 규칙 적출).
**Bridge 가 ECS 보다 먼저 도는 라이브 PlayerLoop 순서**가 이 합성의 근거다(unit 2 실측).

## 1. Phase 도표 — 44 시스템 전수 배치

캡처 # 순서 그대로. 스케치와 다른 지점은 굵게 — **투사체(26·27)가 공격(33)보다 앞**,
**DotApply(15)가 이동(17) 앞**, **CC 감쇠(40)는 사망 창 뒤** — 전부 캡처가 정본.

| Phase | # | 시스템 | 핵심 계약 |
|---|---|---|---|
| **P1 필드·존 재구축 + 주기 효과** | 1 | LastRun | 만료 시 최대체력 비율 자해 `IncomingDamage`(source=Null) |
| | 2 | HazardLifetime | 수명 감산 + `HazardSingleton.cellToEffects` **매 틱 재빌드** |
| | 3 | AllyBuffField | 장판×defender 매칭 **매 틱 재발행**(갱신 = 회수) → StatModifierApply(같은 틱 소비) |
| | 4 | BossPeriodicTrigger | 주기 슬롯 tick → SkyFall 캐리어 + 오라 펄스(같은 틱 소비) |
| | 5 | ZoneApply | 존 안 적에게 stat/dot/cc **매 틱 재발행**(전부 같은 틱 소비) |
| | 6 | ObstacleLifetime | `blockedCells` 매 틱 재빌드 + 만료 |
| | 7 | DefenderField | 보스 유도용 multi-source BFS 매 틱 재빌드 |
| **P2 큐 반입(어그로·모디파이어·CC)** | 8 | AggroState | **전 틱** AggroHit 드레인(구조적 1틱 지연 — §2) → capacity 게이트 → `Aggroed` |
| | 9 | ModifierApply | Stat/Stack 큐 드레인 → 슬롯 병합(`(source,stat,op,stackId)`, refresh=max) + Dirty |
| | 10 | CcApply | EnemyCc 드레인 → `CcEffectMerge`. **보스 CC 면역 게이트 단일 지점** |
| **P3 사망 보완·자폭·전투 준비** | 11 | HealthDeath | 피해 외 유래 HP≤0 → DeadTag(안전망 — DamageApplication 밖 쓰기) |
| | 12 | LethalTimer | 카미카제 만료 → DeadTag |
| | 13 | TauntAttackGrant | `Aggroed`(RO) → taunt 공격 부여/회수 |
| | 14 | EnemyAiState | 적 FSM 전이 — `EnemyAiState` 유일 writer |
| | 15 | DotApply | DoT 부여 드레인 → **틱 지급(`IncomingDamage` — 같은 틱 #34 소비, unit 0 핀)** → 감쇠 |
| | 16 | PatrolField | 순찰 이동 방향 굽기(Movement 가 RO 소비) |
| **P4 이동** | 17 | Movement | 위치 갱신 단일 권한 — 하강·포탈·`PastGoalTag` |
| **P5 이동-후 캐스트·기믹·픽업** | 18 | HazardCast | 캐스트 성사 → HazardSpawnRequest + **Cast(같은 틱 #33 소비 — 명시 핀)** |
| | 19 | ShieldCast | 주기 실드 — `IncomingShield` append 만(병합은 #34) |
| | 20 | ResignationThreshold | 사직서 임계 → 소모 + MeteorBarrageRequest |
| | 21 | HeatAccrual | 온천 열기 → `IncomingHeal`/`IncomingDamage`(같은 틱 #34 소비 핀) |
| | 22 | PickupSpawn | 레드불 스폰 — **`PickupSpawnState.rng` 소비+write-back**(§4) |
| | 23 | PickupConsume | 동일 셀 소비 → 라스트런 부여 |
| | 24 | HitFlash | 자기 Scale 펄스(뷰성 상태 — salvage 판정 대상) |
| | 25 | EffectTick | 캐리어(토네이도/포탈/버프장판) TTL + 만료 제거 |
| **P6 투사체 (공격보다 앞)** | 26 | ProjectileMove | 궤적 전진 + `impactReached`(이동 직후 최신 위치 — unit 0 핀) |
| | 27 | ProjectileHit | 착탄 디스패치 → `IncomingDamage`(같은 틱 #34 소비 핀) + ThreatHit(같은 틱) |
| **P7 모디파이어 tick·집계** | 28 | FatigueAccrual | 피로도 스택 적재(다음 틱 반영 — 박제된 지연) |
| | 29 | StatModifierTick | 슬롯 remaining 감산·만료 + Dirty |
| | 30 | ModifierStatsAggregate | `ModifierStats` **유일 writer** — `(base+Σadd)·Πmul`, dirty-only |
| | 31 | MaxHealthScale | `maxHealthMul`(RO) → `Health.max`(Units 소유 쓰기) |
| | 32 | StackModifierTick | 스택 tick + **엣지 임계 교차 → 파생 효과**(EnemyCc/DotApply/Stat — 다음 틱 반영) |
| **P8 공격** | 33 | Attack | 통합 공격자 루프 — 타겟팅(simId 동률)·출력 해결·발사(**RNG 파생** §4)·CC. Cast 같은-틱 드레인 |
| **P9 피해 정산** | 34 | DamageApplication | Damage/Heal/Shield 드레인 → Health · **DeadTag 마킹(피해 유래)** · CcClear(같은 틱) · 파생 이벤트 |
| **P10 사망 창 관찰** | 35 | ResignationDrop | "죽었지만 아직 있는" 창에서 사직서 스폰(defender 당 정확 1회) |
| | 36 | PatrolLifecycle | 소환사 사망 3중 판정 → 순찰병 DeadTag 전파 |
| | 37 | CcClear | wake-on-hit 소비(당 틱 wake) |
| **P11 발사 명세·후처리** | 38 | ProjectileEmitter | `EmitterInstance` tick → 스폰 요청 캐리어(발사 시각·간격의 소유자) |
| | 39 | DreamCocoon | 잠 완주 감시(파탄→감산→완주 순 — 코드 내 순서 계약) |
| | 40 | CcDecay | **CC 감쇠(이동 후)** — `remainingTime` 감산·만료 |
| **P12 파괴·임계·도약** | 41 | UnitLifecycle | **유일한 파괴자** — 4루프(goal/defender/hazard/general), 파괴 전 이벤트 베이크 |
| | 42 | HealthThreshold | ThreatHit 드레인 + 임계 평가 → last_stand/SelfBlink(BlinkRequest) |
| | 43 | UltimateLeap | 궁 도약 3단(이탈→예고→강습) — 예고 시간은 sim 소유 |
| | 44 | BlinkApply | BlinkRequest 드레인(같은 틱) → 위치 대입(Movement 소유 소비자) |

대조: 44/44 배치, 빠짐 0(#1~#44 = order-capture 와 1:1).

## 2. 내부 9채널 — 생산→소비 화살표 (전수 26쌍)

채널당 sim 내 소비자는 **정확히 1개**(fan-in 만 존재). 같은 틱 12쌍 · 1틱 지연 14쌍 —
**지연은 버그가 아니라 박제된 계약이다**(unit 0). 신 sim 은 이 지연을 그대로 재현해야 한다.

| 채널 | 소비자(#) | 같은 틱 생산자 | 1틱 지연 생산자 |
|---|---|---|---|
| AggroHit | AggroState(8) | — | Attack(33) — **구조적 영구 지연**(소비자가 생산자보다 앞. 선언 없음 — 구조가 보장) |
| Cast | Attack(33) | HazardCast(18) — 명시 핀 | — |
| ThreatHit | HealthThreshold(42) | ProjectileHit(27)·Attack(33) — `ThreatTable.TryCredit` 경유(직접 Enqueue 아님) | — |
| BlinkRequest | BlinkApply(44) | HealthThreshold(42)·UltimateLeap(43) | — |
| EnemyCc | CcApply(10) | ZoneApply(5) | ProjectileHit(27)·StackModifierTick(32)·Attack(33) — **같은 채널, 생산자별 반응 지연 상이** |
| DotApply | DotApply(15) | ZoneApply(5) (+Bridge OnPlace) | StackModifierTick(32) |
| CcClear | CcClear(37) | DamageApplication(34) — 명시 핀 | — |
| StatModifierApply | ModifierApply(9) | AllyBuffField(3)·BossPeriodic(4)·ZoneApply(5) | Pickup(23)·ProjHit(27)·StackTick(32)·Attack(33)·DamageApp(34)·Cocoon(39)·HealthThresh(42) — unit 0 박제 |
| StackModifierApply | ModifierApply(9) | — | ProjHit(27)·Fatigue(28)·Attack(33) — 전부 지연 |

주의: ThreatHit 누적값(`ThreatEntry`)의 하류 소비자는 현재 0(boss-jjangssen unit 4 가 blink 정책
교체) — salvage 판정표(unit 10)의 discard 후보 입력.

## 3. 사망 4단계 릴레이 — 형태 보존 필수 (§3 계약)

마킹(#34 피해 유래 · #11 보완)은 파괴하지 않고, 파괴(#41)는 마킹하지 않는다. 그 사이 #35·#36 이
**"죽었지만 아직 있는" 1틱 창**에서 파괴 전 정보(배치 타일·소환 링크)를 읽는다 — 3중 핀
(`After(DamageApplication)·After(HealthDeath)·Before(UnitLifecycle)`)이 창을 강제. 즉시 삭제로
단순화하면 사직서 드랍·순찰병 전파·DefenderDeath 이벤트 베이크(파괴 전 OnDeathAoe 슬롯 캡처)가
전부 깨진다. 파괴 루프 4개의 상호 배제(`WithNone<DefenderTile, BlockingHazard>`)도 계약.

## 4. RNG 지도

sim 내 보유 상태 2개 — 둘 다 컴포넌트 필드이고 **write-back 이 결정론 조건**:
- `PickupSpawnState.rng` ← `DerivePickupSeed(matchSeed)` — PickupSpawn(22) 소비 후 되쓰기
- `BombLauncherState.rng` ← `max(1, DeriveBombSeed(matchSeed) ^ cellHash)` — **캐스터별 상이**, Attack(33) 소비 후 되쓰기

상태 없는 파생 1개: `PatternShotRandomizer` — seed = `hash(int2(simId, fireCountBase))`.
**MatchSeed 계보가 아니다** — matchSeed 를 바꿔도 같은 유닛의 같은 발사 번호는 같은 패턴
(unit 1 의 의도된 축). 신 sim 이식 시 xorshift 상수 계승(System.Random 치환 금지 — §3).
`MatchSeed` salt 7종 중 sim 이 쓰는 것은 Pickup·Bomb 2개(Meteor 는 Bridge → 규칙 적출 시 sim 편입).

## 5. ECB → "루프 중 기록, 루프 후 적용"

44개 전수 실측: ECB 사용 28개 시스템, **전부 `Allocator.Temp` + 같은 OnUpdate 내 Playback**
(시스템 ECB·지연 재생 0). 신 sim 번역: 각 phase 함수는 구조 변경을 로컬 버퍼에 기록하고 루프
종료 후 적용한다(동일 엔티티 2연산 함정 — ModifierApplySystem 선례). "루프 중 즉시 적용"으로
바꾸면 순회 중 컬렉션 변경 계열 버그가 재현된다.

## 6. 동률 예외·병합 정책 (이식 계약 승격)

unit 4 가 로그 대상으로 두던 것을 신 sim 의 명시 규칙으로:
- **동률 5지점**: KillAttribution 등량(버퍼 적재 순서 — 코드 주석 계약) · Aggro capacity FIFO(실질
  결정자 = Attack 의 후보 순회 순서) · Cc/Stat merge 동키 · Dot merge 동키 · HazardSingleton 셀
  순회(미규정이나 재현됨). HazardCast 최근접은 unit 1 이 simId 로 해소.
- **병합 duration 정책은 경로마다 다르다 — 통일하지 말 것**: 값 축 LWW · 지속 축 max ·
  tickTimer carry-over(주기 변경 시 진행률 환산), 예외로 ApplyStack 의 `remaining` 만 무조건
  덮어쓰기. 이 비대칭 자체가 박제 대상.
- 매 틱 재발행 계열(#3·#5)의 "갱신이 곧 회수" 시맨틱: TTL 은 짧고 발행이 유지를 대신한다 —
  장판 이탈 = 자연 만료. 이식 시 이벤트화하면 회수 이벤트가 필요해지는 함정.

## 7. 재배치 결정

**없음.** 캡처 순서를 그대로 phase 로 접었다. 유일하게 기록할 완화 후보(예: EnemyCc 지연 혼재
정규화)는 침묵 변경 금지 원칙에 따라 **M1 스왑 후** 별도 명시 변경으로만 다룬다.
