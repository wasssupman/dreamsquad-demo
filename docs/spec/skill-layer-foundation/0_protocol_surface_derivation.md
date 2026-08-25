# 0 — 프로토콜 표면 도출

## 목적

`ISkillContext` 의 동사를 **현존 arm 전수에서 뽑는다.** 정의하지 않는다.
`skill-fire-dispatch` rev 3 이 조사를 미루고 추정을 확정문으로 써서 착수 불가 판정을 받았다.
이 unit 은 **코드를 0줄 쓴다** — 산출물은 표뿐이다.

## 변경 대상

문서만: 이 파일에 산출표를 채운다. 코드·에셋 무변경.

읽을 arm (**3어휘**):

| 어휘 | 위치 |
|---|---|
| `DcPayloadKind` (26종) | `Scripts/Bridge/BattleBridge.cs`(28) · `BattleBridge.Dreamcatcher.cs`(18) · `Battle/Combat/AttackSystem.cs`(10) · `Battle/Units/DamageApplicationSystem.cs`(7) · `Battle/Combat/BossPeriodicTriggerSystem.cs`(6) · `Battle/Combat/HealthThresholdSystem.cs`(5) |
| `OnPlaceEffectType` (arm 9종) | `Scripts/Bridge/BattleBridge.cs:5393~5590` if/else 체인 |
| **`SkillEffectType` (6종)** | `Scripts/Bridge/BattleBridge.cs:2505~2583` `CastSkillAtTile`/`CastPortal` switch, 구현 `:2819~2958` |

⚠ 세 번째 어휘를 빼면 액티브 가족이 요구하는 동사(타일 지정 존 스폰 · 2타일 링크 ·
플레이어 시전)가 표면에서 누락된 채 계약 9 가 **형식만** 지켜진다.

## 구현

1. **질의 표** — 동사 · 소비 arm 수 · 포트로 감쌀 수 있는가.
   선행 실측(critic): **12동사** — `Position` · `CellOf/CellCenter` · `Facing` ·
   `Opponents(caster,r,filters)` · `Allies` · `DensestOpponentCluster` · `LandingCellNear` ·
   `Stat(id,kind)` · `Health` · `ShieldValueFrom` · `Has(id,pred)` · `TraversalLayers`.
2. **의도 표** — intent · 대응 채널. 선행 실측: **14종**(존 캐리어 스폰 포함).
3. **`Opponents` 필터 축을 enum flag 로 명세**하고 arm 별 현행 조합을 박제한다.
   오늘 후보 수집이 **5개 구현**이고 필터가 서로 다르다 — BossPeriodic 공유 풀은 무필터,
   `IsLegalOnPlaceTarget`, AttackSystem 3술어, AreaSleep 은 `PendingDeployment` 추가.
   못박지 않으면 「같은 이름, 다른 후보」 버그가 프로토콜 아래로 숨는다.
4. **«큰 것을 읽는» arm 표시.** 선행 실측: `FlowFieldSingleton` 소비는 blink/leap **2개뿐**이고
   `BlinkMath`·`DefenderDensity` 가 이미 격자를 인자로 받는 순수 함수라 질의 2개로 봉합된다.
   새로 발견되면 여기에 추가한다 — **하나라도 감쌀 수 없으면 계약 1 이 거짓이 된다.**
5. **직접 쓰기 구멍 열거.** 계약 3 이 「의도 방출만」인데 오늘 반례가 있다:
   `AttackState.cooldownRemaining` 직접 연장(`BattleBridge.cs:5544`) ·
   `AwakeningReward` 덮어씀(`.Dreamcatcher.cs:1120`) ·
   `EffectSpawner.ApplyCc` 가 `CcEffect` 라이브 버퍼 직접 append(큐 우회 — 같은 CC 경로 2개) ·
   진행형 상태 부착 4종. 각각 intent 화 / 예외 명문화 중 하나로 판정한다.
6. **요청-응답 arm 3종의 처리 결정.** `Execute` 는 void 인데 오늘 반환값 계약이 있다 —
   부착 코드(-1 = 무차감 거절 → 코스트 환불) · `RegisterPlacementAura` revoke 핸들 ·
   affected 수(로그). 스킬 밖 유지 / 별도 포트 메서드 / 어댑터 계측 중 택일.
7. **`SkillParams` 겸직 해소안.** `tileRange` 가 **8가지 이상** 의미를 겸직하고
   (`AoE 반경`·`궤도 반경`·`maxStack`·`피해감소%`·`폴백 반경`·`착지 링 상한`·`최대중첩`·`조준 사거리`),
   `period` 는 AttackN 카운트이자 orbitCount 다. bake 가 값 **변환**까지 한다(coneCosSq 사전계산).
   → **skillId 별 typed params + 디스패처 번역층.** `skill-fire-dispatch` 계약 4 의
   「params 뷰 struct」를 계승한다 — 새 발명이 아니다.
8. **Mono 도메인 의도 분류.** `GainCost`·`ReduceSkillCooldown` 은 ECS 를 전혀 안 만지고,
   hand-op(`RecallAttachedToFront`)은 실행자가 `DreamcatcherHandController` 다.
   의도 어휘를 sim 계열 / Mono 계열로 이원 명시하거나 예외로 판정한다.
9. **행 → 담당 unit 대조표.** census ~75행(적 13 · 방어유닛 규칙 5 · 레거시 9 · 카드 32 ·
   캐스트 8 · 소환 1 · 액티브 6)을 `skill-layer-migration` 의 어느 문서가 맡는지 전부 배정한다.
   배정 없는 행이 남으면 그것이 끝점 미달이다.

## 산출물 — 확정본 (2026-08-25, 5트랙 전수 조사)

조사 영역 5분할: 감시 2개(`BossPeriodicTriggerSystem`·`HealthThresholdSystem`) / RESOLVE
(`AttackSystem`·`DamageApplicationSystem`·`UnitLifecycleSystem`) / 브리지 드림캐쳐 /
어휘 2·3(레거시 배치·액티브) / 행 census.

### 질의 — 14동사

| # | 동사 | 시그니처 | 비고 |
|---|---|---|---|
| 1 | `Position(h)` | `float3` | 전 arm |
| 2 | `CellOf` / `CellCenter` | `int2` / `float3` | 격자 스칼라 3개만 필요(tileSize·gridSize·origin) |
| 3 | `Facing(caster)` | `bool TryFacing(out dir)` | 부재 = 무조준 |
| 4 | `Opponents(caster, r, filters)` | `→ handles` | 필터 축 필수(아래) |
| 5 | `Allies(caster, r, filters)` | 〃 | self 포함 여부가 사양 |
| 6 | `DensestOpponentCluster(cells, r)` | `bool (out cell, out n)` | `DefenderDensity` 순수 |
| 7 | `LandingCellNear(desired, maxRing)` | `bool (out cell)` | `BlinkMath` 순수 — dist 배열 인자 |
| 8 | `Stat(h, kind)` | `float` | range·attackTargetCount·targetTraversalLayers·aggroCapacity |
| 9 | `Health(h)` | `(value, max)` | `maxHpRef` 는 **부착/스폰 시점 스냅샷** |
| 10 | `ShieldValueFrom(target, source)` | `float` | dedup |
| 11 | `Has(h, pred)` | `bool` | 술어 ~10종이 여기로 접힘 |
| 12 | `TraversalLayers(h)` | `byte` | 순수 `CanTarget(a,b)` 동반. **0 = 무필터 통과** |
| 13 | `FactionOf(h)` | `Faction` | ⚠ 오늘 **구현 2벌**(태그 `Has` vs `FactionTag` lookup) — 통일 필요 |
| 14 | `HostCapability(caster)` | `DcHostProfile` | 부착 판정 전용. 이미 「브리지 조회 → 순수 판정」 분리됨 |

**감쌀 수 없는 읽기: 4영역 전부 0건 → 계약 1 성립.** 경로장 실소비는 blink/leap 2 arm 뿐이고
그것도 배열을 인자로 받는 순수 함수(#6·#7)로 봉합된다.

### `Opponents`/`Allies` 필터 축 (실측 6벌 → flag enum)

`ExcludeSelf` · `ExcludeDead` · `ExcludePendingDeployment` · `ExcludeInUltimateLeap` ·
`RequireDamageable` · `LayerMask(casterLayers)` · `Metric(Chebyshev|Euclid)` · `RangeRecheckWorld`

⚠ **무필터 조합이 3개 실재한다** — `BuildEnemyPool`(공유 풀) · `CollectEnemiesInTileRange`(Dead 미제외) ·
`CollectShieldBreakTargets`(`{AttackUnitTag}` 만). 못박지 않으면 「같은 이름, 다른 후보」가 숨는다.
`Metric` 축은 `ForwardProjectile` 만 Euclid 다.

### 의도 — `SimIntent` 14 + `MetaIntent` 2 (⚠ 2계열 이원화)

`SimIntent` 는 큐로 다음 프레임, `MetaIntent` 는 **즉시 반영**이고 M1 이후에도 Mono 에 남는다. 섞지 않는다.

**SimIntent**: `DealDamage` · `Heal` · `ApplyStatModifier` · `ApplyStack` · `ApplyCc` · `ApplyDot` ·
`ClearCc` · `GrantShield` · `Taunt` · `CreditThreat` · `Blink` · `SpawnProjectile` · `EmitPattern` ·
`SpawnZoneCarrier`
**MetaIntent**: `GainCost`(`CostRuntime`) · `ReduceSkillCooldown`(`SkillRuntime`)

뷰 신호(intent 로 세지 않음)는 별도. ⚠ **예외 1건** — `BossLeapVisualEvent` 에 `slamDamage`/
`slamTileRange` 가 실려 **브리지 코루틴이 피해를 실행**한다. 게임 규칙이 연출 타이밍에 얹혀 있어
이전 시 판정이 필요하다.

### 계약으로 승격 (5트랙 산출)

1. **`ApplyStatModifier` 의 병합 키 `(source, stat, op, stackId)` 는 revoke 가능성의 조건이다.**
   회수가 「제거」가 아니라 **같은 stackId 로 항등 재발행 = 중립화**라, 포트가 키 구성을 바꾸면
   **회수가 조용히 깨진다**(host 가 죽어도 버프가 안 풀린다).
2. **`SkillFiredEvent` 페이로드** = `skillId · caster(무효 가능) · firedPos · targetHandle? ·
   targetPos? · dirXZ? · params 값 · targetTraversalLayers`.
   「드레인 시점 재질의로 대체 가능」은 **4영역 통틀어 0건.**
3. **`CasterRef { UnitHandle unit(무효 가능); Faction faction; }`** — 플레이어 시전은 `unit` 무효 +
   `faction` 명시. 액티브 6 arm 중 **caster 위치를 읽는 것이 0개**라 성립한다.
4. **대상 축** = `int2 cellA; int2 cellB; bool hasB`. Portal 만 `hasB`.
   「입구==출구 거절」은 arm 이 아니라 **창구 규칙** → 디스패처/검증층 소유.
5. **skillId 별 typed params + 디스패처 번역층.** 반례 못 찾음 — `tileRange` **13의미** ·
   `magnitude` **12의미** · `duration` **7의미**이고 **겸직의 겸직**도 있다(`ProjectileToTarget` 의
   `tileRange` 가 탄 궤적에 따라 반경↔비행거리로 전환). bake 변환 6종 확인.
   ⚠ 단 **`FromMultiplier` 는 bake 가 아니라 발화 시점**이다.

### 예외 확정 (구조적 근거 있음)

| 항목 | 근거 |
|---|---|
| 공격 출력 수식자 | pre-scan 합성 불변식 — 코드 확인. **판별 기준: 「이번 공격의 출력 조립에 곱·합으로 참여」=안 / 「별도 대상·캐리어·채널로 나감」=밖** |
| hand-op(`RecallAttachedToFront`) | 대상이 판이 아니라 **카드 큐**. `ISkillContext` 어휘가 하나도 안 쓰인다. 소비자 1·실행자 1 → 제약 8. `DcPayloadKinds.IsHandOp` 가 이미 코드로 예외 명문화 |
| 부착 시점 요청-응답 8종 | `Execute`(발동)와 **층이 다르다**. 코스트 「환불」의 실체는 **apply-first + 성공 후 Spend** → void `Execute` 와 충돌 없음 |
| `AwakeningReward` 덮어쓰기 | 큐로 미루면 「표식 직후 같은 프레임 처치」에 배율 누락 창 |
| 진행형 상태 부착 7종 | 계약 5 의 「개시 쓰기」. 부분 적용 금지 preflight 와 큐 지연이 충돌 |

### 미결 4건 — **전부 종결 2026-08-25**

1. **whip 이 시체·배치중에도 buff enqueue → 무해(vacuous).** `ModifierApplySystem.ApplyStat` 의
   가드는 `Exists`+`!StructureTag` 뿐이라 `DeadTag`/`PendingDeployment` 를 안 거른다. 그러나
   순서가 막는다 — `BossPeriodicTriggerSystem`(#4)·`ModifierApplySystem`(#9)이 둘 다
   `DamageApplicationSystem`(#36)보다 앞이라 **#4 시점에 `DeadTag` 엔티티가 존재할 수 없다**
   (지난 프레임 것은 `UnitLifecycleSystem`(#44)이 이미 파괴). 배치중 아군이 오라를 미리 받는
   것은 무해. ⚠ **실행 지점이 옮겨지면 vacuous 가 아니게 되므로** 필터 flag 명세에는 남긴다.
2. **착지 앵커 풀의 `DeadTag` 미제외 → 무해, 단 순서 의존.** `UnitLifecycleSystem`(**#44**)이
   `HealthThresholdSystem`(**#45**)보다 **한 칸 앞**이라 HT 가 defQuery 를 돌 때 이번 프레임
   사망자는 이미 파괴돼 있다. ⚠ 이 무해함은 **#44 < #45 에 기대고 있다** — unit 4 가 디스패처를
   (#45→#46)에 핀할 때 이 관계를 함께 박제한다.
3. **`EnemyCcEventsSingleton` 은 진영 중립이다 → `ApplyCc` intent 로 단일화한다.**
   `CcApplySystem` 의 게이트가 `Exists` → `!StructureTag` → `HasBuffer<CcEffect>` →
   `!(BossTag ∧ BossImmune)` 뿐이고 **진영 조건이 없다.** 이름이 오도할 뿐 방어유닛도 받는다.
   주석이 계약을 명시한다 — *"모든 CC 생산자가 이 큐로 수렴하므로 **부여 시점 1곳**에서 막으면
   끝난다."* → 큐 우회 2곳(배치 Sleep·DreamCocoon, `EffectSpawner.ApplyCc`)은 병합 결과는 같지만
   **보스 면역·`StructureTag` 배제 게이트를 우회한다.** 계약 3 위반이자 잠재 결함이므로 단일화가
   맞다. (`EnemyCcEvent` 개명은 unit 3 포트 명세에서 함께 판단.)
4. **`fireCountBase` 전진 → intent 화 가능.** 전진이 `if (fire)` 안에서 append 와 붙어 있고
   no-fire 비전진이 계약이다(다음 발동이 같은 위상에서 시작). 해법: **concrete 가 조준 질의를
   직접 하고 `fire == true` 일 때만 intent 를 방출**한다. 어댑터는 「전진 + append」를 원자적으로
   하고, no-fire 는 intent 자체가 안 나가므로 비전진이 자동 보존된다. **감지측 예외 불요.**

## 완료 기준

- [x] 질의·의도 표가 **3어휘 전수**에서 도출됐고, 각 동사에 소비 arm 이 1개 이상 붙어 있다
- [x] 감쌀 수 없는 읽기가 **0건**임이 표로 확인됐다 → 계약 1 성립
- [x] 직접 쓰기 구멍 · 요청-응답 arm · Mono 의도가 각각 «intent 화» 또는 «예외» 로 판정됐다
- [x] `Opponents` 필터 축이 flag 로 명세되고 arm 별 현행 조합이 박제됐다 (무필터 3벌 포함)
- [x] **77행 전부에 담당 unit 이 배정됐다 — 미배정 0** (census, `skill-layer-migration` 참조)
- [x] `ISkill`·`ISkillContext`·`SkillFiredEvent` 시그니처가 고정됐다 — **caster 없음**(`CasterRef`)과
      **대상 셀 A/B**(Portal 2타일)를 표현한다
- [x] 코드 변경 0줄

**완료 2026-08-25** — 5트랙 병렬 전수 조사 + 미결 4건 종결. **unit 3(포트) 착수 조건 충족.**
