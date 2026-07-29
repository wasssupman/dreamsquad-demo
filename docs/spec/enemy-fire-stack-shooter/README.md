# enemy-fire-stack-shooter — 킨들러 (레인저 저격 화염 축적 원거리 적)

> 상태: **작성 완료, 구현 대기** (2026-07-30)

## 목표

**레인저만 노리는 원거리 적 `킨들러`(id `kindler`)** 를 추가한다. 파이어볼을 쏴 히트마다
화염(Fire) 스택을 1씩 누적시키고, **5스택에서 소진되며 화상 DoT** 가 터진다.

- 적 로스터에 **"방어유닛을 태워 죽이는 축"** 이 없다 — 현재 적의 압박은 직격 데미지와
  디버프뿐이고, 지속 피해로 방어유닛을 압박하는 적이 0이다.
- 난도질꾼(`bleed-fighter-defender`)이 세운 **누적 → 임계 → 폭발** 리듬을 적 진영에서
  거울처럼 되돌려준다. 플레이어가 적에게 쓰던 문법을 이번엔 자기가 받는다.
- **레인저 전용 하드 타겟팅** — 후열을 지키지 않으면 딜러가 녹는다는 배치 압박을 만든다.

## 검증 질문

> **"투사체로 부여한 화염 스택이 실제로 5까지 누적되어 방어유닛에게 화상을 터뜨리는가?"**

지금은 **NO** 다 — unit 0 의 결함(아래) 때문에 투사체가 쌓는 스택은 영원히 1이다.

## 이 spec 이 처음 밟는 자리

1. **투사체 경로의 `ApplyStack` 첫 실사용.** 현재 `ApplyStack` outputs 를 쓰는 유일한 배포
   에셋은 난도질꾼(근접, `AttackSystem` 경로)이다. 투사체 경로(`ProjectileHitSystem`)는
   사용자가 0이라 결함이 잠복해 있었다 → unit 0.
2. **프로젝트 최초의 Fire 스택 producer.** `ApplyStackToTarget` 카드는 `Card_EmberBite`(Bleed)·
   `Card_Frostbite`(Ice) 둘뿐이고 `StackModifier_Fire` 는 **에셋 자체가 없다**
   (`StatusFxKind.cs` 주석이 이 부재를 명시). `StatusFxKind.Fire` 오라는 프리팹까지 배선돼
   있으나 Stack origin 으로는 한 번도 점등된 적이 없다.
3. **`dot-effect-extraction` 계약 2의 실증.** 그 spec 은 *"화염을 스택으로도 만드는 순간
   깨진다"* 를 예고하며 `(DotOrigin, DotElement)` 2축으로 슬롯을 갈랐다. 이 spec 이 바로 그
   순간이다 — Zone 화염(해저드)과 Stack 화염(킨들러)이 **각자 슬롯을 갖는지** 처음 확인된다.
4. **적 → 방어유닛 방향의 스택·DoT.** 스택/DoT 파이프라인은 faction 무관이지만 지금까지
   피해자는 항상 적이었다. 방어유닛은 생성 시 `IncomingDamage`/`CcEffect`/`DotEffect` 버퍼를
   사전 부착받으므로(`BattleBridge.CreateDefenderEntity`) 코드 변경 없이 성립해야 한다 —
   그 "해야 한다"를 실측으로 바꾼다.

## 유닛 사양 (초기값 — 전부 SO 소유, 튜닝 대상)

| 항목 | 값 | 근거 |
|---|---|---|
| id / displayName | `kindler` / Kindler | "Ember" 는 `Card_EmberBite` 가 Bleed 로 선점해 오염된 어휘 |
| enemyClass | `Shooter` | 라벨(런타임 무영향) |
| attackMethod | `Projectile` | |
| **targetClassMask** | **`Ranger` 단독** | "다른 클래스 무시" 하드 필터 |
| **targetMode** | **`FocusUntilDead`** | ★`Nearest` 면 걸으며 최근접이 바뀌어 **어느 레인저도 5스택에 못 간다** |
| engageMovement | `Halt` | 사거리 진입 시 정지 후 사격 |
| targetPriorityClass | `None` | 마스크가 이미 좁혀 중복 |
| HP / 이동 / 사거리 / 쿨다운 | 45 / 2.0 / 4 / 1.2s | 발화 주기 = `atStack × 쿨다운` = **6.0s** |
| hitDelaySec | 0.3 | 기존 슈터(Needler/Sniper) 관례 |
| outputs | `Damage 5` + `ApplyStack(Fire, +1, perApp 3.0, max 5)` | 직격 4.2 DPS |
| awakeningReward / killScore | 2 / 100 | 잡몹 기준 |

`StackModifier_Fire`: `atStack 5 · Consume · ApplyDot` · **틱당 10 / 0.5s / 지속 2.85s**
= **6틱 · 1회분 60**.

붙잡힌 레인저 기준 총 **≈14 DPS** = 직격 4.2(5 ÷ 1.2) + 화상 평균 10(1회분 60 ÷ 주기 6.0s).
화상이 도는 2.85초 동안의 순간 화력은 20 DPS 이고, 나머지 3.15초는 직격만 들어온다.
⚠ 공속을 바꾸면 발화 주기(= `atStack` × 쿨다운)가 따라 움직여 화상 평균 DPS 가 반비례한다.

## Feature-wide 계약

1. **누적 → 임계 → 폭발, `Consume` 모드.** 5스택에서 발화하며 0으로 리셋되고 다시 쌓인다.
   ⚠ **강도 누적형(스택마다 dps 가산)으로 바꾸지 말 것** — `StackModifierTickSystem` 은
   `stackCount > lastTriggeredStack` 일 때만 발화하므로 Edge 룰을 여러 개 깔면 상한 도달 후
   발화가 멎는다(`bleed-fighter-defender` 계약 1).
2. **화상은 펄스다 — 지속(2.85s) < 발화 주기(6.0s).** 난도질꾼은 2026-07-29에 반대로
   (지속 4.85 > 주기 1.5) 갔지만 **이건 폐기된 규칙의 재도입이 아니라 다른 아키타입의 다른
   선택**이다. 근거: ① 난도질꾼은 붙잡고 때리는 근접이라 끊김이 부자연스럽지만 킨들러는 6초
   주기 원거리라 "타오르다 꺼지고 다시 붙는" 리듬이 읽힌다. ② **끊김 없는 화상은 힐러를
   무의미하게 만든다** — 방어유닛은 회복 수단이 제한적이라 상시 화상 = 확정 사망이다.
3. **`duration` 을 `tickInterval` 의 정확한 배수에 걸치지 말 것.** 첫 틱이 즉발이고 만료와
   마지막 틱이 같은 프레임에서 경합하면 틱 수가 프레임레이트에 따라 흔들린다.
   `틱 수 = floor((duration − ε) ÷ tickInterval) + 1`. **2.85 = (6−1)×0.5 + 0.35** — 마지막 틱
   2.5s 뒤 0.35s, 유령 틱 3.0s 앞 0.15s 여유(Bleed 4.85 와 같은 형태).
4. **`maxStack`·`perAppDuration` 은 producer(유닛 SO outputs)가 소유하고 `thresholds` 는
   `StackModifierSO` 가 소유한다.** 한쪽만 바꾸면 조용히 어긋난다 — 양쪽을 **명시 저작**한다
   (`stackMaxStack 5` ↔ SO `maxStack 5`, output `duration 3.0` ↔ SO `perAppDuration 3.0`).
   `stackMaxStack 0`(폴백) 에 의존하지 않는다.
   ⚠ `perAppDuration(3.0) > 공격 쿨다운(1.2)` 이어야 사격 중 스택이 만료되지 않는다.
5. **스택 귀속은 공격자(`ProjectileState.owner`) 단위** — unit 0 이 세우는 계약. 근접 경로
   (`AttackSystem`, `source = attackerEntity`)와 같은 규약이 된다. 킨들러 2기가 같은 레인저를
   때리면 **각자 슬롯을 쌓는다**(난도질꾼 2기와 동형).
6. **파생 DoT 는 `(Stack, Fire)` 슬롯 하나로 병합된다.** 킨들러 2기가 동시에 터뜨려도 화상은
   합산되지 않고 `remainingTime = max` 로 갱신된다 — `dot-effect-extraction` 계약 3의 사양이며
   버그가 아니다. 다중 공격자 DoT 합산은 그 spec 의 후속 후보.
7. **`StackModifier_Fire` 는 전역이다.** 화염 스택 임계 규칙은 적↔방어유닛 양방향 공용이다.
   현재 이 SO 를 쓰는 배포 에셋은 **킨들러뿐** — Fire 를 쓰는 카드/유닛이 생기면 그때 밸런스
   공유를 재검토한다(`StackModifier_Bleed` 와 같은 취급).
8. **벤더 원본 프리팹을 `ProjectileData` 에 직접 참조하지 않는다.** `Assets/_Project/VFX/` 아래
   복제본만 연결한다(`projectile-ga-reskin` 공통 원칙).
9. **레인저가 판에 없으면 아무도 안 쏘고 통과한다 — 사양.** (2026-07-30 사용자 결정)
10. **`ApplyStat` 의 같은 결함은 이번에 고치지 않는다.** 라이브 밸런스가 바뀌므로 별도 결정
    (아래 후속 후보).
11. 전 수치는 SO — 하드코딩 금지.

## 작업 단위

| 파일 | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | code | `0_projectile_stack_source.md` | 투사체 `ApplyStack` 귀속을 투사체 → **사수**로. **이 spec 의 토대** |
| 1 | asset | `1_stack_modifier_fire.md` | `StackModifier_Fire.asset` + BattleScene 배선 |
| 2 | asset | `2_fireball_projectile.md` | PixPlays Fireball 뷰 복제 + `Projectile_Enemy_Fireball.asset` |
| 3 | asset | `3_kindler_asset_and_catalog.md` | `Enemy_Kindler.asset` + 카탈로그 + 덱 풀 등록 + Play 검증 |
| 4 | docs | `4_handoff_summary.md` | 인계 요약 |

unit 0 은 단독 커밋한다 — 결함 수정이라 되돌릴 때 콘텐츠와 함께 딸려가면 안 된다.

## 파이프라인 커버리지

신규 플레이 오브젝트 2종(적 유닛 + 투사체). `docs/reference/object-pipeline-map.md` 대조.

### 적 (Enemy)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Enemy_Kindler.asset` 신규 + **`EnemyCatalog` 등록 + 덱 `attackUnitPool` 노출**(unit 3) |
| 스폰 진입점 | 변경 없음 — `BattleBridge.SpawnUnit` |
| ECS 컴포넌트 | 표준 세트 그대로. `EnemyTargetFilter`(classMask/priorityClass) 는 기존 bake 경로 |
| 시뮬 시스템 | 변경 없음 — `AttackSystem`·`EnemyAiStateSystem` 이 이미 classMask 를 양쪽에서 미러 |
| 이벤트 큐 | **신규 채널 0**. `StackModifierApplyEvents`·`DotApplyEvents` 재사용 |
| View/Pool | 기존 `SpineUnitPool` (파츠 placeholder — 기존 적 스켈레톤 재사용) |
| 체력 표시 | 변경 없음 — `UnitOverheadUiLayer` |
| 씬 wiring | **`stackModifierAuthoring` 배열에 1칸 추가**(unit 1). 그 외 신규 SerializeField 없음 |

### 투사체 (Projectile)

| 정거장 | 이 spec 에서 |
|---|---|
| 데이터 SO | `Projectile_Enemy_Fireball.asset` 신규. `flightMode: Homing` **명시 저작**(기본값 의존 금지) |
| 스폰 진입점 | 변경 없음 — `AttackSystem` RESOLVE → `ProjectileSpawnRequest` → `SpawnProjectile` |
| ECS 컴포넌트 | 표준 `ProjectileState`/`ProjectileTag` + `AttackOutputElement`(호스트 outputs 스냅샷) |
| 시뮬 시스템 | **`ProjectileHitSystem` 1줄**(unit 0). `ProjectileMoveSystem` 무변경 |
| 이벤트 큐 | 변경 없음 — `ProjectileHitEventsSingleton` |
| View/Pool | 기존 `ProjectileViewPool`. 벤더 프리팹 복제본 3종(projectile/hit/cast) |
| 씬 wiring | **N/A** — 풀은 이미 배선돼 있고 SO 참조만 늘어난다 |

## 알려진 한계 (이번 범위 밖)

- **스택 축적이 화면에 안 보인다.** `OverheadStackKind` 는 기믹 전용(`Fatigue`/`Heat`) 2종뿐이고
  `StackKind → OverheadStackKind` 번역이 없다. 5스택이 터지기 전까지 플레이어가 받는 신호는
  **파이어볼 피격 그 자체**뿐이고, 화상이 붙은 뒤에야 Fire 오라가 켜진다. 난도질꾼도 같은 조건
  으로 출시됐고 "폭발 자체가 신호"가 이 패턴의 업계 표준이라는 판단이 이미 서 있다.
- **블로킹 해저드는 클래스 마스크를 우회한다.** `DefenderClassTag` 가 없는 후보는 필터를 통과
  하므로(`EnemyTargetFilter` 주석), 레인저 전용이어도 블로킹 해저드는 쏜다.
- **투사체 outputs 는 `SingleSplash` 페이로드에서만 처리된다.** `PathHit`(방향탄)·`TileAoe`
  (착탄 셀 AoE) 는 outputs 를 읽지 않는다 — 파이어볼을 나중에 방향탄/광역으로 바꾸면 **스택이
  조용히 멎는다.** 소비자가 없으므로 이번에 넓히지 않는다(제약 8).
- **전용 아트 없음.** Spine 은 기존 적 스켈레톤 + 파츠 placeholder, 투사체는 벤더 as-is.

## 후속 후보

- **`ApplyStat` 의 투사체 귀속 결함** [S] · `ProjectileHitSystem` 의 `ApplyStat` 도 `source` 를
  투사체 엔티티로 보내 매 발 새 `StatModifierSlot` 을 만든다. `Enemy_Debuffer`(Needle 투사체,
  `DamageMul ×0.6 Multiplicative`)는 **한 기만 있어도** 0.6ⁿ 로 곱누적된다 —
  `modifier-stacking-policy` 가 "서로 다른 소스"로 진단하고 clamp `[0.2, 5]` 로 막은 증상의
  실제 뿌리로 보인다. 고치면 Debuffer 가 곱누적 → 상시 ×0.6 으로 **라이브 밸런스가 바뀌므로**
  수치 재조정과 한 묶음이어야 한다.
- **전투 스택 오버헤드 아이콘** [M] · `OverheadStackKind` 에 전투 스택 4종을 넣을지가 선결 결정.
  `bleed-fighter-defender` 후속 후보와 같은 항목 — 먼저 착수하는 쪽이 흡수한다.
- **화상 히트 VFX** [S] · 지금은 벤더 `FireballHit` 원샷뿐. 스택 적재 순간의 피격 피드백과
  5스택 발화 순간의 폭발 연출이 구분되지 않는다.
- **다중 킨들러 화상 합산** [M] · 계약 6의 사양을 뒤집는 결정. `dot-effect-extraction` 후속 후보
  "다중 공격자 출혈 합산"과 같은 작업 — 도트 전용 가산 병합이 필요하다.
- **전용 아트 패스** [S] · Spine 파츠/틴트 + 화염 계열 투사체 색 조정(guid 유지 교체).
